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
        [MainTexture] _MainTex ("Base Map (RGB / Alpha)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0.0, 2.0)) = 1.0
        [Toggle] _UseColorCorrection ("Enable Color Correction", Float) = 0
        _HueShift("Hue Shift", Range(-0.5, 0.5)) = 0.0
        _Saturation("Saturation", Range(0.0, 2.0)) = 1.0
        _ValueMulti("Value Multiplier", Range(0.0, 2.0)) = 1.0
        _DetailMap("Detail Map", 2D) = "black" {}
        _DetailColor("Detail Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset][Normal] _DetailNormalMap("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Scale", Range(0.0, 2.0)) = 1.0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _ShadowCutoffBias ("Shadow Cutoff Bias (fatten)", Range(0.0, 0.5)) = 0.2
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        // --- 半透明描画 ---------------------------------------------------------
        [Header(Surface Options (Transparency))]
        [ToggleUI] _SurfaceTransparent ("Alpha Blend (Transparent)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0
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
        [Toggle] _UseEmission ("Enable Emission", Float) = 0
        [NoScaleOffset] _EmissionMap ("Emission Map (RGB)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0.0, 10.0)) = 1.0

        // --- 顔の自己陰を自動で消す仕組み -------------
        [Header(Auto Face Shadow Fix (No Mask Needed))]
        _FrontMaskStrength ("Front Brightness", Range(0.0, 1.0)) = 0.0
        _UpMaskStrength ("Up Brightness", Range(0.0, 1.0)) = 0.0
        _MaskFalloff ("Mask Falloff", Range(0.1, 10.0)) = 2.0

        // --- ライティングと影 ------------------------------------------------
        [Header(Light and Shadow)]
        [Enum(Smooth, 0, Toon, 1)] _ShadingStyle ("Shading Style", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0.7, 0.7, 0.75, 1)
        [Toggle] _UseFaceSDF ("Enable Face SDF Shadow", Float) = 0
        [NoScaleOffset] _FaceSDFMap ("Face SDF Map", 2D) = "white" {}
        [Toggle] _FaceSDFFlip ("Face SDF Flip Forward", Float) = 0
        _FaceSDFSoftness ("Face SDF Softness", Range(0.001, 0.5)) = 0.5
        _FaceSDFShadowMix ("Face SDF External Shadow Mix", Range(0.0, 1.0)) = 1.0
        _FaceSDFBlendNormalMin("SDF Blend Normal Min (SDF無効化のしきい値)", Range(-1.5, 1.0)) = -1.0
        _FaceSDFBlendNormalMax("SDF Blend Normal Max (SDF有効化のしきい値)", Range(-1.0, 1.5)) = 0.0
        _ReceiveShadowMask ("Receive Shadow Mask (R=Shadow)", 2D) = "white" {}
        [KeywordEnum(Off, Pcf (Tent), Pcf (Vogel), Pcss)] _ShadowMode ("Self Shadow Mode", Float) = 1
        
        _ReceiverNormalBias ("Receiver Normal Bias", Range(0.0, 3.0)) = 0.6
        _ReceiveShadowStrength ("Receive Shadow Strength", Range(0.0, 1.0)) = 1.0
        _ShadowMapSoftness ("Shadow Softness", Range(0.0, 1.0)) = 0.4
        _ShadowDither ("Shadow Edge Dither", Range(0.0, 1.0)) = 0.5
        _HalfLambertWrap ("Light Wrap", Range(0.0, 1.0)) = 0.0
        _DiffuseLightLimit ("Diffuse Light Limit", Range(0.1, 5.0)) = 1.0
        [Enum(Add, 0, Max, 1)] _AdditionalLightBlendMode ("Additional Light Blend", Float) = 1
        [Space(10)]
        _ToonStep ("Toon Threshold", Range(0.0, 1.0)) = 0.5
        _ToonFeather ("Toon Softness", Range(0.0, 1.0)) = 0.2

        // --- ブルーノイズ（影ディザ・グレイン共通）-------------------------
        [Header(Blue Noise)]
        [NoScaleOffset] _BlueNoiseTex ("Blue Noise Texture", 2D) = "grey" {}

        // --- スペキュラと映り込み --------------------------------------------
        [Header(Specular and Reflection)]
        [Enum(BlinnPhong, 0, Ggx, 1)] _SpecularModel ("Specular Model", Float) = 1
        _SpecularAA ("Specular Anti-Aliasing", Range(0.0, 1.0)) = 1.0
        _SpecularF0 ("Fresnel (F0)", Range(0.0, 1.0)) = 0.04
        _SpecularMask ("Specular Mask (R)", 2D) = "white" {}
        [Space(10)]
        _SpecularColor ("Primary Specular Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Primary Smoothness", Range(0.01, 1.0)) = 0.8
        _SpecularIntensity ("Primary Intensity", Range(0.0, 5.0)) = 0.0
        _PriSpecularLightLimit ("Primary Light Limit", Range(0.1, 10.0)) = 2
        [Space(10)]
        _SecSpecularColor ("Secondary Specular Color", Color) = (1, 1, 1, 1)
        _SecSmoothness ("Secondary Smoothness", Range(0.01, 1.0)) = 0.2
        _SecSpecularIntensity ("Secondary Intensity", Range(0.0, 5.0)) = 0.0
        _SecSpecularLightLimit ("Secondary Light Limit", Range(0.1, 5.0)) = 1.2
        [Space(10)]
        _ReflectionStrength ("Environment Reflection", Range(0.0, 1.0)) = 0.0
        [Header(Anisotropic Highlight)]
        [HDR] _AnisoColor ("Aniso Color", Color) = (0, 0, 0, 0)
        _AnisoThickness ("Aniso Thickness", Range(0.0, 1.0)) = 0.2
        _AnisoOffset ("Aniso Position Offset", Range(-1.0, 1.0)) = 0.0
        _AnisoAngle ("Aniso Angle", Range(-180.0, 180.0)) = 0.0
        _AnisoStrandScale ("Strand Scale", Range(1.0,500.0)) = 50.0
        _AnisoStrandStrength ("Strand Strength", Range(0.0, 1.0)) = 0.05
        _AnisoStrandDir ("Strand Direction", Range(-180.0, 180.0)) = 0.0
        [HDR] _AnisoSecColor ("Aniso 2nd Color (A=Enable)", Color) = (0,0,0,0)
        _AnisoSecThickness ("Aniso 2nd Thickness", Range(0.0, 1.0)) = 0.7
        _AnisoSecOffset ("Aniso 2nd Offset", Range(-1.0, 1.0)) = -0.15
        [Space(10)]
        [Toggle] _UseMatCap ("Enable MatCap", Float) = 0
        [Enum(Add, 0, Multiply, 1)] _MatCapBlend ("MatCap Blend Mode", Float) = 0
        [NoScaleOffset] _MatCapTex ("MatCap Texture (RGB)", 2D) = "black" {}
        _MatCapColor ("MatCap Tint", Color) = (1, 1, 1, 1)
        _MatCapIntensity ("MatCap Intensity", Range(0.0, 5.0)) = 1.0
        _MatCapLightInfluence ("MatCap Light Influence", Range(0.0, 1.0)) = 0.0

        // --- Dissolve (消失エフェクト) --------------------------------
        [Header(Dissolve)]
        [Toggle(_DISSOLVE_ON)] _UseDissolve ("Enable Dissolve", Float) = 0
        _DissolveAmount ("Dissolve Amount", Range(0.0, 1.0)) = 0.0
        [Toggle] _DissolveInvert ("Invert Dissolve", Float) = 0
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
        [NoScaleOffset] _GlitterMask ("Glitter Mask (R)", 2D) = "white" {}
        [HDR] _GlitterColor ("Glitter Color (HDR)", Color) = (2, 2, 2, 1)
        _GlitterIntensity ("Glitter Intensity", Range(0.0, 50.0)) = 0.0
        _GlitterScale ("Glitter Density (Scale)", Range(10.0, 1000.0)) = 100.0
        _GlitterSize ("Dot Size", Range(0.0005, 0.05)) = 0.005
        _GlitterTilt ("Normal Tilt Strength", Range(0.0, 2.0)) = 0.8
        _GlitterSparsity ("Sparsity (間引き率)", Range(0.0, 1.0)) = 0.5
        _GlitterIridescence ("Iridescence Amount (虹色強度)", Range(0.0, 1.0)) = 0.5
        _GlitterIridescenceShift ("Iridescence Shift (虹色移動)", Range(0, 1)) = 0.5
        _GlitterBaseReflection ("Base Reflection (暗い反射)", Range(0.0, 0.5)) = 0.05
        _GrainIntensity ("Grain Intensity", Range(0.0, 1.0)) = 0.2
        _GrainScale ("Grain UV Scale", Float) = 10.0
        [NoScaleOffset] _OcclusionMap ("Occlusion Map (R)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0.0, 2.0)) = 0.0
        [NoScaleOffset] _BentNormalMap ("Bent Normal", 2D) = "bump" {}
        _BentNormalStrength ("Bent Normal Strength", Range(0.0, 1.0)) = 0.0
        [NoScaleOffset] _CavityMap ("Cavity Map (R)", 2D) = "white" {}
        _CavityStrength ("Cavity Strength", Range(0.0, 2.0)) = 0.0
        [NoScaleOffset] _CurvatureMap ("Curvature Map", 2D) = "gray" {}
        _CurvatureStrength ("Curvature Strength", Range(0.0, 2.0)) = 0.0
        [Space(10)]
        [NoScaleOffset] _SSSMask ("SSS Mask (R)", 2D) = "white" {}
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
        _RimIntensity ("Rim Light Intensity", Range(0.0, 5.0)) = 0.0
        _RimThickness ("Rim Light Thickness", Range(0.0, 1.0)) = 0.2
        [Space(10)]
        _BlackOut ("Black Out", Range(0.0, 1.0)) = 0

        // --- アウトライン ---
        [Header(Outline)]
        [Toggle] _UseOutline ("Enable Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.2, 0.1, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 10.0)) = 1.0
        _OutlineCutoffShift ("Outline Cutoff Shift", Range(-1, 1)) = 0
        
        _OutlineStencilRef ("Outline Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _OutlineStencilComp ("Outline Stencil Compare", Float) = 8 // 8 = Always
        [Enum(UnityEngine.Rendering.StencilOp)] _OutlineStencilPass ("Outline Stencil Pass", Float) = 0 // 0 = Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _OutlineStencilFail ("Outline Stencil Fail", Float) = 0 // 0 = Keep
        [Enum(UnityEngine.Rendering.StencilOp)] _OutlineStencilZFail ("Outline Stencil ZFail", Float) = 0 // 0 = Keep
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
            // 共通変数をインクルード
            #include "DollInput.hlsl"
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
            // _SURFACE_TRANSPARENT / _SHADINGSTYLE_TOON / _SPECULARMODEL_* は
            // バリアントを生まない uniform 動的分岐へ移行（混在マテリアルのバッチング維持）。
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY
            #pragma shader_feature_local_fragment _SHADOWMODE_OFF _SHADOWMODE_TENTPCF _SHADOWMODE_VOGELPCF _SHADOWMODE_PCSS
            

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // メイン処理を記述したパスファイルをインクルード
            #include "Passes/ForwardPass.hlsl"
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

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // 影処理を記述したパスファイルをインクルード
            #include "Passes/ShadowPass.hlsl"
            ENDHLSL
        }

        // =====================================================================
        //  DepthOnly パス
        //  Forward の Depth Prepass / Depth Priming、Forward+ の深度生成に使用。
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert_depth
            #pragma fragment frag_depth

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY

            #include "Passes/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // =====================================================================
        //  DepthNormals パス
        //  Forward+ の Depth Normals Prepass や SSAO / Decal 用の法線生成に使用。
        // =====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert_depthnormals
            #pragma fragment frag_depthnormals

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY

            #include "Passes/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        // =====================================================================
        //  Outline パス
        //  LightMode は独自タグ "DollOutline"。URP は既定で描画しないため、
        //  Forward と交互描画されず ForwardLit のバッチングを阻害しない。
        //  描画には DollOutlineFeature（RendererFeature）が必要。
        //  セットアップは Window > EasyPBR > Doll Outline Setup から。
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "DollOutline" }
            
            Stencil
            {
                Ref [_OutlineStencilRef]
                Comp [_OutlineStencilComp]
                Pass [_OutlineStencilPass]
                Fail [_OutlineStencilFail]
                ZFail [_OutlineStencilZFail]
            }
            
            Cull Front // 背面法なので
            ZWrite On
            Offset 1, 1
            
            HLSLPROGRAM
            #pragma vertex vert_outline
            #pragma fragment frag_outline
            
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _DISSOLVETYPE_NONE _DISSOLVETYPE_WORLDY _DISSOLVETYPE_LOCALY

            #include "Passes/OutlinePass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Origuma.EasyPBR.URP.Editor.DollShaderGUI"
}
