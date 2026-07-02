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
    half3 indirectLight, float sdfLit, half sdfMask, half fillIntensity)
{
    // 直接光のみ Anti-Blowout（Diffuse Light Limit）でクランプし、間接光は
    // その後に加算する。合算後にクランプすると、直接光が上限に達した時点で
    // 間接光の寄与が丸ごと削られ、Indirect Light の調整が見た目に反映されない。
    // 間接光側の上限管理は Indirect Intensity が担う。
    float3 directDiffuse = light.color * light.distanceAttenuation;
    float3 diffuseLightEnergy = ApplyLightEnergyLimit(directDiffuse, _DiffuseLightLimit) + indirectLight;

    float3 rawSpecLight = light.color * light.distanceAttenuation;
    float3 priSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _PriSpecularLightLimit);
    float3 secSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _SecSpecularLightLimit);

    float proceduralMask = GetProceduralMask(s.baseProceduralMask, objectForwardWS, light.direction);
    // 拡散の陰はシェーディング法線（未ベイク時は detailNormalWS と同一）で駆動。
    half3 diffuseNormalWS = s.shadeNormalWS;

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

    // 陰色は GatherSurface で事前計算済み（Shadow Color + Hue Shift/Saturation 適用済み）。
    // Cast Shadow Color 有効時は「角度の陰」と「落ち影」を分離して別の色で塗る
    // （落ち影を寒色に振る等の映像的な塗り分け）。無効時は従来どおり同色に合成。
    half3 diffuseColor;
    UNITY_BRANCH
    if (_CastShadowColor.a > 0.0)
    {
        float angleShade = litMask;
        float castPart   = castShadow;
        if (sdfLit >= 0.0)
        {
            float sdfAngle = lerp(sdfLit, 1.0, proceduralMask);
            angleShade = lerp(litMask, sdfAngle, sdfMask);
            castPart   = lerp(castShadow, lerp(1.0, castShadow, _FaceSDFShadowMix), sdfMask);
        }
        diffuseColor = lerp(s.shadowAlbedo, s.albedo, angleShade);
        diffuseColor = lerp(s.castShadowAlbedo, diffuseColor, castPart);
    }
    else
    {
        diffuseColor = lerp(s.shadowAlbedo, s.albedo, finalShade);
    }

    // 2影: 1影より深い位置に第2の陰ランプを重ねる（アニメの1影・2影構成）。
    // 光角度ベース（落ち影は1影のまま）。顔 SDF 領域では SDF 由来の連続値で
    // 駆動し、SDF が消したはずの法線由来の陰バンドを顔に持ち込まない。
    float shade2 = 1.0;
    bool shadow2On = (_Shadow2Color.a > 0.0);
    UNITY_BRANCH
    if (shadow2On)
    {
        float shadeSrc = (sdfLit >= 0.0) ? lerp(halfLambert, sdfLit, sdfMask) : halfLambert;
        // ToonRamp（smoothstep + fwidth AA）を両スタイル共通で使用。
        // Smooth では Softness を上げて広いグラデーションにする。
        shade2 = ToonRamp(shadeSrc, _Shadow2Step, _Shadow2Feather);
        shade2 = lerp(shade2, 1.0, proceduralMask);
        diffuseColor = lerp(s.shadow2Albedo, diffuseColor, shade2);
    }

    // スキンスキャッタ: 明暗境界を赤方向へ滲ませる（pre-integrated 近似）。
    // 2影が有効なら 1影・2影 両方の境界に乗る。
    UNITY_BRANCH
    if (_SkinScatterIntensity > 0.0)
    {
        float scatterAmount = _SkinScatterIntensity * s.scatterCurvMask;
        diffuseColor = ApplyTerminatorScatter(
            diffuseColor, s.albedo, _SkinScatterColor.rgb,
            finalShade, _SkinScatterWidth, scatterAmount);
        UNITY_BRANCH
        if (shadow2On)
        {
            diffuseColor = ApplyTerminatorScatter(
                diffuseColor, s.albedo, _SkinScatterColor.rgb,
                shade2, _SkinScatterWidth, scatterAmount);
        }
    }
    half3 finalDiffuse = diffuseColor * diffuseLightEnergy;

    // 照り返し（フィルライト）: 指定方向からのバウンス光を陰側に注ぐ。
    // 地面照り返し（Pitch -90 = 真下から）が典型。メインライトの寄与とは独立した
    // 加算光なので、暗転側でも設定した強さで発色する（メインライト呼び出しのみ有効）。
    UNITY_BRANCH
    if (fillIntensity > 0.0)
    {
        float pitchRad = radians(_FillPitch);
        float yawRad   = radians(_FillYaw);
        // サーフェスから光源へ向かう方向（light.direction と同じ規約）。
        float3 fillDirWS = float3(cos(pitchRad) * sin(yawRad), sin(pitchRad), cos(pitchRad) * cos(yawRad));

        float fillShade = HalfLambert(dot(diffuseNormalWS, fillDirWS), 0.5); // 柔らかく回り込む
        float shadeSide = lerp(1.0, 1.0 - finalShade, _FillShadeOnly);       // 主光の陰側に限定
        finalDiffuse += s.albedo * _FillColor.rgb * (fillIntensity * fillShade * shadeSide);
    }
    float NdotL_Specular = dot(s.detailNormalWS, light.direction);
    float specularMaskVal;
    half3 finalSpecular = CalculateDualLobeSpecular(
        s.detailNormalWS, light.direction, viewDirectionWS, NdotL_Specular,
        priSpecLightEnergy, _SpecularColor, _Smoothness, _SpecularIntensity,
        secSpecLightEnergy, _SecSpecularColor, _SecSmoothness, _SecSpecularIntensity,
        s.specMask, castShadow, _SpecularF0, s.specAAVariance, specularMaskVal);
    finalSpecular *= (1.0 + s.curvRidge);

    // トーンスペキュラ: トーンマップした輝度をしきい値で切り、縁のパキッとした
    // 様式的ハイライトにする（内側のグラデーションは保持。0..1 で連続とブレンド）。
    UNITY_BRANCH
    if (_ToonSpecular > 0.0)
    {
        float specLum = Luminance601(finalSpecular);
        float mapped = specLum / (1.0 + specLum); // HDR 輝度を 0..1 に写像
        float feather = max(_ToonSpecularFeather * 0.25, fwidth(mapped));
        float quantized = smoothstep(_ToonSpecularStep - feather, _ToonSpecularStep + feather, mapped);
        finalSpecular *= lerp(1.0, quantized, _ToonSpecular);
    }

    // 陰面スペキュラ減衰: 陰ランプ（1影・2影）に入った面のハイライトを沈める。
    // 落ち影（castShadow）はローブ内で従来から適用済み。0 で従来どおり。
    UNITY_BRANCH
    if (_SpecularShadeInfluence > 0.0)
    {
        finalSpecular *= lerp(1.0, min(finalShade, shade2), _SpecularShadeInfluence);
    }

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
