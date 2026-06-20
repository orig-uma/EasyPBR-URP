// =============================================================================
//  DollLighting.hlsl
// -----------------------------------------------------------------------------
//  Origuma/EasyPBR_URP/Doll の陰影計算ロジックをまとめた統合ライティングファイル。
// =============================================================================
#ifndef DOLL_LIGHTING_INCLUDED
#define DOLL_LIGHTING_INCLUDED


// =============================================================================
//  フェイスロジック（旧 Doll_FaceLogic.hlsl）
// =============================================================================

// -----------------------------------------------------------------------------
// [Mask] GetProceduralMaskBase
//  「正面向き」「上向き」の面ほど 1 に近づくマスクのベース値（ライト非依存）。
//  顔の自己陰を消すための土台。frag で 1 回だけ計算して各ライトに渡す。
// -----------------------------------------------------------------------------
float GetProceduralMaskBase(half3 normalWS, float3 forwardWS, float frontStrength, float upStrength, float falloff)
{
    float frontMask = saturate(dot(normalWS, forwardWS));
    float upMask = saturate(normalWS.y);
    float mask = pow(frontMask, falloff) * frontStrength + pow(upMask, falloff) * upStrength;
    return smoothstep(0.0, 1.0, saturate(mask));
}


// -----------------------------------------------------------------------------
// [Mask] GetProceduralMask
//  baseMask に「逆光時は陰を残す」ための backlightFade を掛けた、ライト毎の最終マスク。
// -----------------------------------------------------------------------------
float GetProceduralMask(float baseMask, float3 forwardWS, float3 lightDirWS, float backlightPreserve)
{
    float lightToForwardDot = dot(forwardWS, lightDirWS);
    // ライトが正面側にあるほど 1、背後にあるほど 0 へフェード
    float backlightFade = smoothstep(-0.3, 0.2, lightToForwardDot);
    backlightFade = lerp(1.0, backlightFade, backlightPreserve);
    return baseMask * backlightFade;
}


// -----------------------------------------------------------------------------
// [Normal] GetFaceSmoothedNormal
// -----------------------------------------------------------------------------
half3 GetFaceSmoothedNormal(half3 detailNormalWS, half3 cleanNormalWS, float faceNormalSmoothness)
{
    return normalize(lerp(detailNormalWS, cleanNormalWS, faceNormalSmoothness));
}


// -----------------------------------------------------------------------------
// [Diffuse] GetHalfLambert
//  Half Lambert (Valve流): NdotL を 0..1 に再マップして陰側を持ち上げる。
// -----------------------------------------------------------------------------
float GetHalfLambert(float ndotl, float wrap)
{
    return saturate((ndotl + wrap) / (1.0 + wrap));
}


// -----------------------------------------------------------------------------
// [Shadow] GetCastShadow
//  落ち影(shadow map)専用のソフトランプ。
//  ブルーノイズのディザで量子化バンドを分解してからスムーズに減衰させる。
//  顔の正面（proceduralMaskが強い所）では落ち影自体も消す。
// -----------------------------------------------------------------------------
float GetCastShadow(float shadowAttenuation, float receiveShadowMask, float receiveShadowStrength,
                     float ditherValue, float shadowDither, float shadowMapSoftness, float proceduralMask)
{
    float rawShadow = lerp(1.0, shadowAttenuation, receiveShadowMask * receiveShadowStrength);

    #if defined(_SHADOWQUALITY_PCF) || defined(_SHADOWQUALITY_PCSS)
        // PCFが連続的なペナンブラを生成済み → ディザ＆再量子化しない
        float castShadow = rawShadow;
    #else
        float ditheredShadow = rawShadow + (ditherValue - 0.5) * shadowDither * 0.1;
        float castShadow = smoothstep(0.5 - shadowMapSoftness * 0.5, 0.5 + shadowMapSoftness * 0.5, ditheredShadow);
    #endif

    return lerp(castShadow, 1.0, proceduralMask);
}


// -----------------------------------------------------------------------------
// [Diffuse] GetLitMask
//  顔の陰(halfLambert)を Toon/Smooth で量子化する。
//  Toon の場合のみ fwidth で 1px のアンチエイリアス幅を確保（落ち影には掛けない）。
//  顔の正面（proceduralMaskが強い所）では陰自体も消す。
// -----------------------------------------------------------------------------
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

