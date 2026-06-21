// =============================================================================
//  Doll_ForwardPass.hlsl
//  メインの描画パス（UniversalForward）
// =============================================================================
#ifndef DOLL_FORWARD_PASS_INCLUDED
#define DOLL_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "EasyPBR_Effects.hlsl"
#include "DollLighting.hlsl"
#include "DollShadows.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 uv           : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float3 positionWS   : TEXCOORD0;
    float3 normalWS     : TEXCOORD1;
    float2 uv           : TEXCOORD2; 
    float4 shadowCoord  : TEXCOORD3;
    float3 forwardWS    : TEXCOORD4;
    float3 positionOS   : TEXCOORD5;
    float3 tangentWS    : TEXCOORD6;
    float3 bitangentWS  : TEXCOORD7;
};

Varyings vert(Attributes input)
{
    Varyings output;
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS   = normalInput.normalWS;
    output.tangentWS  = normalInput.tangentWS;
    output.bitangentWS = normalInput.bitangentWS;
    output.uv = input.uv;
    output.shadowCoord = GetShadowCoord(vertexInput);
    output.forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
    output.positionOS = input.positionOS.xyz;
    return output;
}

half3 CalculateSingleLight(
    Light light, half3 detailNormalWS,
    half3 viewDirectionWS, float3 objectForwardWS,
    half3 baseColor, half receiveShadowMask, half specMask, half sssMask, half ditherValue,
    float baseProceduralMask, float rimFresnel, float fuzzFresnel, half3 indirectLight,
    AnisoPrecomp anisoPrecomp,
    GlitterGeom glitterGeom,
    bool glitterActive)
{
    float3 rawDiffuseLight = (light.color * light.distanceAttenuation) + indirectLight;
    float3 diffuseLightEnergy = ApplyLightEnergyLimit(rawDiffuseLight, _DiffuseLightLimit);

    float3 rawSpecLight = light.color * light.distanceAttenuation;
    float3 priSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _PriSpecularLightLimit);
    float3 secSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _SecSpecularLightLimit);

    float proceduralMask = GetProceduralMask(baseProceduralMask, objectForwardWS, light.direction);
    half3 diffuseNormalWS = detailNormalWS;

    float diffuseNdotL = dot(diffuseNormalWS, light.direction);
    float halfLambert = GetHalfLambert(diffuseNdotL, _HalfLambertWrap);

    float castShadow = GetCastShadow(light.shadowAttenuation, receiveShadowMask, _ReceiveShadowStrength, ditherValue, _ShadowDither, _ShadowMapSoftness, proceduralMask);

    float litMask = GetLitMask(halfLambert, proceduralMask, _ToonStep, _ToonFeather);
    float finalShade = min(litMask, castShadow);

    half3 diffuseColor = GetShadedAlbedo(baseColor, _ShadowColor.rgb, finalShade);
    half3 finalDiffuse = diffuseColor * diffuseLightEnergy;

    float NdotL_Specular = dot(detailNormalWS, light.direction);
    float specularMaskVal;
    half3 finalSpecular = CalculateDualLobeSpecular(
        detailNormalWS, light.direction, viewDirectionWS, NdotL_Specular,
        priSpecLightEnergy, _SpecularColor, _Smoothness, _SpecularIntensity,
        secSpecLightEnergy, _SecSpecularColor, _SecSmoothness, _SecSpecularIntensity,
        specMask, castShadow, _SpecularF0, specularMaskVal);

    float specLuminance = saturate(dot(finalSpecular, half3(0.299, 0.587, 0.114)));
    finalDiffuse *= (1.0 - specLuminance);

    half3 finalSSS  = CalculateSSS(detailNormalWS, light.direction, viewDirectionWS, _SSSColor.rgb, _SSSIntensity * sssMask, _SSSPower, _SSSDistortion, diffuseLightEnergy, castShadow);
    half3 finalRim  = CalculateRimLight(_RimColor.rgb, rimFresnel, _RimIntensity, diffuseLightEnergy, NdotL_Specular, castShadow);
    half3 finalFuzz = CalculatePeachFuzz(_FuzzColor.rgb, fuzzFresnel, _FuzzIntensity, diffuseLightEnergy, NdotL_Specular, castShadow);

    half3 finalAniso = CalculateAnisotropicSpecular(
        anisoPrecomp,
        detailNormalWS, light.direction, viewDirectionWS,
        _AnisoColor, _AnisoThickness,
        _AnisoSecColor, _AnisoSecThickness,
        diffuseLightEnergy, castShadow);

    half3 finalGlitter = half3(0, 0, 0);
    if (glitterActive)
    {
        finalGlitter = ApplyGlitterLight(
            glitterGeom,
            light.direction, viewDirectionWS,
            _GlitterColor.rgb, _GlitterIntensity,
            _GlitterIridescence, _GlitterIridescenceShift,
            _GlitterBaseReflection, diffuseLightEnergy);
    }

    // 最終出力に合算
    return finalDiffuse + finalSpecular + finalSSS + finalRim + finalFuzz + finalAniso + finalGlitter;
}

