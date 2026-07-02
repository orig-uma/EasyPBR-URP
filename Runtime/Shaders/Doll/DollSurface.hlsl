// =============================================================================
//  DollSurface.hlsl
// -----------------------------------------------------------------------------
//  GatherSurface / ComputeFaceSDF / ApplyEnvironmentAndCoat / ApplyPostEffects
//  型定義: DollSurfaceTypes.hlsl（DollLighting 等が include）
//  実装:   ForwardPass で Varyings 定義後に #define DOLL_SURFACE_IMPL して include
// =============================================================================

#if defined(DOLL_SURFACE_IMPL) && !defined(DOLL_SURFACE_IMPL_INCLUDED)
#define DOLL_SURFACE_IMPL_INCLUDED

#include "../Common/URP/Reflection_URP.hlsl"

DollSurfaceData GatherSurface(Varyings input, half3 viewDirectionWS, float3 objectForwardWS, out half alpha)
{
    DollSurfaceData s;

    half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;

    UNITY_BRANCH
    if (_UseColorCorrection > 0.5)
    {
        albedo.rgb = ApplyColorCorrection(albedo.rgb, _HueShift, _Saturation, _ValueMulti);
    }

    float2 detailUV = input.uv * _DetailMap_ST.xy + _DetailMap_ST.zw;
    half4 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_MainTex, detailUV) * _DetailColor;
    albedo.rgb = lerp(albedo.rgb, detail.rgb, detail.a);

    alpha = albedo.a;

    #if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
    #endif

    half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_MainTex, input.uv);
    half3 normalTS = UnpackNormalScale(normalSample, _NormalScale);

    half3 detailNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_MainTex, detailUV), _DetailNormalScale);
    normalTS = normalize(half3(normalTS.xy + detailNormalTS.xy, normalTS.z * detailNormalTS.z));

    half3 cleanNormalWS = normalize(normalTS.x * input.tangentWS + normalTS.y * input.bitangentWS + normalTS.z * input.normalWS);
    ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, cleanNormalWS, albedo.rgb, s.dissolveEmission);

    s.coatNormalWS  = normalize(input.normalWS);
    s.clearcoatMask = SAMPLE_TEXTURE2D(_ClearcoatMask, sampler_MainTex, input.uv).r;

    half4 blueNoiseSample = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, input.uv * _GrainScale);
    half3 noiseVec   = blueNoiseSample.rgb * 2.0 - 1.0;
    s.ditherValue = blueNoiseSample.r;

    s.detailNormalWS = GetGrainNormal(cleanNormalWS, noiseVec, _GrainIntensity);

    // シェーディング法線: ベイクした平滑化法線で拡散の陰ランプだけを駆動し、
    // シワ・ファセット起伏がグラデーションを汚すのを防ぐ（スペキュラ・リム・
    // SSS はディテール法線のまま）。未ベイク（bump / Strength 0）で無効。
    s.shadeNormalWS = s.detailNormalWS;
    UNITY_BRANCH
    if (_ShadeNormalStrength > 0.0)
    {
        half4 shadeSample = SAMPLE_TEXTURE2D(_ShadeNormalMap, sampler_MainTex, input.uv);
        half3 shadeTS = shadeSample.xyz * 2.0 - 1.0;
        half3 shadeWS = normalize(shadeTS.x * input.tangentWS + shadeTS.y * input.bitangentWS + shadeTS.z * input.normalWS);
        s.shadeNormalWS = normalize(lerp(s.detailNormalWS, shadeWS, _ShadeNormalStrength));
    }

    s.receiveShadowMask = SAMPLE_TEXTURE2D(_ReceiveShadowMask, sampler_MainTex, input.uv).r;
    s.specMask = SAMPLE_TEXTURE2D(_SpecularMask, sampler_MainTex, input.uv).r;

    half4 sssSample  = SAMPLE_TEXTURE2D(_SSSMap, sampler_MainTex, input.uv);
    s.sssMask    = sssSample.a;
    half3 sssTransTS = sssSample.rgb * 2.0 - 1.0;
    s.sssTransWS = normalize(sssTransTS.x * input.tangentWS
                       + sssTransTS.y * input.bitangentWS
                       + sssTransTS.z * input.normalWS);

    s.occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_MainTex, input.uv).r;
    albedo.rgb *= lerp(1.0, s.occlusion, _OcclusionStrength);

    s.bentNormalWS = s.detailNormalWS;
    s.bentOpenness = 1.0;
    UNITY_BRANCH
    if (_BentNormalStrength > 0.0)
    {
        half4 bentSample = SAMPLE_TEXTURE2D(_BentNormalMap, sampler_MainTex, input.uv);
        half3 bentTS = bentSample.xyz * 2.0 - 1.0;
        bentTS = normalize(bentTS);
        half3 bentWS = normalize(bentTS.x * input.tangentWS + bentTS.y * input.bitangentWS + bentTS.z * s.detailNormalWS);
        s.bentNormalWS = normalize(lerp(s.detailNormalWS, bentWS, _BentNormalStrength));
        s.bentOpenness = bentSample.a;
    }

    s.cavity = SAMPLE_TEXTURE2D(_CavityMap, sampler_MainTex, input.uv).r;
    albedo.rgb *= lerp(1.0, s.cavity, _CavityStrength);

    s.curvRidge = 0.0;
    s.scatterCurvMask = 1.0;
    UNITY_BRANCH
    if (_CurvatureStrength > 0.0)
    {
        half curv = SAMPLE_TEXTURE2D(_CurvatureMap, sampler_MainTex, input.uv).r;
        half signedCurv = (curv * 2.0 - 1.0) * _CurvatureStrength;
        s.curvRidge  = saturate( signedCurv);
        albedo.rgb *= 1.0 - saturate(-signedCurv);

        // スキンスキャッタ用の曲率マスク: 曲率の大きい（＝細い/薄い）部位ほど
        // 散乱を強く。0 で曲率非依存の均一適用。
        s.scatterCurvMask = lerp(1.0, saturate(abs(signedCurv) * 4.0), _SkinScatterCurvatureMask);
    }

    s.specAAVariance = (_SpecularAA > 0.0)
        ? ComputeSpecularAAVariance(s.detailNormalWS, _SpecularAA, 0.25)
        : 0.0;

    s.cleanNormalWS = cleanNormalWS;
    s.albedo = albedo.rgb;

    // 陰側の最終色をライト非依存に 1 回だけ算出（per-light の再計算を排除）。
    // Hue Shift / Saturation は「ただ暗い影」を彩度と色相の残る影にする（既定は素通し）。
    half3 shadowBase = albedo.rgb;
    UNITY_BRANCH
    if (abs(_ShadowHueShift) > 0.0001 || abs(_ShadowSaturation - 1.0) > 0.0001)
    {
        shadowBase = ApplyColorCorrection(shadowBase, _ShadowHueShift, _ShadowSaturation, 1.0);
    }
    s.shadowAlbedo  = shadowBase * _ShadowColor.rgb;
    s.shadow2Albedo = shadowBase * _Shadow2Color.rgb;
    s.castShadowAlbedo = shadowBase * _CastShadowColor.rgb;
    s.baseProceduralMask = GetProceduralMaskBase(cleanNormalWS, objectForwardWS, _FrontMaskStrength, _UpMaskStrength, _MaskFalloff);
    s.NdotV = saturate(dot(s.detailNormalWS, viewDirectionWS));
    GetFresnelTerms(s.NdotV, _RimIntensity, _RimThickness, _FuzzIntensity, _FuzzPower, s.rimFresnel, s.fuzzFresnel);

    float hairFlowC2 = 1.0, hairFlowS2 = 0.0, hairFlowConf = 0.0;
    UNITY_BRANCH
    if (_HairFlowStrength > 0.0)
    {
        half3 hf = SAMPLE_TEXTURE2D(_HairFlowMap, sampler_MainTex, input.uv).rgb;
        hairFlowC2   = hf.r * 2.0 - 1.0;
        hairFlowS2   = hf.g * 2.0 - 1.0;
        hairFlowConf = hf.b;
    }

    s.anisoPrecomp = PrecomputeAnisoTangent(
        input.tangentWS, input.bitangentWS, s.detailNormalWS, input.uv,
        _AnisoAngle, _AnisoStrandDir, _AnisoStrandScale, _AnisoStrandStrength,
        _AnisoOffset, _AnisoSecOffset,
        hairFlowC2, hairFlowS2, hairFlowConf, _HairFlowStrength);

    half glitterMask = SAMPLE_TEXTURE2D(_GlitterMask, sampler_MainTex, input.uv).r;

    s.glitterActive = PrepareGlitter(
        s.detailNormalWS, viewDirectionWS,
        input.uv, _GlitterScale, _GlitterSize,
        _GlitterTilt, glitterMask,
        _GlitterIntensity, _GlitterSparsity,
        s.glitterGeom);

    // 間接光（SH）: 平坦化で方向成分を潰し、キャラ全体を均一なアンビエントで包む。
    // SampleSH(0) は SH の定数項（平均環境光）のみを返す。ベント法線由来の
    // 方向補正（0 側）と平坦化（1 側）はトレードオフの関係。
    half3 indirect = SampleSH(s.bentNormalWS);
    UNITY_BRANCH
    if (_IndirectFlatten > 0.0)
    {
        indirect = lerp(indirect, SampleSH(half3(0.0, 0.0, 0.0)), _IndirectFlatten);
    }
    s.indirectLight = indirect * (_IndirectTint.rgb * _IndirectIntensity);

    return s;
}

