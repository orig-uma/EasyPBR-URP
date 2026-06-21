// =============================================================================
//  DollShadows.hlsl  (policy / thin wrapper)
// -----------------------------------------------------------------------------
//  旧公開名 SampleMainShadowHQ(...) を維持しつつ、URP結合の汎用サンプラへ委譲。
//  ここで保持するキャラ/プロジェクト固有ポリシー:
//    - _SHADOWQUALITY_PCSS キーワード → contactHardening
//    - _ReceiverNormalBias マテリアルプロパティ → 引数
//
//  前提: URP Core.hlsl を本ファイルより前に include しておくこと。
//  ※ Common フォルダの配置に合わせて include パスを調整すること。
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
#if defined(_SHADOWQUALITY_PCSS)
    contactHardening = true;
#endif
    return EasyPBR_SampleMainShadowHQ(positionWS, normalWS, NdotL, screenPix, softness,
                                      _ReceiverNormalBias, contactHardening);
}

#endif // DOLL_SHADOWS_INCLUDED