// -----------------------------------------------------------------------------
// [Anti-Blowout] ApplyLightEnergyLimit
//  rawLight の輝度(luminance)が limit を超えないようスケールする。
//  Diffuse / Primary Spec / Secondary Spec それぞれ個別の limit で呼ぶ。
// -----------------------------------------------------------------------------
float3 ApplyLightEnergyLimit(float3 rawLight, float limit)
{
    float lum = max(0.001, dot(rawLight, float3(0.299, 0.587, 0.114)));
    float safeLum = min(lum, limit);
    return rawLight * (safeLum / lum);
}


// -----------------------------------------------------------------------------
// [Diffuse] GetShadedAlbedo
//  ベースカラーに影色(Tint)を乗算し、finalShade で明暗をブレンドする。
//  元のテクスチャの色味を残しつつ影部分にだけ色を乗せる。
// -----------------------------------------------------------------------------
half3 GetShadedAlbedo(half3 baseColor, half3 shadowColorTint, float finalShade)
{
    half3 shadedBaseColor = baseColor * shadowColorTint;
    return lerp(shadedBaseColor, baseColor, finalShade);
}

// -----------------------------------------------------------------------------
// [Specular] 微小面 GGX BRDF ヘルパー（_SPECULARMODEL_GGX 時のみ使用）
// -----------------------------------------------------------------------------
float Doll_D_GGX(float NdotH, float alpha)
{
    float a2 = alpha * alpha;
    float d  = (NdotH * a2 - NdotH) * NdotH + 1.0; // NdotH^2 (a2-1) + 1
    return a2 / (PI * d * d + 1e-7);
}

// 高さ相関 Smith 可視性（1/(4 NdotL NdotV) を内包）
float Doll_V_SmithGGX(float NdotL, float NdotV, float alpha)
{
    float a2 = alpha * alpha;
    float ggxV = NdotL * sqrt(NdotV * NdotV * (1.0 - a2) + a2);
    float ggxL = NdotV * sqrt(NdotL * NdotL * (1.0 - a2) + a2);
    return 0.5 / max(ggxV + ggxL, 1e-5);
}

float3 Doll_F_Schlick(float VdotH, float3 f0)
{
    float f = pow(1.0 - VdotH, 5.0);
    return f0 + (1.0 - f0) * f;
}

// 1ローブ分の Cook-Torrance（D*V*F。NdotL は呼び出し側で乗算）
half3 Doll_GGXLobe(float NdotH, float NdotL, float NdotV, float VdotH,
              float smoothness, half3 tint, float3 f0)
{
    float roughness = 1.0 - saturate(smoothness);
    float alpha     = max(roughness * roughness, 2e-3); // 完全鏡面のギラつき/NaN回避
    float D = Doll_D_GGX(NdotH, alpha);
    float V = Doll_V_SmithGGX(NdotL, NdotV, alpha);
    half3 F = Doll_F_Schlick(VdotH, f0);
    return D * V * F * tint;
}

// -----------------------------------------------------------------------------
// [Specular] CalculateDualLobeSpecular
//  Blinn-Phong を 2 ローブ重ねたスペキュラ。
//  out specularMaskVal は呼び出し側でエネルギー保存(Diffuse減算)に使う。
// -----------------------------------------------------------------------------
half3 CalculateDualLobeSpecular(
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS, float ndotlSpecular,
    float3 priSpecEnergy, half4 specColor1, float smoothness1, float intensity1,
    float3 secSpecEnergy, half4 specColor2, float smoothness2, float intensity2,
    float specMask, float castShadow, float specF0,
    out float specularMaskVal)
{
    float3 halfVector = SafeNormalize(lightDirWS + viewDirectionWS);
    float  NdotH = saturate(dot(detailNormalWS, halfVector));

    specularMaskVal = saturate(ndotlSpecular * 10.0) * specMask * castShadow;

    #if defined(_SPECULARMODEL_GGX)
    // --- 物理ベース: Fresnel・可視性込みで自然な裾と縁の輝き ---
    float NdotL = saturate(ndotlSpecular);
    float NdotV = saturate(dot(detailNormalWS, viewDirectionWS));
    float VdotH = saturate(dot(viewDirectionWS, halfVector));
    float3 f0   = specF0.xxx;

    half3 spec1 = Doll_GGXLobe(NdotH, NdotL, NdotV, VdotH, smoothness1, specColor1.rgb, f0)
                  * intensity1 * priSpecEnergy;
    half3 spec2 = Doll_GGXLobe(NdotH, NdotL, NdotV, VdotH, smoothness2, specColor2.rgb, f0)
                  * intensity2 * secSpecEnergy;

    return (spec1 + spec2) * NdotL * (specMask * castShadow);
    #else
    // --- 従来 Blinn-Phong（既定・最軽量・見た目互換） ---
    float specPower1 = exp2(10.0 * smoothness1 + 1.0);
    half3 spec1 = specColor1.rgb * pow(NdotH, specPower1) * intensity1 * priSpecEnergy;

    float specPower2 = exp2(10.0 * smoothness2 + 1.0);
    half3 spec2 = specColor2.rgb * pow(NdotH, specPower2) * intensity2 * secSpecEnergy;

    return (spec1 + spec2) * specularMaskVal;
    #endif
}


