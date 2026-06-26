// =============================================================================
//  DollInput.hlsl
//  テクスチャと変数の宣言まとめ
// =============================================================================
#ifndef EASYPBR_INPUT_INCLUDED
#define EASYPBR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// --- テクスチャ宣言 ---
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_DissolveTex);
TEXTURE2D(_MatCapTex);
TEXTURE2D(_BlueNoiseTex);
TEXTURE2D(_GlitterMask);

// 特化用テクスチャ（Doll等で使用）
TEXTURE2D(_ReceiveShadowMask);
TEXTURE2D(_SpecularMask);
TEXTURE2D(_NormalMap);
TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
TEXTURE2D(_SSSMask);
TEXTURE2D(_OcclusionMap);
TEXTURE2D(_CavityMap);
TEXTURE2D(_FaceSDFMap);

// --- 変数宣言 (SRP Batcher対応のため一つにまとめる) --- 
CBUFFER_START(UnityPerMaterial)
    // [EasyPBR Core] 基本設定
    half4 _BaseColor;
    half _NormalScale;
    half _UseColorCorrection;
    half _HueShift;
    half _Saturation;
    half _ValueMulti;
    float4 _DetailMap_ST; // ScaleとOffset用
    half4 _DetailColor;
    half _DetailNormalScale;
    half _AlphaClip;
    half _Cutoff;
    half _ShadowCutoffBias;
    half _Cull;
    half _SurfaceTransparent;
    half _SrcBlend;
    half _DstBlend;
    half _ZWrite;
    half _ZTest;

    // [EasyPBR Core] ステンシル
    half _StencilRef;
    half _StencilComp;
    half _StencilPass;
    half _StencilFail;
    half _StencilZFail;

    // [EasyPBR Core] 発光・特殊効果
    half _UseEmission;
    half4 _EmissionColor;
    half _EmissionIntensity;
    
    half _UseDissolve;
    half _DissolveAmount;
    half _DissolveInvert;
    half _DissolveType;
    half _DissolveStartY;
    half _DissolveEndY;
    half _DissolveNoiseScale;
    half _DissolveNoiseStrength;
    half4 _DissolveEdgeColor;
    half4 _DissolveEdgeColor2;
    half _DissolveEdgeWidth;
    half _DissolveEdgeStep;

    half _UseMatCap;
    half _MatCapBlend;
    half4 _MatCapColor;
    half _MatCapIntensity;
    half _MatCapLightInfluence;

    // [Doll Specific] 顔影・ライティング設定
    half _FrontMaskStrength;
    half _UpMaskStrength;
    half _MaskFalloff;
    half _ShadingStyle;
    half4 _ShadowColor;
    half _UseFaceSDF;
    half _FaceSDFFlip;
    half _FaceSDFSoftness;
    half _FaceSDFShadowMix;
    half _FaceSDFBlendNormalMin;
    half _FaceSDFBlendNormalMax;
    half _ReceiverNormalBias;
    half _ReceiveShadowStrength;
    half _ShadowMapSoftness;
    half _ShadowDither;
    half _HalfLambertWrap;
    half _DiffuseLightLimit;
    half _AdditionalLightBlendMode;
    half _ToonStep;
    half _ToonFeather;
    
    // [Doll Specific] 質感・ディテール
    half _GrainIntensity;
    half _GrainScale;
    half _OcclusionStrength;
    half _CavityStrength;

    half _SpecularModel;
    half _SpecularAA;
    half4 _SpecularColor;
    half _Smoothness;
    half _SpecularIntensity;
    half _PriSpecularLightLimit;

    half4 _SecSpecularColor;
    half _SecSmoothness;
    half _SecSpecularIntensity;
    half _SecSpecularLightLimit;

    half _ReflectionStrength;

    half4 _AnisoColor;
    half _AnisoThickness;
    half _AnisoOffset;
    half _AnisoAngle;
    half _AnisoStrandScale;
    half _AnisoStrandStrength;
    half _AnisoStrandDir;
    half _SpecularF0;
    half4 _AnisoSecColor;
    half _AnisoSecThickness;
    half _AnisoSecOffset;

    // [Sequin Glitter]
    half4 _GlitterColor;
    half _GlitterIntensity;
    half _GlitterScale;
    half _GlitterSize;
    half _GlitterTilt;
    half _GlitterSparsity;
    half _GlitterIridescence;
    half _GlitterIridescenceShift;
    half _GlitterBaseReflection;

    half4 _SSSColor;
    half _SSSIntensity;
    half _SSSPower;
    half _SSSDistortion;

    half4 _FuzzColor;
    half _FuzzIntensity;
    half _FuzzPower;

    half4 _RimColor;
    half _RimIntensity;
    half _RimThickness;

    half _BlackOut;

    half4 _OutlineColor;
    half _OutlineWidth;
    half _OutlineCutoffShift;
CBUFFER_END

#endif // EASYPBR_INPUT_INCLUDED
