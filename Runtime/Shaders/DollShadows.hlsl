// =============================================================================
//  DollShadows.hlsl
// -----------------------------------------------------------------------------
//  メインディレクショナルライト専用 高品質セルフシャドウサンプラ。
//
//   - ピクセル単位シャドウ座標        : 補間誤差を排除（頂点補間より高精度）
//   - 受け側ノーマルオフセット        : シャドウアクネ（縞ノイズ）を除去
//   - スクリーン空間IGN回転 Vogel PCF : 連続ペナンブラ・面の上で泳がない
//   - 任意 PCSS                       : 接地点は鋭く・遠方はボケるコンタクトハードニング
//
//  追加ライトには適用しない（コスト管理）。自己影のリッチさはメインライトで十分。
//  キーワード: _SHADOWQUALITY_PCF / _SHADOWQUALITY_PCSS（どちらも無ければ即 return）
// =============================================================================
#ifndef DOLL_SHADOWS_INCLUDED
#define DOLL_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// -----------------------------------------------------------------------------
//  タップ数: 品質と負荷の主トレードオフ。8 が実用上の最適点。
//  ハードウェア比較サンプリング（2x2 PCF）が各タップに掛かるため、
//  8タップでも実効 32 サンプル相当の被覆になる。16 にすると更に滑らか（負荷+約2倍）。
// -----------------------------------------------------------------------------
#ifndef DOLL_SHADOW_TAPS
    #define DOLL_SHADOW_TAPS 8
#endif

// PCSS のブロッカー深度を読むための point sampler。
// 多くの URP バージョンで sampler_PointClamp は宣言済み。
// 未宣言で重複エラーにならない環境では、下行のコメントを外して使う。
#if defined(_SHADOWQUALITY_PCSS)
    // SAMPLER(sampler_PointClamp);
#endif

// スクリーン空間ノイズ（テクスチャ不要・ALUのみ・面の上で泳がない）
float Doll_IGN(float2 pix)
{
    const float3 m = float3(0.06711056, 0.00583715, 52.9829189);
    return frac(m.z * frac(dot(pix, m.xy)));
}

// Vogel ディスク: 低タップ数でも均一被覆（Poisson より縞が出にくい）
float2 Doll_VogelDisk(int i, int count, float phi)
{
    float r     = sqrt((i + 0.5) / (float)count);
    float theta = i * 2.39996323 + phi;        // golden angle
    float s, c; sincos(theta, s, c);
    return float2(c, s) * r;
}

#if defined(_SHADOWQUALITY_PCSS)
// ブロッカー探索: 平均遮蔽深度を求めてペナンブラ幅を動的算出する
bool Doll_FindBlocker(float2 baseUV, float receiverZ, float2 texel, float phi,
                      float searchRadius, out float avgBlockerZ)
{
    float sumZ = 0.0;
    int   count = 0;
    UNITY_UNROLL
    for (int i = 0; i < DOLL_SHADOW_TAPS; i++)
    {
        float2 o = Doll_VogelDisk(i, DOLL_SHADOW_TAPS, phi) * texel * searchRadius;
        float  z = SAMPLE_TEXTURE2D_LOD(_MainLightShadowmapTexture, sampler_PointClamp,
                                        baseUV + o, 0).r;
    #if UNITY_REVERSED_Z
        if (z > receiverZ) { sumZ += z; count++; }
    #else
        if (z < receiverZ) { sumZ += z; count++; }
    #endif
    }
    avgBlockerZ = (count > 0) ? sumZ / count : receiverZ;
    return count > 0;
}
#endif

// -----------------------------------------------------------------------------
//  SampleMainShadowHQ
//   返り値 0(影) .. 1(光)。GetCastShadow に shadowAttenuation として渡す。
//   normalWS / NdotL は grain を乗せていない cleanNormalWS 由来を渡すこと。
//   softness は既存プロパティ _ShadowMapSoftness をそのまま使う（ペナンブラ幅）。
// -----------------------------------------------------------------------------
half SampleMainShadowHQ(float3 positionWS, float3 normalWS, float NdotL,
                        float2 screenPix, float softness)
{
#if !defined(_MAIN_LIGHT_SHADOWS) && !defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    return 1.0h; // 影キーワードが無ければコンパイル時に分岐ごと除去
#else
    // --- 受け側ノーマルオフセット: 傾斜面ほど強く押し出してアクネを除去 ---
    //  キャスター側バイアスを上げずに済むため、ピーターパン（影の浮き）を抑えられる。
    float  slope     = saturate(1.0 - NdotL);
    float3 offsetPos = positionWS + normalWS * (_ReceiverNormalBias * (0.5 + slope)) * 0.01;

    float4 coord = TransformWorldToShadowCoord(offsetPos); // カスケード選択込み・ピクセル単位
    float2 texel = _MainLightShadowmapSize.xy;
    float  phi   = Doll_IGN(screenPix) * TWO_PI;            // 毎ピクセル回転（スクリーン安定）

    float radius = 1.0 + softness * 6.0;                    // ペナンブラ幅（texel）

#if defined(_SHADOWQUALITY_PCSS)
    // --- コンタクトハードニング: 近接遮蔽は鋭く・遠方遮蔽はボケる ---
    float avgBlockerZ;
    if (!Doll_FindBlocker(coord.xy, coord.z, texel, phi, radius * 1.5, avgBlockerZ))
        return 1.0h; // 遮蔽物なし = 完全に光
    float penumbra = abs(coord.z - avgBlockerZ) / max(avgBlockerZ, 1e-4);
    radius = clamp(penumbra * radius * 8.0, 1.0, radius * 2.0);
#endif

    // --- 回転 Vogel ディスク PCF（ハードウェア比較サンプリング） ---
    half atten = 0.0h;
    UNITY_UNROLL
    for (int i = 0; i < DOLL_SHADOW_TAPS; i++)
    {
        float2 o = Doll_VogelDisk(i, DOLL_SHADOW_TAPS, phi) * texel * radius;
        atten += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,
                                         sampler_LinearClampCompare,
                                         float3(coord.xy + o, coord.z));
    }
    atten /= DOLL_SHADOW_TAPS;

    // URP と同じ距離フェード（シャドウ距離端での影のポップを防ぐ）
    half fade = GetMainLightShadowFade(positionWS);
    return lerp(atten, 1.0h, fade);
#endif
}

#endif // DOLL_SHADOWS_INCLUDED
