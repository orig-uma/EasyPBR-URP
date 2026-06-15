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
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        float depth = abs(TransformWorldToView(positionWS).z);
        float fade = smoothstep(30.0, 15.0, depth);
        float expand = _OutlineWidth * 0.002 * depth * fade;
            
        positionWS += normalWS * expand;
        output.positionCS = TransformWorldToHClip(positionWS);
    #else
        // 縮退ポリゴンで面積0としてfragをスキップ
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
