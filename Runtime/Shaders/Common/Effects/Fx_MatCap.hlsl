// =============================================================================
//  Fx_MatCap.hlsl
// -----------------------------------------------------------------------------
//  MatCap（ビュー空間法線によるスフィアマップ）。
//  前提: URP Core.hlsl（GetWorldToViewMatrix）。
// =============================================================================
#ifndef EASYPBR_FX_MATCAP_INCLUDED
#define EASYPBR_FX_MATCAP_INCLUDED

float2 GetMatCapUV(half3 normalWS)
{
    float3 normalVS = mul((float3x3)GetWorldToViewMatrix(), normalWS);
    return normalVS.xy * 0.5 + 0.5;
}

// blendMode: 0 = Add, 1 = Multiply。
half3 ApplyMatCap(half3 finalColor, half3 matcapColor, float matcapIntensity, float blendMode)
{
    half3 addResult = finalColor + matcapColor * matcapIntensity;
    half3 mulResult = finalColor * lerp(half3(1.0, 1.0, 1.0), matcapColor, saturate(matcapIntensity));
    return (blendMode > 0.5) ? mulResult : addResult;
}

#endif // EASYPBR_FX_MATCAP_INCLUDED
