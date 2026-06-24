// =============================================================================
//  DollLighting.hlsl  (policy / thin wrapper)
// -----------------------------------------------------------------------------
//  キャラ(Doll)固有のポリシーだけを保持する薄い層。
//  汎用計算は Common ライブラリへ委譲し、ここでは
//   (1) 顔の手続き的マスク（自己陰消し）
//   (2) キーワード分岐（toon / specular model / shadow quality）の解決
//   (3) 旧公開関数名の互換ラッパー
//  のみを担う。既存フラグメントの呼び出しはこのファイルで従来通り通る。
//
//  前提: URP Core.hlsl を本ファイルより前に include しておくこと。
//  ※ Common フォルダの配置に合わせて下の include パスを調整すること。
// =============================================================================
#ifndef DOLL_LIGHTING_INCLUDED
#define DOLL_LIGHTING_INCLUDED

#include "../Common/Common.hlsl"

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
half3 CalculateDualLobeSpecular(
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS, float ndotlSpecular,
    float3 priSpecEnergy, half4 specColor1, float smoothness1, float intensity1,
    float3 secSpecEnergy, half4 specColor2, float smoothness2, float intensity2,
    float specMask, float castShadow, float specF0,
    out float specularMaskVal)
{
    UNITY_BRANCH
    if (_SpecularModel > 0.5)
    {
        return DualLobeSpecularGGX(
            detailNormalWS, lightDirWS, viewDirectionWS, ndotlSpecular,
            priSpecEnergy, specColor1, smoothness1, intensity1,
            secSpecEnergy, specColor2, smoothness2, intensity2,
            specMask, castShadow, specF0, specularMaskVal);
    }
    return DualLobeSpecularBlinn(
        detailNormalWS, lightDirWS, viewDirectionWS, ndotlSpecular,
        priSpecEnergy, specColor1, smoothness1, intensity1,
        secSpecEnergy, specColor2, smoothness2, intensity2,
        specMask, castShadow, specularMaskVal);
}

// CalculateSSS / CalculateRimLight / CalculatePeachFuzz / GetFresnelTerms /
// GetGrainNormal / AnisoPrecomp 系 / Glitter 系 は Common 側で同名提供される
// （include 済みのためここで再定義不要）。

#endif // DOLL_LIGHTING_INCLUDED
