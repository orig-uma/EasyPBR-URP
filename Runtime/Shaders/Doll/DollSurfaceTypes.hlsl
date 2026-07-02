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
    half3 shadowAlbedo;   // 1影の最終色（Shadow Color + Hue Shift/Saturation 適用済み）
    half3 shadow2Albedo;  // 2影の最終色（同上ベースに 2nd Shadow Color を乗算）
    half3 castShadowAlbedo; // 落ち影の最終色（同上ベースに Cast Shadow Color を乗算）
    half3 cleanNormalWS;
    half3 detailNormalWS;
    half3 shadeNormalWS;  // 拡散の陰専用の平滑化法線（未ベイク時は detailNormalWS と同一）
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
    half  scatterCurvMask;  // スキンスキャッタの曲率マスク（曲率未使用時は 1）
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
