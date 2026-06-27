// =============================================================================
//  DollSurfaceTypes.hlsl
//  DollSurfaceData 型定義（URP の SurfaceData と名前衝突しないよう分離）
// =============================================================================
#pragma once

#ifndef DOLL_SURFACE_TYPES_INCLUDED
#define DOLL_SURFACE_TYPES_INCLUDED

struct DollSurfaceData
{
    half3 albedo;
    half3 cleanNormalWS;
    half3 detailNormalWS;
    half3 bentNormalWS;
    half  bentOpenness;
    half3 sssTransWS;
    half  sssMask;
    half3 coatNormalWS;
    half  clearcoatMask;
    half  receiveShadowMask;
    half  specMask;
    half  occlusion;
    half  cavity;
    half  curvRidge;
    half  ditherValue;
    half  baseProceduralMask;
    half  NdotV;
    half  rimFresnel;
    half  fuzzFresnel;
    half  specAAVariance;
    AnisoPrecomp anisoPrecomp;
    GlitterGeom  glitterGeom;
    bool         glitterActive;
    half3 dissolveEmission;
    half3 indirectLight;
};

#endif // DOLL_SURFACE_TYPES_INCLUDED