float ComputeFaceSDF(Varyings input, Light mainLight, float3 objectForwardWS, out half sdfMask)
{
    float sdfLit = -1.0;
    sdfMask = 1.0;

    UNITY_BRANCH
    if (_UseFaceSDF > 0.5)
    {
        float3 normalOS = TransformWorldToObjectDir(input.normalWS);

        sdfMask = smoothstep(_FaceSDFBlendNormalMin, _FaceSDFBlendNormalMax, normalOS.y);

        float3 faceUp    = normalize(TransformObjectToWorldDir(float3(0, 1, 0)));
        float3 faceFwd   = normalize(objectForwardWS) * (_FaceSDFFlip > 0.5 ? -1.0 : 1.0);
        float3 faceRight = normalize(cross(faceUp, faceFwd));

        float dirX = dot(mainLight.direction, faceRight);
        float dirY = dot(mainLight.direction, faceUp);

        float frontness = dot(mainLight.direction, faceFwd);

        half4 sdfRGBA = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_MainTex, input.uv);

        float weightRight = max(0.0, dirX);
        float weightLeft  = max(0.0, -dirX);
        float weightUp    = max(0.0, dirY);
        float weightDown  = max(0.0, -dirY);

        float weightSum = weightRight + weightLeft + weightUp + weightDown + 0.0001;

        float sdf = (sdfRGBA.r * weightRight +
                     sdfRGBA.g * weightLeft +
                     sdfRGBA.b * weightUp +
                     sdfRGBA.a * weightDown) / weightSum;

        float baseSoft = max(_FaceSDFSoftness, fwidth(sdf));
        float soft = max(baseSoft, _HalfLambertWrap * 0.5);
        float f = frontness * 0.5 + 0.5;
        sdfLit = smoothstep(sdf - soft, sdf + soft, f);
    }

    return sdfLit;
}

