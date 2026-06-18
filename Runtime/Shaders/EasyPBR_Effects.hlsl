// =============================================================================
//  EasyPBR_Effects.hlsl
//  ライティングに依存しない汎用エフェクト処理 (Dissolve, MatCap, Emission)
// =============================================================================
#ifndef EASYPBR_EFFECTS_INCLUDED
#define EASYPBR_EFFECTS_INCLUDED

// -----------------------------------------------------------------------------
// [Color Correction] HSV色調補正
// -----------------------------------------------------------------------------
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

half3 ApplyColorCorrection(half3 color, half hueShift, half saturation, half valueMulti)
{
    half3 hsv = RgbToHsv(color);
    hsv.x = frac(hsv.x + hueShift); // 色相を回す
    hsv.y = saturate(hsv.y * saturation); // 彩度をスケール
    hsv.z = hsv.z * valueMulti; // 明度をスケール
    return HsvToRgb(hsv);
}

// -----------------------------------------------------------------------------
// [Dissolve] 消失エフェクトの判定とClip処理
// ForwardとShadowの両方から呼ばれる共通ロジック
// -----------------------------------------------------------------------------
void ApplyDissolveClip(float2 uv, float3 positionWS, float3 positionOS, float3 normalWS, inout half3 albedo, out half3 dissolveEmission)
{
    dissolveEmission = half3(0, 0, 0);

    #if defined(_DISSOLVE_ON)
        float dissolveNoise = 0.5;
        float dissolveGrad = 0.5;

        #if defined(_DISSOLVETYPE_WORLDY)
            // 面の向き（絶対値）を取得して、どの方向からの投影を強くするか決める
            float3 blendWeights = abs(normalWS);
            blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);
            float minW = min(blendWeights.x, min(blendWeights.y, blendWeights.z));
            blendWeights = max(blendWeights - minW, 0.0);
            blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);

            // ウェイトが実質 0 の軸はサンプリングをスキップ（1 軸は常に省略）
            float noiseX = 0.0, noiseY = 0.0, noiseZ = 0.0;
            if (blendWeights.x > 0.0) noiseX = SAMPLE_TEXTURE2D(_DissolveTex, sampler_MainTex, positionWS.zy * _DissolveNoiseScale).r;
            if (blendWeights.y > 0.0) noiseY = SAMPLE_TEXTURE2D(_DissolveTex, sampler_MainTex, positionWS.xz * _DissolveNoiseScale).r;
            if (blendWeights.z > 0.0) noiseZ = SAMPLE_TEXTURE2D(_DissolveTex, sampler_MainTex, positionWS.xy * _DissolveNoiseScale).r;

            // 重みに合わせてブレンド（これでどの角度から見ても歪まない空間ノイズになる）
            dissolveNoise = noiseX * blendWeights.x + noiseY * blendWeights.y + noiseZ * blendWeights.z;
            
            dissolveGrad = saturate((positionWS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));

        #else
            // LocalY や None のモード：従来通りモデル固有のUVを使う
            dissolveNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_MainTex, uv * _DissolveNoiseScale).r;

            #if defined(_DISSOLVETYPE_LOCALY)
                dissolveGrad = saturate((positionOS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
            #endif
        #endif

        float dissolveVal = dissolveGrad;
        #if defined(_DISSOLVETYPE_NONE)
            dissolveVal = dissolveNoise;
        #else
            dissolveVal = dissolveGrad + (dissolveNoise - 0.5) * _DissolveNoiseStrength;
        #endif

        float dMin = -0.5 * _DissolveNoiseStrength;
        float dMax = 1.0 + 0.5 * _DissolveNoiseStrength;
        #if defined(_DISSOLVETYPE_NONE)
            dMin = 0.0;
            dMax = 1.0;
        #endif
        
        float adjustedAmount = lerp(dMin - _DissolveEdgeWidth - 0.01, dMax + _DissolveEdgeWidth + 0.01, _DissolveAmount);
        float clipVal = dissolveVal - adjustedAmount;
        
        // _DissolveInvert=0 で +1、1 で -1 を掛けて符号を反転（分岐レス）。
        clipVal *= lerp(1.0, -1.0, saturate(_DissolveInvert));
        
        // 閾値未満ならピクセルを破棄
        clip(clipVal);
        
        // 境界のマスク（1.0が消失の最前線、0.0がマテリアル内部）
        float dissolveEdgeMask = smoothstep(0.0, _DissolveEdgeWidth + 0.0001, clipVal);
        float edgeFactor = 1.0 - dissolveEdgeMask;
        
        // 段階的な階調化（Toon調エッジの設定がONの場合）
        if (_DissolveEdgeStep > 0.5)
        {
            edgeFactor = ceil(edgeFactor * 2.0) / 2.0 * step(0.01, edgeFactor);
        }

        albedo = lerp(albedo, _DissolveEdgeColor2.rgb, edgeFactor);

        float emissionMask = smoothstep(0.5, 1.0, edgeFactor);
        if (_DissolveEdgeStep > 0.5)
        {
             emissionMask = step(0.9, edgeFactor); 
        }
        
        dissolveEmission = _DissolveEdgeColor.rgb * emissionMask;
    #endif
}

// -----------------------------------------------------------------------------
// [MatCap] UV取得と適用
// -----------------------------------------------------------------------------
float2 GetMatCapUV(half3 normalWS)
{
    float3 normalVS = mul((float3x3)GetWorldToViewMatrix(), normalWS);
    return normalVS.xy * 0.5 + 0.5;
}

// blendMode: 0 = Add, 1 = Multiply（_MatCapBlend の値をそのまま渡す）
half3 ApplyMatCap(half3 finalColor, half3 matcapColor, float matcapIntensity, float blendMode)
{
    half3 addResult = finalColor + matcapColor * matcapIntensity;
    half3 mulResult = finalColor * lerp(half3(1.0, 1.0, 1.0), matcapColor, saturate(matcapIntensity));
    return (blendMode > 0.5) ? mulResult : addResult;
}

// -----------------------------------------------------------------------------
// [Emission] 自己発光
// -----------------------------------------------------------------------------
half3 CalculateEmission(half3 emissionMapColor, half3 emissionColor, float emissionIntensity)
{
    return emissionMapColor * emissionColor * emissionIntensity;
}

#endif // EASYPBR_EFFECTS_INCLUDED
