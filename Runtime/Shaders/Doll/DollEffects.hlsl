// =============================================================================
//  DollEffects.hlsl  (policy / thin wrapper)
// -----------------------------------------------------------------------------
//  ライティング非依存の汎用エフェクト (Dissolve, MatCap, Emission, 色補正)。
//  汎用ロジックは Common/Effects・Common_Color に委譲し、ここでは
//  ディゾルブのテクスチャサンプリングとキーワード/プロパティ解決のみ保持する。
//
//  前提: マテリアルプロパティ宣言 (_DissolveTex, sampler_MainTex, _Dissolve* 等)
//        が本ファイルより前に見えていること。
// =============================================================================
#ifndef EASYPBR_EFFECTS_INCLUDED
#define EASYPBR_EFFECTS_INCLUDED

#include "../Common/Common_Color.hlsl"
#include "../Common/Effects/Fx_MatCap.hlsl"
#include "../Common/Effects/Fx_Emission.hlsl"
#include "../Common/Effects/Fx_Dissolve.hlsl"

// -----------------------------------------------------------------------------
// [Dissolve] サンプリング + キーワード解決（プロジェクト固有）→ 汎用ロジックへ委譲。
//  Forward と Shadow の両方から呼ばれる共通エントリ。公開名は従来どおり。
// -----------------------------------------------------------------------------
void ApplyDissolveClip(float2 uv, float3 positionWS, float3 positionOS, float3 normalWS,
                       inout half3 albedo, out half3 dissolveEmission)
{
    dissolveEmission = half3(0, 0, 0);

#if defined(_DISSOLVE_ON)
    float dissolveNoise = 0.5;
    float dissolveGrad  = 0.5;

    #if defined(_DISSOLVETYPE_WORLDY)
        // 三平面投影（どの角度から見ても歪まない空間ノイズ）。
        float3 blendWeights = abs(normalWS);
        blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);
        float minW = min(blendWeights.x, min(blendWeights.y, blendWeights.z));
        blendWeights = max(blendWeights - minW, 0.0);
        blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);

        float noiseX = 0.0, noiseY = 0.0, noiseZ = 0.0;
        // MainTexを使わないときにsampler_MainTexがストリッピングされるのでここではグローバルのsampler_LinearRepeatを借りる
        if (blendWeights.x > 0.0) noiseX = SAMPLE_TEXTURE2D(_DissolveTex, sampler_LinearRepeat, positionWS.zy * _DissolveNoiseScale).r;
        if (blendWeights.y > 0.0) noiseY = SAMPLE_TEXTURE2D(_DissolveTex, sampler_LinearRepeat, positionWS.xz * _DissolveNoiseScale).r;
        if (blendWeights.z > 0.0) noiseZ = SAMPLE_TEXTURE2D(_DissolveTex, sampler_LinearRepeat, positionWS.xy * _DissolveNoiseScale).r;

        dissolveNoise = noiseX * blendWeights.x + noiseY * blendWeights.y + noiseZ * blendWeights.z;
        dissolveGrad  = saturate((positionWS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
    #else
        dissolveNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_LinearRepeat, uv * _DissolveNoiseScale).r;
        #if defined(_DISSOLVETYPE_LOCALY)
            dissolveGrad = saturate((positionOS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
        #endif
    #endif

    DissolveInput di;
    di.noise         = dissolveNoise;
    di.grad          = dissolveGrad;
    di.amount        = _DissolveAmount;
    di.edgeWidth     = _DissolveEdgeWidth;
    di.noiseStrength = _DissolveNoiseStrength;
    di.edgeColor     = _DissolveEdgeColor.rgb;
    di.edgeColor2    = _DissolveEdgeColor2.rgb;
    di.invert        = _DissolveInvert;
    di.edgeStep      = (_DissolveEdgeStep > 0.5);
    #if defined(_DISSOLVETYPE_NONE)
        di.isNoneType = true;
    #else
        di.isNoneType = false;
    #endif

    ResolveDissolve(di, albedo, dissolveEmission);
#endif
}

// AO・ラフネス・NoV からスペキュラ遮蔽（Lagarde/Frostbite 近似）。
// 反射が荒いほど遮蔽が効き、鋭いほど抜ける。
float SpecularOcclusion(float NoV, float ao, float perceptualRoughness)
{
    return saturate(pow(abs(NoV + ao), exp2(-16.0 * perceptualRoughness - 1.0)) - 1.0 + ao);
}

#endif // EASYPBR_EFFECTS_INCLUDED
