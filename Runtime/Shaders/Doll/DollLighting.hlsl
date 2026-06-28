// =============================================================================
//  DollLighting.hlsl  (policy / thin wrapper)
// -----------------------------------------------------------------------------
//  キャラ(Doll)固有のポリシーだけを保持する薄い層。
//  汎用計算は Common ライブラリへ委譲し、ここでは
//   (1) 顔の手続き的マスク（自己陰消し）
//   (2) キーワード分岐（toon / specular model / shadow quality）の解決
//   (3) CalculateSingleLight（DollSurfaceData 経由）
//  のみを担う。
//
//  前提: URP Core.hlsl を本ファイルより前に include しておくこと。
// =============================================================================
#ifndef DOLL_LIGHTING_INCLUDED
#define DOLL_LIGHTING_INCLUDED

#include "../Common/Common.hlsl"
#include "DollSurfaceTypes.hlsl"

// 旧 Hash2DTo1D を直接呼んでいた箇所のための後方互換エイリアス。
#define Hash2DTo1D Hash21

// =============================================================================
//  顔ポリシー（Doll 固有）
// =============================================================================

// 「正面向き」「上向き」の面ほど 1 に近づくマスクのベース（ライト非依存）。
float GetProceduralMaskBase(half3 normalWS, float3 forwardWS, float frontStrength, float upStrength, float falloff)
{
    float frontMask = saturate(dot(normalWS, forwardWS));
    float upMask    = saturate(normalWS.y);
    float mask = pow(frontMask, falloff) * frontStrength + pow(upMask, falloff) * upStrength;
    return smoothstep(0.0, 1.0, saturate(mask));
}

// baseMask に「逆光時は陰を残す」backlightFade を掛けたライト毎の最終マスク。
float GetProceduralMask(float baseMask, float3 forwardWS, float3 lightDirWS)
{
    float lightToForwardDot = dot(forwardWS, lightDirWS);
    float backlightFade = smoothstep(-0.3, 0.2, lightToForwardDot);
    return baseMask * backlightFade;
}

// =============================================================================
//  互換ラッパー（汎用関数へ委譲。公開名は従来どおり）
// =============================================================================

float GetHalfLambert(float ndotl, float wrap)
{
    return HalfLambert(ndotl, wrap);
}

float3 ApplyLightEnergyLimit(float3 rawLight, float limit)
{
    return ApplyLuminanceClamp(rawLight, limit);
}

half3 GetShadedAlbedo(half3 baseColor, half3 shadowColorTint, float finalShade)
{
    return ShadedAlbedo(baseColor, shadowColorTint, finalShade);
}

// 落ち影: 汎用整形 + 顔正面での落ち影消し（Doll ポリシー）。
float GetCastShadow(float shadowAttenuation, float receiveShadowMask, float receiveShadowStrength,
                    float ditherValue, float shadowDither, float shadowMapSoftness, float proceduralMask)
{
    bool penumbraReady = false;
#if defined(_SHADOWMODE_TENTPCF) || defined(_SHADOWMODE_VOGELPCF) || defined(_SHADOWMODE_PCSS)
    penumbraReady = true; // PCF/PCSS が連続ペナンブラ生成済み → ディザ＆再量子化しない
#endif
    float castShadow = ResolveCastShadow(shadowAttenuation, receiveShadowMask, receiveShadowStrength,
                                         ditherValue, shadowDither, shadowMapSoftness, penumbraReady);
    return lerp(castShadow, 1.0, proceduralMask);
}

// 陰の量子化: toon/smooth を uniform 動的分岐で解決 + 顔正面での陰消し（Doll ポリシー）。
float GetLitMask(float halfLambert, float proceduralMask, float toonStep, float toonFeather)
{
    bool useToon = (_ShadingStyle > 0.5);
    float litMask = ShadeRamp(halfLambert, useToon, toonStep, toonFeather);
    return lerp(litMask, 1.0, proceduralMask);
}

// デュアルローブスペキュラ: スペキュラモデルを uniform 動的分岐で解決。
// aaVariance: Geometric Specular AA のカーネル量（frag 側で1回算出して渡す。0で無効）。
half3 CalculateDualLobeSpecular(
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS, float ndotlSpecular,
    float3 priSpecEnergy, half4 specColor1, float smoothness1, float intensity1,
    float3 secSpecEnergy, half4 specColor2, float smoothness2, float intensity2,
    float specMask, float castShadow, float specF0, float aaVariance,
    out float specularMaskVal)
{
    UNITY_BRANCH
    if (_SpecularModel > 0.5)
    {
        return DualLobeSpecularGGX(
            detailNormalWS, lightDirWS, viewDirectionWS, ndotlSpecular,
            priSpecEnergy, specColor1, smoothness1, intensity1,
            secSpecEnergy, specColor2, smoothness2, intensity2,
            specMask, castShadow, specF0, aaVariance, specularMaskVal);
    }
    return DualLobeSpecularBlinn(
        detailNormalWS, lightDirWS, viewDirectionWS, ndotlSpecular,
        priSpecEnergy, specColor1, smoothness1, intensity1,
        secSpecEnergy, specColor2, smoothness2, intensity2,
        specMask, castShadow, aaVariance, specularMaskVal);
}

