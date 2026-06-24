// =============================================================================
//  DepthOnlyPass.hlsl
//  深度バッファのみへ書き込むパス（DepthOnly）。
//  Forward の Depth Prepass / Depth Priming、Forward+ の深度生成に使用する。
// =============================================================================
#ifndef DOLL_DEPTH_ONLY_PASS_INCLUDED
#define DOLL_DEPTH_ONLY_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "../DollEffects.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 positionOS : TEXCOORD2;
    float3 normalWS   : TEXCOORD3;
};

Varyings vert_depth(Attributes input)
{
    Varyings output = (Varyings)0;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv         = input.uv;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionOS = input.positionOS.xyz;
    output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 frag_depth(Varyings input) : SV_Target
{
    // 深度のみ書き込むため RGB は不要。アルファクリップ用に .a のみ取得する。
    #if defined(_ALPHATEST_ON)
        half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _BaseColor.a;
        clip(alpha - _Cutoff);
    #endif

    half3 dummyAlbedo = half3(0, 0, 0);
    half3 dummyEmission;
    ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, input.normalWS, dummyAlbedo, dummyEmission);

    return 0;
}

#endif // DOLL_DEPTH_ONLY_PASS_INCLUDED
