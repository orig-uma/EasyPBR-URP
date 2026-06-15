// =============================================================================
//  Doll_OutlinePass.hlsl
//  背面法（Inverted Hull）によるアウトライン描画パス
// =============================================================================
#ifndef DOLL_OUTLINE_PASS_INCLUDED
#define DOLL_OUTLINE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "EasyPBR_Input.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
};

Varyings vert_outline(Attributes input)
{
    Varyings output = (Varyings)0;
    #if defined(_OUTLINE_ON)
        // オブジェクトのワールド座標と法線を取得
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        
        // カメラからの距離を計算
        float dist = distance(_WorldSpaceCameraPos, positionWS);
        float clampedDist = clamp(dist, 0.3, 10.0);

        // 距離の伸び方を完全にリニア（直線）にするのではなく、
        // 遠くに行くほど太くなる「ペース」を少しだけ落とす（緩やかにする）テクニックです。
        // これにより、遠景のチラつきを抑えつつ、太くなりすぎるのを防ぎます。
        float distanceScale = pow(clampedDist, 0.8); 

        float expand = _OutlineWidth * 0.002 * distanceScale;
        
    positionWS += normalWS * expand;
        // クリップ空間（画面上の座標）に変換
        output.positionCS = TransformWorldToHClip(positionWS);
    #else
        // OFFの時：頂点を原点に潰す（縮退ポリゴン）
        // これにより、ピクセル描画（フラグメントシェーダー）が完全にスキップされ、GPU負荷がゼロになります。
        output.positionCS = float4(0, 0, 0, 0);
    #endif
    
    return output;
}

half4 frag_outline(Varyings input) : SV_Target
{
    #if defined(_OUTLINE_ON)
        return _OutlineColor;
    #else
        return half4(0,0,0,0);
    #endif
}

#endif // DOLL_OUTLINE_PASS_INCLUDED
