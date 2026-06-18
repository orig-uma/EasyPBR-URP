// =============================================================================
//  Doll_ShadowPass.hlsl
//  影を落とすためのパス（ShadowCaster）
// =============================================================================
#ifndef DOLL_SHADOW_PASS_INCLUDED
#define DOLL_SHADOW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "EasyPBR_Effects.hlsl"

// URPの組み込み変数を明示
float4 _LightPosition;

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

Varyings vert_shadow(Attributes input)
{
    Varyings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // URP標準関数からメインライトの方向を取得
    float3 lightDirectionWS = GetMainLight().direction;

    // ポイントライト等の場合
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        lightDirectionWS = normalize(_LightPosition.xyz - positionWS);
    #endif

    float3 biasedPositionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
    float4 positionCS = TransformWorldToHClip(biasedPositionWS);

    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    output.positionCS = positionCS;
    output.uv = input.uv;
    output.positionWS = positionWS;
    output.positionOS = input.positionOS.xyz;
    output.normalWS = normalWS;
    return output;
}

half4 frag_shadow(Varyings input) : SV_Target 
{ 
    // ShadowCaster は ColorMask 0 のため RGB は不要。
    //  アルファクリップ用に .a チャンネルのみ取得する（rgb 計算を省略）。
    #if defined(_ALPHATEST_ON)
        half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _BaseColor.a;
        clip(alpha - _Cutoff);
    #endif

    half3 dummyAlbedo = half3(0, 0, 0);
    half3 dummyEmission;
    ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, input.normalWS, dummyAlbedo, dummyEmission);

    return 0;
}

#endif // DOLL_SHADOW_PASS_INCLUDED
