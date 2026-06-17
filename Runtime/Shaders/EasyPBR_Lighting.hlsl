// =============================================================================
//  EasyPBR_Lighting.hlsl
//  マテリアルに依存しない汎用的なライティング計算
// =============================================================================
#ifndef EASYPBR_LIGHTING_INCLUDED
#define EASYPBR_LIGHTING_INCLUDED

// [Anti-Blowout] 輝度制限
float3 ApplyLightEnergyLimit(float3 rawLight, float limit)
{
    float lum = max(0.001, dot(rawLight, float3(0.299, 0.587, 0.114)));
    float safeLum = min(lum, limit);
    return rawLight * (safeLum / lum);
}

// [Fresnel] フレネル項の事前計算
void GetFresnelTerms(float ndotv, float rimIntensity, float rimThickness, float fuzzIntensity, float fuzzPower,
                      out float rimFresnel, out float fuzzFresnel)
{
    rimFresnel = 0.0;
    UNITY_BRANCH
    if (rimIntensity > 0.0)
    {
        // Thicknessが 0のとき指数12(極細)、1のとき指数0.5(極太)
        float actualPower = lerp(12.0, 0.5, rimThickness);
        rimFresnel = pow(1.0 - ndotv, actualPower);
    }

    fuzzFresnel = 0.0;
    UNITY_BRANCH
    if (fuzzIntensity > 0.0) { fuzzFresnel = pow(saturate(1.0 - ndotv), fuzzPower); }
}

// [Detail] ブルーノイズによる法線の微細な揺らぎ（肌や布の質感用）
half3 GetGrainNormal(half3 cleanNormalWS, half3 noiseVec, float grainIntensity)
{
    return normalize(cleanNormalWS + noiseVec * grainIntensity * 0.15);
}

// [Specular] デュアルローブスペキュラ計算
half3 CalculateDualLobeSpecular(
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS, float ndotlSpecular,
    float3 priSpecEnergy, half4 specColor1, float smoothness1, float intensity1,
    float3 secSpecEnergy, half4 specColor2, float smoothness2, float intensity2,
    float specMask, float castShadow, out float specularMaskVal)
{
    float3 halfVector = SafeNormalize(lightDirWS + viewDirectionWS);
    float NdotH = saturate(dot(detailNormalWS, halfVector));

    float specPower1 = exp2(10.0 * smoothness1 + 1.0);
    half3 spec1 = specColor1.rgb * pow(NdotH, specPower1) * intensity1 * priSpecEnergy;

    float specPower2 = exp2(10.0 * smoothness2 + 1.0);
    half3 spec2 = specColor2.rgb * pow(NdotH, specPower2) * intensity2 * secSpecEnergy;

    specularMaskVal = saturate(ndotlSpecular * 10.0) * specMask * castShadow;
    return (spec1 + spec2) * specularMaskVal;
}

