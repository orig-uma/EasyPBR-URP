// =============================================================================
//  OutlinePass.hlsl
// =============================================================================
#ifndef DOLL_OUTLINE_PASS_INCLUDED
#define DOLL_OUTLINE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "../DollInput.hlsl"
#include "../DollEffects.hlsl"

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
    
        half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
        #if defined(_ALPHATEST_ON)
            float outlineCutoff = clamp(_Cutoff + _OutlineCutoffShift, 0.0, 0.99);
            clip(albedo.a - outlineCutoff);
        #endif

        #if defined(_DISSOLVE_ON)
            half3 dummyEmission;
            half3 cleanNormalWS = normalize(input.normalWS);
            ApplyDissolveClip(input.uv, input.positionWS, input.positionOS, cleanNormalWS, albedo.rgb, dummyEmission);
        #endif

        // アルベド連動: その場のアルベド × Outline Color を線の色にブレンド。
        // 髪には髪の、肌には肌の系統色の線が付き、固定単色より馴染む。
        half3 lineColor = lerp(_OutlineColor.rgb, albedo.rgb * _OutlineColor.rgb, _OutlineAlbedoBlend);

        // 暗転は輪郭にも掛ける（T-361）。本体だけに掛けていたので、
        // **暗転しきったキャラの輪郭線だけが明るく残って宙に浮いていた。**
        lineColor = lerp(lineColor, half3(0, 0, 0), _BlackOut);

        return half4(lineColor, _OutlineColor.a);
        
    #else
        return half4(0,0,0,0);
    #endif
}

#endif // DOLL_OUTLINE_PASS_INCLUDED
