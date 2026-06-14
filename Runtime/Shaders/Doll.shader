// =============================================================================
//  Origuma/EasyPBR_URP/Doll
// -----------------------------------------------------------------------------
//  URP (Universal Render Pipeline) Forward 用のキャラクター向けシェーダー。
// =============================================================================
Shader "Origuma/EasyPBR_URP/Doll"
{
    Properties
    {
        // --- 基本パラメータ ---------------------
        [Header(Base Core)]
        _MainTex ("Base Map (RGB / Alpha)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        // --- 半透明描画 ---------------------------------------------------------
        [Header(Surface Options (Transparency))]
        [Toggle(_SURFACE_TRANSPARENT)] _SurfaceTransparent ("Transparent Surface (Use Alpha Blend)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 // 4 = LEqual

        // --- ステンシル設定 ------------------------------------------
        [Header(Stencil)]
        _StencilRef ("Stencil Reference", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare", Float) = 8 // 8 = Always
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 0 // 0 = Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Float) = 0 // 0 = Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Float) = 0 // 0 = Keep

        // --- 自己発光 -------------------------------------------------------------
        [Header(Emission)]
        [Toggle(_EMISSION_ON)] _UseEmission ("Enable Emission", Float) = 0
        [NoScaleOffset] _EmissionMap ("Emission Map (RGB)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0.0, 10.0)) = 1.0

        // --- 顔の自己陰を自動で消す仕組み -------------
        [Header(Auto Face Shadow Fix (No Mask Needed))]
        _FrontMaskStrength ("Front Brightness", Range(0.0, 1.0)) = 0.8
        _UpMaskStrength ("Up Brightness", Range(0.0, 1.0)) = 0.3
        _MaskFalloff ("Shadow Erase Breadth", Range(0.1, 10.0)) = 2.0
        _BacklightPreserve ("Backlight Shadow Preserve", Range(0.0, 1.0)) = 1.0 
        [Space(10)]
        _FaceNormalSmoothness ("Face Normal Smoothing", Range(0.0, 1.0)) = 0.8 

        // --- ライティングと影 ------------------------------------------------
        [Header(Light and Shadow)]
        [KeywordEnum(Smooth, Toon)] _ShadingStyle ("Shading Style", Float) = 0
        _ShadowColor ("Shadow Color Tint", Color) = (0.7, 0.7, 0.75, 1)
        _ReceiveShadowMask ("Receive Shadow Mask (R=Shadow)", 2D) = "white" {}
        _ReceiveShadowStrength ("Receive Shadow Strength", Range(0.0, 1.0)) = 1.0
        _ShadowMapSoftness ("Receive Shadow Softness", Range(0.0, 1.0)) = 0.4
        _ShadowDither ("Shadow Edge Dither", Range(0.0, 1.0)) = 0.5
        _HalfLambertWrap ("Light Wrap", Range(0.0, 1.0)) = 0.5
        _DiffuseLightLimit ("Diffuse Light Limit", Range(0.1, 5.0)) = 1.0
        [Space(10)]
        _ToonStep ("Toon Shadow Threshold", Range(0.0, 1.0)) = 0.5
        _ToonFeather ("Toon Shadow Softness", Range(0.0, 1.0)) = 0.2

        // --- 微細なザラつき ------------------------------------
        [Header(Surface Micro Detail)]
        [NoScaleOffset] _BlueNoiseTex ("Micro Grain Pattern (Blue Noise)", 2D) = "grey" {}
        _GrainIntensity ("Grain Intensity", Range(0.0, 1.0)) = 0.2
        _GrainScale ("Grain UV Scale", Float) = 10.0

        // --- スペキュラと映り込み --------------------------------------------
        [Header(Specular and Reflection)]
        _SpecularMask ("Specular Mask (R)", 2D) = "white" {}
        [Space(10)]
        _SpecularColor ("Primary Specular Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Primary Smoothness", Range(0.01, 1.0)) = 0.8
        _SpecularIntensity ("Primary Intensity", Range(0.0, 5.0)) = 1.5
        _PriSpecularLightLimit ("Primary Specular Limit", Range(0.1, 10.0)) = 2
        [Space(10)]
        _SecSpecularColor ("Secondary Specular Color", Color) = (1, 1, 1, 1)
        _SecSmoothness ("Secondary Smoothness", Range(0.01, 1.0)) = 0.2
        _SecSpecularIntensity ("Secondary Intensity", Range(0.0, 5.0)) = 0.15
        _SecSpecularLightLimit ("Secondary Specular Limit", Range(0.1, 5.0)) = 1.2
        [Space(10)]
        [Toggle(_MATCAP_ON)] _UseMatCap ("Enable MatCap", Float) = 0
        [KeywordEnum(Add, Multiply)] _MatCapBlend ("MatCap Blend Mode", Float) = 0
        [NoScaleOffset] _MatCapTex ("MatCap Texture (RGB)", 2D) = "black" {}
        _MatCapColor ("MatCap Tint", Color) = (1, 1, 1, 1)
        _MatCapIntensity ("MatCap Intensity", Range(0.0, 5.0)) = 1.0

        // --- Dissolve (消失エフェクト) --------------------------------
        [Header(Dissolve)]
        [Toggle(_DISSOLVE_ON)] _UseDissolve ("Enable Dissolve", Float) = 0
        _DissolveAmount ("Dissolve Amount", Range(0.0, 1.0)) = 0.0
        [Toggle(_DISSOLVE_INVERT)] _DissolveInvert ("Invert Dissolve", Float) = 0
        [KeywordEnum(None, WorldY, LocalY)] _DissolveType ("Dissolve Axis", Float) = 1
        _DissolveStartY ("Start Y", Float) = 0.0
        _DissolveEndY ("End Y", Float) = 2.0
        [NoScaleOffset] _DissolveTex ("Dissolve Noise", 2D) = "white" {}
        _DissolveNoiseScale ("Noise Scale", Float) = 1.0
        _DissolveNoiseStrength ("Noise Strength", Range(0.0, 1.0)) = 0.5
        [HDR] _DissolveEdgeColor ("Edge Burn Color", Color) = (1.0, 0.2, 0.0, 1.0)
        _DissolveEdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.05

        // --- 追加効果 ---
        [Header(Optional Effects)]
        [Space(4)]
        _SSSColor ("SSS Color", Color) = (1, 1, 1, 1)
        _SSSIntensity ("SSS Intensity", Range(0.0, 5.0)) = 0.0
        _SSSPower ("SSS Falloff", Range(0.1, 10.0)) = 4.0
        _SSSDistortion ("SSS Distortion", Range(0.0, 1.0)) = 0.1
        [Space(10)]
        _FuzzColor ("Peach Fuzz Color", Color) = (1.0, 0.95, 0.9, 1.0)
        _FuzzIntensity ("Peach Fuzz Intensity", Range(0.0, 5.0)) = 0.0
        _FuzzPower ("Peach Fuzz Width", Range(0.1, 10.0)) = 4.0
        [Space(10)]
        _RimColor ("Rim Light Color", Color) = (1, 1, 1, 1)
        _RimIntensity ("Rim Light Intensity", Range(0.0, 5.0)) = 1.0
        _RimPower ("Rim Light Thickness", Range(0.1, 10.0)) = 3.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "AlphaTest" 
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AlphaClip;
                half _Cutoff;
                half _Cull;

                half _SurfaceTransparent;
                half _SrcBlend;
                half _DstBlend;
                half _ZWrite;
                
                half _ZTest;
                half _StencilRef;
                half _StencilComp;
                half _StencilPass;
                half _StencilFail;
                half _StencilZFail;

                half _UseEmission;
                half4 _EmissionColor;
                half _EmissionIntensity;

                half _FrontMaskStrength;
                half _UpMaskStrength;
                half _MaskFalloff;
                half _BacklightPreserve;
                half _FaceNormalSmoothness;

                half _ShadingStyle;
                half4 _ShadowColor;
                half _ReceiveShadowStrength;
                half _ShadowMapSoftness;
                half _ShadowDither;
                half _HalfLambertWrap;
                half _DiffuseLightLimit;
                half _ToonStep;
                half _ToonFeather;

                half _GrainIntensity;
                half _GrainScale;

                half4 _SpecularColor;
                half _Smoothness;
                half _SpecularIntensity;
                half _PriSpecularLightLimit;

                half4 _SecSpecularColor;
                half _SecSmoothness;
                half _SecSpecularIntensity;
                half _SecSpecularLightLimit;

                half _UseMatCap;
                half _MatCapBlend;
                half4 _MatCapColor;
                half _MatCapIntensity;

                // ★新規追加: Dissolve用 CBUFFER
                half _UseDissolve;
                half _DissolveAmount;
                half _DissolveInvert;
                half _DissolveType;
                half _DissolveStartY;
                half _DissolveEndY;
                half _DissolveNoiseScale;
                half _DissolveNoiseStrength;
                half4 _DissolveEdgeColor;
                half _DissolveEdgeWidth;

                half4 _SSSColor;
                half _SSSIntensity;
                half _SSSPower;
                half _SSSDistortion;

                half4 _FuzzColor;
                half _FuzzIntensity;
                half _FuzzPower;

                half4 _RimColor;
                half _RimIntensity;
                half _RimPower;
            CBUFFER_END
        ENDHLSL

        // =====================================================================
        //  ForwardLit パス
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilPass]
                Fail [_StencilFail]
                ZFail [_StencilZFail]
            }

            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TRANSPARENT
            #pragma shader_feature_local_fragment _EMISSION_ON
            #pragma shader_feature_local_fragment _SHADINGSTYLE_TOON
            #pragma shader_feature_local_fragment _MATCAP_ON
            #pragma shader_feature_local_fragment _MATCAPBLEND_ADD _MATCAPBLEND_MULTIPLY
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY
            #pragma shader_feature_local_fragment _DISSOLVE_INVERT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DollLighting.hlsl"

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_ReceiveShadowMask);
            TEXTURE2D(_SpecularMask);
            TEXTURE2D(_BlueNoiseTex);
            TEXTURE2D(_MatCapTex);
            TEXTURE2D(_EmissionMap);
            TEXTURE2D(_DissolveTex);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            // ... (CalculateSingleLight は変更なしのため省略せずそのまま記述します) ...
            half3 CalculateSingleLight(
                Light light, half3 detailNormalWS, half3 viewDirectionWS, float3 objectForwardWS,
                half3 baseColor, half receiveShadowMask, half specMask, half ditherValue,
                float baseProceduralMask, float rimFresnel, float fuzzFresnel, half3 indirectLight)
            {
                float3 rawDiffuseLight = (light.color * light.distanceAttenuation) + indirectLight;
                float3 diffuseLightEnergy = ApplyLightEnergyLimit(rawDiffuseLight, _DiffuseLightLimit);

                float3 rawSpecLight = light.color * light.distanceAttenuation;
                float3 priSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _PriSpecularLightLimit);
                float3 secSpecLightEnergy = ApplyLightEnergyLimit(rawSpecLight, _SecSpecularLightLimit);

                float proceduralMask = GetProceduralMask(baseProceduralMask, objectForwardWS, light.direction, _BacklightPreserve);
                half3 diffuseNormalWS = GetFaceSmoothedNormal(detailNormalWS, objectForwardWS, proceduralMask, _FaceNormalSmoothness);

                float diffuseNdotL = dot(diffuseNormalWS, light.direction);
                float halfLambert = GetHalfLambert(diffuseNdotL, _HalfLambertWrap);

                float castShadow = GetCastShadow(light.shadowAttenuation, receiveShadowMask, _ReceiveShadowStrength,
                                                  ditherValue, _ShadowDither, _ShadowMapSoftness, proceduralMask);

                float litMask = GetLitMask(halfLambert, proceduralMask, _ToonStep, _ToonFeather);
                float finalShade = min(litMask, castShadow);

                half3 diffuseColor = GetShadedAlbedo(baseColor, _ShadowColor.rgb, finalShade);
                half3 finalDiffuse = diffuseColor * diffuseLightEnergy;

                float NdotL_Specular = dot(detailNormalWS, light.direction);
                float specularMaskVal;
                half3 finalSpecular = CalculateDualLobeSpecular(
                    detailNormalWS, light.direction, viewDirectionWS, NdotL_Specular,
                    priSpecLightEnergy, _SpecularColor, _Smoothness, _SpecularIntensity,
                    secSpecLightEnergy, _SecSpecularColor, _SecSmoothness, _SecSpecularIntensity,
                    specMask, castShadow, specularMaskVal);

                float specLuminance = saturate(dot(finalSpecular, half3(0.299, 0.587, 0.114)));
                finalDiffuse *= (1.0 - specLuminance);

                half3 finalSSS = CalculateSSS(detailNormalWS, light.direction, viewDirectionWS,
                                               _SSSColor.rgb, _SSSIntensity, _SSSPower, _SSSDistortion,
                                               diffuseLightEnergy, castShadow);

                half3 finalRim = CalculateRimLight(_RimColor.rgb, rimFresnel, _RimIntensity,
                                                     diffuseLightEnergy, NdotL_Specular, castShadow);

                half3 finalFuzz = CalculatePeachFuzz(_FuzzColor.rgb, fuzzFresnel, _FuzzIntensity,
                                                       diffuseLightEnergy, NdotL_Specular, castShadow);

                return finalDiffuse + finalSpecular + finalSSS + finalRim + finalFuzz;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 finalColor = half3(0, 0, 0);
                half3 dissolveEmission = half3(0, 0, 0);

                // ★新規追加: Dissolve判定（完全に消えるピクセルはここで破棄されます）
                #if defined(_DISSOLVE_ON)
                    float dissolveNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_MainTex, input.uv * _DissolveNoiseScale).r;
                    float dissolveGrad = 0.5;
                    
                    #if defined(_DISSOLVETYPE_WORLDY)
                        dissolveGrad = saturate((input.positionWS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
                    #elif defined(_DISSOLVETYPE_LOCALY)
                        dissolveGrad = saturate((input.positionOS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
                    #endif

                    float dissolveVal = dissolveGrad;
                    #if defined(_DISSOLVETYPE_NONE)
                        dissolveVal = dissolveNoise;
                    #else
                        dissolveVal = dissolveGrad + (dissolveNoise - 0.5) * _DissolveNoiseStrength;
                    #endif

                    float dMin = -0.5 * _DissolveNoiseStrength;
                    float dMax = 1.0 + 0.5 * _DissolveNoiseStrength;
                    #if defined(_DISSOLVETYPE_NONE)
                        dMin = 0.0; dMax = 1.0;
                    #endif
                    
                    // 端に燃焼エフェクトが残らないように余裕を持たせてRemap
                    float adjustedAmount = lerp(dMin - _DissolveEdgeWidth - 0.01, dMax + _DissolveEdgeWidth + 0.01, _DissolveAmount);
                    float clipVal = dissolveVal - adjustedAmount;
                    
                    #if defined(_DISSOLVE_INVERT)
                        clipVal = -clipVal;
                    #endif
                    
                    clip(clipVal); // 0未満ならピクセルを破棄（消える）
                    
                    // 消えゆく境界での燃え上がりエフェクト
                    float dissolveEdgeMask = smoothstep(0.0, _DissolveEdgeWidth + 0.0001, clipVal);
                    dissolveEmission = _DissolveEdgeColor.rgb * (1.0 - dissolveEdgeMask);
                #endif

                half3 cleanNormalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 objectForwardWS = input.forwardWS;

                float3 noiseVec = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, input.uv * _GrainScale).rgb * 2.0 - 1.0;
                half3 detailNormalWS = GetGrainNormal(cleanNormalWS, noiseVec, _GrainIntensity);
                
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;

                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif

                half receiveShadowMask = SAMPLE_TEXTURE2D(_ReceiveShadowMask, sampler_MainTex, input.uv).r;
                half specMask = SAMPLE_TEXTURE2D(_SpecularMask, sampler_MainTex, input.uv).r;

                float2 ditherUV = input.positionCS.xy / 256.0;
                half ditherValue = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, ditherUV).r;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                float4 shadowCoord = input.shadowCoord;

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    shadowCoord = ComputeScreenPos(input.positionCS);
                #endif

                float baseProceduralMask = GetProceduralMaskBase(cleanNormalWS, objectForwardWS, _FrontMaskStrength, _UpMaskStrength, _MaskFalloff);
                float NdotV = saturate(dot(detailNormalWS, viewDirectionWS));
                float rimFresnel;
                float fuzzFresnel;
                GetFresnelTerms(NdotV, _RimIntensity, _RimPower, _FuzzIntensity, _FuzzPower, rimFresnel, fuzzFresnel);

                half3 indirectLight = SampleSH(cleanNormalWS);

                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
                finalColor += CalculateSingleLight(mainLight, detailNormalWS, viewDirectionWS, objectForwardWS, albedo.rgb, receiveShadowMask, specMask, ditherValue, baseProceduralMask, rimFresnel, fuzzFresnel, indirectLight);

                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        finalColor += CalculateSingleLight(addLight, detailNormalWS, viewDirectionWS, objectForwardWS, albedo.rgb, receiveShadowMask, specMask, ditherValue, baseProceduralMask, rimFresnel, fuzzFresnel, half3(0,0,0));
                    LIGHT_LOOP_END
                #endif

                #if defined(_MATCAP_ON)
                    float2 matcapUV = GetMatCapUV(detailNormalWS);
                    half3 matcapColor = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MainTex, matcapUV).rgb * _MatCapColor.rgb;
                    finalColor = ApplyMatCap(finalColor, matcapColor, _MatCapIntensity);
                #endif

                #if defined(_EMISSION_ON)
                    half3 emissionMapColor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, input.uv).rgb;
                    finalColor += CalculateEmission(emissionMapColor, _EmissionColor.rgb, _EmissionIntensity);
                #endif

                finalColor += dissolveEmission;

                half outputAlpha = 1.0h;
                #if defined(_SURFACE_TRANSPARENT)
                    outputAlpha = albedo.a;
                #endif

                return half4(finalColor, outputAlpha);
            }
            ENDHLSL
        }

        // =====================================================================
        //  ShadowCaster パス 
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}
            
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            // ★新規追加: 影も連動して消えるようにキーワードを追加
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY
            #pragma shader_feature_local_fragment _DISSOLVE_INVERT

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;
            
            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_DissolveTex);

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
                float3 positionWS : TEXCOORD1; // ★追加
                float3 positionOS : TEXCOORD2; // ★追加
            };

            Varyings vert_shadow(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
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
                output.positionWS = positionWS; // ★追加
                output.positionOS = input.positionOS.xyz; // ★追加
                return output;
            }
            
            half4 frag_shadow(Varyings input) : SV_Target 
            { 
                // ★影のパスでもDissolve判定を行うことで、落ち影も自然に消えます
                #if defined(_DISSOLVE_ON)
                    float dissolveNoise = SAMPLE_TEXTURE2D(_DissolveTex, sampler_MainTex, input.uv * _DissolveNoiseScale).r;
                    float dissolveGrad = 0.5;
                    
                    #if defined(_DISSOLVETYPE_WORLDY)
                        dissolveGrad = saturate((input.positionWS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
                    #elif defined(_DISSOLVETYPE_LOCALY)
                        dissolveGrad = saturate((input.positionOS.y - _DissolveStartY) / (_DissolveEndY - _DissolveStartY + 0.0001));
                    #endif

                    float dissolveVal = dissolveGrad;
                    #if defined(_DISSOLVETYPE_NONE)
                        dissolveVal = dissolveNoise;
                    #else
                        dissolveVal = dissolveGrad + (dissolveNoise - 0.5) * _DissolveNoiseStrength;
                    #endif

                    float dMin = -0.5 * _DissolveNoiseStrength;
                    float dMax = 1.0 + 0.5 * _DissolveNoiseStrength;
                    #if defined(_DISSOLVETYPE_NONE)
                        dMin = 0.0; dMax = 1.0;
                    #endif
                    
                    float adjustedAmount = lerp(dMin - _DissolveEdgeWidth - 0.01, dMax + _DissolveEdgeWidth + 0.01, _DissolveAmount);
                    float clipVal = dissolveVal - adjustedAmount;
                    
                    #if defined(_DISSOLVE_INVERT)
                        clipVal = -clipVal;
                    #endif
                    
                    clip(clipVal);
                #endif

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }

    CustomEditor "Origuma.EasyPBR.URP.Editor.DollShaderGUI"
}