// -----------------------------------------------------------------------------
// [Optional] CalculateSSS
//  逆光時に肌が透けるような擬似サブサーフェス散乱。
//  sssIntensity = 0（既定）のときは pow/normalize を含めて完全にスキップされる。
// -----------------------------------------------------------------------------
half3 CalculateSSS(
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS,
    half3 sssColor, float sssIntensity, float sssPower, float sssDistortion,
    float3 diffuseLightEnergy, float castShadow)
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


// -----------------------------------------------------------------------------
// [Optional] CalculateRimLight
//  フレネル(輪郭)に乗る縁の光。rimFresnel は frag 側で算出済みのものを渡す。
//  rimIntensity = 0 のときは rimFresnel = 0 のため寄与は自動的に 0。
// -----------------------------------------------------------------------------
half3 CalculateRimLight(half3 rimColor, float rimFresnel, float rimIntensity,
                         float3 diffuseLightEnergy, float ndotlSpecular, float castShadow)
{
    float rimLightMask = saturate(ndotlSpecular * 5.0) * castShadow;
    return rimColor * rimFresnel * rimIntensity * diffuseLightEnergy * rimLightMask;
}


// -----------------------------------------------------------------------------
// [Optional] CalculatePeachFuzz
//  輪郭にうっすら乗る産毛のような柔らかい縁の光沢。fuzzFresnel は frag 側で算出済み。
// -----------------------------------------------------------------------------
half3 CalculatePeachFuzz(half3 fuzzColor, float fuzzFresnel, float fuzzIntensity,
                          float3 diffuseLightEnergy, float ndotlSpecular, float castShadow)
{
    float fuzzMask = fuzzFresnel * saturate(ndotlSpecular) * castShadow;
    return fuzzColor * fuzzMask * fuzzIntensity * diffuseLightEnergy;
}


// -----------------------------------------------------------------------------
// [Fresnel] GetFresnelTerms
//  Rim / Peach Fuzz 用のフレネル項 (1-NdotV)^power をまとめて算出（ライト非依存）。
//  rimThickness(0..1): 0 = 極細(指数12)、1 = 極太(指数0.5)
// -----------------------------------------------------------------------------
void GetFresnelTerms(float ndotv, float rimIntensity, float rimThickness, float fuzzIntensity, float fuzzPower,
                      out float rimFresnel, out float fuzzFresnel)
{
    rimFresnel = 0.0;
    UNITY_BRANCH
    if (rimIntensity > 0.0)
    {
        // Thickness が 0 のとき指数12(極細)、1 のとき指数0.5(極太)
        float actualPower = lerp(12.0, 0.5, rimThickness);
        rimFresnel = pow(1.0 - ndotv, actualPower);
    }

    fuzzFresnel = 0.0;
    UNITY_BRANCH
    if (fuzzIntensity > 0.0)
    {
        fuzzFresnel = pow(saturate(1.0 - ndotv), fuzzPower);
    }
}


// -----------------------------------------------------------------------------
// [Detail] GetGrainNormal
//  ブルーノイズで法線を僅かに揺らし、つるんとし過ぎない肌質感(grain)を作る。
//  noiseVec はテクスチャサンプル結果を *2-1 した値( -1..1 )を渡す。
//  grainIntensity = 0 のときは normalize をスキップして cleanNormalWS をそのまま返す。
// -----------------------------------------------------------------------------
half3 GetGrainNormal(half3 cleanNormalWS, half3 noiseVec, float grainIntensity)
{
    UNITY_BRANCH
    if (grainIntensity <= 0.0) return cleanNormalWS;
    return normalize(cleanNormalWS + noiseVec * grainIntensity * 0.15);
}


// =============================================================================
//  異方性ハイライト 
// =============================================================================

struct AnisoPrecomp
{
    float3 tangentDir;   // 主ハイライト用（鋭い・天使の輪）
    float3 tangentDir2;  // 副ハイライト用（広い・毛色付き）
    float  strandNoise;  // 毛束ノイズ（副バンドのきらめきマスクに再利用）
};

