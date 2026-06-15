// =============================================================================
//  Doll_ForwardPass.hlsl
//  メインの描画パス（UniversalForward）
// =============================================================================
#ifndef DOLL_FORWARD_PASS_INCLUDED
#define DOLL_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "EasyPBR_Effects.hlsl"
#include "EasyPBR_Lighting.hlsl"
#include "Doll_FaceLogic.hlsl"

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
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.tangentWS = normalInput.tangentWS;
    output.bitangentWS = normalInput.bitangentWS;
    output.uv = input.uv;
    output.shadowCoord = GetShadowCoord(vertexInput);
    output.forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
    output.positionOS = input.positionOS.xyz;
    return output;
}

half3 CalculateSingleLight(
    Light light, half3 detailNormalWS, float3 tangentWS, float3 bitangentWS, 
    half3 viewDirectionWS, float3 objectForwardWS,
    half3 baseColor, half receiveShadowMask, half specMask, half ditherValue,
    float baseProceduralMask, float rimFresnel, float fuzzFresnel, float2 uv, half3 indirectLight)
{
    float3 rawDiffuseLight = (light.color * light.distanceAttenuation) + indirectLight;
    float3 diffuseLightEnergy = ApplyLightEnergyLimit(rawDiffuseLight, _DiffuseLightLimit);

    float3 rawSpecLight = light.color * light.distanceAttenuation;
    float3 priSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _PriSpecularLightLimit);
    float3 secSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _SecSpecularLightLimit);

    float proceduralMask = GetProceduralMask(baseProceduralMask, objectForwardWS, light.direction, _BacklightPreserve);
    half3 diffuseNormalWS = GetFaceSmoothedNormal(detailNormalWS, objectForwardWS, proceduralMask, _FaceNormalSmoothness);

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
        specMask, castShadow, specularMaskVal);

    float specLuminance = saturate(dot(finalSpecular, half3(0.299, 0.587, 0.114)));
    finalDiffuse *= (1.0 - specLuminance);

    half3 finalSSS = CalculateSSS(detailNormalWS, light.direction, viewDirectionWS, _SSSColor.rgb, _SSSIntensity, _SSSPower, _SSSDistortion, diffuseLightEnergy, castShadow);
    half3 finalRim = CalculateRimLight(_RimColor.rgb, rimFresnel, _RimIntensity, diffuseLightEnergy, NdotL_Specular, castShadow);
    half3 finalFuzz = CalculatePeachFuzz(_FuzzColor.rgb, fuzzFresnel, _FuzzIntensity, diffuseLightEnergy, NdotL_Specular, castShadow);

    half3 finalAniso = CalculateAnisotropicSpecular(
        detailNormalWS, tangentWS, bitangentWS, light.direction, viewDirectionWS,
        _AnisoColor, _AnisoThickness, _AnisoOffset, _AnisoAngle, _AnisoStrandScale, _AnisoStrandStrength, uv, diffuseLightEnergy, castShadow);

    return finalDiffuse + finalSpecular + finalSSS + finalRim + finalFuzz + finalAniso;
}

half4 frag(Varyings input) : SV_Target
{
    half3 finalColor = half3(0, 0, 0);
    
    half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
    #if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
    #endif

    half3 cleanNormalWS = normalize(input.normalWS);
    half3 dissolveEmission;
    ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, cleanNormalWS, albedo.rgb, dissolveEmission);

    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 objectForwardWS = input.forwardWS;

    float3 noiseVec = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, input.uv * _GrainScale).rgb * 2.0 - 1.0;
    half3 detailNormalWS = GetGrainNormal(cleanNormalWS, noiseVec, _GrainIntensity);

    #if defined(_ALPHATEST_ON)
        clip(albedo.a - _Cutoff);
    #endif

    half receiveShadowMask = SAMPLE_TEXTURE2D(_ReceiveShadowMask, sampler_MainTex, input.uv).r;
    half specMask = SAMPLE_TEXTURE2D(_SpecularMask, sampler_MainTex, input.uv).r;
    float2 ditherUV = input.positionCS.xy / 256.0;
    half ditherValue = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, ditherUV).r;

    float4 shadowCoord = input.shadowCoord;
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        shadowCoord = ComputeScreenPos(input.positionCS);
    #endif

    // ライトループ外の事前計算
    float baseProceduralMask = GetProceduralMaskBase(cleanNormalWS, objectForwardWS, _FrontMaskStrength, _UpMaskStrength, _MaskFalloff);
    float NdotV = saturate(dot(detailNormalWS, viewDirectionWS));
    float rimFresnel, fuzzFresnel;
    GetFresnelTerms(NdotV, _RimIntensity, _RimThickness, _FuzzIntensity, _FuzzPower, rimFresnel, fuzzFresnel);

    // メインライト計算
    half3 indirectLight = SampleSH(cleanNormalWS);
    Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
    finalColor += CalculateSingleLight(mainLight, detailNormalWS, input.tangentWS, input.bitangentWS, viewDirectionWS, objectForwardWS, albedo.rgb, receiveShadowMask, specMask, ditherValue, baseProceduralMask, rimFresnel, fuzzFresnel, input.uv, indirectLight);

    // 追加ライト計算
    #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalizedScreenSpaceUV = input.positionCS.xy / _ScreenParams.xy;

        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
    finalColor += CalculateSingleLight(addLight, detailNormalWS, input.tangentWS, input.bitangentWS, viewDirectionWS, objectForwardWS, albedo.rgb, receiveShadowMask, specMask, ditherValue, baseProceduralMask, rimFresnel, fuzzFresnel, input.uv, half3(0,0,0));
        LIGHT_LOOP_END
    #endif

    // 追加エフェクト適用
    #if defined(_MATCAP_ON)
        float2 matcapUV = GetMatCapUV(detailNormalWS);
        half3 matcapColor = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MainTex, matcapUV).rgb * _MatCapColor.rgb;
        finalColor = ApplyMatCap(finalColor, matcapColor, _MatCapIntensity);
    #endif

    #if defined(_EMISSION_ON)
        half3 emissionMapColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, input.uv).rgb;
        finalColor += CalculateEmission(emissionMapColor, _EmissionColor.rgb, _EmissionIntensity);
    #endif

    finalColor += dissolveEmission;

    half outputAlpha = 1.0h;
    #if defined(_SURFACE_TRANSPARENT)
        outputAlpha = albedo.a;
    #endif

    return half4(finalColor, outputAlpha);
}

#endif // DOLL_FORWARD_PASS_INCLUDED
