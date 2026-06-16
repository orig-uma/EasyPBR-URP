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
