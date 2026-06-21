// =============================================================================
//  Common_Color.hlsl
// -----------------------------------------------------------------------------
//  純粋な色変換ユーティリティ。RGB<->HSV / Hue->RGB / HSV補正。
//  前提: なし。
// =============================================================================
#ifndef EASYPBR_COMMON_COLOR_INCLUDED
#define EASYPBR_COMMON_COLOR_INCLUDED

half3 RgbToHsv(half3 c)
{
    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
    half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

half3 HsvToRgb(half3 c)
{
    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

// iridescence（虹色）用の Hue -> RGB。
half3 HueToRGB(float hue)
{
    return saturate(abs(frac(hue + half3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0);
}

// 色相回転 / 彩度 / 明度のまとめてHSV補正。
half3 ApplyColorCorrection(half3 color, half hueShift, half saturation, half valueMulti)
{
    half3 hsv = RgbToHsv(color);
    hsv.x = frac(hsv.x + hueShift);
    hsv.y = saturate(hsv.y * saturation);
    hsv.z = hsv.z * valueMulti;
    return HsvToRgb(hsv);
}

#endif // EASYPBR_COMMON_COLOR_INCLUDED
