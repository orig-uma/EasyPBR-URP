using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origuma.EasyPBR.URP.Editor
{
    public class DollShaderGUI : ShaderGUI
    {
        private const string KeyPrefix = "Origuma.EasyPBR.URP.Doll.";
        private const string LangKey = KeyPrefix + "lang.jp";
        private const string HelpKey = KeyPrefix + "show.help";
        private const string CustomUIKey = KeyPrefix + "use.custom.ui";

        private bool _jp;
        private bool _showHelp;
        private bool _useCustomUI = true;
        private bool _prefsLoaded;

        // Section の折りたたみ状態（初回のみ EditorPrefs から読み込み）
        private readonly Dictionary<string, bool> _foldCache = new();

        // GUIContent（言語切り替え時に Clear）
        private readonly Dictionary<string, GUIContent> _labelCache = new();

        // MaterialProperty（properties 配列参照が変わったら再構築）
        private MaterialProperty[] _cachedPropsRef;
        private readonly Dictionary<string, MaterialProperty> _propCache = new();

        // シェーダー初期値の取得用（Shader が変わったら破棄）
        private Shader _cachedShader;
        private readonly Dictionary<string, int> _shaderIndexCache = new();

        // リセットボタン（↺）の GUIContent / GUIStyle（言語変更時に _resetIcon を破棄）
        private GUIContent _resetIcon;
        private GUIStyle _resetStyle;

        // ----------------------------------------------------------------
        //  静的定数（毎フレームの new を排除）
        // ----------------------------------------------------------------
        private static readonly Color s_BarPro = new(0.22f, 0.22f, 0.24f);
        private static readonly Color s_BarPersonal = new(0.78f, 0.78f, 0.80f);
        private static readonly Color s_BarShadow = new(0f, 0f, 0f, 0.15f);
        private static readonly Color s_SubHeader = new(0.5f, 0.5f, 0.5f, 0.2f);

        private static readonly string[] s_UIModeEn = { "Custom", "Default" };
        private static readonly string[] s_UIModeJp = { "カスタム", "デフォルト" };
        private static readonly string[] s_LangOptions = { "English", "日本語" };
        private static readonly string[] s_RenderModeEn = { "Opaque", "Cutout", "Transparent" };
        private static readonly string[] s_RenderModeJp = { "Opaque (不透明)", "Cutout (くり抜き)", "Transparent (半透明)" };

        // ================================================================
        //  エントリポイント
        // ================================================================
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            LoadPrefs();
            RebuildPropCacheIfNeeded(properties);
            DrawToolbar();

            if (!_useCustomUI)
            {
                base.OnGUI(materialEditor, properties);
                return;
            }

            if (_showHelp)
                EditorGUILayout.HelpBox(
                    _jp
                        ? "各項目を初期値から変更すると、その行の右端に \u21BA が表示されます。押すとその項目だけをシェーダーの初期値に戻せます。"
                        : "When a value differs from its default, a \u21BA appears at the right of that row. Click it to reset just that property to the shader default.",
                    MessageType.None);

            // -----------------------------------------------------------
            // 1. Surface Options
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("surface", false, "Surface Options", "サーフェス設定", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawRenderModeSetup(materialEditor, properties);
                        EditorGUILayout.Space(4);

                        P(materialEditor, "_Cull", "Cull", "Cull", "", "描画する面 (Off / Front / Back)");
                        P(materialEditor, "_ZWrite", "ZWrite", "ZWrite", "", "深度バッファへの書き込み (On / Off)");
                        P(materialEditor, "_ZTest", "ZTest", "ZTest", "", "深度テストの条件 (LEqual: 通常, Always: 常に前面に描画 など)");
                        P(materialEditor, "_SrcBlend", "Source Blend", "Source Blend", "", "背景と合成する際の元カラーの係数");
                        P(materialEditor, "_DstBlend", "Destination Blend", "Destination Blend", "",
                            "背景と合成する際の背景カラーの係数");

                        EditorGUILayout.Space(4);
                        materialEditor.RenderQueueField();

                        EditorGUILayout.Space(4);
                        P(materialEditor, "_SurfaceTransparent", "Output Alpha (_SURFACE_TRANSPARENT)",
                            "アルファ出力 (_SURFACE_TRANSPARENT)", "", "アルファ値を出力するかどうか");

                        var alphaClipProp = Prop("_AlphaClip");
                        P(materialEditor, alphaClipProp, "Alpha Clipping (_ALPHATEST_ON)", "アルファ切り抜き (_ALPHATEST_ON)",
                            "", "アルファ値によるピクセルの破棄");
                        if (alphaClipProp != null && alphaClipProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_Cutoff", "Alpha Cutoff", "Alpha Cutoff", "", "クリッピングの閾値");
                            }
                        

                        EditorGUILayout.Space(4);
                        {
                            bool stencilOpen;
                            if (!_foldCache.TryGetValue("stencil", out stencilOpen))
                            {
                                stencilOpen = EditorPrefs.GetBool(KeyPrefix + "fold.stencil", false);
                                _foldCache["stencil"] = stencilOpen;
                            }

                            var newStencilOpen = EditorGUILayout.Foldout(stencilOpen, "Stencil", true,
                                EditorStyles.foldoutHeader);
                            if (newStencilOpen != stencilOpen)
                            {
                                _foldCache["stencil"] = newStencilOpen;
                                EditorPrefs.SetBool(KeyPrefix + "fold.stencil", newStencilOpen);
                            }

                            if (newStencilOpen)
                                using (new EditorGUI.IndentLevelScope())
                                {
                                    P(materialEditor, "_StencilRef", "Stencil Ref", "Stencil Ref", "",
                                        "ステンシルの参照値 (0-255)");
                                    P(materialEditor, "_StencilComp", "Compare Function", "Compare Function", "",
                                        "ステンシルテストの比較条件 (Always, Equal, NotEqual など)");
                                    P(materialEditor, "_StencilPass", "Pass Operation", "Pass Operation", "",
                                        "テスト通過時の処理 (Keep, Replace など)");
                                    P(materialEditor, "_StencilFail", "Fail Operation", "Fail Operation", "",
                                        "ステンシルテスト失敗時の処理");
                                    P(materialEditor, "_StencilZFail", "ZFail Operation", "ZFail Operation", "",
                                        "ステンシルテスト成功、かつZテスト失敗時の処理");
                                }
                        }
                    }
            }

            // -----------------------------------------------------------
            // 2. Base Core
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("base", false, "Base Core", "基本設定", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var mainTex = Prop("_MainTex");
                        var baseColor = Prop("_BaseColor");
                        if (mainTex != null)
                        {
                            materialEditor.TexturePropertySingleLine(
                                Label("Base Map (RGB / Alpha)", "ベースマップ (RGB/Alpha)", "", ""),
                                mainTex, baseColor);
                            materialEditor.TextureScaleOffsetProperty(mainTex);
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Color Correction", "色調補正 (HSV)");
                        var useCCProp = Prop("_UseColorCorrection");
                        P(materialEditor, useCCProp, "Enable Color Correction", "色調補正を有効にする", "",
                            "OFFのとき HSV 変換をスキップします（軽量）");
                        if (useCCProp != null && useCCProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_HueShift", "Hue Shift", "色相シフト", "", "色合いを回転させます");
                                P(materialEditor, "_Saturation", "Saturation", "彩度", "", "鮮やかさを調整します");
                                P(materialEditor, "_ValueMulti", "Value Multiplier", "明度", "", "明るさを調整します");
                            }

                        EditorGUILayout.Space(4);
                        SubHeader("Detail Map", "ディテールマップ (タトゥーやチーク等)");
                        var detailTex = Prop("_DetailMap");
                        if (detailTex != null)
                        {
                            materialEditor.TexturePropertySingleLine(
                                Label("Detail Map (RGBA)", "ディテールマップ (RGBA)", "", "アルファ値でブレンドされます"),
                                detailTex, Prop("_DetailColor"));
                            materialEditor.TextureScaleOffsetProperty(detailTex);
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Normal Map", "ノーマルマップ (凹凸)");
                        var normalTex = Prop("_NormalMap");
                        if (normalTex != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Normal Map", "ノーマルマップ", "", ""),
                                normalTex, Prop("_NormalScale"));
                    }
            }

            // -----------------------------------------------------------
            // 3. Light and Shadow
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("light", false, "Light and Shadow", "ライトと影", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var shadingStyleProp = Prop("_ShadingStyle");
                        P(materialEditor, shadingStyleProp, "Shading Style", "シェーディング", "", "");
                        P(materialEditor, "_ShadowColor", "Shadow Color", "影の色", "", "");

                        var recvMask = Prop("_ReceiveShadowMask");
                        if (recvMask != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Receive Shadow Mask (R)", "落ち影マスク (R)", "", ""), recvMask);

                        P(materialEditor, "_ReceiveShadowStrength", "Receive Strength", "落ち影の強さ", "", "");
                        var shadowQualityProp = Prop("_ShadowQuality");
                        P(materialEditor, shadowQualityProp, "Self Shadow Quality", "落ち影の品質", "",
                            "Off: URP標準 / Pcf: 高品質ソフト影（推奨）/ Pcss: 接地で硬く遠方で柔らかく（高負荷）");

                        P(materialEditor, "_ShadowMapSoftness", "Shadow Softness", "落ち影のソフトさ", "",
                            "PCF/PCSS時はペナンブラ幅、Off時はエッジの柔らかさ");

                        bool hqShadow = shadowQualityProp != null && shadowQualityProp.floatValue >= 0.5f;

                        if (hqShadow)
                        {
                            // PCF/PCSS のときだけ意味を持つ：受け側ノーマルオフセット
                            P(materialEditor, "_ReceiverNormalBias", "Receiver Normal Bias", "影アクネ補正", "",
                                "縞ノイズ(アクネ)が出るなら上げる。上げ過ぎると影が痩せる。Light側のNormal Biasは下げる");
                        }
                        else
                        {
                            // Off のときだけ意味を持つ：UV連動ディザ
                            P(materialEditor, "_ShadowDither", "Shadow Dither", "影のディザ", "", "");
                        }
                        P(materialEditor, "_HalfLambertWrap", "Light Wrap", "ライトラップ", "", "");

                        if (shadingStyleProp != null && shadingStyleProp.floatValue >= 0.5f)
                        {
                            EditorGUILayout.Space(2);
                            P(materialEditor, "_ToonStep", "Toon Threshold", "トゥーン境界位置", "", "");
                            P(materialEditor, "_ToonFeather", "Toon Softness", "トゥーン境界の柔らかさ", "", "");
                        }

                        EditorGUILayout.Space(4);
                        {
                            bool shadowFixOpen;
                            if (!_foldCache.TryGetValue("auto_shadow_fix", out shadowFixOpen))
                            {
                                shadowFixOpen = EditorPrefs.GetBool(KeyPrefix + "fold.auto_shadow_fix", false);
                                _foldCache["auto_shadow_fix"] = shadowFixOpen;
                            }

                            var newShadowFixOpen = EditorGUILayout.Foldout(shadowFixOpen,
                                _jp ? "影補正 (自動)" : "Auto Shadow Fix", true, EditorStyles.foldoutHeader);
                            if (newShadowFixOpen != shadowFixOpen)
                            {
                                _foldCache["auto_shadow_fix"] = newShadowFixOpen;
                                EditorPrefs.SetBool(KeyPrefix + "fold.auto_shadow_fix", newShadowFixOpen);
                            }

                            if (newShadowFixOpen)
                                using (new EditorGUI.IndentLevelScope())
                                {
                                    P(materialEditor, "_FrontMaskStrength", "Front Brightness", "正面の明るさ", "", "");
                                    P(materialEditor, "_UpMaskStrength", "Up Brightness", "上向きの明るさ", "", "");
                                    P(materialEditor, "_MaskFalloff", "Erase Breadth", "補正の範囲", "", "");
                                }
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Anti-Blowout", "白飛び防止");
                        P(materialEditor, "_DiffuseLightLimit", "Diffuse Light Limit", "ベース明るさ上限", "", "1灯あたりの拡散光の上限");
                        P(materialEditor, "_AdditionalLightBlendMode", "Additional Light Blend", "追加ライトの合成方法", "",
                            "Add: 物理的（白飛びしやすい）/ Max: アニメ向け（彩度を保つ）");
                    }
            }

            // -----------------------------------------------------------
            // 4. Specular and Reflection
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("specular", false, "Specular and Reflection", "ハイライトと映り込み", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        // --- Model ---
                        var specModelProp = Prop("_SpecularModel");
                        P(materialEditor, specModelProp, "Specular Model", "スペキュラモデル", "",
                            "BlinnPhong: 軽量・従来互換 / Ggx: 物理ベース（Fresnel・自然な裾）");
                        if (specModelProp != null && specModelProp.floatValue >= 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                                P(materialEditor, "_SpecularF0", "Fresnel (F0)", "フレネル反射率", "", "誘電体は 0.04 前後");

                        var specMask = Prop("_SpecularMask");
                        if (specMask != null)
                            materialEditor.TexturePropertySingleLine(Label("Specular Mask (R)", "スペキュラマスク (R)", "", ""),
                                specMask);

                        // --- Dual-Lobe ---
                        SubHeader("Primary (Sharp)", "Primary（シャープ）");
                        P(materialEditor, "_SpecularColor", "Color", "色", "", "");
                        P(materialEditor, "_Smoothness", "Smoothness", "なめらかさ", "", "");
                        P(materialEditor, "_SpecularIntensity", "Intensity", "強度", "", "");
                        P(materialEditor, "_PriSpecularLightLimit", "Light Limit", "明るさ上限", "", "");

                        SubHeader("Secondary (Matte)", "Secondary（マット）");
                        P(materialEditor, "_SecSpecularColor", "Color", "色", "", "");
                        P(materialEditor, "_SecSmoothness", "Smoothness", "なめらかさ", "", "");
                        P(materialEditor, "_SecSpecularIntensity", "Intensity", "強度", "", "");
                        P(materialEditor, "_SecSpecularLightLimit", "Light Limit", "明るさ上限", "", "");

                        // --- Anisotropic ---
                        SubHeader("Anisotropic (Hair / Silk)", "異方性ハイライト (髪 / シルク)");
                        var anisoColorProp = Prop("_AnisoColor");
                        P(materialEditor, anisoColorProp, "Color (A=0 is Off)", "色 (アルファ0で無効)", "",
                            "アルファ値を0にすると計算自体がスキップされます");
                        if (anisoColorProp != null && anisoColorProp.colorValue.a > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_AnisoThickness", "Thickness", "太さ", "", "");
                                P(materialEditor, "_AnisoOffset", "Position Offset", "位置のズレ", "", "");
                                P(materialEditor, "_AnisoAngle", "Angle", "角度 (向き)", "", "");
                                P(materialEditor, "_AnisoStrandScale", "Strand Scale", "繊維の細かさ", "",
                                    "数値を上げるほど毛束が細かくなります");
                                P(materialEditor, "_AnisoStrandStrength", "Strand Strength", "繊維の凹凸感", "",
                                    "ハイライトが毛束に沿ってギザギザに割れます");
                                P(materialEditor, "_AnisoStrandDir", "Strand Direction", "繊維の方向", "",
                                    "繊維（ノイズ）が流れるUVの方向を回転させます");

                                // 2nd Lobe（主ハイライトが有効なときのみ意味を持つ）
                                EditorGUILayout.Space(2);
                                SubHeader("Sub Highlight (2nd Lobe)", "サブハイライト (2nd Lobe)");
                                var anisoSecProp = Prop("_AnisoSecColor");
                                P(materialEditor, anisoSecProp, "Color (A=0 is Off)", "副の色 (アルファ0で無効)", "",
                                    "主の逆側にもう一本の広いツヤを足します");
                                if (anisoSecProp != null && anisoSecProp.colorValue.a > 0f)
                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        P(materialEditor, "_AnisoSecThickness", "Thickness", "太さ", "", "");
                                        P(materialEditor, "_AnisoSecOffset", "Position Offset", "位置のズレ", "",
                                            "主と逆符号にすると上下に分かれて髪らしくなります");
                                    }
                            }

                        // --- MatCap ---
                        SubHeader("MatCap", "MatCap");
                        var useMatCapProp = Prop("_UseMatCap");
                        P(materialEditor, useMatCapProp, "Enable MatCap", "MatCapを使う", "", "");
                        if (useMatCapProp != null && useMatCapProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_MatCapBlend", "Blend Mode", "合成モード", "", "");
                                var matcap = Prop("_MatCapTex");
                                if (matcap != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("MatCap Texture (RGB)", "MatCapテクスチャ (RGB)", "", ""), matcap);
                                P(materialEditor, "_MatCapColor", "Tint", "色", "", "");
                                P(materialEditor, "_MatCapIntensity", "Intensity", "強度", "", "");
                            }
                    }
            }

            // -----------------------------------------------------------
            // 5. Outline
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("outline", false, "Outline", "アウトライン (輪郭線)", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawOutlineSetup(materialEditor, properties);
                    }
                }
            }

            // -----------------------------------------------------------
            // 6. Emission
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("emission", false, "Emission", "発光", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var useEmissionProp = Prop("_UseEmission");
                        P(materialEditor, useEmissionProp, "Enable Emission", "発光を有効にする", "", "");

                        if (useEmissionProp != null && useEmissionProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                var emTex = Prop("_EmissionMap");
                                var emColor = Prop("_EmissionColor");
                                if (emTex != null && emColor != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Emission Map & Color", "発光マップと色 (HDR)", "", ""),
                                        emTex, emColor);

                                P(materialEditor, "_EmissionIntensity", "Intensity", "発光の強度", "", "");
                                materialEditor.LightmapEmissionProperty();
                            }
                    }
            }

            // -----------------------------------------------------------
            // 7. Optional Effects
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("optional", false, "Optional Effects", "追加質感エフェクト", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Glitter", "グリッター");
                        var glitterMask = Prop("_GlitterMask");
                        if (glitterMask != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Glitter Mask (R)", "発生マスク (R)", "", "白く塗られた部分にだけグリッターが発生します"), glitterMask);

                        P(materialEditor, "_GlitterColor", "Color (HDR)", "色 (HDR)", "",
                            "HDRで白飛びさせることで画面のBloomやGlareエフェクトを誘発します");
                        var glitterIntProp = Prop("_GlitterIntensity");
                        P(materialEditor, glitterIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "",
                            "発光の強さ。ポストエフェクトが反応するまで上げてください");

                        if (glitterIntProp != null && glitterIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_GlitterScale", "Density Scale", "密度", "", "数値を上げるほど粒が細かく密集します");
                                P(materialEditor, "_GlitterSize", "Dot Size", "粒の大きさ", "", "発光の起点となるコアの大きさ");
                                P(materialEditor, "_GlitterTilt", "Normal Tilt", "法線の傾き(ばらつき)", "",
                                    "0でモデルの表面に沿い、数値を上げるほどランダムな方向を向いてチラつきます");
                                P(materialEditor, "_GlitterSparsity", "Sparsity", "間引き率", "",
                                    "スパンコールの密集度。値が小さいほどまばらになります（推奨:0.5）");
                                P(materialEditor, "_GlitterIridescence", "Iridescence", "虹色強度", "",
                                    "1に近づけるほど、視線角度に応じて虹色（ホログラム）に変化します");
                                P(materialEditor, "_GlitterIridescenceShift", "IridescenceShift", "虹色移動", "",
                                    "1に近づけるほど、視線角度に応じて虹色が変化します");
                                P(materialEditor, "_GlitterBaseReflection", "Base Reflection", "暗い反射（ベース）", "",
                                    "光っていない時のスパンコール自体の存在感（メタリック感）。0～0.1程度推奨");
                            }

                        SubHeader("SSS (Subsurface)", "SSS（表面下散乱）");
                        P(materialEditor, "_SSSColor", "Color", "色", "", "");
                        var sssIntProp = Prop("_SSSIntensity");
                        P(materialEditor, sssIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (sssIntProp != null && sssIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_SSSPower", "Falloff", "減衰", "", "");
                                P(materialEditor, "_SSSDistortion", "Distortion", "歪み", "", "");
                            }

                        SubHeader("Peach Fuzz (Soft Edge Sheen)", "Peach Fuzz（縁の柔らかい光沢）");
                        P(materialEditor, "_FuzzColor", "Color", "色", "", "");
                        var fuzzIntProp = Prop("_FuzzIntensity");
                        P(materialEditor, fuzzIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (fuzzIntProp != null && fuzzIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_FuzzPower", "Width", "幅", "", "");
                            }

                        SubHeader("Rim Light", "Rim Light（リムライト）");
                        P(materialEditor, "_RimColor", "Color", "色", "", "");
                        var rimIntProp = Prop("_RimIntensity");
                        P(materialEditor, rimIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (rimIntProp != null && rimIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_RimThickness", "Thickness", "太さ", "", "");
                            }

                        SubHeader("Grain", "グレイン（表面の微細ザラつき）");
                        var grainIntProp = Prop("_GrainIntensity");
                        P(materialEditor, grainIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "",
                            "ブルーノイズで法線を僅かに揺らし、つるつる感を抑えます");
                        if (grainIntProp != null && grainIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_GrainScale", "Scale", "スケール", "",
                                    "ブルーノイズの UV スケール");
                            }
                    }
            }

            // -----------------------------------------------------------
            // 8. Special Effects
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("special_effects", false, "Special Effects", "特殊エフェクト", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Dissolve", "消失エフェクト");
                        var useDissolveProp = Prop("_UseDissolve");
                        P(materialEditor, useDissolveProp, "Enable Dissolve", "ディゾルブを有効にする", "",
                            "ディゾルブ（消失）エフェクトを有効にします");

                        if (useDissolveProp != null && useDissolveProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_DissolveAmount", "Dissolve Amount", "消失量", "", "0で完全表示、1で完全消失");
                                P(materialEditor, "_DissolveInvert", "Invert Dissolve", "方向を反転", "",
                                    "チェックを入れると消失方向（条件）が逆転します");
                                var typeProp = Prop("_DissolveType");
                                P(materialEditor, typeProp, "Dissolve Axis", "消失軸", "",
                                    "None=テクスチャのみ, WorldY=空間のY座標, LocalY=モデルのY座標");

                                if (typeProp != null && typeProp.floatValue > 0.5f)
                                {
                                    P(materialEditor, "_DissolveStartY", "Start Y", "開始Y", "", "フェードが始まるY座標");
                                    P(materialEditor, "_DissolveEndY", "End Y", "終了Y", "", "フェードが終わるY座標");
                                }

                                EditorGUILayout.Space(2);
                                var disTex = Prop("_DissolveTex");
                                if (disTex != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Dissolve Noise", "ノイズテクスチャ", "", "境界を揺らすためのノイズテクスチャ"), disTex);

                                P(materialEditor, "_DissolveNoiseScale", "Noise Scale", "ノイズスケール", "", "ノイズの細かさ");
                                P(materialEditor, "_DissolveNoiseStrength", "Noise Strength", "ノイズ強度", "",
                                    "ノイズによる境界の揺れ幅");

                                EditorGUILayout.Space(2);
                                P(materialEditor, "_DissolveEdgeColor", "Edge Outer Color (HDR)", "エッジ外側の色 (HDR)", "",
                                    "消失の最前線の輝き");
                                P(materialEditor, "_DissolveEdgeColor2", "Edge Inner Color (HDR)", "エッジ内側の色 (HDR)", "",
                                    "少し内側のグラデーション色");
                                P(materialEditor, "_DissolveEdgeWidth", "Edge Width", "エッジ幅", "", "発光する境界線の太さ");
                                P(materialEditor, "_DissolveEdgeStep", "Step Edge (Toon Style)", "エッジの段階化 (Toon調)", "",
                                    "チェックを入れるとグラデーションがパキッとした階調（層）になります");
                            }

                        EditorGUILayout.Space(6);
                        SubHeader("Black Out", "暗転エフェクト");
                        P(materialEditor, "_BlackOut", "Black Out Amount", "暗転率", "", "");
                    }
            }

            // -----------------------------------------------------------
            // 9. Blue Noise
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("blue_noise", false, "Blue Noise", "ブルーノイズ",
                        "Shared texture for shadow edge dither (Self Shadow Quality: Off) and surface grain.",
                        "Self Shadow Quality が Off のときの影エッジ・ディザと、グレイン（法線の微細揺らぎ）で共通サンプルされます。"))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var noise = Prop("_BlueNoiseTex");
                        if (noise != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Blue Noise Texture", "ブルーノイズテクスチャ", "", ""), noise);
                    }
            }

            // -----------------------------------------------------------
            // 10. Advanced Options
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("advanced", false, "Advanced Options", "高度な設定", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        materialEditor.EnableInstancingField();
                        materialEditor.DoubleSidedGIField();
                    }
            }
        }

        // ================================================================
        //  サブ描画メソッド
        // ================================================================
        private void DrawRenderModeSetup(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var surfaceProp = Prop("_SurfaceTransparent");
            var alphaClipProp = Prop("_AlphaClip");
            if (surfaceProp == null || alphaClipProp == null) return;

            var currentMode = 0;
            if (surfaceProp.floatValue > 0.5f) currentMode = 2;
            else if (alphaClipProp.floatValue > 0.5f) currentMode = 1;

            EditorGUI.BeginChangeCheck();
            var lbl = Label("Render Mode (Preset)", "Render Mode (プリセット)", "Sets Queue, Blend, ZWrite automatically",
                "一括で半透明用の設定に切り替えます");
            var newMode = EditorGUILayout.Popup(lbl, currentMode, _jp ? s_RenderModeJp : s_RenderModeEn);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Render Mode Setup");
                foreach (Material mat in materialEditor.targets)
                    SetupRenderMode(mat, newMode);
            }
        }

        private void SetupRenderMode(Material mat, int mode)
        {
            switch (mode)
            {
                case 0: // Opaque
                    mat.SetFloat("_SurfaceTransparent", 0f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.renderQueue = 2000;
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.DisableKeyword("_SURFACE_TRANSPARENT");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    break;
                case 1: // Cutout
                    mat.SetFloat("_SurfaceTransparent", 0f);
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.renderQueue = 2450;
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.DisableKeyword("_SURFACE_TRANSPARENT");
                    mat.EnableKeyword("_ALPHATEST_ON");
                    break;
                case 2: // Transparent
                    mat.SetFloat("_SurfaceTransparent", 1f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.renderQueue = 3000;
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.EnableKeyword("_SURFACE_TRANSPARENT");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    break;
            }
        }

        private void DrawOutlineSetup(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var outlineProp = Prop("_UseOutline");
            if (outlineProp == null) return;

            EditorGUI.BeginChangeCheck();

            P(materialEditor, outlineProp, "Enable Outline", "アウトラインを有効にする", "", "");
            var isOutlineOn = outlineProp.floatValue > 0.5f;

            if (isOutlineOn)
                using (new EditorGUI.IndentLevelScope())
                {
                    P(materialEditor, "_OutlineColor", "Color", "色", "", "");
                    P(materialEditor, "_OutlineWidth", "Width", "太さ", "", "");

                    var alphaClipProp = Prop("_AlphaClip");
                    if (alphaClipProp != null && alphaClipProp.floatValue > 0.5f)
                    {
                        EditorGUILayout.Space(2);
                        P(materialEditor, "_OutlineCutoffShift", "Cutoff Shift (Fix)", "透過エッジの補正", "",
                            "毛先などの半透明グラデーション部分で、アウトラインが黒く太く残ってしまう現象を打ち消します");
                    }

                    EditorGUILayout.Space(4);
                    SubHeader("Masking (Stencil)", "マスク処理 (ステンシル)");
                    P(materialEditor, "_OutlineStencilRef", "Stencil Ref", "参照値", "", "本体側のStencil Refと同じ数値を入れます");
                    P(materialEditor, "_OutlineStencilComp", "Compare Function", "比較条件", "",
                        "NotEqualにすると、本体が描画された部分には線が描かれなくなります");
                    P(materialEditor, "_OutlineStencilPass", "Pass Operation", "Pass Operation", "",
                        "テスト通過時の処理 (基本はKeep)");
                    P(materialEditor, "_OutlineStencilFail", "Fail Operation", "Fail Operation", "", "ステンシルテスト失敗時の処理");
                    P(materialEditor, "_OutlineStencilZFail", "ZFail Operation", "ZFail Operation", "",
                        "ステンシルテスト成功、かつZテスト失敗時の処理");
                }

            if (EditorGUI.EndChangeCheck() || !_prefsLoaded)
                foreach (Material mat in materialEditor.targets)
                    if (isOutlineOn) mat.EnableKeyword("_OUTLINE_ON");
                    else mat.DisableKeyword("_OUTLINE_ON");
        }

        // ================================================================
        //  初期化・キャッシュ
        // ================================================================
        private void LoadPrefs()
        {
            if (_prefsLoaded) return;
            _jp = EditorPrefs.GetBool(LangKey, Application.systemLanguage == SystemLanguage.Japanese);
            _showHelp = EditorPrefs.GetBool(HelpKey, true);
            _useCustomUI = EditorPrefs.GetBool(CustomUIKey, true);
            _prefsLoaded = true;
        }

        // properties 配列の参照が変わった時だけ Dictionary を再構築する
        private void RebuildPropCacheIfNeeded(MaterialProperty[] properties)
        {
            if (ReferenceEquals(properties, _cachedPropsRef)) return;
            _cachedPropsRef = properties;
            _propCache.Clear();
            foreach (var p in properties)
                _propCache[p.name] = p;
        }

        // ================================================================
        //  ツールバー
        // ================================================================
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("EasyPBR / Doll", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                var uiMode = EditorGUILayout.Popup(_useCustomUI ? 0 : 1,
                    _jp ? s_UIModeJp : s_UIModeEn, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _useCustomUI = uiMode == 0;
                    EditorPrefs.SetBool(CustomUIKey, _useCustomUI);
                }

                EditorGUI.BeginChangeCheck();
                var lang = EditorGUILayout.Popup(_jp ? 1 : 0, s_LangOptions, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _jp = lang == 1;
                    EditorPrefs.SetBool(LangKey, _jp);
                    _labelCache.Clear(); // 言語変更時にラベルキャッシュを破棄
                    _resetIcon = null;   // ↺ ボタンのツールチップも作り直す
                }

                EditorGUI.BeginChangeCheck();
                var help = GUILayout.Toggle(_showHelp, _jp ? "説明" : "Help", "Button", GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    _showHelp = help;
                    EditorPrefs.SetBool(HelpKey, help);
                }
            }
        }

        // ================================================================
        //  ユーティリティ
        // ================================================================

        // Section ヘッダー（折りたたみ状態を _foldCache にキャッシュ）
        private bool Section(string id, bool defaultOpen, string titleEn, string titleJp, string descEn, string descJp)
        {
            bool open;
            if (!_foldCache.TryGetValue(id, out open))
            {
                open = EditorPrefs.GetBool(KeyPrefix + "fold." + id, defaultOpen);
                _foldCache[id] = open;
            }

            var rect = EditorGUILayout.GetControlRect(false, 24f);
            var barColor = EditorGUIUtility.isProSkin ? s_BarPro : s_BarPersonal;
            EditorGUI.DrawRect(rect, barColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), s_BarShadow);
            EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y + 3f, 14f, 18f), open ? "\u25BC" : "\u25B6");
            EditorGUI.LabelField(new Rect(rect.x + 22f, rect.y + 3f, rect.width - 26f, 18f), _jp ? titleJp : titleEn,
                EditorStyles.boldLabel);

            var e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                open = !open;
                _foldCache[id] = open;
                EditorPrefs.SetBool(KeyPrefix + "fold." + id, open);
                e.Use();
            }

            if (open && _showHelp && !string.IsNullOrEmpty(descJp))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(_jp ? descJp : descEn, MessageType.None);
            }

            if (open) EditorGUILayout.Space(4);
            return open;
        }

        private void SubHeader(string en, string jp)
        {
            EditorGUILayout.Space(4);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), s_SubHeader);
            EditorGUILayout.LabelField(_jp ? jp : en, EditorStyles.miniBoldLabel);
        }

        // GUIContent をキャッシュして返す（言語変更時は _labelCache.Clear() で破棄）
        private GUIContent Label(string en, string jp, string tipEn, string tipJp)
        {
            GUIContent content;
            if (!_labelCache.TryGetValue(en, out content))
            {
                content = new GUIContent(_jp ? jp : en, _jp ? tipJp : tipEn);
                _labelCache[en] = content;
            }

            return content;
        }

        // キャッシュから MaterialProperty を取得
        private MaterialProperty Prop(string name)
        {
            MaterialProperty p;
            return _propCache.TryGetValue(name, out p) ? p : null;
        }

        // MaterialProperty を直接渡すオーバーロード
        private void P(MaterialEditor editor, MaterialProperty prop, string en, string jp, string tipEn, string tipJp)
        {
            if (prop == null) return;

            var label = Label(en, jp, tipEn, tipJp);
            var changed = DiffersFromDefault(prop);

            var h = editor.GetPropertyHeight(prop);
            var row = EditorGUILayout.GetControlRect(true, h);

            // 初期値と異なるときだけ、右端にリセットボタン用の隙間を空ける
            var fieldRect = row;
            if (changed) fieldRect.width -= 20f;

            editor.ShaderProperty(fieldRect, prop, label);

            if (changed)
            {
                var btn = new Rect(row.xMax - 18f, row.y + 1f, 16f, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(btn, ResetContent(), ResetStyle()))
                    ResetProperty(editor, prop);
            }
        }

        // 名前で引いて描画（propCache 経由）
        private void P(MaterialEditor editor, string name, string en, string jp, string tipEn, string tipJp)
        {
            P(editor, Prop(name), en, jp, tipEn, tipJp);
        }

        // ================================================================
        //  初期値リセット
        // ================================================================

        // prop が属する Shader 内のプロパティ index を返す（キャッシュ付き）。
        // 同時に _cachedShader を prop の Shader に合わせる。見つからなければ -1。
        private int ShaderIndex(MaterialProperty prop)
        {
            if (prop.targets == null || prop.targets.Length == 0) return -1;
            var shader = (prop.targets[0] as Material)?.shader;
            if (shader == null) return -1;

            if (!ReferenceEquals(shader, _cachedShader))
            {
                _cachedShader = shader;
                _shaderIndexCache.Clear();
            }

            if (!_shaderIndexCache.TryGetValue(prop.name, out var idx))
            {
                idx = shader.FindPropertyIndex(prop.name);
                _shaderIndexCache[prop.name] = idx;
            }

            return idx;
        }

        // 現在値がシェーダー定義の初期値と異なるか
        private bool DiffersFromDefault(MaterialProperty prop)
        {
            if (prop.hasMixedValue) return true;

            var idx = ShaderIndex(prop);
            if (idx < 0 || _cachedShader == null) return false;

            switch (prop.propertyType)
            {
                case ShaderPropertyType.Color:
                {
                    Vector4 def = _cachedShader.GetPropertyDefaultVectorValue(idx);
                    Vector4 cur = prop.colorValue;
                    return (def - cur).sqrMagnitude > 1e-8f;
                }
                case ShaderPropertyType.Vector:
                {
                    Vector4 def = _cachedShader.GetPropertyDefaultVectorValue(idx);
                    return (def - prop.vectorValue).sqrMagnitude > 1e-8f;
                }
                case ShaderPropertyType.Texture:
                    return prop.textureValue != null
                           || prop.textureScaleAndOffset != new Vector4(1f, 1f, 0f, 0f);
#if UNITY_2021_1_OR_NEWER
                case ShaderPropertyType.Int:
                    return prop.intValue != Mathf.RoundToInt(_cachedShader.GetPropertyDefaultFloatValue(idx));
#endif
                default: // Float / Range
                    return !Mathf.Approximately(prop.floatValue, _cachedShader.GetPropertyDefaultFloatValue(idx));
            }
        }

        // prop をシェーダー定義の初期値に戻す（Undo 対応）
        private void ResetProperty(MaterialEditor editor, MaterialProperty prop)
        {
            var idx = ShaderIndex(prop);
            if (idx < 0 || _cachedShader == null) return;

            editor.RegisterPropertyChangeUndo(prop.displayName);

            switch (prop.propertyType)
            {
                case ShaderPropertyType.Color:
                    prop.colorValue = _cachedShader.GetPropertyDefaultVectorValue(idx);
                    break;
                case ShaderPropertyType.Vector:
                    prop.vectorValue = _cachedShader.GetPropertyDefaultVectorValue(idx);
                    break;
                case ShaderPropertyType.Texture:
                    prop.textureValue = null; // フォールバック（white/bump 等）に戻す
                    prop.textureScaleAndOffset = new Vector4(1f, 1f, 0f, 0f);
                    break;
#if UNITY_2021_1_OR_NEWER
                case ShaderPropertyType.Int:
                    prop.intValue = Mathf.RoundToInt(_cachedShader.GetPropertyDefaultFloatValue(idx));
                    break;
#endif
                default: // Float / Range
                    prop.floatValue = _cachedShader.GetPropertyDefaultFloatValue(idx);
                    break;
            }

            GUI.changed = true;
        }

        private GUIContent ResetContent()
        {
            return _resetIcon ??= new GUIContent("\u21BA", _jp ? "初期値に戻す" : "Reset to default");
        }

        private GUIStyle ResetStyle()
        {
            if (_resetStyle == null)
                _resetStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            return _resetStyle;
        }
    }
}