AnisoPrecomp PrecomputeAnisoTangent(
    float3 tangentWS, float3 bitangentWS, half3 normalWS, float2 uv,
    float angle, float strandDir, float strandScale, float strandStrength,
    float offset, float offset2)
{
    float rad = radians(angle + 90.0);
    float s, c; sincos(rad, s, c);
    float3 tBase = normalize(tangentWS * c + bitangentWS * s);

    float dirRad = radians(strandDir);
    float2 dirVec = float2(cos(dirRad), sin(dirRad));
    float strandCoord = dot(uv, dirVec);

    float strandNoise = sin(strandCoord * strandScale)
                      + sin(strandCoord * strandScale * 2.34) * 0.5
                      + sin(strandCoord * strandScale * 3.71) * 0.25;

    float noiseShift = (strandNoise * 0.5) * strandStrength;

    AnisoPrecomp result;
    result.tangentDir  = normalize(tBase + normalWS * (offset  + noiseShift));
    result.tangentDir2 = normalize(tBase + normalWS * (offset2 + noiseShift));
    result.strandNoise = strandNoise;
    return result;
}

half3 CalculateAnisotropicSpecular(
    AnisoPrecomp anisoPrecomp,
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS,
    half4 anisoColor, float thickness,
    half4 anisoColor2, float thickness2,
    float3 diffuseLightEnergy, float castShadow)
{
    half3 result = half3(0, 0, 0);
    UNITY_BRANCH
    if (anisoColor.a > 0.0)
    {
        float3 h = SafeNormalize(lightDirWS + viewDirectionWS);
        float  mask = saturate(dot(detailNormalWS, lightDirWS) * 5.0) * castShadow;

        // --- 主バンド（従来と同一） ---
        float dotTH1 = dot(anisoPrecomp.tangentDir, h);
        float sinTH1 = sqrt(1.0 - saturate(dotTH1 * dotTH1));
        float power1 = exp2(lerp(10.0, 1.0, thickness));
        result = anisoColor.rgb * pow(saturate(sinTH1), power1);

        // --- 副バンド（広い・毛色付き・ノイズきらめき・縁で強まる） ---
        UNITY_BRANCH
        if (anisoColor2.a > 0.0)
        {
            float VdotH   = saturate(dot(viewDirectionWS, h));
            float fresnel = lerp(0.5, 1.0, pow(1.0 - VdotH, 4.0));
            float dotTH2  = dot(anisoPrecomp.tangentDir2, h);
            float sinTH2  = sqrt(1.0 - saturate(dotTH2 * dotTH2));
            float power2  = exp2(lerp(10.0, 1.0, thickness2));
            float sparkle = saturate(anisoPrecomp.strandNoise * 0.5 + 0.5);
            result += anisoColor2.rgb * pow(saturate(sinTH2), power2) * sparkle * fresnel;
        }

        result = result * mask * diffuseLightEnergy;
    }
    return result;
}


// =============================================================================
//  グリッタ（スパンコール)
// =============================================================================

// [Glitter] Hash2DTo1D  — 2D → 1D のハッシュ関数
float Hash2DTo1D(float2 p)
{
    p = frac(p * float2(443.897, 441.423));
    p += dot(p, p.yx + 19.19);
    return frac((p.x + p.y) * p.x);
}