// -----------------------------------------------------------------------------
// [Specular] CalculateAnisotropicSpecular
//  特定の方向に伸びる異方性ハイライト（髪の毛の天使の輪やシルクなど）。
//  テクスチャを使わず、頂点の接線(Tangent)と従法線(Bitangent)から方向を計算する。
// -----------------------------------------------------------------------------
half3 CalculateAnisotropicSpecular(
    half3 detailNormalWS, float3 tangentWS, float3 bitangentWS, 
    float3 lightDirWS, half3 viewDirectionWS, 
    half4 anisoColor, float thickness, float offset, float angle, 
    float strandScale, float strandStrength, float strandDir, float2 uv, float diffuseLightEnergy, float castShadow)
{
    half3 result = half3(0, 0, 0);
    UNITY_BRANCH
    if (anisoColor.a > 0.0)
    {
        float rad = radians(angle + 90.0); // 90度回した状態が合うことが多かったので90度オフセットしています。
        float s, c;
        sincos(rad, s, c);
        float3 t = normalize(tangentWS * c + bitangentWS * s);

        float dirRad = radians(strandDir);
        float2 dirVec = float2(cos(dirRad), sin(dirRad));
        float strandCoord = dot(uv, dirVec); 

        // --- プロシージャル毛束（繊維）ノイズ ---
        // uv.x の代わりに strandCoord を使用する
        float strandNoise = sin(strandCoord * strandScale) 
                          + sin(strandCoord * strandScale * 2.34) * 0.5 
                          + sin(strandCoord * strandScale * 3.71) * 0.25;

        // オフセット（基本位置）に対して、毛束ノイズでハイライトを上下に揺らす
        float shift = offset + (strandNoise * 0.5) * strandStrength;
        t = normalize(t + detailNormalWS * shift);

        float3 h = SafeNormalize(lightDirWS + viewDirectionWS);
        float dotTH = dot(t, h);
        float sinTH = sqrt(1.0 - saturate(dotTH * dotTH));

        float power = exp2(lerp(10.0, 1.0, thickness)); 
        float spec = pow(saturate(sinTH), power);
        float mask = saturate(dot(detailNormalWS, lightDirWS) * 5.0) * castShadow;

        result = anisoColor.rgb * spec * mask * diffuseLightEnergy;
    }
    return result;
}

float Hash2DTo1D(float2 p)
{
    p = frac(p * float2(443.897, 441.423));
    p += dot(p, p.yx + 19.19);
    return frac((p.x + p.y) * p.x);
}