void ApplyEnvironmentAndCoat(inout half3 finalColor, DollSurfaceData s, half3 viewDirectionWS)
{
    UNITY_BRANCH
    if (_ReflectionStrength > 0.0 || _ClearcoatStrength > 0.0)
    {
        half  perceptualRoughness = 1.0 - _Smoothness;
        half3 reflectVector = reflect(-viewDirectionWS, s.detailNormalWS);
        half3 env = EasyPBR_SampleEnvironment(reflectVector, perceptualRoughness);

        half horizon = saturate(1.0 + dot(reflectVector, s.detailNormalWS));
        horizon *= horizon;

        UNITY_BRANCH
        if (_ReflectionStrength > 0.0)
        {
            float specOcclusion = SpecularOcclusion(s.NdotV, s.occlusion, perceptualRoughness);
            UNITY_BRANCH
            if (_BentNormalStrength > 0.0)
            {
                float align = dot(reflectVector, s.bentNormalWS) * 0.5 + 0.5;
                specOcclusion *= lerp(s.bentOpenness, 1.0, align);
            }
            half baseFresnel = _SpecularF0 + (1.0 - _SpecularF0) * pow(1.0 - s.NdotV, 5.0);
            finalColor += env * baseFresnel * horizon * _ReflectionStrength * (specOcclusion * s.specMask);
        }

        UNITY_BRANCH
        if (_ClearcoatStrength > 0.0)
        {
            half  ndvC        = saturate(dot(s.coatNormalWS, viewDirectionWS));
            half  coatFresnel = 0.04 + 0.96 * pow(1.0 - ndvC, 5.0);
            half3 coatIrid    = ClearcoatIridescence(ndvC, _IridescenceIntensity, _IridescenceThickness, _IridescenceShift);
            finalColor += env * coatFresnel * horizon * coatIrid * (_ClearcoatStrength * _ClearcoatReflStrength * s.clearcoatMask);
        }
    }
}

void ApplyPostEffects(inout half3 finalColor, Varyings input, DollSurfaceData s, Light mainLight, half3 viewDirectionWS)
{
    UNITY_BRANCH
    if (_UseMatCap > 0.5)
    {
        float2 matcapUV = GetMatCapUVLightAligned(s.detailNormalWS, mainLight.direction, _MatCapLightInfluence);
        half3 matcapColor = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MainTex, matcapUV).rgb * _MatCapColor.rgb;
        finalColor = ApplyMatCap(finalColor, matcapColor, _MatCapIntensity, _MatCapBlend);
    }

    UNITY_BRANCH
    if (_UseEmission > 0.5)
    {
        half3 emissionMapColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, input.uv).rgb;
        finalColor += CalculateEmission(emissionMapColor, _EmissionColor.rgb, _EmissionIntensity);
    }

    finalColor += s.dissolveEmission;
    float3 black = float3(0.0f, 0.0f, 0.0f);
    finalColor = lerp(finalColor, black, _BlackOut);
}

#endif // DOLL_SURFACE_IMPL_INCLUDED
