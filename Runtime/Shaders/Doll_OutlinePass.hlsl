// =============================================================================
//  Doll_OutlinePass.hlsl
// =============================================================================
#ifndef DOLL_OUTLINE_PASS_INCLUDED
#define DOLL_OUTLINE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "EasyPBR_Input.hlsl"
#include "EasyPBR_Effects.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 uv           : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    float3 positionOS   : TEXCOORD2;
    float3 normalWS     : TEXCOORD3;
};

Varyings vert_outline(Attributes input)
{
    Varyings output = (Varyings)0;
    
    #if defined(_OUTLINE_ON)
        float3 originalPositionWS = TransformObjectToWorld(input.positionOS.xyz);
        float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        
        float depth = abs(TransformWorldToView(originalPositionWS).z);
        float fade = smoothstep(30.0, 15.0, depth);
        float expand = _OutlineWidth * 0.002 * depth * fade;

        float3 expandedPositionWS = originalPositionWS + normalWS * expand;
        output.positionCS = TransformWorldToHClip(expandedPositionWS);
        
        output.uv = input.uv;
        
        output.positionWS = originalPositionWS; 
        output.positionOS = input.positionOS.xyz;
        output.normalWS = normalWS;
    #else
        output.positionCS = float4(0, 0, 0, 0);
    #endif
    
    return output;
}

half4 frag_outline(Varyings input) : SV_Target
{
    #if defined(_OUTLINE_ON)
    
        // 1. アルファクリップの完全同期（_BaseColorを掛けるように修正）
        half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
        #if defined(_ALPHATEST_ON)
            float outlineCutoff = clamp(_Cutoff + _OutlineCutoffShift, 0.0, 0.99);
            clip(albedo.a - outlineCutoff);
        #endif

        // 2. ディゾルブの完全同期（本体が消えたら輪郭も消える）
        #if defined(_DISSOLVE_ON)
            half3 dummyEmission;
            half3 cleanNormalWS = normalize(input.normalWS);
            ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, cleanNormalWS, albedo.rgb, dummyEmission);
        #endif

        return _OutlineColor;
        
    #else
        return half4(0,0,0,0);
    #endif
}

#endif // DOLL_OUTLINE_PASS_INCLUDED
