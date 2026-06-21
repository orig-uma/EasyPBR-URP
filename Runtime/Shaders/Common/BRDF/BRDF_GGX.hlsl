// =============================================================================
//  BRDF_GGX.hlsl
// -----------------------------------------------------------------------------
//  微小面 GGX BRDF の純粋ヘルパーと 1ローブ分の Cook-Torrance。
//  Blinn-Phong ローブも併設し、モデル選択は呼び出し側の責務とする
//  （ライブラリ内にキーワード分岐を持たない）。
//  前提: URP Core.hlsl（PI を使用）。
// =============================================================================
#ifndef EASYPBR_BRDF_GGX_INCLUDED
#define EASYPBR_BRDF_GGX_INCLUDED

float EasyPBR_D_GGX(float NdotH, float alpha)
{
    float a2 = alpha * alpha;
    float d  = (NdotH * a2 - NdotH) * NdotH + 1.0; // NdotH^2 (a2-1) + 1
    return a2 / (PI * d * d + 1e-7);
}

// 高さ相関 Smith 可視性（1/(4 NdotL NdotV) を内包）。
float EasyPBR_V_SmithGGX(float NdotL, float NdotV, float alpha)
{
    float a2 = alpha * alpha;
    float ggxV = NdotL * sqrt(NdotV * NdotV * (1.0 - a2) + a2);
    float ggxL = NdotV * sqrt(NdotL * NdotL * (1.0 - a2) + a2);
    return 0.5 / max(ggxV + ggxL, 1e-5);
}

float3 EasyPBR_F_Schlick(float VdotH, float3 f0)
{
    float f = pow(1.0 - VdotH, 5.0);
    return f0 + (1.0 - f0) * f;
}

float SmoothnessToAlpha(float smoothness)
{
    float roughness = 1.0 - saturate(smoothness);
    return max(roughness * roughness, 2e-3); // 完全鏡面のギラつき/NaN回避
}

// 1ローブ分の Cook-Torrance（D*V*F。NdotL は呼び出し側で乗算）。
half3 GGXLobe(float NdotH, float NdotL, float NdotV, float VdotH,
              float smoothness, half3 tint, float3 f0)
{
    float alpha = SmoothnessToAlpha(smoothness);
    float D = EasyPBR_D_GGX(NdotH, alpha);
    float V = EasyPBR_V_SmithGGX(NdotL, NdotV, alpha);
    half3 F = EasyPBR_F_Schlick(VdotH, f0);
    return D * V * F * tint;
}

// Blinn-Phong 1ローブ（最軽量・見た目互換）。NdotH のみで完結。
half3 BlinnPhongLobe(float NdotH, float smoothness, half3 tint, float intensity)
{
    float specPower = exp2(10.0 * smoothness + 1.0);
    return tint * pow(NdotH, specPower) * intensity;
}

#endif // EASYPBR_BRDF_GGX_INCLUDED
