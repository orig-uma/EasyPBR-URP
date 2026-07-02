// =============================================================================
//  ForwardPass.hlsl
//  メインの描画パス（UniversalForward）
// =============================================================================
#ifndef DOLL_FORWARD_PASS_INCLUDED
#define DOLL_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../DollEffects.hlsl"
#include "../DollLighting.hlsl"
#include "../DollShadows.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 uv           : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float3 positionWS   : TEXCOORD0;
    float3 normalWS     : TEXCOORD1;
    float2 uv           : TEXCOORD2;
    float4 shadowCoord  : TEXCOORD3;
    float3 forwardWS    : TEXCOORD4;
    float3 positionOS   : TEXCOORD5;
    float3 tangentWS    : TEXCOORD6;
    float3 bitangentWS  : TEXCOORD7;
};

#define DOLL_SURFACE_IMPL
#include "../DollSurface.hlsl"

Varyings vert(Attributes input)
{
    Varyings output;
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS   = normalInput.normalWS;
    output.tangentWS  = normalInput.tangentWS;
    output.bitangentWS = normalInput.bitangentWS;
    output.uv = input.uv;
    output.shadowCoord = GetShadowCoord(vertexInput);
    output.forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
    output.positionOS = input.positionOS.xyz;
    return output;
}

half4 frag(Varyings input) : SV_Target
{
    half3 finalColor = half3(0, 0, 0);

    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 objectForwardWS = input.forwardWS;

    half alpha;
    DollSurfaceData s = GatherSurface(input, viewDirectionWS, objectForwardWS, alpha);

    #if defined(_SHADOWMODE_TENTPCF) || defined(_SHADOWMODE_VOGELPCF) || defined(_SHADOWMODE_PCSS)
        Light mainLight = GetMainLight();
        float mainNdotL = dot(s.cleanNormalWS, mainLight.direction);
        mainLight.shadowAttenuation = SampleMainShadowHQ(
            input.positionWS, s.cleanNormalWS, mainNdotL,
            input.positionCS.xy, _ShadowMapSoftness);
    #else
        float4 shadowCoord = input.shadowCoord;
        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
            shadowCoord = ComputeScreenPos(input.positionCS);
        #endif
        Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
    #endif

    // キャラ用ライト整形: メインライトの色を可読性側へ寄せる防御層
    // （色影響度 / 彩度上限 / 輝度下限）。既定値では素通し。
    UNITY_BRANCH
    if (_LightColorInfluence < 1.0 || _LightSaturationLimit < 1.0 || _LightMinBrightness > 0.0)
    {
        mainLight.color = ConditionLightColor(mainLight.color,
            _LightColorInfluence, _LightSaturationLimit, _LightMinBrightness);
    }

    half sdfMask;
    float sdfLit = ComputeFaceSDF(input, mainLight, objectForwardWS, sdfMask);

    finalColor += CalculateSingleLight(
        mainLight, s, viewDirectionWS, objectForwardWS,
        s.indirectLight, sdfLit, sdfMask, _FillIntensity);

    UNITY_BRANCH
    if (_ClearcoatStrength > 0.0)
    {
        finalColor += CalculateClearcoat(
            s.coatNormalWS, mainLight.direction, viewDirectionWS,
            _ClearcoatSmoothness, _ClearcoatStrength, s.clearcoatMask,
            mainLight.color * mainLight.distanceAttenuation, mainLight.shadowAttenuation,
            _IridescenceIntensity, _IridescenceThickness, _IridescenceShift);
    }

    #if defined(USE_CLUSTER_LIGHT_LOOP)
        #define DOLL_CLUSTER_LIGHT_LOOP USE_CLUSTER_LIGHT_LOOP
    #elif defined(USE_FORWARD_PLUS)
        #define DOLL_CLUSTER_LIGHT_LOOP USE_FORWARD_PLUS
    #else
        #define DOLL_CLUSTER_LIGHT_LOOP 0
    #endif

    #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
        InputData inputData = (InputData)0;
        inputData.positionWS = input.positionWS;
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);

        uint pixelLightCount = GetAdditionalLightsCount();

        // 追加ライトのライト整形は色影響度・彩度上限のみ（輝度下限は適用しない:
        // 下限は「シーンに 1 本のメインライト」の責務で、灯数ぶん持ち上がるのを防ぐ）。
        bool conditionAdditional = (_ConditionAdditionalLights > 0.5)
            && (_LightColorInfluence < 1.0 || _LightSaturationLimit < 1.0);

        #define DOLL_ACCUMULATE_ADDITIONAL_LIGHT(index)                                   \
            {                                                                             \
                Light addLight = GetAdditionalLight(index, input.positionWS, half4(1,1,1,1)); \
                UNITY_BRANCH                                                              \
                if (conditionAdditional)                                                  \
                {                                                                         \
                    addLight.color = ConditionLightColor(addLight.color,                  \
                        _LightColorInfluence, _LightSaturationLimit, 0.0);                \
                }                                                                         \
                half3 addContrib = CalculateSingleLight(                                  \
                    addLight, s, viewDirectionWS, objectForwardWS,                      \
                    half3(0,0,0), -1.0, 1.0, 0.0);                                      \
                finalColor = (_AdditionalLightBlendMode > 0.5)                            \
                    ? max(finalColor, addContrib)                                         \
                    : finalColor + addContrib;                                            \
            }

        #if DOLL_CLUSTER_LIGHT_LOOP
        [loop] for (uint dirLightIndex = 0u;
                    dirLightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                    dirLightIndex++)
        {
            DOLL_ACCUMULATE_ADDITIONAL_LIGHT(dirLightIndex)
        }
        #endif

        LIGHT_LOOP_BEGIN(pixelLightCount)
            DOLL_ACCUMULATE_ADDITIONAL_LIGHT(lightIndex)
        LIGHT_LOOP_END

        #undef DOLL_ACCUMULATE_ADDITIONAL_LIGHT
    #endif
    #undef DOLL_CLUSTER_LIGHT_LOOP

    ApplyEnvironmentAndCoat(finalColor, s, viewDirectionWS);
    ApplyPostEffects(finalColor, input, s, mainLight, viewDirectionWS);

    return half4(finalColor, alpha);
}

#endif // DOLL_FORWARD_PASS_INCLUDED