// [Glitter] HueToRGB  — iridescence（虹色）用 Hue → RGB 変換
half3 HueToRGB(float hue)
{
    half3 rgb = saturate(abs(frac(hue + half3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0);
    return rgb;
}

// -----------------------------------------------------------------------------
// [Glitter] GlitterGeom
//  PrepareGlitter が計算したライト非依存データをまとめる構造体。
//  全ライトで共有される。
// -----------------------------------------------------------------------------
struct GlitterGeom
{
    float  dotMask;          // スパンコール円盤のマスク（距離ベース）
    float  outerMask;        // 円盤外縁マスク
    float  innerGlow;        // 中心ほど明るい内部グロー
    half3  glitterNormal;    // ランダムチルト後の法線
    float  NdotV;            // glitterNormal · viewDir（ベース反射グレージング用）
    float  baseHue;          // iridescence 基準色相（セル固有・ライト非依存）
    float  perSequinOffset;  // スパンコールごとの色相個体差
};

// [Glitter] PrepareGlitter
//  ライトに依存しない幾何・ランダム計算。frag でライトループの前に 1 回だけ呼ぶ。
//  false を返した場合は ApplyGlitterLight の呼び出しをスキップできる。
bool PrepareGlitter(
    half3 baseNormalWS, half3 viewDirectionWS,
    float2 uv, float scale, float dotSize,
    float tiltStrength, float glitterMask,
    float intensity, float sparsity,
    out GlitterGeom geom)
{
    geom = (GlitterGeom)0;
    if (glitterMask <= 0.0 || intensity <= 0.0) return false;

    float2 gridUV   = uv * scale;
    float2 id       = floor(gridUV);
    float2 localUV  = frac(gridUV);
    float  invScale = rcp(scale);

    float  minDistSq = 999.0;
    float2 bestId    = id;
    float  bestRand1 = 0.0, bestRand2 = 0.0;

    // 近傍 3×3 セルで最近傍スパンコールを探索
    UNITY_UNROLL
    for (int y = -1; y <= 1; y++)
    {
        UNITY_UNROLL
        for (int x = -1; x <= 1; x++)
        {
            float2 neighborId = id + float2(x, y);

            float r4      = Hash2DTo1D(neighborId + float2(98.76, 54.32));
            float r1      = Hash2DTo1D(neighborId);
            float r2      = Hash2DTo1D(neighborId + float2(45.67, 89.12));

            float2 diff   = float2(x, y) + float2(r1, r2) - localUV;
            float  distSq = dot(diff, diff);
            // sparsity で間引き率を制御
            distSq = (r4 >= sparsity) ? distSq : 999.0;

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
    float bestRand4 = Hash2DTo1D(bestId + float2(33.21, 77.65));

    float absoluteDist  = sqrt(minDistSq) * invScale;
    float actualDotSize = dotSize * lerp(0.7, 1.0, bestRand3);

    // 形状：外縁と内部を分けて「円盤感」を出す
    geom.outerMask = 1.0 - smoothstep(actualDotSize * 0.85, actualDotSize, absoluteDist);
    geom.innerGlow = 1.0 - smoothstep(0.0, actualDotSize * 0.5, absoluteDist);
    geom.dotMask   = geom.outerMask;

    // 円盤外なら以降の計算をスキップ
    if (geom.dotMask <= 0.0) return false;

    // ランダムチルト後の法線と NdotV（ビュー依存・ライト非依存）
    float3 randomTilt   = float3(bestRand1 - 0.5, bestRand2 - 0.5, bestRand3 - 0.5) * tiltStrength;
    geom.glitterNormal  = normalize(baseNormalWS + randomTilt);
    geom.NdotV          = saturate(dot(geom.glitterNormal, viewDirectionWS));

    // iridescence 用乱数（セル固有・ライト非依存）
    geom.baseHue         = bestRand4;
    geom.perSequinOffset = bestRand4 * 0.8;

    return true;
}

// [Glitter] ApplyGlitterLight
//  PrepareGlitter で計算した幾何データにライトエネルギーを乗算して最終輝度を返す。
//  ハーフベクトルと iridescence 色相はライト依存のためここで計算する。
half3 ApplyGlitterLight(
    GlitterGeom geom,
    float3 lightDirWS, half3 viewDirectionWS,
    half3 color, float intensity,
    float iridescenceAmount, float iridescenceShift,
    float baseReflection, float3 diffuseLightEnergy)
{
    float3 halfVector = SafeNormalize(lightDirWS + viewDirectionWS);
    float  NdotH      = saturate(dot(geom.glitterNormal, halfVector));

    // フラッシュ：急峻な on/off 感を出す（スパンコールは鏡に近い）
    float flashSharp  = pow(NdotH, 500.0) * step(0.94, NdotH);   // メインフラッシュ（点）
    float flashSoft   = pow(NdotH, 80.0)  * step(0.70, NdotH);   // 周囲のやわらかい光
    float flash       = flashSharp + flashSoft * 0.15;

    // iridescence（虹色）：ハーフベクトルの方位角に基づく色相変化（ライト依存）
    float2 halfFlat    = halfVector.xz;
    float  halfAzimuth = dot(halfFlat, float2(0.8, 0.6));
    float  iridHue    = frac(geom.baseHue + halfAzimuth * iridescenceShift + geom.perSequinOffset);
    half3  iridColor  = HueToRGB(iridHue);
    half3  finalColor = lerp(color, color * iridColor * 2.0, iridescenceAmount);

    // 合成：フラッシュ時 + ベース反射（スパンコールは光っていない時も暗いメタリック感がある）
    half3 baseReflColor = finalColor * baseReflection * (1.0 - geom.NdotV * 0.5);
    half3 flashContrib  = finalColor * flash * intensity;
    half3 baseContrib   = baseReflColor * geom.outerMask * (1.0 - geom.innerGlow * 0.5);

    return (flashContrib + baseContrib) * geom.dotMask * diffuseLightEnergy;
}


#endif // DOLL_LIGHTING_INCLUDED
