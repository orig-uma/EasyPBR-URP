// =============================================================================
//  DollLighting.hlsl
// -----------------------------------------------------------------------------
//  Origuma/EasyPBR_URP/Doll の陰影計算ロジックを「関数単位」に分割したファイル。
//  各関数は単一の役割のみを持つ。ペアプロ・レビュー・修正は基本この単位で行う。
//
//  依存:
//    - Core.hlsl / Lighting.hlsl が呼び出し側で include されていること
//    - UNITY_BRANCH, SafeNormalize, GetWorldToViewMatrix などURPの定義に依存
// =============================================================================
#ifndef DOLL_LIGHTING_INCLUDED
#define DOLL_LIGHTING_INCLUDED


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
//  マスクが強い面ほど法線をモデル正面(forwardWS)へ寄せる。
//  鼻や頬の凹凸が作る自己陰を NdotL の段階で消すための法線平滑化。
// -----------------------------------------------------------------------------
half3 GetFaceSmoothedNormal(half3 detailNormalWS, float3 forwardWS, float proceduralMask, float faceNormalSmoothness)
{
    return normalize(lerp(detailNormalWS, forwardWS, proceduralMask * faceNormalSmoothness));
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
    // 効きをマイルドにするため 0.1 を乗算
    float ditheredShadow = rawShadow + (ditherValue - 0.5) * shadowDither * 0.1;
    float castShadow = smoothstep(0.5 - shadowMapSoftness * 0.5, 0.5 + shadowMapSoftness * 0.5, ditheredShadow);
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
// [Specular] CalculateDualLobeSpecular
//  Blinn-Phong を 2 ローブ重ねたスペキュラ。
//  out specularMaskVal は呼び出し側でエネルギー保存(Diffuse減算)に使う。
// -----------------------------------------------------------------------------
half3 CalculateDualLobeSpecular(
    half3 detailNormalWS, float3 lightDirWS, half3 viewDirectionWS, float ndotlSpecular,
    float3 priSpecEnergy, half4 specColor1, float smoothness1, float intensity1,
    float3 secSpecEnergy, half4 specColor2, float smoothness2, float intensity2,
    float specMask, float castShadow,
    out float specularMaskVal)
{
    float3 halfVector = SafeNormalize(lightDirWS + viewDirectionWS);
    float NdotH = saturate(dot(detailNormalWS, halfVector));

    // smoothness(0..1) を exp2 で鏡面指数へ変換（大きいほど鋭いハイライト）
    float specPower1 = exp2(10.0 * smoothness1 + 1.0);
    half3 spec1 = specColor1.rgb * pow(NdotH, specPower1) * intensity1 * priSpecEnergy;

    float specPower2 = exp2(10.0 * smoothness2 + 1.0);
    half3 spec2 = specColor2.rgb * pow(NdotH, specPower2) * intensity2 * secSpecEnergy;

    // ライトの裏側ではハイライトを出さない + マスク + 落ち影で減衰
    specularMaskVal = saturate(ndotlSpecular * 10.0) * specMask * castShadow;
    return (spec1 + spec2) * specularMaskVal;
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
//  各 Intensity = 0 のときは pow をスキップ（uniform分岐なので安価・variant増加なし）。
// -----------------------------------------------------------------------------
void GetFresnelTerms(float ndotv, float rimIntensity, float rimPower, float fuzzIntensity, float fuzzPower,
                      out float rimFresnel, out float fuzzFresnel)
{
    rimFresnel = 0.0;
    UNITY_BRANCH
    if (rimIntensity > 0.0)
    {
        rimFresnel = pow(1.0 - ndotv, rimPower);
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
// -----------------------------------------------------------------------------
half3 GetGrainNormal(half3 cleanNormalWS, half3 noiseVec, float grainIntensity)
{
    return normalize(cleanNormalWS + noiseVec * grainIntensity * 0.15);
}


// -----------------------------------------------------------------------------
// [MatCap] GetMatCapUV
//  ビュー空間法線のXYを 0..1 のUVに変換する（球状の映り込み風の擬似ライティング）。
// -----------------------------------------------------------------------------
float2 GetMatCapUV(half3 normalWS)
{
    float3 normalVS = mul((float3x3)GetWorldToViewMatrix(), normalWS);
    return normalVS.xy * 0.5 + 0.5;
}


// -----------------------------------------------------------------------------
// [MatCap] ApplyMatCap
//  サンプルした matcapColor を Add/Multiply で finalColor に合成する。
// -----------------------------------------------------------------------------
half3 ApplyMatCap(half3 finalColor, half3 matcapColor, float matcapIntensity)
{
#if defined(_MATCAPBLEND_ADD)
    return finalColor + matcapColor * matcapIntensity; // 加算: 光沢を足す
#elif defined(_MATCAPBLEND_MULTIPLY)
    return finalColor * lerp(half3(1.0, 1.0, 1.0), matcapColor, saturate(matcapIntensity)); // 乗算: 陰影付け
#else
    return finalColor;
#endif
}


// -----------------------------------------------------------------------------
// [Optional] CalculateEmission
//  ライティングに依存しない自己発光色。
//  emissionMapColor はテクスチャから取得した色(0..1)、emissionColor は[HDR]の色、
//  emissionIntensity は全体の強さ。3つを掛け合わせて最終加算色を返す。
// -----------------------------------------------------------------------------
half3 CalculateEmission(half3 emissionMapColor, half3 emissionColor, float emissionIntensity)
{
    return emissionMapColor * emissionColor * emissionIntensity;
}


#endif // DOLL_LIGHTING_INCLUDED
