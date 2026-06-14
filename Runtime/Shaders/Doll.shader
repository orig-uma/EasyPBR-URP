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
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 1
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
        [Space(10)]
        [HDR] _DissolveEdgeColor ("Edge Outer Color (HDR)", Color) = (1.0, 0.6, 0.0, 1.0)
        [HDR] _DissolveEdgeColor2 ("Edge Inner Color (HDR)", Color) = (1.0, 0.0, 0.0, 1.0)
        _DissolveEdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.05
        [Toggle] _DissolveEdgeStep ("Step Edge (Toon Style)", Float) = 0

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
        _RimThickness ("Rim Light Thickness", Range(0.0, 1.0)) = 0.2
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
            // 共通変数をインクルード
            #include "EasyPBR_Input.hlsl"
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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // メイン処理を記述したパスファイルをインクルード
            #include "Doll_ForwardPass.hlsl"
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
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY
            #pragma shader_feature_local_fragment _DISSOLVE_INVERT

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // 影処理を記述したパスファイルをインクルード
            #include "Doll_ShadowPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Origuma.EasyPBR.URP.Editor.DollShaderGUI"
}