half4 frag(Varyings input) : SV_Target
{
    half3 finalColor = half3(0, 0, 0);

    half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;

    UNITY_BRANCH
    if (_UseColorCorrection > 0.5)
    {
        albedo.rgb = ApplyColorCorrection(albedo.rgb, _HueShift, _Saturation, _ValueMulti);
    }

    float2 detailUV = input.uv * _DetailMap_ST.xy + _DetailMap_ST.zw;
    half4 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_MainTex, detailUV) * _DetailColor;
    albedo.rgb = lerp(albedo.rgb, detail.rgb, detail.a); // アルファブレンドで重ねる

    #if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
    #endif

    half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_MainTex, input.uv);
    half3 normalTS = UnpackNormalScale(normalSample, _NormalScale);
    // TBNベクトルを用いてTangent空間の法線をWorld空間へ変換
    half3 cleanNormalWS = normalize(normalTS.x * input.tangentWS + normalTS.y * input.bitangentWS + normalTS.z * input.normalWS);
    half3 dissolveEmission;
    ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, cleanNormalWS, albedo.rgb, dissolveEmission);

    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 objectForwardWS = input.forwardWS;
    
    half4 blueNoiseSample = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, input.uv * _GrainScale);
    half3 noiseVec   = blueNoiseSample.rgb * 2.0 - 1.0;
    half  ditherValue = blueNoiseSample.r;

    half3 detailNormalWS = GetGrainNormal(cleanNormalWS, noiseVec, _GrainIntensity);

    half receiveShadowMask = SAMPLE_TEXTURE2D(_ReceiveShadowMask, sampler_MainTex, input.uv).r;
    half specMask = SAMPLE_TEXTURE2D(_SpecularMask, sampler_MainTex, input.uv).r;
    half sssMask  = SAMPLE_TEXTURE2D(_SSSMask, sampler_MainTex, input.uv).r;

    // ライトループ外の事前計算
    float baseProceduralMask = GetProceduralMaskBase(cleanNormalWS, objectForwardWS, _FrontMaskStrength, _UpMaskStrength, _MaskFalloff);
    float NdotV = saturate(dot(detailNormalWS, viewDirectionWS));
    float rimFresnel, fuzzFresnel;
    GetFresnelTerms(NdotV, _RimIntensity, _RimThickness, _FuzzIntensity, _FuzzPower, rimFresnel, fuzzFresnel);
    
    AnisoPrecomp anisoPrecomp = PrecomputeAnisoTangent(
        input.tangentWS, input.bitangentWS, detailNormalWS, input.uv,
        _AnisoAngle, _AnisoStrandDir, _AnisoStrandScale, _AnisoStrandStrength,
        _AnisoOffset, _AnisoSecOffset);
    
    half glitterMask = SAMPLE_TEXTURE2D(_GlitterMask, sampler_MainTex, input.uv).r;

    GlitterGeom glitterGeom;
    bool glitterActive = PrepareGlitter(
        detailNormalWS, viewDirectionWS,
        input.uv, _GlitterScale, _GlitterSize,
        _GlitterTilt, glitterMask,
        _GlitterIntensity, _GlitterSparsity,
        glitterGeom);

    // メインライト計算
    half3 indirectLight = SampleSH(cleanNormalWS);

    #if defined(_SHADOWQUALITY_PCF) || defined(_SHADOWQUALITY_PCSS)
        Light mainLight = GetMainLight();              // URP内部シャドウサンプルをスキップ
        float mainNdotL = dot(cleanNormalWS, mainLight.direction);
        mainLight.shadowAttenuation = SampleMainShadowHQ(
            input.positionWS, cleanNormalWS, mainNdotL,
            input.positionCS.xy, _ShadowMapSoftness);
    #else
        float4 shadowCoord = input.shadowCoord;
        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            shadowCoord = ComputeScreenPos(input.positionCS);
        #endif
        Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
    #endif

    finalColor += CalculateSingleLight(
        mainLight, detailNormalWS, viewDirectionWS, objectForwardWS,
        albedo.rgb, receiveShadowMask, specMask, sssMask, ditherValue,
        baseProceduralMask, rimFresnel, fuzzFresnel,
        indirectLight, anisoPrecomp, glitterGeom, glitterActive);

    // 追加ライト計算
    #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalizedScreenSpaceUV = input.positionCS.xy / _ScreenParams.xy;

        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
            half3 addContrib = CalculateSingleLight(
                addLight, detailNormalWS, viewDirectionWS, objectForwardWS,
                albedo.rgb, receiveShadowMask, specMask, sssMask, ditherValue,
                baseProceduralMask, rimFresnel, fuzzFresnel,
                half3(0,0,0), anisoPrecomp, glitterGeom, glitterActive);
            // 0 = Add（物理的・白飛びしやすい）, 1 = Max（アニメ向け・彩度を保つ）
            finalColor = (_AdditionalLightBlendMode > 0.5)
                ? max(finalColor, addContrib)
                : finalColor + addContrib;
        LIGHT_LOOP_END
    #endif

    // 追加エフェクト適用
    // keyword (_MATCAP_ON / _EMISSION_ON) を廃止し uniform 動的分岐に変更。
    // 無効時(=0)は UNITY_BRANCH によりテクスチャサンプルごとスキップされる。
    UNITY_BRANCH
    if (_UseMatCap > 0.5)
    {
        float2 matcapUV = GetMatCapUV(detailNormalWS);
        half3 matcapColor = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MainTex, matcapUV).rgb * _MatCapColor.rgb;
        finalColor = ApplyMatCap(finalColor, matcapColor, _MatCapIntensity, _MatCapBlend);
    }

    UNITY_BRANCH
    if (_UseEmission > 0.5)
    {
        half3 emissionMapColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, input.uv).rgb;
        finalColor += CalculateEmission(emissionMapColor, _EmissionColor.rgb, _EmissionIntensity);
    }

    finalColor += dissolveEmission;
    float3 black = float3(0.0f, 0.0f, 0.0f);
    finalColor = lerp(finalColor, black, _BlackOut);

    half outputAlpha = 1.0h;
    #if defined(_SURFACE_TRANSPARENT)
        outputAlpha = albedo.a;
    #endif

    return half4(finalColor, outputAlpha);
}

#endif // DOLL_FORWARD_PASS_INCLUDED
