// =============================================================================
//  Doll_FaceLogic.hlsl
//  キャラクターの顔向けに特化した陰影・マスク計算（Toon・影消し）
// =============================================================================
#ifndef DOLL_FACE_LOGIC_INCLUDED
#define DOLL_FACE_LOGIC_INCLUDED

// [Mask] 顔の正面・上向き判定のベース（ライト非依存）
float GetProceduralMaskBase(half3 normalWS, float3 forwardWS, float frontStrength, float upStrength, float falloff)
{
    float frontMask = saturate(dot(normalWS, forwardWS));
    float upMask = saturate(normalWS.y);
    float mask = pow(frontMask, falloff) * frontStrength + pow(upMask, falloff) * upStrength;
    return smoothstep(0.0, 1.0, saturate(mask));
}

// [Mask] 逆光時の陰残しを考慮した最終マスク
float GetProceduralMask(float baseMask, float3 forwardWS, float3 lightDirWS, float backlightPreserve)
{
    float lightToForwardDot = dot(forwardWS, lightDirWS);
    float backlightFade = smoothstep(-0.3, 0.2, lightToForwardDot);
    backlightFade = lerp(1.0, backlightFade, backlightPreserve);
    return baseMask * backlightFade;
}

// [Normal] 自己陰を消すための法線平滑化
half3 GetFaceSmoothedNormal(half3 detailNormalWS, float3 forwardWS, float proceduralMask, float faceNormalSmoothness)
{
    return normalize(lerp(detailNormalWS, forwardWS, proceduralMask * faceNormalSmoothness));
}

// [Diffuse] ハーフランバート（陰側を持ち上げる）
float GetHalfLambert(float ndotl, float wrap)
{
    return saturate((ndotl + wrap) / (1.0 + wrap));
}

// [Shadow] 落ち影のディザリングとマイルド化
float GetCastShadow(float shadowAttenuation, float receiveShadowMask, float receiveShadowStrength, float ditherValue, float shadowDither, float shadowMapSoftness, float proceduralMask)
{
    float rawShadow = lerp(1.0, shadowAttenuation, receiveShadowMask * receiveShadowStrength);
    float ditheredShadow = rawShadow + (ditherValue - 0.5) * shadowDither * 0.1;
    float castShadow = smoothstep(0.5 - shadowMapSoftness * 0.5, 0.5 + shadowMapSoftness * 0.5, ditheredShadow);
    return lerp(castShadow, 1.0, proceduralMask);
}

// [Diffuse] トゥーンシャドウの境界処理
float GetLitMask(float halfLambert, float proceduralMask, float toonStep, float toonFeather)
{
    float litMask;
#if defined(_SHADINGSTYLE_TOON)
    float softness = max(fwidth(halfLambert), toonFeather);
    litMask = smoothstep(toonStep - softness, toonStep + softness, halfLambert);
#else
    litMask = halfLambert;
#endif
    return lerp(litMask, 1.0, proceduralMask);
}

// [Diffuse] 最終的なアルベドの陰色ブレンド
half3 GetShadedAlbedo(half3 baseColor, half3 shadowColorTint, float finalShade)
{
    half3 shadedBaseColor = baseColor * shadowColorTint;
    return lerp(shadedBaseColor, baseColor, finalShade);
}

#endif // DOLL_FACE_LOGIC_INCLUDED
