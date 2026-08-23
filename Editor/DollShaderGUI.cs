using System;
using UnityEditor;
using UnityEngine;
using Origuma.EasyShaderCore.Editor;

namespace Origuma.EasyPBR.URP.Editor
{
    // =========================================================================
    //  Doll のカスタムインスペクター。
    //  最上部の固定タブバーでページ分割し（縦長対策・機能の所在の明確化）、
    //  検索ボックス入力中はタブを無視して一致プロパティをフラット表示する。
    //  描画プリミティブは ShaderGuiKit、状態変更は DollMaterialSetup、
    //  ベイク UI は DollBakingPanel に委譲。
    // =========================================================================
    public class DollShaderGUI : ShaderGUI
    {
        private const string KeyPrefix = "Origuma.EasyPBR.URP.Doll.";
        private const string TabKey = KeyPrefix + "tab";

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

        // タブと検索（検索文字列はセッション内のみ保持）。
        private int _tab = -1;
        private string _search = "";

        // 8 タブ（4 列 × 2 段）。詳細（Advanced）は T-354 で Baking から独立させた
        // ── ベイクと高度な設定は別物という利用者の指摘（EasyToon Idol と同時変更・
        // タブ構成同一の原則）。
        private static readonly string[] s_TabsEn = { "Base", "Shading", "Lighting", "Specular", "Effects", "FX", "Advanced", "Baking" };
        private static readonly string[] s_TabsJp = { "基本", "陰・影", "ライト", "スペキュラ", "質感", "演出", "詳細", "Baking" };

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

            // --- 検索（入力中はタブを無視して一致プロパティをフラット表示）---
            EditorGUILayout.Space(2);
            _search = EditorGUILayout.TextField(GUIContent.none, _search, EditorStyles.toolbarSearchField);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                DrawSearchResults(materialEditor, properties);
                return;
            }

