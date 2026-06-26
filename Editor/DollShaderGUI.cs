using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origuma.EasyPBR.URP.Editor
{
    public class DollShaderGUI : ShaderGUI
    {
        private const string KeyPrefix = "Origuma.EasyPBR.URP.Doll.";

        // GitHub 上のドキュメント（GUI からリンクで開く）。
        private const string DocBaseUrl = "https://github.com/orig-uma/EasyPBR-URP/blob/main/Documentation~/";
        private const string ShadowsDocUrl = DocBaseUrl + "SHADOWS.md";
        private const string SrpBatcherDocUrl = DocBaseUrl + "SRP_BATCHER.md";

        // 再利用可能な描画キット（言語・キャッシュ・折りたたみ等の状態を所有）と Baking パネル。
        private ShaderGuiKit _kit;
        private DollBakingPanel _baking;

        // OnGUI 中に kit から同期する表示状態（セクション内容が参照する）。
        private bool _jp;
        private bool _useCustomUI = true;

        // UI 表示名（Render Mode / Self Shadow Mode）。キーワード等のロジックは DollMaterialSetup へ。
        private static readonly string[] s_RenderModeEn = { "Opaque", "Cutout", "Transparent" };
        private static readonly string[] s_RenderModeJp = { "Opaque (不透明)", "Cutout (くり抜き)", "Transparent (半透明)" };
        private static readonly string[] s_ShadowModeEn = { "Off", "PCF (Tent)", "PCF (Vogel)", "PCSS" };
        private static readonly string[] s_ShadowModeJp = { "Off", "PCF (Tent)", "PCF (Vogel)", "PCSS" };

        // ================================================================
        //  エントリポイント
        // ================================================================
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _kit ??= new ShaderGuiKit(KeyPrefix);
            _baking ??= new DollBakingPanel();
            _kit.LoadPrefs();
            _kit.RebuildPropCache(properties);
            _kit.DrawToolbar("EasyPBR / Doll", _kit.Jp ? "SRP Batcher ガイド" : "SRP Batcher guide", SrpBatcherDocUrl);
            _jp = _kit.Jp;
            _useCustomUI = _kit.UseCustomUI;

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
                            var newStencilOpen = Foldout("stencil", false, "Stencil");

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

                        var detailNormalTex = Prop("_DetailNormalMap");
                        if (detailNormalTex != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Detail Normal Map",
                                    "Tiling micro-surface normal (skin pores, fabric weave). Shares the Detail Map tiling. \"bump\" (default) = no effect. Generic/CC0 tiling normals work without per-model authoring",
                                    "タイリングの微細ノーマル（肌のキメ・布の織り）。Detail Map のタイリングを共有。\"bump\"（既定）で無効。汎用/CC0 のタイリング素材でOK（モデル別オーサリング不要）"),
                                detailNormalTex, Prop("_DetailNormalScale"));
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

                        EditorGUILayout.Space(2);
                        SubHeader("Face SDF Shadow", "顔 SDF シャドウ");
                        var useSdfProp = Prop("_UseFaceSDF");
                        P(materialEditor, useSdfProp, "Enable Face SDF Shadow",
                            "Drives the main-light face shadow from a baked 2-channel SDF map (R=right-lit, G=left-lit) so it sweeps smoothly with the light (no shadow-map jaggies). Bake the map in the Baking section below. Asymmetric faces are supported (no UV mirroring)",
                            "メインライトの顔影をベイクした2chSDFマップ(R=右光/G=左光)で駆動し、光に合わせて滑らかに動かす（シャドウマップのガタつき無し）。マップは下のBakingセクションで焼く。左右非対称の顔もOK（ミラー不使用）");
                        if (useSdfProp != null && useSdfProp.floatValue > 0.5f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                var sdfMap = Prop("_FaceSDFMap");
                                if (sdfMap != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Face SDF Map",
                                            "Baked face SDF (auto-assigned when you bake). White = always lit",
                                            "ベイクした顔SDF（焼くと自動アサイン）。白=常に光"), sdfMap);
                                P(materialEditor, "_FaceSDFFlip", "Flip Forward",
                                    "Enable if the shadow moves the wrong way (face faces -Z)",
                                    "陰が逆方向に動くとき ON（顔が -Z 向き）");
                                P(materialEditor, "_FaceSDFSoftness", "Softness",
                                    "Width of the shadow transition. Low = crisp anime edge (always at least 1px anti-aliased)",
                                    "陰の境界のぼかし幅。低いほどパキッとしたアニメ調（最低1pxのAAは常に確保）");
                                P(materialEditor, "_FaceSDFShadowMix", "External Shadow Mix",
                                    "SDF replaces the self-shadow map on the face (no acne, no Vogel needed). Raise to mix back EXTERNAL cast shadows (hair on face), at the cost of some shadow-map artifacts. 0 = pure SDF",
                                    "SDF が顔の自己影マップを置き換える（アクネ無し・Vogel不要）。上げると髪などの外部落ち影を混ぜ戻せるが、シャドウマップのアーティファクトも戻る。0で完全SDF");
                                P(materialEditor, "_FaceSDFFrontBlend", "Front Blend",
                                    "Smoothly blends the left/right SDF as the light crosses front, removing the hard left/right pop. Larger = wider, softer crossover",
                                    "光が正面を横切るとき左右SDFを滑らかに補間し、左右の『パキッ』とした切り替わりを消す。大きいほど広く柔らかいクロスフェード");
                                P(materialEditor, "_FaceSDFFrontFade", "Front Fade",
                                    "Fades the SDF shadow toward lit as the light approaches front, hiding the left/right hand-off entirely (front light naturally has little face shadow). Larger = fades over a wider front range. 0 = off",
                                    "光が正面に近いほどSDF影を『光』へ弱め、左右の受け渡しを完全に隠す（正面光は元々顔影が薄い）。大きいほど広い正面範囲でフェード。0で無効");
                            }

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
                                    DollMaterialSetup.SyncShadowMode(mat, smNew);
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.FlexibleSpace();
                                DocLink(_jp ? "影モードの選び方" : "Shadow mode guide", ShadowsDocUrl);
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

                        // ソフトネスは Vogel のみで有効（Tent=固定カーネル / PCSS=接地硬化で自動）
                        if (isVogel)
                            P(materialEditor, "_ShadowMapSoftness", "Shadow Softness",
                                "Penumbra width. PCF (Vogel) only (Tent uses a fixed kernel, PCSS auto-derives it)",
                                "ペナンブラ幅。PCF (Vogel) のみ有効（Tentは固定カーネル、PCSSは接地硬化で自動決定）");

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
                            var newShadowFixOpen = Foldout("auto_shadow_fix", false,
                                _jp ? "顔の影補正" : "Auto Face Shadow Fix");

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
                        P(materialEditor, "_SpecularAA", "Specular Anti-Aliasing",
                            "Geometric specular AA. Suppresses highlight shimmer/jaggies under motion on large LED screens by widening roughness where normals vary fast. 0 = off, 1 = full (recommended on)",
                            "幾何スペキュラAA。法線が急変する箇所でラフネスを広げ、大型LED・激しいモーション時のハイライトのチラつき(ジャギ)を抑える。0でOFF、1で最大（基本ONを推奨）");
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

                        SubHeader("Environment Reflection", "環境反射（Reflection Probe）");
                        P(materialEditor, "_ReflectionStrength", "Strength (0 = Off)",
                            "Reflects the scene Reflection Probe onto the surface (wet eyes, enamel, glossy accessories that react to stage lighting). Uses Primary Smoothness for blur and Fresnel (F0) for edge weighting. Modulated by the Occlusion map and Specular Mask. 0 = off",
                            "シーンの Reflection Probe を表面に反射させる（濡れた瞳・エナメル・小物がステージ照明に反応）。ぼけは Primary Smoothness、縁の強さは Fresnel(F0) を流用。Occlusion マップと Specular Mask で減衰。0でOFF");

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
                                P(materialEditor, "_MatCapLightInfluence", "Light Influence",
                                    "Rotates the MatCap lookup to follow the main light's on-screen direction, so the baked reflection reacts to stage lighting. 0 = classic view-locked",
                                    "メインライトの画面内方向にMatCapのサンプリングを回転させ、焼かれた映り込みをステージ照明に反応させる。0で従来のビュー固定");
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

                        SubHeader("Occlusion (AO Map)", "オクルージョン（AOマップ）");
                        var occMap = Prop("_OcclusionMap");
                        if (occMap != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Occlusion Map (R)",
                                    "Baked ambient occlusion. R channel darkens diffuse in creases. White (default) = no effect",
                                    "ベイクした AO。Rチャンネルでくぼみの拡散光を沈める。白（既定）で無効"),
                                occMap);
                        P(materialEditor, "_OcclusionStrength", "Strength",
                            "How strongly the occlusion map darkens diffuse",
                            "AOマップで拡散光を沈める強さ");

                        SubHeader("Cavity (Crease Map)", "キャビティ（くぼみマップ）");
                        var cavMap = Prop("_CavityMap");
                        if (cavMap != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Cavity Map (R)",
                                    "Fine crease darkening (pores / seams), separate from broad AO. Bake it in the Baking section. White (default) = no effect",
                                    "細かいくぼみ（しわ・継ぎ目）の暗化。広域AOとは別。Bakingセクションで焼く。白（既定）で無効"),
                                cavMap);
                        P(materialEditor, "_CavityStrength", "Strength",
                            "How strongly the cavity map darkens diffuse",
                            "キャビティマップで拡散光を沈める強さ");
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

            // -----------------------------------------------------------
            // 11. Baking（マップ生成ツール）— 実装は DollBakingPanel
            // -----------------------------------------------------------
            _baking.Draw(materialEditor, _kit);
        }

        // ================================================================
        //  セットアップ系 UI（状態変更ロジックは DollMaterialSetup へ委譲）
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
                    DollMaterialSetup.ApplyRenderMode(mat, newMode);
            }
        }

        private void DrawOutlineSetup(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var outlineProp = Prop("_UseOutline");
            if (outlineProp == null) return;

            Pv(materialEditor, outlineProp, "Enable Outline",
                "Inverted-hull outline pass", "背面法線を押し出す輪郭線パス");
            var isOutlineOn = outlineProp.floatValue > 0.5f;

            if (isOutlineOn)
                using (new EditorGUI.IndentLevelScope())
                {
                    // アウトラインは独自パス（LightMode=DollOutline）。RendererFeature が必要。
                    EditorGUILayout.HelpBox(
                        _jp
                            ? "アウトラインの表示には Doll Outline Feature を Renderer に追加する必要があります（ForwardLit のバッチング維持のため独自パス化）。"
                            : "Outline requires the Doll Outline Feature on your Renderer (separated pass keeps ForwardLit batching).",
                        MessageType.Info);
                    if (GUILayout.Button(_jp ? "Outline セットアップを開く" : "Open Outline Setup"))
                        DollOutlineSetupWindow.Open();
                    EditorGUILayout.Space(2);

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

            // キーワードは毎フレーム冪等同期（EnableKeyword は冪等）。
            foreach (Material mat in materialEditor.targets)
                if (isOutlineOn) mat.EnableKeyword("_OUTLINE_ON");
                else mat.DisableKeyword("_OUTLINE_ON");
        }

        // マテリアル読み込み/検証時に float からキーワードを復元（stale / リネーム耐性）。
        public override void ValidateMaterial(Material material)
        {
            base.ValidateMaterial(material);
            if (material.HasProperty("_ShadowMode"))
                DollMaterialSetup.SyncShadowMode(material, (int)material.GetFloat("_ShadowMode"));
            DollMaterialSetup.CleanupDeprecated(material);
        }

        // ================================================================
        //  描画プリミティブの委譲（実装と状態は ShaderGuiKit が所有）
        // ================================================================
        private bool Section(string id, bool defaultOpen, string titleEn, string titleJp, string descEn, string descJp)
            => _kit.Section(id, defaultOpen, titleEn, titleJp, descEn, descJp);
        private bool Foldout(string id, bool defaultOpen, string label) => _kit.Foldout(id, defaultOpen, label);
        private void SubHeader(string en, string jp) => _kit.SubHeader(en, jp);
        private GUIContent Label(string label, string tipEn, string tipJp) => _kit.Label(label, tipEn, tipJp);
        private MaterialProperty Prop(string name) => _kit.Prop(name);
        private void P(MaterialEditor e, MaterialProperty prop, string label, string tipEn, string tipJp)
            => _kit.P(e, prop, label, tipEn, tipJp);
        private void P(MaterialEditor e, string name, string label, string tipEn, string tipJp)
            => _kit.P(e, name, label, tipEn, tipJp);
        private void Pv(MaterialEditor e, MaterialProperty prop, string label, string tipEn, string tipJp)
            => _kit.Pv(e, prop, label, tipEn, tipJp);
        private void Pv(MaterialEditor e, string name, string label, string tipEn, string tipJp)
            => _kit.Pv(e, name, label, tipEn, tipJp);
        private GUIContent VariantLabel(string label, string tipEn, string tipJp)
            => _kit.VariantLabel(label, tipEn, tipJp);
        private void DocLink(string label, string url) => _kit.DocLink(label, url);
    }
}
