// =============================================================================
//  DollMaterialSetup.cs
// -----------------------------------------------------------------------------
//  Doll マテリアルの「状態変更」ロジック（描画ではない）を集約した純粋ユーティリティ。
//   - Render Mode プリセット（Opaque / Cutout / Transparent）
//   - Self Shadow Mode のキーワード手動同期
//   - 0.3.5 で廃止したキーワードの掃除
//  GUI（DollShaderGUI）と検証（ValidateMaterial）の双方から呼ばれる。
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Origuma.EasyPBR.URP.Editor
{
    internal static class DollMaterialSetup
    {
        private static readonly int SurfaceTransparent = Shader.PropertyToID("_SurfaceTransparent");
        private static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        // Self Shadow Mode（Off / PCF Tent / PCF Vogel / PCSS）に対応するキーワード。
        public static readonly string[] ShadowModeKeywords =
            { "_SHADOWMODE_OFF", "_SHADOWMODE_TENTPCF", "_SHADOWMODE_VOGELPCF", "_SHADOWMODE_PCSS" };

        private static readonly string[] s_DeprecatedKeywords =
        {
            "_SURFACE_TRANSPARENT",
            "_SHADINGSTYLE_TOON",
            "_SPECULARMODEL_BLINNPHONG",
            "_SPECULARMODEL_GGX",
        };

        // Render Mode プリセットを一括適用（Queue / Blend / ZWrite / RenderType / Alpha Clip）。
        public static void ApplyRenderMode(Material mat, int mode)
        {
            switch (mode)
            {
                case 0: // Opaque
                    mat.SetFloat(SurfaceTransparent, 0f);
                    mat.SetFloat(AlphaClip, 0f);
                    mat.SetFloat(SrcBlend, (float)BlendMode.One);
                    mat.SetFloat(DstBlend, (float)BlendMode.Zero);
                    mat.SetFloat(ZWrite, 1f);
                    mat.renderQueue = 2000;
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    break;
                case 1: // Cutout
                    mat.SetFloat(SurfaceTransparent, 0f);
                    mat.SetFloat(AlphaClip, 1f);
                    mat.SetFloat(SrcBlend, (float)BlendMode.One);
                    mat.SetFloat(DstBlend, (float)BlendMode.Zero);
                    mat.SetFloat(ZWrite, 1f);
                    mat.renderQueue = 2450;
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.EnableKeyword("_ALPHATEST_ON");
                    break;
                case 2: // Transparent
                    mat.SetFloat(SurfaceTransparent, 1f);
                    mat.SetFloat(AlphaClip, 0f);
                    mat.SetFloat(SrcBlend, (float)BlendMode.SrcAlpha);
                    mat.SetFloat(DstBlend, (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat(ZWrite, 0f);
                    mat.renderQueue = 3000;
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    break;
            }
        }

        // KeywordEnum を使わずキーワードを手動同期（表示名を自由にするため）。
        public static void SyncShadowMode(Material mat, int mode)
        {
            mode = Mathf.Clamp(mode, 0, ShadowModeKeywords.Length - 1);
            for (var i = 0; i < ShadowModeKeywords.Length; i++)
            {
                if (i == mode) mat.EnableKeyword(ShadowModeKeywords[i]);
                else mat.DisableKeyword(ShadowModeKeywords[i]);
            }
        }

        // 廃止キーワードが残っていても無害だが、バリアント表を汚さないよう除去する。
        public static void CleanupDeprecated(Material material)
        {
            foreach (var kw in s_DeprecatedKeywords)
                if (material.IsKeywordEnabled(kw))
                    material.DisableKeyword(kw);
        }
    }
}