            // --- タブバー（4 列グリッド・選択を永続化）---
            if (_tab < 0) _tab = EditorPrefs.GetInt(TabKey, 0);
            EditorGUI.BeginChangeCheck();
            _tab = GUILayout.SelectionGrid(Mathf.Clamp(_tab, 0, s_TabsEn.Length - 1),
                _jp ? s_TabsJp : s_TabsEn, 4, EditorStyles.miniButtonMid);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetInt(TabKey, _tab);
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case 0: DrawTabBase(materialEditor, properties); break;
                case 1: DrawTabShading(materialEditor); break;
                case 2: DrawTabLighting(materialEditor); break;
                case 3: DrawTabSpecular(materialEditor); break;
                case 4: DrawTabEffects(materialEditor); break;
                case 5: DrawTabFx(materialEditor, properties); break;
                case 6: DrawTabAdvanced(materialEditor); break;
                case 7: DrawTabBaking(materialEditor); break;
            }
        }

        // ================================================================
        //  検索: 表示名 / プロパティ名の部分一致（大文字小文字無視）
        // ================================================================
        private void DrawSearchResults(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var query = _search.Trim();
            var hits = 0;

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var prop in properties)
                {
                    // MaterialProperty.propertyFlags は新しい 6000.x で追加された API。
                    // それ以前のエディタでは従来の flags で判定する。
#if UNITY_6000_3_OR_NEWER
                    if ((prop.propertyFlags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0)
                        continue;
#else
                    if ((prop.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
                        continue;
#endif
                    if (prop.displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                        prop.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    materialEditor.ShaderProperty(prop, prop.displayName);
                    hits++;
                }

                if (hits == 0)
                    EditorGUILayout.LabelField(
                        _jp ? $"\"{query}\" に一致するプロパティはありません（英語の表示名 / プロパティ名で検索）。"
                            : $"No properties match \"{query}\" (searches English display names / property names).",
                        EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField(
                _jp ? $"{hits} 件ヒット。検索を消すとタブ表示に戻ります。"
                    : $"{hits} match(es). Clear the search to return to tabs.",
                EditorStyles.miniLabel);
        }

        // ================================================================
        //  Tab 0: 基本（Surface Options / Base Core / Emission）
        // ================================================================
        private void DrawTabBase(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.surface", true, "Surface Options", "サーフェス設定", "", ""))
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
                        if (Foldout("stencil", false, "Stencil"))
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

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.base", true, "Base Core", "基本設定", "", ""))
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

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.emission", true, "Emission", "発光", "", ""))
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

            // 輪郭線は「キャラの基本の見た目」（マテリアルごとの恒久設定）で
            // あって演出ではないので基本タブに置く（T-353。Idol も同じ棚）。
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.outline", true, "Outline", "アウトライン（輪郭線）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawOutlineSetup(materialEditor, properties);
                    }
            }
        }

        // ================================================================
        //  Tab 1: 陰・影（階調 / シェーディング法線 / 顔 SDF / セルフシャドウ）
        // ================================================================
        private void DrawTabShading(MaterialEditor materialEditor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.shade", true, "Shade (Colors and Ramp)", "陰（色と階調）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var shadingStyleProp = Prop("_ShadingStyle");
                        P(materialEditor, shadingStyleProp, "Shading Style",
                            "Smooth: continuous shading / Toon: hard two-tone anime look",
                            "Smooth: なめらかな階調 / Toon: 境界で2値化したアニメ調");
                        P(materialEditor, "_HalfLambertWrap", "Light Wrap",
                            "Lifts the shaded side to soften shading (Half-Lambert wrap). 0 = Lambert, 1 = brighter overall",
                            "陰側を持ち上げて陰影を柔らかくする（Half-Lambert の wrap 量）。0でランバート、1で全体的に明るい");

                        if (shadingStyleProp != null && shadingStyleProp.floatValue >= 0.5f)
                        {
                            P(materialEditor, "_ToonStep", "Toon Threshold",
                                "Lit/shadow boundary position (threshold where lit flips to shadow)",
                                "トゥーン陰の境界位置（明→暗が切り替わるしきい値）");
                            P(materialEditor, "_ToonFeather", "Toon Softness",
                                "Blur width of that boundary. 0 = crisp",
                                "境界のぼかし幅。0でくっきり、上げるほど柔らかい");
                        }

                        EditorGUILayout.Space(2);
                        P(materialEditor, "_ShadowColor", "Shadow Color",
                            "Tint multiplied into the base color in shadowed areas",
                            "影部分でベースカラーに乗算する色味");
                        P(materialEditor, "_ShadowHueShift", "Shadow Hue Shift",
                            "Rotates the hue of the shaded side (e.g. skin shadows toward red-purple). Keeps shadows rich instead of just dark. 0 = off (skips the HSV conversion)",
                            "陰側の色相を回す（肌の陰を赤紫側へ等）。ただ暗いだけの影を色が転がるリッチな影にする。0でOFF（HSV変換をスキップ）");
                        P(materialEditor, "_ShadowSaturation", "Shadow Saturation",
                            "Saturation multiplier for the shaded side. Slightly above 1 keeps color alive inside shadows (anime look). 1 = off",
                            "陰側の彩度倍率。1より少し上げると影の中でも色が沈まない（アニメ調）。1でOFF");

                        var shadow2Prop = Prop("_Shadow2Color");
                        P(materialEditor, shadow2Prop, "2nd Shadow Color (A = Enable)",
                            "Adds a second, deeper shadow band below the 1st (classic two-band anime shading). Light-angle based; cast shadows stay at the 1st shadow tone. Alpha 0 = off",
                            "1影より深い位置に2段目の陰を重ねる（アニメの1影・2影構成）。光の角度ベースで、落ち影は1影のまま。アルファ0でOFF");
                        if (shadow2Prop != null && shadow2Prop.colorValue.a > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_Shadow2Step", "2nd Shadow Threshold",
                                    "Position of the 2nd boundary. Keep it below the 1st (Toon Threshold) so the bands stack: lit → 1st → 2nd",
                                    "2影の境界位置。1影（Toon Threshold）より低くすると 明→1影→2影 の順に重なる");
                                P(materialEditor, "_Shadow2Feather", "2nd Shadow Softness",
                                    "Blur width of the 2nd boundary. Raise for a soft gradation (works in Smooth style too)",
                                    "2影境界のぼかし幅。上げると柔らかいグラデーションになる（Smooth スタイルでも有効）");
                            }

                        P(materialEditor, "_CastShadowColor", "Cast Shadow Color (A = Enable)",
                            "Paints cast shadows (shadow map) in their own tint, separate from the angle-based shade — e.g. push cast shadows cooler for a filmic look. Alpha 0 = same color as the shade (default)",
                            "落ち影（shadow map）を角度の陰とは別の色で塗る——落ち影だけ寒色に振ると映像的な画になる。アルファ0で陰と同色（既定・従来どおり）");

                        EditorGUILayout.Space(2);
                        SubHeader("Shade Normal", "シェーディング法線");
                        var shadeNormalTex = Prop("_ShadeNormalMap");
                        if (shadeNormalTex != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Shade Normal Map",
                                    "Baked smoothed normal that drives ONLY the diffuse shade ramp. Keeps wrinkles/facets from breaking the gradation into messy patches (specular, rim and SSS keep the detail normal). Bake in the Baking tab. bump (default) = off",
                                    "拡散の陰ランプだけを駆動するベイク済み平滑化法線。シワやファセットの起伏でグラデーションが汚く割れるのを防ぐ（スペキュラ・リム・SSSはディテール法線のまま）。Bakingタブで焼く。bump（既定）で無効"),
                                shadeNormalTex);
                        P(materialEditor, "_ShadeNormalStrength", "Strength",
                            "0 = off (detail normal). Baking auto-enables to 1. Blends toward the smoothed normal",
                            "0=無効（ディテール法線）。ベイクで自動的に1に。平滑化法線側へのブレンド量");
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.selfshadow", true, "Self Shadow (Shadow Map)", "セルフシャドウ（落ち影）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
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
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.face", true, "Face (SDF / Shadow Fix)", "顔（SDF / 陰補正）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Face SDF Shadow", "顔 SDF シャドウ");
                        var useSdfProp = Prop("_UseFaceSDF");
                        P(materialEditor, useSdfProp, "Enable Face SDF Shadow",
                            "Drives the main-light face shadow from a baked 2-channel SDF map (R=right-lit, G=left-lit) so it sweeps smoothly with the light (no shadow-map jaggies). Bake the map in the Baking tab. Asymmetric faces are supported (no UV mirroring)",
                            "メインライトの顔影をベイクした2chSDFマップ(R=右光/G=左光)で駆動し、光に合わせて滑らかに動かす（シャドウマップのガタつき無し）。マップはBakingタブで焼く。左右非対称の顔もOK（ミラー不使用）");
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
                                P(materialEditor, "_FaceSDFBlendNormalMin", "SDF Blend Normal Min",
                                    "Local Y normal threshold where Face SDF shadow influence reaches zero (fully disabled). Useful for fading out SDFs on downward-facing areas like the neck or under-chin.",
                                    "顔のSDFシャドウの影響が完全にゼロ（無効化）になるローカルY法線のしきい値。主に顎下や首など、下向きの面でSDFをフェードアウトさせるのに使用します。");
                                P(materialEditor, "_FaceSDFBlendNormalMax", "SDF Blend Normal Max",
                                    "Local Y normal threshold where Face SDF shadow influence is fully applied (100% enabled). Normals falling between Min and Max will smoothly fade the SDF effect.",
                                    "顔のSDFシャドウの影響が100%適用（有効化）されるローカルY法線のしきい値。MinとMaxの間の法線を持つ面では、SDFの効果が滑らかにフェードします。");
                            }

                        EditorGUILayout.Space(2);
                        SubHeader("Auto Face Shadow Fix", "顔の影補正（マスク不要）");
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
        }

        // ================================================================
        //  Tab 2: ライト（ライト整形 / フィル / 間接光 / 白飛び防止）
        // ================================================================
        private void DrawTabLighting(MaterialEditor materialEditor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.conditioning", true, "Light Conditioning", "キャラ用ライト整形", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, "_LightColorInfluence", "Light Color Influence",
                            "How much the main light's color tints the character. Lowering it treats the light as white of the same brightness, so the character's color design survives saturated stage lighting. 1 = physical (default)",
                            "メインライトの色がキャラに乗る度合い。下げると同輝度の白色光として扱われ、原色のステージ照明でもキャラの色設計が保たれる。1で物理どおり（既定）");
                        P(materialEditor, "_LightSaturationLimit", "Light Saturation Limit",
                            "Caps the main light's saturation (hue is kept). Prevents skin/hair hues from collapsing under deep red/blue lighting. 1 = no limit (default)",
                            "メインライトの彩度上限（色相は保持）。深い赤・青の照明でも肌や髪の色相が破綻しない。1で制限なし（既定）");
                        P(materialEditor, "_LightMinBrightness", "Light Min Brightness",
                            "Guarantees a minimum light brightness so the character never goes fully black in dark scenes (use Black Out for intentional blackouts). Main light only. 0 = off (default)",
                            "ライト輝度の下限。暗いシーンでもキャラが完全黒に沈まない（意図的な暗転は Black Out を使用）。メインライトのみ。0でOFF（既定）");
                        P(materialEditor, "_ConditionAdditionalLights", "Condition Additional Lights",
                            "Applies Color Influence and Saturation Limit to additional lights too (colored stage spots hitting the skin). Min Brightness stays main-light-only so it doesn't stack per light. Off = additional lights pass through (default)",
                            "追加ライトにも Color Influence と Saturation Limit を適用（原色スポットの肌直撃対策）。Min Brightness は灯数ぶん持ち上がらないようメインライト限定のまま。OFFで追加ライトは素通し（既定）");
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.fill", true, "Fill Light (Bounce)", "フィルライト（照り返し）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, "_FillColor", "Color (HDR)",
                            "Bounce light tint (e.g. warm from the floor, cool from the sky)",
                            "照り返しの色（床からの暖色、空からの寒色など）");
                        var fillIntProp = Prop("_FillIntensity");
                        P(materialEditor, fillIntProp, "Intensity (0 = Off)",
                            "Directional bounce light poured into the shaded side (floor bounce is the classic use). Independent of the main light's brightness. 0 = off",
                            "陰側に注ぐ方向性のあるバウンス光（床の照り返しが典型）。メインライトの明るさから独立。0でOFF");
                        if (fillIntProp != null && fillIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_FillPitch", "Pitch",
                                    "Vertical direction of the bounce source. -90 = straight below (floor), +90 = straight above (sky)",
                                    "照り返し光源の上下方向。-90=真下（床）、+90=真上（空）");
                                P(materialEditor, "_FillYaw", "Yaw",
                                    "Horizontal direction of the bounce source (world space)",
                                    "照り返し光源の水平方向（ワールド空間）");
                                P(materialEditor, "_FillShadeOnly", "Shade Side Only",
                                    "1 = only inside the main light's shade (classic bounce look) / 0 = whole surface (acts like a second fill light)",
                                    "1=主光の陰側だけに乗せる（照り返しらしい見た目）/ 0=全面（第2のフィルライトとして機能）");
                            }
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.indirect", true, "Indirect Light (Ambient)", "間接光（アンビエント）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, "_IndirectFlatten", "Flatten",
                            "Flattens the directional component of ambient/light-probe SH so the whole character sits in a uniform ambient. Prevents venue GI from painting uneven patches on faces. Trades off against Bent Normal's directional ambient. 0 = physical (default)",
                            "環境光（ライトプローブ/SH）の方向成分を潰し、キャラ全体を均一なアンビエントで包む。会場GIの方向ムラが顔に出るのを防ぐ。Bent Normal の方向補正とはトレードオフ。0で物理どおり（既定）");
                        P(materialEditor, "_IndirectIntensity", "Intensity",
                            "Ambient contribution multiplier. 1 = as-is (default)",
                            "間接光の寄与倍率。1でそのまま（既定）");
                        P(materialEditor, "_IndirectTint", "Tint",
                            "Ambient tint color. White = no change (default)",
                            "間接光の色補正。白で無変化（既定）");
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.antiblowout", true, "Anti-Blowout", "白飛び防止", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, "_DiffuseLightLimit", "Diffuse Light Limit",
                            "Luminance cap of diffuse light per light (blowout prevention)",
                            "1灯あたりの拡散光の輝度上限（白飛び防止）");
                        P(materialEditor, "_AdditionalLightBlendMode", "Additional Light Blend",
                            "Add: physical (can blow out) / Max: anime-friendly (keeps saturation)",
                            "Add: 物理的（白飛びしやすい）/ Max: アニメ向け（彩度を保つ）");
                    }
            }
        }

        // ================================================================
        //  Tab 3: スペキュラ（デュアルローブ / スタイライズ / 環境反射 / 異方性 / MatCap）
        // ================================================================
        private void DrawTabSpecular(MaterialEditor materialEditor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.specular", true, "Specular (Dual-Lobe)", "スペキュラ（デュアルローブ）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
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

                        SubHeader("Primary (Sharp)", "Primary（シャープ）");
                        P(materialEditor, "_SpecularColor", "Color (HDR)",
                            "Primary specular tint. HDR values push the highlight past 1 to trigger Bloom (strong stylized reflections)",
                            "主スペキュラの色味。HDRで1を超えさせるとBloomを誘発（様式的な強い反射）");
                        P(materialEditor, "_Smoothness", "Smoothness",
                            "Higher = tighter, sharper highlight", "高いほど締まった鋭いハイライト");
                        P(materialEditor, "_SpecularIntensity", "Intensity",
                            "Primary specular strength. 0 = off", "主スペキュラの強度。0でOFF");
                        P(materialEditor, "_PriSpecularLightLimit", "Light Limit",
                            "Luminance cap for the primary lobe (blowout prevention)",
                            "主ローブの輝度上限（白飛び防止）");

                        SubHeader("Secondary (Matte)", "Secondary（マット）");
                        P(materialEditor, "_SecSpecularColor", "Color (HDR)",
                            "Secondary specular tint (HDR)", "副スペキュラの色味（HDR対応）");
                        P(materialEditor, "_SecSmoothness", "Smoothness",
                            "Higher = tighter (broad matte sheen when low)", "高いほど締まる（低いと広いマット質感）");
                        P(materialEditor, "_SecSpecularIntensity", "Intensity",
                            "Secondary specular strength. 0 = off", "副スペキュラの強度。0でOFF");
                        P(materialEditor, "_SecSpecularLightLimit", "Light Limit",
                            "Luminance cap for the secondary lobe", "副ローブの輝度上限");

                        SubHeader("Stylize (Toon / Shade)", "スタイライズ（トゥーン化 / 陰連動）");
                        var toonSpecProp = Prop("_ToonSpecular");
                        P(materialEditor, toonSpecProp, "Toon Specular (0 = Off)",
                            "Cuts the specular edge at a threshold for a crisp stylized highlight (interior gradient is kept). Blends continuous ⇄ toon from 0 to 1. Applies to both lobes",
                            "スペキュラの縁をしきい値で切り、パキッとした様式的ハイライトにする（内側のグラデーションは保持）。0〜1で連続⇄トゥーンをブレンド。両ローブに適用");
                        if (toonSpecProp != null && toonSpecProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_ToonSpecularStep", "Threshold",
                                    "Brightness where the highlight edge is cut (tone-mapped luminance). Lower keeps the broad secondary lobe alive",
                                    "縁を切る輝度（トーンマップ後）。低くすると広い Secondary ローブも残る");
                                P(materialEditor, "_ToonSpecularFeather", "Softness",
                                    "Edge blur of the cut (always at least 1px anti-aliased)",
                                    "切り口のぼかし幅（最低1pxのAAは常に確保）");
                            }
                        P(materialEditor, "_SpecularShadeInfluence", "Shade Dimming",
                            "Dims specular on faces inside the shade ramp (1st/2nd shadow). Cast shadows already dim it; this adds the angle-based shade. 1 = no highlight inside shade (anime-strict)",
                            "陰ランプ（1影・2影）に入った面のスペキュラを沈める。落ち影では従来から消えるが、角度ベースの陰でも消したいときに。1で陰の中は完全消灯（アニメ的に厳密）");

                        SubHeader("Environment Reflection", "環境反射（Reflection Probe）");
                        P(materialEditor, "_ReflectionStrength", "Strength (0 = Off)",
                            "Reflects the scene Reflection Probe onto the surface (wet eyes, enamel, glossy accessories that react to stage lighting). Uses Primary Smoothness for blur and Fresnel (F0) for edge weighting. Modulated by the Occlusion map and Specular Mask. Above 1 over-boosts stylistically (strong stylized reflections). 0 = off",
                            "シーンの Reflection Probe を表面に反射させる（濡れた瞳・エナメル・小物がステージ照明に反応）。ぼけは Primary Smoothness、縁の強さは Fresnel(F0) を流用。Occlusion マップと Specular Mask で減衰。1超は様式的なブースト（強い反射表現）。0でOFF");
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.aniso", true, "Anisotropic (Hair / Silk)", "異方性ハイライト（髪 / シルク）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
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

                                var hairFlowMap = Prop("_HairFlowMap");
                                if (hairFlowMap != null)
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Hair Flow Map (RGB)",
                                            "R/G=double-angle flow, B=confidence. Bake in the Baking tab. 0 strength = off",
                                            "R/G=倍角毛流れ、B=信頼度。Bakingタブで焼く。Strength 0=無効"),
                                        hairFlowMap);
                                P(materialEditor, "_HairFlowStrength", "Flow Strength",
                                    "0 = off (UV tangent only). Baking auto-enables to 1",
                                    "0=無効（UV接線のみ）。ベイクで自動的に1に");

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
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.matcap", true, "MatCap", "MatCap", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
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
                                P(materialEditor, "_MatCapColor", "Tint (HDR)",
                                    "MatCap tint color (HDR)", "MatCapの色味（HDR対応）");
                                P(materialEditor, "_MatCapIntensity", "Intensity",
                                    "MatCap strength", "MatCapの強度");
                                P(materialEditor, "_MatCapLightInfluence", "Light Influence",
                                    "Rotates the MatCap lookup to follow the main light's on-screen direction, so the baked reflection reacts to stage lighting. 0 = classic view-locked",
                                    "メインライトの画面内方向にMatCapのサンプリングを回転させ、焼かれた映り込みをステージ照明に反応させる。0で従来のビュー固定");
                            }
                    }
            }
        }

        // ================================================================
        //  Tab 4: 質感（コート / グリッター / 散乱 / SSS / リム / マップ類）
        // ================================================================
        private void DrawTabEffects(MaterialEditor materialEditor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.coat_glitter", true, "Coat and Glitter", "コートとグリッター", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Clearcoat + Iridescence", "クリアコート＋イリデッセンス");
                        var clearcoatMask = Prop("_ClearcoatMask");
                        if (clearcoatMask != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Clearcoat Mask (R)",
                                    "Where to place gloss (additive only). Cavity/curvature maps work as masks without darkening",
                                    "艶の置き場（加算のみ）。キャビティ/曲率マップを流用可（陰影は増えない）"),
                                clearcoatMask);
                        var coatStrProp = Prop("_ClearcoatStrength");
                        P(materialEditor, coatStrProp, "Strength (0 = Off)",
                            "Additive clearcoat layer. Does not darken base shading",
                            "加算クリアコート。下地の陰影には干渉しない");
                        if (coatStrProp != null && coatStrProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_ClearcoatSmoothness", "Smoothness",
                                    "Higher = sharper, tighter gloss",
                                    "高いほどシャープなテカリ");
                                P(materialEditor, "_ClearcoatReflStrength", "Env Refl Strength",
                                    "Environment reflection on the coat layer (view-dependent, AR-friendly)",
                                    "コート層の環境反射（視点依存・AR映え）");
                                P(materialEditor, "_IridescenceIntensity", "Iridescence",
                                    "0 = colorless coat. Higher = thin-film rainbow",
                                    "0=無色。上げると薄膜の虹色");
                                var iridProp = Prop("_IridescenceIntensity");
                                if (iridProp != null && iridProp.floatValue > 0f)
                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        P(materialEditor, "_IridescenceThickness", "Iridescence Thickness",
                                            "Color cycle frequency (higher = finer bands)",
                                            "色相の周期（高いほど細かく回る）");
                                        P(materialEditor, "_IridescenceShift", "Iridescence Shift",
                                            "Hue offset of the iridescence",
                                            "虹色の色相起点");
                                    }
                            }

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
                    }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.skin_edge", true, "Skin and Edge", "肌と縁の質感", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Skin Scatter (Terminator)", "スキンスキャッタ（明暗境界のにじみ）");
                        P(materialEditor, "_SkinScatterColor", "Color",
                            "Tint that bleeds into the lit/shadow boundary (skin: warm red)",
                            "明暗境界に滲ませる色（肌なら暖色の赤系）");
                        var skinScatterIntProp = Prop("_SkinScatterIntensity");
                        P(materialEditor, skinScatterIntProp, "Intensity (0 = Off)",
                            "Pre-integrated-style skin scattering: tints the terminator (lit/shadow boundary) so skin looks translucent instead of flatly shaded. Works on toon ramps, shadow penumbra and Face SDF boundaries alike. 0 = off",
                            "Pre-integrated 風の肌散乱。明暗境界（ターミネータ）に色を滲ませ、のっぺりした陰影を血色のある肌にする。トゥーン境界・落ち影ペナンブラ・顔SDF境界のいずれにも乗る。0でOFF");
                        if (skinScatterIntProp != null && skinScatterIntProp.floatValue > 0f)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, "_SkinScatterWidth", "Width",
                                    "Width of the tinted band along the boundary",
                                    "境界に沿った滲みバンドの広さ");
                                P(materialEditor, "_SkinScatterCurvatureMask", "Curvature Mask",
                                    "Uses the baked Curvature Map so thin/high-curvature areas (ears, nose, fingers) scatter more. 0 = uniform. Requires Curvature Map + Strength > 0",
                                    "ベイク済み曲率マップで薄い・曲率の高い部位（耳・鼻・指）ほど強く散乱させる。0で均一。Curvature Map と Strength > 0 が必要");
                            }

                        SubHeader("SSS (Subsurface)", "SSS（表面下散乱）");
                        var sssMap = Prop("_SSSMap");
                        if (sssMap != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("SSS Map (RGBA)",
                                    "RGB=transmission direction (tangent space), A=thickness. Bake in the Baking tab",
                                    "RGB=透過方向（接線空間）、A=厚み。Bakingタブで焼く"),
                                sssMap);
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
                        P(materialEditor, "_FuzzColor", "Color (HDR)",
                            "Soft edge sheen tint (HDR)", "縁の柔らかい光沢の色（HDR対応）");
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
                        P(materialEditor, "_RimColor", "Color (HDR)",
                            "Rim light color. HDR values can trigger Bloom",
                            "リムライトの色。HDRでBloomを誘発できる");
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

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.bakedmaps", true, "Baked Maps (AO / Bent / Cavity / Curvature)", "ベイクマップ（AO / Bent / Cavity / 曲率）", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
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

                        SubHeader("Bent Normal Map", "ベント法線マップ");
                        var bentMap = Prop("_BentNormalMap");
                        if (bentMap != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Bent Normal Map (RGB)",
                                    "Tangent-space open direction for ambient/SH. Bake in the Baking tab. Pairs with AO (direction vs strength). bump (default) = off",
                                    "接線空間の開いた方向。SH/アンビエントの評価方向に使う。Bakingタブで焼く。AO(強度)と併用。bump（既定）=無効"),
                                bentMap);
                        P(materialEditor, "_BentNormalStrength", "Strength",
                            "0 = off. Baking auto-enables to 1. Blends bent normal toward geometric normal",
                            "0=無効。ベイクで自動的に1に。幾何法線とのブレンド");

                        SubHeader("Cavity (Crease Map)", "キャビティ（くぼみマップ）");
                        var cavMap = Prop("_CavityMap");
                        if (cavMap != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Cavity Map (R)",
                                    "Fine crease darkening (pores / seams), separate from broad AO. Bake it in the Baking tab. White (default) = no effect",
                                    "細かいくぼみ（しわ・継ぎ目）の暗化。広域AOとは別。Bakingタブで焼く。白（既定）で無効"),
                                cavMap);
                        P(materialEditor, "_CavityStrength", "Strength",
                            "How strongly the cavity map darkens diffuse",
                            "キャビティマップで拡散光を沈める強さ");

                        SubHeader("Curvature Map", "曲率マップ");
                        var curvMap = Prop("_CurvatureMap");
                        if (curvMap != null)
                            materialEditor.TexturePropertySingleLine(
                                Label("Curvature Map (R)",
                                    "Signed curvature: 0.5=flat, bright=convex (ridge), dark=concave. Bake in the Baking tab",
                                    "符号付き曲率: 0.5=平坦、明=凸(稜線)、暗=凹(くぼみ)。Bakingタブで焼く"),
                                curvMap);
                        P(materialEditor, "_CurvatureStrength", "Strength",
                            "0 = off. Baking auto-enables to 1. Ridge specular boost and concave darkening",
                            "0=無効。ベイクで自動的に1に。稜線スペキュラ強調と凹部暗化の強さ");
                    }
            }
        }

        // ================================================================
        //  Tab 5: 演出（アウトライン / ディゾルブ / ブラックアウト）
        // ================================================================
        private void DrawTabFx(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // アウトラインは基本タブへ移動した（T-353）。演出＝時間で変化する
            // 効果（ディゾルブ / 暗転）だけが残る。
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.dissolve", true, "Dissolve / Black Out", "ディゾルブ / 暗転", "", ""))
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

                        EditorGUILayout.Space(2);
                        EditorGUILayout.HelpBox(
                            _jp ? "キャラ単位の一括制御: 暗転は BlackOutController、Dissolve は DissolveController（どちらも EasyShaderCore・Timeline 対応）。Fill Light は DollLiveDirector です。※DollLiveDirector の Black Out override と BlackOutController を同じキャラで併用しないこと（書き込み合戦になります）。"
                                : "Per-character control: Black Out via BlackOutController, Dissolve via DissolveController (both in EasyShaderCore, Timeline-friendly). Fill Light stays on DollLiveDirector. Do not run DollLiveDirector's Black Out override and BlackOutController on the same character - they fight over the same property.",
                            MessageType.None);
                    }
            }
        }

        // ================================================================
        //  Tab 6: 詳細（Advanced Options。T-354 で Baking から独立）
        // ================================================================
        private void DrawTabAdvanced(MaterialEditor materialEditor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.advanced", true, "Advanced Options", "高度な設定", "", ""))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        materialEditor.EnableInstancingField();
                        var anyInstancing = false;
                        foreach (Material mat in materialEditor.targets)
                            if (mat.enableInstancing) { anyInstancing = true; break; }
                        if (anyInstancing)
                            EditorGUILayout.HelpBox(
                                _jp ? "GPU Instancing は SkinnedMeshRenderer には効かず、ON のレンダラーは SRP Batcher の対象から外れます。キャラ用途では通常 OFF を推奨（→ SRP_BATCHER.md）。"
                                    : "GPU Instancing does not work with SkinnedMeshRenderer, and renderers using it are excluded from the SRP Batcher. Usually keep it OFF for characters (see SRP_BATCHER.md).",
                                MessageType.Warning);
                        materialEditor.DoubleSidedGIField();
                    }
            }
        }

        // ================================================================
        //  Tab 7: Baking（マップ生成）＋ Blue Noise（ベイク素材）
        // ================================================================
        private void DrawTabBaking(MaterialEditor materialEditor)
        {
            _baking.Draw(materialEditor, _kit);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("v2.blue_noise", true, "Blue Noise", "ブルーノイズ",
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
                    // 未追加検知は EasyShaderCore の FeatureSetup に委譲（追加済みなら Info、未追加なら Warning）。
                    FeatureSetup.DrawFeatureGuard<DollOutlineFeature>(
                        _jp ? "Doll Outline Feature は追加済みです。"
                            : "Doll Outline Feature is set up.",
                        _jp ? "Doll Outline Feature が Renderer に追加されていません。アウトラインの表示にはセットアップウィンドウから追加してください（ForwardLit のバッチング維持のため独自パス化）。"
                            : "Doll Outline Feature is NOT on the active Renderer. Add it via the setup window to draw outlines (separated pass keeps ForwardLit batching).",
                        _jp ? "Outline セットアップを開く" : "Open Outline Setup",
                        DollOutlineSetupWindow.Open);
                    EditorGUILayout.Space(2);

                    P(materialEditor, "_OutlineColor", "Color",
                        "Outline color. With Albedo Blend it acts as a multiplier over the surface albedo",
                        "輪郭線の色。Albedo Blend 使用時はアルベドへの乗算色として働く");
                    P(materialEditor, "_OutlineAlbedoBlend", "Albedo Blend",
                        "Blends the line color toward (albedo x Color): hair gets hair-toned lines, skin gets skin-toned lines — subtler than a fixed single color. Keep Color darkish since it multiplies. 0 = fixed color (default)",
                        "線の色を（アルベド×Color）側へブレンド。髪には髪系統、肌には肌系統の線が付き、固定単色より馴染む。乗算なので Color は暗めに。0で固定色（既定）");
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
