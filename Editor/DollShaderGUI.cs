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
        private const string CustomUIKey = KeyPrefix + "use.custom.ui";

        private bool _jp;
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
        private static readonly string[] s_ShadowModeEn = { "Off", "PCF (Tent)", "PCF (Vogel)", "PCSS" };
        private static readonly string[] s_ShadowModeJp = { "Off", "PCF (Tent)", "PCF (Vogel)", "PCSS" };
        private static readonly string[] s_ShadowModeKeywords =
            { "_SHADOWMODE_OFF", "_SHADOWMODE_TENTPCF", "_SHADOWMODE_VOGELPCF", "_SHADOWMODE_PCSS" };
        private static readonly int SurfaceTransparent = Shader.PropertyToID("_SurfaceTransparent");
        private static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

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

                        P(materialEditor, "_Cull", "Cull",
                            "Which faces to render (Off / Front / Back)",
                            "描画する面 (Off / Front / Back)");
                        P(materialEditor, "_ZWrite", "ZWrite",
                            "Write to the depth buffer (On / Off)",
                            "深度バッファへの書き込み (On / Off)");
                        P(materialEditor, "_ZTest", "ZTest",
                            "Depth test condition (LEqual: normal, Always: draw on top, etc.)",
                            "深度テストの条件 (LEqual: 通常, Always: 常に前面に描画 など)");
                        P(materialEditor, "_SrcBlend", "Source Blend",
                            "Blend factor for the source (this object's) color",
                            "背景と合成する際の元カラーの係数");
                        P(materialEditor, "_DstBlend", "Destination Blend",
                            "Blend factor for the destination (background) color",
                            "背景と合成する際の背景カラーの係数");

                        EditorGUILayout.Space(4);
                        materialEditor.RenderQueueField();

                        EditorGUILayout.Space(4);
                        P(materialEditor, "_SurfaceTransparent", "Alpha Blend (Transparent)",
                            "Render as alpha-blended transparent",
                            "半透明（アルファブレンド）として描画します");

                        var alphaClipProp = Prop("_AlphaClip");
                        Pv(materialEditor, alphaClipProp, "Alpha Clipping",
                            "Discard pixels by alpha value",
                            "アルファ値によるピクセルの破棄");
                        if (alphaClipProp != null && alphaClipProp.floatValue > 0.5f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_Cutoff", "Alpha Cutoff",
                                    "Clipping threshold",
                                    "クリッピングの閾値");
                            }

                            P(materialEditor, "_ShadowCutoffBias", "Shadow Cutoff Bias",
                                "Casts a slightly fatter alpha shadow to stabilize wispy hair-tip flicker. 0 = same as the visible cutoff",
                                "影だけ少し太めのアルファで落として毛先のチラつきを安定させる。0で前面cutoffと同じ");
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
                                    P(materialEditor, "_StencilRef", "Stencil Ref",
                                        "Stencil reference value (0-255)",
                                        "ステンシルの参照値 (0-255)");
                                    P(materialEditor, "_StencilComp", "Compare Function",
                                        "Stencil compare function (Always, Equal, NotEqual, etc.)",
                                        "ステンシルテストの比較条件 (Always, Equal, NotEqual など)");
                                    P(materialEditor, "_StencilPass", "Pass Operation",
                                        "Operation when the test passes (Keep, Replace, etc.)",
                                        "テスト通過時の処理 (Keep, Replace など)");
                                    P(materialEditor, "_StencilFail", "Fail Operation",
                                        "Operation when the stencil test fails",
                                        "ステンシルテスト失敗時の処理");
                                    P(materialEditor, "_StencilZFail", "ZFail Operation",
                                        "Operation when stencil passes but the depth test fails",
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
                                Label("Base Map (RGB / Alpha)",
                                    "Albedo. Alpha is used for clipping / transparency",
                                    "アルベド。アルファはクリップ／透過に使用"),
                                mainTex, baseColor);
                            materialEditor.TextureScaleOffsetProperty(mainTex);
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Color Correction", "色調補正 (HSV)");
                        var useCCProp = Prop("_UseColorCorrection");
                        P(materialEditor, useCCProp, "Enable Color Correction",
                            "Skips HSV conversion when off (cheaper)",
                            "OFFのとき HSV 変換をスキップします（軽量）");
                        if (useCCProp != null && useCCProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_HueShift", "Hue Shift",
                                    "Rotates the hue",
                                    "色合いを回転させます");
                                P(materialEditor, "_Saturation", "Saturation",
                                    "Adjusts vividness",
                                    "鮮やかさを調整します");
                                P(materialEditor, "_ValueMulti", "Value Multiplier",
                                    "Adjusts brightness",
                                    "明るさを調整します");
                            }

                        EditorGUILayout.Space(4);
                        SubHeader("Detail Map", "ディテールマップ (タトゥーやチーク等)");
                        var detailTex = Prop("_DetailMap");
                        if (detailTex != null)
                        {
                            materialEditor.TexturePropertySingleLine(
                                Label("Detail Map (RGBA)",
                                    "Blended using its alpha channel",
                                    "アルファ値でブレンドされます"),
                                detailTex, Prop("_DetailColor"));
                            materialEditor.TextureScaleOffsetProperty(detailTex);
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Normal Map", "ノーマルマップ (凹凸)");
                        var normalTex = Prop("_NormalMap");
                        if (normalTex != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Normal Map",
                                    "Tangent-space normal map",
                                    "接空間ノーマルマップ"),
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
                        P(materialEditor, shadingStyleProp, "Shading Style",
                            "Smooth: continuous shading / Toon: hard two-tone anime look",
                            "Smooth: なめらかな階調 / Toon: 境界で2値化したアニメ調");
                        P(materialEditor, "_ShadowColor", "Shadow Color",
                            "Tint multiplied into the base color in shadowed areas",
                            "影部分でベースカラーに乗算する色味");

                        var recvMask = Prop("_ReceiveShadowMask");
                        if (recvMask != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Receive Shadow Mask (R)",
                                    "Only the white parts of the R channel receive cast shadows",
                                    "Rチャンネルの白い部分だけ落ち影を受ける"), recvMask);

                        P(materialEditor, "_ReceiveShadowStrength", "Receive Shadow Strength",
                            "Strength of darkening from the cast shadow map. 0 = no cast shadow",
                            "落ち影(shadow map)で暗くする強さ。0で落ち影なし");
                        var shadowModeProp = Prop("_ShadowMode");
                        if (shadowModeProp != null)
                        {
                            EditorGUI.BeginChangeCheck();
                            var smLbl = VariantLabel("Self Shadow Mode",
                                "Off: URP default (lightest). PCF (Tent): deterministic, noise-free (best for live). PCF (Vogel): adjustable/wide soft, slight noise. PCSS: Vogel + contact hardening (costly). See Documentation~/SHADOWS.md",
                                "Off: URP標準（最軽量）/ PCF (Tent): 決定論的・ノイズなし（ライブ向け）/ PCF (Vogel): 可変・広いぼかし・微ノイズ / PCSS: Vogel＋接地硬化（高負荷）。詳細は Documentation~/SHADOWS.md");
                            var smCur = Mathf.Clamp((int)shadowModeProp.floatValue, 0, 3);
                            var smNew = EditorGUILayout.Popup(smLbl, smCur, _jp ? s_ShadowModeJp : s_ShadowModeEn);
                            if (EditorGUI.EndChangeCheck())
                            {
                                materialEditor.RegisterPropertyChangeUndo("Self Shadow Mode");
                                shadowModeProp.floatValue = smNew;
                                foreach (Material mat in materialEditor.targets)
                                    SetShadowModeKeyword(mat, smNew);
                            }
                        }

                        var shadowMode = shadowModeProp != null ? shadowModeProp.floatValue : 1f;
                        var isOff  = shadowMode < 0.5f;                        // Off
                        var isTent = shadowMode >= 0.5f && shadowMode < 1.5f;  // TentPcf
                        var isVogel = shadowMode >= 1.5f && shadowMode < 2.5f; // Vogel
                        var isHQ   = !isOff;                                   // TentPcf / VogelPcf / Pcss

                        // 受け側ノーマルオフセットは HQ 全モードで効く
                        if (isHQ)
                            P(materialEditor, "_ReceiverNormalBias", "Receiver Normal Bias",
                                "Raise if you see shadow acne (banding). Too high thins the shadow; lower the Light's Normal Bias too",
                                "縞ノイズ(アクネ)が出るなら上げる。上げ過ぎると影が痩せる。Light側のNormal Biasは下げる");

                        // ソフトネスは Tent 以外で効く（Off=エッジ柔らかさ / VogelPcf・Pcss=ペナンブラ幅）
                        if (isVogel)
                            P(materialEditor, "_ShadowMapSoftness", "Shadow Softness",
                                "Penumbra width for VogelPcf/Pcss; edge softness for Off. Tent uses a fixed kernel (no effect)",
                                "VogelPcf/Pcss時はペナンブラ幅、Off時はエッジの柔らかさ。Tentは固定カーネルなので無効");

                        // ディザは Off のときだけ意味を持つ（UV連動ブルーノイズ）
                        if (isOff)
                            P(materialEditor, "_ShadowDither", "Shadow Edge Dither",
                                "When Self Shadow Mode is Off, dithers the shadow edge with blue noise to break up banding",
                                "Self Shadow Mode が Off のとき、影エッジをブルーノイズでディザして階調の段差を散らす");
                        P(materialEditor, "_HalfLambertWrap", "Light Wrap",
                            "Lifts the shaded side to soften shading (Half-Lambert wrap). 0 = Lambert, 1 = brighter overall",
                            "陰側を持ち上げて陰影を柔らかくする（Half-Lambert の wrap 量）。0でランバート、1で全体的に明るい");

                        if (shadingStyleProp != null && shadingStyleProp.floatValue >= 0.5f)
                        {
                            EditorGUILayout.Space(2);
                            P(materialEditor, "_ToonStep", "Toon Threshold",
                                "Lit/shadow boundary position (threshold where lit flips to shadow)",
                                "トゥーン陰の境界位置（明→暗が切り替わるしきい値）");
                            P(materialEditor, "_ToonFeather", "Toon Softness",
                                "Blur width of that boundary. 0 = crisp",
                                "境界のぼかし幅。0でくっきり、上げるほど柔らかい");
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
                                _jp ? "顔の影補正" : "Auto Face Shadow Fix", true, EditorStyles.foldoutHeader);
                            if (newShadowFixOpen != shadowFixOpen)
                            {
                                _foldCache["auto_shadow_fix"] = newShadowFixOpen;
                                EditorPrefs.SetBool(KeyPrefix + "fold.auto_shadow_fix", newShadowFixOpen);
                            }

                            if (newShadowFixOpen)
                                using (new EditorGUI.IndentLevelScope())
                                {
                                    P(materialEditor, "_FrontMaskStrength", "Front Brightness",
                                        "Front-facing surfaces suppress self-shadow and stay bright (face shadow fix)",
                                        "正面を向いた面ほどセルフシャドウを抑えて明るくする（顔の陰落ち対策）");
                                    P(materialEditor, "_UpMaskStrength", "Up Brightness",
                                        "Upward-facing surfaces suppress self-shadow and stay bright",
                                        "上を向いた面ほどセルフシャドウを抑えて明るくする");
                                    P(materialEditor, "_MaskFalloff", "Mask Falloff",
                                        "Tightness of the erase region. Higher limits it to near front/up only, making it narrower and sharper",
                                        "陰を消す範囲の絞り。大きいほど真正面・真上だけに限定され、消える範囲が狭くシャープになる");
                                }
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Anti-Blowout", "白飛び防止");
                        P(materialEditor, "_DiffuseLightLimit", "Diffuse Light Limit",
                            "Luminance cap of diffuse light per light (blowout prevention)",
                            "1灯あたりの拡散光の輝度上限（白飛び防止）");
                        P(materialEditor, "_AdditionalLightBlendMode", "Additional Light Blend",
                            "Add: physical (can blow out) / Max: anime-friendly (keeps saturation)",
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
                        P(materialEditor, specModelProp, "Specular Model",
                            "BlinnPhong: cheap, legacy-compatible / Ggx: physically based (Fresnel, natural falloff)",
                            "BlinnPhong: 軽量・従来互換 / Ggx: 物理ベース（Fresnel・自然な裾）");
                        if (specModelProp != null && specModelProp.floatValue >= 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                                P(materialEditor, "_SpecularF0", "Fresnel (F0)",
                                    "~0.04 for dielectrics",
                                    "誘電体は 0.04 前後");

                        var specMask = Prop("_SpecularMask");
                        if (specMask != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Specular Mask (R)",
                                    "R channel masks specular intensity",
                                    "Rチャンネルでスペキュラ強度をマスク"),
                                specMask);

                        // --- Dual-Lobe ---
                        SubHeader("Primary (Sharp)", "Primary（シャープ）");
                        P(materialEditor, "_SpecularColor", "Color",
                            "Primary specular tint", "主スペキュラの色味");
                        P(materialEditor, "_Smoothness", "Smoothness",
                            "Higher = tighter, sharper highlight", "高いほど締まった鋭いハイライト");
                        P(materialEditor, "_SpecularIntensity", "Intensity",
                            "Primary specular strength. 0 = off", "主スペキュラの強度。0でOFF");
                        P(materialEditor, "_PriSpecularLightLimit", "Light Limit",
                            "Luminance cap for the primary lobe (blowout prevention)",
                            "主ローブの輝度上限（白飛び防止）");

                        SubHeader("Secondary (Matte)", "Secondary（マット）");
                        P(materialEditor, "_SecSpecularColor", "Color",
                            "Secondary specular tint", "副スペキュラの色味");
                        P(materialEditor, "_SecSmoothness", "Smoothness",
                            "Higher = tighter (broad matte sheen when low)", "高いほど締まる（低いと広いマット質感）");
                        P(materialEditor, "_SecSpecularIntensity", "Intensity",
                            "Secondary specular strength. 0 = off", "副スペキュラの強度。0でOFF");
                        P(materialEditor, "_SecSpecularLightLimit", "Light Limit",
                            "Luminance cap for the secondary lobe", "副ローブの輝度上限");

                        // --- Anisotropic ---
                        SubHeader("Anisotropic (Hair / Silk)", "異方性ハイライト (髪 / シルク)");
                        var anisoColorProp = Prop("_AnisoColor");
                        P(materialEditor, anisoColorProp, "Color (A=0 is Off)",
                            "Set alpha to 0 to skip the calculation entirely",
                            "アルファ値を0にすると計算自体がスキップされます");
                        if (anisoColorProp != null && anisoColorProp.colorValue.a > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_AnisoThickness", "Thickness",
                                    "Highlight band width", "ハイライト帯の太さ");
                                P(materialEditor, "_AnisoOffset", "Position Offset",
                                    "Shifts the highlight band along the normal", "ハイライト帯を法線方向にずらす");
                                P(materialEditor, "_AnisoAngle", "Angle",
                                    "Highlight direction (tangent rotation)", "ハイライトの向き（接線の回転）");
                                P(materialEditor, "_AnisoStrandScale", "Strand Scale",
                                    "Higher = finer strands",
                                    "数値を上げるほど毛束が細かくなります");
                                P(materialEditor, "_AnisoStrandStrength", "Strand Strength",
                                    "Breaks the highlight into jagged strands",
                                    "ハイライトが毛束に沿ってギザギザに割れます");
                                P(materialEditor, "_AnisoStrandDir", "Strand Direction",
                                    "Rotates the UV direction the strands flow along",
                                    "繊維（ノイズ）が流れるUVの方向を回転させます");

                                // 2nd Lobe（主ハイライトが有効なときのみ意味を持つ）
                                EditorGUILayout.Space(2);
                                SubHeader("Sub Highlight (2nd Lobe)", "サブハイライト (2nd Lobe)");
                                var anisoSecProp = Prop("_AnisoSecColor");
                                P(materialEditor, anisoSecProp, "Color (A=0 is Off)",
                                    "Adds a second, broader sheen on the opposite side",
                                    "主の逆側にもう一本の広いツヤを足します");
                                if (anisoSecProp != null && anisoSecProp.colorValue.a > 0f)
                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        P(materialEditor, "_AnisoSecThickness", "Thickness",
                                            "Width of the secondary band", "副の帯の太さ");
                                        P(materialEditor, "_AnisoSecOffset", "Position Offset",
                                            "Use the opposite sign to the primary to split them top/bottom like hair",
                                            "主と逆符号にすると上下に分かれて髪らしくなります");
                                    }
                            }

                        // --- MatCap ---
                        SubHeader("MatCap", "MatCap");
                        var useMatCapProp = Prop("_UseMatCap");
                        P(materialEditor, useMatCapProp, "Enable MatCap",
                            "Adds a view-space MatCap sphere", "ビュー空間のMatCapを合成します");
                        if (useMatCapProp != null && useMatCapProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_MatCapBlend", "Blend Mode",
                                    "Add or Multiply", "Add（加算）/ Multiply（乗算）");
                                var matcap = Prop("_MatCapTex");
                                if (matcap != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("MatCap Texture (RGB)",
                                            "Sphere / MatCap lighting texture",
                                            "球状ライティングテクスチャ"), matcap);
                                P(materialEditor, "_MatCapColor", "Tint",
                                    "MatCap tint color", "MatCapの色味");
                                P(materialEditor, "_MatCapIntensity", "Intensity",
                                    "MatCap strength", "MatCapの強度");
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
                        P(materialEditor, useEmissionProp, "Enable Emission",
                            "Adds self-illumination", "自己発光を加えます");

                        if (useEmissionProp != null && useEmissionProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                var emTex = Prop("_EmissionMap");
                                var emColor = Prop("_EmissionColor");
                                if (emTex != null && emColor != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Emission Map & Color",
                                            "Emission texture (RGB) x HDR color",
                                            "発光テクスチャ(RGB) × HDRカラー"),
                                        emTex, emColor);

                                P(materialEditor, "_EmissionIntensity", "Intensity",
                                    "Emission strength", "発光の強度");
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
                                Label("Glitter Mask (R)",
                                    "Glitter only appears where the R channel is white",
                                    "白く塗られた部分にだけグリッターが発生します"), glitterMask);

                        P(materialEditor, "_GlitterColor", "Color (HDR)",
                            "Use HDR values to trigger Bloom / Glare post effects",
                            "HDRで白飛びさせることで画面のBloomやGlareエフェクトを誘発します");
                        var glitterIntProp = Prop("_GlitterIntensity");
                        P(materialEditor, glitterIntProp, "Intensity (0 = Off)",
                            "Flash strength. Raise until post effects react. 0 = off",
                            "発光の強さ。ポストエフェクトが反応するまで上げてください");

                        if (glitterIntProp != null && glitterIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_GlitterScale", "Density Scale",
                                    "Higher = finer, denser particles", "数値を上げるほど粒が細かく密集します");
                                P(materialEditor, "_GlitterSize", "Dot Size",
                                    "Size of each particle's glowing core", "発光の起点となるコアの大きさ");
                                P(materialEditor, "_GlitterTilt", "Normal Tilt",
                                    "0 follows the surface; higher tilts particles randomly for more sparkle",
                                    "0でモデルの表面に沿い、数値を上げるほどランダムな方向を向いてチラつきます");
                                P(materialEditor, "_GlitterSparsity", "Sparsity",
                                    "Thinning rate. Lower = sparser (recommended 0.5)",
                                    "スパンコールの密集度。値が小さいほどまばらになります（推奨:0.5）");
                                P(materialEditor, "_GlitterIridescence", "Iridescence",
                                    "Closer to 1 shifts toward rainbow (hologram) by view angle",
                                    "1に近づけるほど、視線角度に応じて虹色（ホログラム）に変化します");
                                P(materialEditor, "_GlitterIridescenceShift", "Iridescence Shift",
                                    "How much the rainbow hue moves with view angle",
                                    "視線角度に応じて虹色がずれる量");
                                P(materialEditor, "_GlitterBaseReflection", "Base Reflection",
                                    "Metallic presence of sequins when not flashing (~0-0.1 recommended)",
                                    "光っていない時のスパンコール自体の存在感（メタリック感）。0～0.1程度推奨");
                            }

                        SubHeader("SSS (Subsurface)", "SSS（表面下散乱）");
                        var sssMask = Prop("_SSSMask");
                        if (sssMask != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("SSS Mask (R)",
                                    "R channel masks subsurface intensity",
                                    "Rチャンネルで表面下散乱の強度をマスク"),
                                sssMask);
                        P(materialEditor, "_SSSColor", "Color",
                            "Subsurface tint (backlit glow)", "表面下散乱の色味（逆光の透け）");
                        var sssIntProp = Prop("_SSSIntensity");
                        P(materialEditor, sssIntProp, "Intensity (0 = Off)",
                            "Backlit transmission strength. 0 = off", "逆光の透け強度。0でOFF");
                        if (sssIntProp != null && sssIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_SSSPower", "Falloff",
                                    "Higher = tighter, more concentrated glow", "高いほど透けが締まる");
                                P(materialEditor, "_SSSDistortion", "Distortion",
                                    "Bends the backlight direction by the normal", "逆光方向を法線で歪める");
                            }

                        SubHeader("Peach Fuzz (Soft Edge Sheen)", "Peach Fuzz（縁の柔らかい光沢）");
                        P(materialEditor, "_FuzzColor", "Color",
                            "Soft edge sheen tint", "縁の柔らかい光沢の色");
                        var fuzzIntProp = Prop("_FuzzIntensity");
                        P(materialEditor, fuzzIntProp, "Intensity (0 = Off)",
                            "Edge sheen strength. 0 = off", "縁光沢の強度。0でOFF");
                        if (fuzzIntProp != null && fuzzIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_FuzzPower", "Width",
                                    "Higher = narrower rim", "高いほど縁が細い");
                            }

                        SubHeader("Rim Light", "Rim Light（リムライト）");
                        P(materialEditor, "_RimColor", "Color",
                            "Rim light color", "リムライトの色");
                        var rimIntProp = Prop("_RimIntensity");
                        P(materialEditor, rimIntProp, "Intensity (0 = Off)",
                            "Rim light strength. 0 = off", "リムライトの強度。0でOFF");
                        if (rimIntProp != null && rimIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_RimThickness", "Thickness",
                                    "Higher = thicker rim", "高いほど縁が太い");
                            }

                        SubHeader("Grain", "グレイン（表面の微細ザラつき）");
                        var grainIntProp = Prop("_GrainIntensity");
                        P(materialEditor, grainIntProp, "Intensity (0 = Off)",
                            "Slightly jitters the normal with blue noise to reduce a too-smooth look. 0 = off",
                            "ブルーノイズで法線を僅かに揺らし、つるつる感を抑えます");
                        if (grainIntProp != null && grainIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_GrainScale", "Scale",
                                    "Blue-noise UV scale",
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
                        Pv(materialEditor, useDissolveProp, "Enable Dissolve",
                            "Enables the dissolve effect",
                            "ディゾルブ（消失）エフェクトを有効にします");

                        if (useDissolveProp != null && useDissolveProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_DissolveAmount", "Dissolve Amount",
                                    "0 = fully visible, 1 = fully dissolved", "0で完全表示、1で完全消失");
                                P(materialEditor, "_DissolveInvert", "Invert Dissolve",
                                    "Reverses the dissolve direction / condition",
                                    "チェックを入れると消失方向（条件）が逆転します");
                                var typeProp = Prop("_DissolveType");
                                Pv(materialEditor, typeProp, "Dissolve Axis",
                                    "None = texture only, WorldY = world Y, LocalY = object Y",
                                    "None=テクスチャのみ, WorldY=空間のY座標, LocalY=モデルのY座標");

                                if (typeProp != null && typeProp.floatValue > 0.5f)
                                {
                                    P(materialEditor, "_DissolveStartY", "Start Y",
                                        "Y where the fade starts", "フェードが始まるY座標");
                                    P(materialEditor, "_DissolveEndY", "End Y",
                                        "Y where the fade ends", "フェードが終わるY座標");
                                }

                                EditorGUILayout.Space(2);
                                var disTex = Prop("_DissolveTex");
                                if (disTex != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Dissolve Noise",
                                            "Noise texture that perturbs the edge",
                                            "境界を揺らすためのノイズテクスチャ"), disTex);

                                P(materialEditor, "_DissolveNoiseScale", "Noise Scale",
                                    "Noise frequency", "ノイズの細かさ");
                                P(materialEditor, "_DissolveNoiseStrength", "Noise Strength",
                                    "How much the noise perturbs the edge", "ノイズによる境界の揺れ幅");

                                EditorGUILayout.Space(2);
                                P(materialEditor, "_DissolveEdgeColor", "Edge Outer Color (HDR)",
                                    "Glow at the dissolving frontline", "消失の最前線の輝き");
                                P(materialEditor, "_DissolveEdgeColor2", "Edge Inner Color (HDR)",
                                    "Gradient color just inside the edge", "少し内側のグラデーション色");
                                P(materialEditor, "_DissolveEdgeWidth", "Edge Width",
                                    "Thickness of the glowing edge", "発光する境界線の太さ");
                                P(materialEditor, "_DissolveEdgeStep", "Step Edge (Toon Style)",
                                    "Quantizes the gradient into crisp toon-style bands",
                                    "チェックを入れるとグラデーションがパキッとした階調（層）になります");
                            }

                        EditorGUILayout.Space(6);
                        SubHeader("Black Out", "暗転エフェクト");
                        P(materialEditor, "_BlackOut", "Black Out Amount",
                            "Darkens the final color toward black", "最終色を黒へ暗転させます");
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
                                Label("Blue Noise Texture",
                                    "Shared by shadow-edge dither and grain",
                                    "影エッジディザとグレインで共通使用"), noise);
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
            var lbl = Label("Render Mode (Preset)",
                "Sets Queue, Blend, ZWrite automatically",
                "一括で半透明用の設定に切り替えます");
            var newMode = EditorGUILayout.Popup(lbl, currentMode, _jp ? s_RenderModeJp : s_RenderModeEn);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Render Mode Setup");
                foreach (Material mat in materialEditor.targets)
                    SetupRenderMode(mat, newMode);
            }
        }

        // KeywordEnum を使わずキーワードを手動同期（表示名を自由にするため）。
        private static void SetShadowModeKeyword(Material mat, int mode)
        {
            mode = Mathf.Clamp(mode, 0, s_ShadowModeKeywords.Length - 1);
            for (var i = 0; i < s_ShadowModeKeywords.Length; i++)
            {
                if (i == mode) mat.EnableKeyword(s_ShadowModeKeywords[i]);
                else mat.DisableKeyword(s_ShadowModeKeywords[i]);
            }
        }

        // 0.3.5 で uniform 動的分岐へ移行し廃止したキーワード。既存マテリアルから掃除する。
        private static readonly string[] s_DeprecatedKeywords =
        {
            "_SURFACE_TRANSPARENT",
            "_SHADINGSTYLE_TOON",
            "_SPECULARMODEL_BLINNPHONG",
            "_SPECULARMODEL_GGX",
        };

        // マテリアル読み込み/検証時に float からキーワードを復元（stale / リネーム耐性）。
        public override void ValidateMaterial(Material material)
        {
            base.ValidateMaterial(material);
            if (material.HasProperty("_ShadowMode"))
                SetShadowModeKeyword(material, (int)material.GetFloat("_ShadowMode"));

            // 廃止キーワードが残っていても無害だが、バリアント表を汚さないよう除去する。
            foreach (var kw in s_DeprecatedKeywords)
                if (material.IsKeywordEnabled(kw))
                    material.DisableKeyword(kw);
        }

        private void SetupRenderMode(Material mat, int mode)
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

        private void DrawOutlineSetup(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var outlineProp = Prop("_UseOutline");
            if (outlineProp == null) return;

            EditorGUI.BeginChangeCheck();

            Pv(materialEditor, outlineProp, "Enable Outline",
                "Inverted-hull outline pass", "背面法線を押し出す輪郭線パス");
            var isOutlineOn = outlineProp.floatValue > 0.5f;

            if (isOutlineOn)
                using (new EditorGUI.IndentLevelScope())
                {
                    P(materialEditor, "_OutlineColor", "Color",
                        "Outline color", "輪郭線の色");
                    P(materialEditor, "_OutlineWidth", "Width",
                        "Outline thickness", "輪郭線の太さ");

                    var alphaClipProp = Prop("_AlphaClip");
                    if (alphaClipProp != null && alphaClipProp.floatValue > 0.5f)
                    {
                        EditorGUILayout.Space(2);
                        P(materialEditor, "_OutlineCutoffShift", "Cutoff Shift",
                            "Cancels thick dark outlines left on semi-transparent gradients like hair tips",
                            "毛先などの半透明グラデーション部分で、アウトラインが黒く太く残ってしまう現象を打ち消します");
                    }

                    EditorGUILayout.Space(4);
                    SubHeader("Masking (Stencil)", "マスク処理 (ステンシル)");
                    P(materialEditor, "_OutlineStencilRef", "Stencil Ref",
                        "Use the same value as the body's Stencil Ref", "本体側のStencil Refと同じ数値を入れます");
                    P(materialEditor, "_OutlineStencilComp", "Compare Function",
                        "Set to NotEqual to hide the outline where the body is drawn",
                        "NotEqualにすると、本体が描画された部分には線が描かれなくなります");
                    P(materialEditor, "_OutlineStencilPass", "Pass Operation",
                        "Operation when the test passes (usually Keep)",
                        "テスト通過時の処理 (基本はKeep)");
                    P(materialEditor, "_OutlineStencilFail", "Fail Operation",
                        "Operation when the stencil test fails", "ステンシルテスト失敗時の処理");
                    P(materialEditor, "_OutlineStencilZFail", "ZFail Operation",
                        "Operation when stencil passes but the depth test fails",
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
                }

                EditorGUI.BeginChangeCheck();
            }

            // ⚡ 印の凡例（バリアント生成プロパティであることの説明）。
            var legend = _jp
                ? "⚡ = シェーダーバリアントを生成（混在すると SRP Batcher のバッチが分断）"
                : "⚡ = generates a shader variant (mixing splits SRP Batcher batches)";
            EditorGUILayout.LabelField(legend, EditorStyles.miniLabel);
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

            if (open) EditorGUILayout.Space(4);
            return open;
        }

        private void SubHeader(string en, string jp)
        {
            EditorGUILayout.Space(4);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), s_SubHeader);
            EditorGUILayout.LabelField(_jp ? jp : en, EditorStyles.miniBoldLabel);
        }

        // ラベルは英語固定。説明は tooltip に日英で持たせ、言語に応じて切り替える。
        // キャッシュは「ラベル＋日英ツールチップ」をキーにする（同名ラベルで別ツールチップでも衝突しない）。
        // 言語変更時は DrawToolbar 内で _labelCache.Clear() している。
        private GUIContent Label(string label, string tipEn, string tipJp)
        {
            var key = label + "\u241F" + tipEn + "\u241F" + tipJp;
            GUIContent content;
            if (!_labelCache.TryGetValue(key, out content))
            {
                content = new GUIContent(label, _jp ? tipJp : tipEn);
                _labelCache[key] = content;
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
        private void P(MaterialEditor editor, MaterialProperty prop, string label, string tipEn, string tipJp)
        {
            if (prop == null) return;

            var content = Label(label, tipEn, tipJp);
            var h = editor.GetPropertyHeight(prop);
            var row = EditorGUILayout.GetControlRect(true, h);

            editor.ShaderProperty(row, prop, content);
        }

        // 名前で引いて描画（propCache 経由）
        private void P(MaterialEditor editor, string name, string label, string tipEn, string tipJp)
        {
            P(editor, Prop(name), label, tipEn, tipJp);
        }

        // ----------------------------------------------------------------
        //  バリアント生成プロパティの明示
        //  値がマテリアル間で割れると SRP Batcher のバッチが分断されるプロパティ。
        //  ラベルに印(⚡)を付け、ツールチップに注記を足して GUI 上で可視化する。
        //  詳細は Documentation~/SRP_BATCHER.md を参照。
        // ----------------------------------------------------------------
        private const string VariantMark = " ⚡"; // ⚡
        private const string VariantTipEn =
            "\n\n[⚡ Shader variant] Differing values between materials split SRP Batcher batches. See Documentation~/SRP_BATCHER.md.";
        private const string VariantTipJp =
            "\n\n[⚡ シェーダーバリアント] マテリアル間で値が異なると SRP Batcher のバッチが分断されます。詳細は Documentation~/SRP_BATCHER.md。";

        // バリアント生成プロパティを ⚡ 付きで描画（MaterialProperty 版）。
        private void Pv(MaterialEditor editor, MaterialProperty prop, string label, string tipEn, string tipJp)
        {
            P(editor, prop, label + VariantMark, tipEn + VariantTipEn, tipJp + VariantTipJp);
        }

        // バリアント生成プロパティを ⚡ 付きで描画（名前版）。
        private void Pv(MaterialEditor editor, string name, string label, string tipEn, string tipJp)
        {
            Pv(editor, Prop(name), label, tipEn, tipJp);
        }

        // バリアント生成 Popup 用のラベル（⚡ 付き）。
        private GUIContent VariantLabel(string label, string tipEn, string tipJp)
        {
            return Label(label + VariantMark, tipEn + VariantTipEn, tipJp + VariantTipJp);
        }
    }
}