// =============================================================================
//  ライト1灯ぶんの寄与（DollSurfaceData 経由）
// =============================================================================

half3 CalculateSingleLight(
    Light light, DollSurfaceData s, half3 viewDirectionWS, float3 objectForwardWS,
    half3 indirectLight, float sdfLit, half sdfMask)
{
    float3 rawDiffuseLight = (light.color * light.distanceAttenuation) + indirectLight;
    float3 diffuseLightEnergy = ApplyLightEnergyLimit(rawDiffuseLight, _DiffuseLightLimit);

    float3 rawSpecLight = light.color * light.distanceAttenuation;
    float3 priSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _PriSpecularLightLimit);
    float3 secSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _SecSpecularLightLimit);

    float proceduralMask = GetProceduralMask(s.baseProceduralMask, objectForwardWS, light.direction);
    half3 diffuseNormalWS = s.detailNormalWS;

    float diffuseNdotL = dot(diffuseNormalWS, light.direction);
    float halfLambert = GetHalfLambert(diffuseNdotL, _HalfLambertWrap);

    float castShadow = GetCastShadow(light.shadowAttenuation, s.receiveShadowMask, _ReceiveShadowStrength, s.ditherValue, _ShadowDither, _ShadowMapSoftness, proceduralMask);

    float finalShade;

    float litMask = GetLitMask(halfLambert, proceduralMask, _ToonStep, _ToonFeather);
    float normalShade = min(litMask, castShadow);

    if (sdfLit >= 0.0)
    {
        float sdfShade = lerp(sdfLit, 1.0, proceduralMask);
        float sdfFinal = min(sdfShade, lerp(1.0, castShadow, _FaceSDFShadowMix));

        finalShade = lerp(normalShade, sdfFinal, sdfMask);
    }
    else
    {
        finalShade = normalShade;
    }

    half3 diffuseColor = GetShadedAlbedo(s.albedo, _ShadowColor.rgb, finalShade);
    half3 finalDiffuse = diffuseColor * diffuseLightEnergy;
    float NdotL_Specular = dot(s.detailNormalWS, light.direction);
    float specularMaskVal;
    half3 finalSpecular = CalculateDualLobeSpecular(
        s.detailNormalWS, light.direction, viewDirectionWS, NdotL_Specular,
        priSpecLightEnergy, _SpecularColor, _Smoothness, _SpecularIntensity,
        secSpecLightEnergy, _SecSpecularColor, _SecSmoothness, _SecSpecularIntensity,
        s.specMask, castShadow, _SpecularF0, s.specAAVariance, specularMaskVal);
    finalSpecular *= (1.0 + s.curvRidge);

    float specLuminance = saturate(dot(finalSpecular, half3(0.299, 0.587, 0.114)));
    finalDiffuse *= (1.0 - specLuminance);

    half3 finalSSS  = CalculateSSS(s.sssTransWS, light.direction, viewDirectionWS, _SSSColor.rgb, _SSSIntensity * s.sssMask, _SSSPower, _SSSDistortion, diffuseLightEnergy, castShadow);
    half3 finalRim  = CalculateRimLight(_RimColor.rgb, s.rimFresnel, _RimIntensity, diffuseLightEnergy, NdotL_Specular, castShadow);
    half3 finalFuzz = CalculatePeachFuzz(_FuzzColor.rgb, s.fuzzFresnel, _FuzzIntensity, diffuseLightEnergy, NdotL_Specular, castShadow);

    half3 finalAniso = CalculateAnisotropicSpecular(
        s.anisoPrecomp,
        s.detailNormalWS, light.direction, viewDirectionWS,
        _AnisoColor, _AnisoThickness,
        _AnisoSecColor, _AnisoSecThickness,
        diffuseLightEnergy, castShadow);

    half3 finalGlitter = half3(0, 0, 0);
    if (s.glitterActive)
    {
        finalGlitter = ApplyGlitterLight(
            s.glitterGeom,
            light.direction, viewDirectionWS,
            _GlitterColor.rgb, _GlitterIntensity,
            _GlitterIridescence, _GlitterIridescenceShift,
            _GlitterBaseReflection, diffuseLightEnergy);
    }

    return finalDiffuse + finalSpecular + finalSSS + finalRim + finalFuzz + finalAniso + finalGlitter;
}

#endif // DOLL_LIGHTING_INCLUDED