// Hue → RGB（iridescence用）
half3 HueToRGB(float hue)
{
    half3 rgb = saturate(abs(frac(hue + half3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0);
    return rgb;
}

half3 CalculateGlitter(
    half3 baseNormalWS, float3 lightDirWS, half3 viewDirectionWS,
    float2 uv, float scale, float intensity, float dotSize,
    float tiltStrength, half3 color, float glitterMask,
    float sparsity, float iridescenceAmount, float iridescenceShift,
    float baseReflection)
{
    UNITY_BRANCH
    if (glitterMask <= 0.0 || intensity <= 0.0) return half3(0, 0, 0);

    float2 gridUV  = uv * scale;
    float2 id      = floor(gridUV);
    float2 localUV = frac(gridUV);
    float  invScale = rcp(scale);

    float  minDistSq = 999.0;
    float2 bestId    = id;
    float  bestRand1 = 0.0, bestRand2 = 0.0;

    UNITY_UNROLL
    for (int y = -1; y <= 1; y++)
    {
        UNITY_UNROLL
        for (int x = -1; x <= 1; x++)
        {
            float2 neighborId = id + float2(x, y);

            float r4     = Hash2DTo1D(neighborId + float2(98.76, 54.32));
            float r1     = Hash2DTo1D(neighborId);
            float r2     = Hash2DTo1D(neighborId + float2(45.67, 89.12));

            float2 diff  = float2(x, y) + float2(r1, r2) - localUV;
            float  distSq = dot(diff, diff);
            distSq = (r4 >= sparsity) ? distSq : 999.0; // sparsityで間引き率を制御

            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                bestId    = neighborId;
                bestRand1 = r1;
                bestRand2 = r2;
            }
        }
    }

    float bestRand3 = Hash2DTo1D(bestId + float2(12.34, 56.78));
    float bestRand4 = Hash2DTo1D(bestId + float2(33.21, 77.65)); // 色相用の追加乱数

    float absoluteDist  = sqrt(minDistSq) * invScale;
    float actualDotSize = dotSize * lerp(0.7, 1.0, bestRand3); // サイズばらつきを抑える（スパンコール感↑）

    // ── 形状：外縁と内部を分けて「円盤感」を出す ──────────────
    float outerMask  = 1.0 - smoothstep(actualDotSize * 0.85, actualDotSize, absoluteDist);
    float innerGlow  = 1.0 - smoothstep(0.0, actualDotSize * 0.5, absoluteDist); // 中心ほど明るい
    float dotMask    = outerMask;

    if (dotMask <= 0.0) return half3(0, 0, 0);

    // ── 法線とフラッシュ ────────────────────────────────────────
    float3 randomTilt     = float3(bestRand1 - 0.5, bestRand2 - 0.5, bestRand3 - 0.5) * tiltStrength;
    half3  glitterNormal  = normalize(baseNormalWS + randomTilt);

    float3 halfVector = SafeNormalize(lightDirWS + viewDirectionWS);
    float  NdotH      = saturate(dot(glitterNormal, halfVector));
    float NdotV       = saturate(dot(glitterNormal, viewDirectionWS));

    // フラッシュ：急峻なon/off感を出す（スパンコールは鏡に近い）
    float flashSharp  = pow(NdotH, 500.0) * step(0.94, NdotH);   // メインフラッシュ（点）
    float flashSoft   = pow(NdotH, 80.0)  * step(0.70, NdotH);   // 周囲のやわらかい光
    float flash       = flashSharp + flashSoft * 0.15;

    // ── 色：iridescence（虹色）+ 個体差 ───────────────────────
    // 各スパンコールにランダムな色相を持たせる
    float baseHue         = bestRand4;

    // スパンコールごとに異なるオフセット（個体差）
    // これがないと全スパンコールが同色になる
    float perSequinOffset = bestRand4 * 0.8;

    float2 halfFlat   = halfVector.xz;
    float  halfAzimuth = dot(halfFlat, float2(0.8, 0.6));
    float  iridHue    = frac(baseHue + halfAzimuth * iridescenceShift + perSequinOffset);
    half3 iridColor  = HueToRGB(iridHue);
    half3 finalColor = lerp(color, color * iridColor * 2.0, iridescenceAmount);

    // ── 合成：フラッシュ時 + ベース反射（存在感） ───────────────
    // スパンコールは光っていない時も暗いメタリック感がある
    half3 baseReflColor   = finalColor * baseReflection * (1.0 - NdotV * 0.5); // グレージング

    half3 flashContrib    = finalColor * flash * intensity;
    half3 baseContrib     = baseReflColor * outerMask * (1.0 - innerGlow * 0.5);

    return (flashContrib + baseContrib) * dotMask * glitterMask;
}

// [Optional] 擬似サブサーフェス散乱 (SSS)
half3 CalculateSSS(half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS, half3 sssColor, float sssIntensity, float sssPower, float sssDistortion, float3 diffuseLightEnergy, float castShadow)
{
    half3 result = half3(0, 0, 0);
    UNITY_BRANCH
    if (sssIntensity > 0.0)
    {
        float3 backlightDir = normalize(lightDirWS + detailNormalWS * sssDistortion);
        float backlightTerm = pow(saturate(dot(viewDirectionWS, -backlightDir)), sssPower);
        float sssShadow = lerp(0.4, 1.0, castShadow);
        result = sssColor * backlightTerm * sssIntensity * diffuseLightEnergy * sssShadow;
    }
    return result;
}

// [Optional] リムライト
half3 CalculateRimLight(half3 rimColor, float rimFresnel, float rimIntensity, float3 diffuseLightEnergy, float ndotlSpecular, float castShadow)
{
    float rimLightMask = saturate(ndotlSpecular * 5.0) * castShadow;
    return rimColor * rimFresnel * rimIntensity * diffuseLightEnergy * rimLightMask;
}

// [Optional] ピーチファズ（産毛表現）
half3 CalculatePeachFuzz(half3 fuzzColor, float fuzzFresnel, float fuzzIntensity, float3 diffuseLightEnergy, float ndotlSpecular, float castShadow)
{
    float fuzzMask = fuzzFresnel * saturate(ndotlSpecular) * castShadow;
    return fuzzColor * fuzzMask * fuzzIntensity * diffuseLightEnergy;
}

#endif // EASYPBR_LIGHTING_INCLUDED
