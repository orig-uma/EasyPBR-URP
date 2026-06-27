// =============================================================================
//  DollShadows.hlsl  (policy / thin wrapper)
// -----------------------------------------------------------------------------
//  旧公開名 SampleMainShadowHQ(...) を維持しつつ、URP結合の汎用サンプラへ委譲。
//  ここで保持するキャラ/プロジェクト固有ポリシー:
//    - _SHADOWQUALITY_PCSS キーワード → contactHardening
//    - _ReceiverNormalBias マテリアルプロパティ → 引数
//
//  前提: URP Core.hlsl を本ファイルより前に include しておくこと。
// =============================================================================
#ifndef DOLL_SHADOWS_INCLUDED
#define DOLL_SHADOWS_INCLUDED

#include "../Common/URP/Shadow_HQ_URP.hlsl"

// 旧タップ数定義との互換（指定があれば汎用側へ反映）。
#if defined(DOLL_SHADOW_TAPS) && !defined(EASYPBR_SHADOW_TAPS)
    #define EASYPBR_SHADOW_TAPS DOLL_SHADOW_TAPS
#endif

half SampleMainShadowHQ(float3 positionWS, float3 normalWS, float NdotL,
                        float2 screenPix, float softness)
{
    bool contactHardening = false;
    bool useTent          = false;
#if defined(_SHADOWMODE_TENTPCF)
    useTent = true;
#elif defined(_SHADOWMODE_PCSS)
    contactHardening = true;   // PCSS = Vogel + 接地硬化
#endif
    // VogelPcf は両方 false（素の Vogel PCF）。Off はこの関数を呼ばない。
    return EasyPBR_SampleMainShadowHQ(positionWS, normalWS, NdotL, screenPix, softness,
                                      _ReceiverNormalBias, contactHardening, useTent);
}

#endif // DOLL_SHADOWS_INCLUDED
