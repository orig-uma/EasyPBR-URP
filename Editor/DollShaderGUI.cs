using UnityEditor;
using UnityEngine;

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

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            LoadPrefs();
            DrawToolbar();

            if (!_useCustomUI)
            {
                base.OnGUI(materialEditor, properties);
                return;
            }

            // -----------------------------------------------------------
            // 1. Surface Options (レンダリングの基礎設定)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("surface", true, "Surface Options", "サーフェス設定", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawRenderModeSetup(materialEditor, properties);
                        EditorGUILayout.Space(4);

                        P(materialEditor, properties, "_Cull", "Cull", "Cull", "", "描画する面 (Off / Front / Back)");
                        P(materialEditor, properties, "_ZWrite", "ZWrite", "ZWrite", "", "深度バッファへの書き込み (On / Off)");
                        P(materialEditor, properties, "_ZTest", "ZTest", "ZTest", "", "深度テストの条件 (LEqual: 通常, Always: 常に前面に描画 など)");
                        P(materialEditor, properties, "_SrcBlend", "Source Blend", "Source Blend", "", "背景と合成する際の元カラーの係数");
                        P(materialEditor, properties, "_DstBlend", "Destination Blend", "Destination Blend", "", "背景と合成する際の背景カラーの係数");

                        EditorGUILayout.Space(4);
                        materialEditor.RenderQueueField();
                        
                        EditorGUILayout.Space(4);
                        P(materialEditor, properties, "_SurfaceTransparent", "Output Alpha (_SURFACE_TRANSPARENT)", "Output Alpha (_SURFACE_TRANSPARENT)", "", "アルファ値を出力するかどうか");
                        
                        var alphaClipProp = FindProperty("_AlphaClip", properties, false);
                        P(materialEditor, alphaClipProp, "Alpha Clipping (_ALPHATEST_ON)", "Alpha Clipping (_ALPHATEST_ON)", "", "アルファ値によるピクセルの破棄");

                        if (alphaClipProp != null && alphaClipProp.floatValue > 0.5f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_Cutoff", "Alpha Cutoff", "Alpha Cutoff", "", "クリッピングの閾値");
                            }
                        }
                    }
                }
            }

            // -----------------------------------------------------------
            // 2. Base Core (ベースとなる色とテクスチャ)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("base", true, "Base Core", "基本設定", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var mainTex = FindProperty("_MainTex", properties, false);
                        var baseColor = FindProperty("_BaseColor", properties, false);
                        if (mainTex != null)
                        {
                            materialEditor.TexturePropertySingleLine(
                                Label("Base Map (RGB / Alpha)", "ベースマップ (RGB/Alpha)", "", ""),
                                mainTex, baseColor);
                            materialEditor.TextureScaleOffsetProperty(mainTex);
                        }
                        
                        EditorGUILayout.Space(4);
                        SubHeader("Color Correction", "色調補正 (HSV)");
                        P(materialEditor, properties, "_HueShift", "Hue Shift", "色相シフト", "", "色合いを回転させます");
                        P(materialEditor, properties, "_Saturation", "Saturation", "彩度", "", "鮮やかさを調整します");
                        P(materialEditor, properties, "_ValueMulti", "Value Multiplier", "明度", "", "明るさを調整します");

                        EditorGUILayout.Space(4);
                        SubHeader("Detail Map", "ディテールマップ (タトゥーやチーク等)");
                        var detailTex = FindProperty("_DetailMap", properties, false);
                        if (detailTex != null)
                        {
                            materialEditor.TexturePropertySingleLine(
                                Label("Detail Map (RGBA)", "ディテールマップ (RGBA)", "", "アルファ値でブレンドされます"),
                                detailTex, FindProperty("_DetailColor", properties, false));
                            materialEditor.TextureScaleOffsetProperty(detailTex);
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Normal Map", "ノーマルマップ (凹凸)");
                        var normalTex = FindProperty("_NormalMap", properties, false);
                        if (normalTex != null)
                        {
                            materialEditor.TexturePropertySingleLine(
                                Label("Normal Map", "ノーマルマップ", "", ""),
                                normalTex, FindProperty("_NormalScale", properties, false));
                        }
                    }
                }
            }

            // -----------------------------------------------------------
            // 3. Light and Shadow (ライティングと影設定)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("light", true, "Light and Shadow", "ライトと影", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var shadingStyleProp = FindProperty("_ShadingStyle", properties, false);
                        P(materialEditor, shadingStyleProp, "Shading Style", "シェーディング", "", "");
                        P(materialEditor, properties, "_ShadowColor", "Shadow Color", "影の色", "", "");

                        var recvMask = FindProperty("_ReceiveShadowMask", properties, false);
                        if (recvMask != null)
                            materialEditor.TexturePropertySingleLine(Label("Receive Shadow Mask (R)", "落ち影マスク (R)", "", ""), recvMask);
                        
                        P(materialEditor, properties, "_ReceiveShadowStrength", "Receive Strength", "落ち影の強さ", "", "");
                        P(materialEditor, properties, "_ShadowMapSoftness", "Shadow Softness", "落ち影のソフトさ", "", "");
                        P(materialEditor, properties, "_ShadowDither", "Shadow Dither", "影のディザ", "", "");
                        P(materialEditor, properties, "_HalfLambertWrap", "Light Wrap", "ライトラップ", "", "");

                        if (shadingStyleProp != null && shadingStyleProp.floatValue >= 0.5f) // IsToon
                        {
                            EditorGUILayout.Space(2);
                            P(materialEditor, properties, "_ToonStep", "Toon Threshold", "トゥーン境界位置", "", "");
                            P(materialEditor, properties, "_ToonFeather", "Toon Softness", "トゥーン境界の柔らかさ", "", "");
                        }

                        EditorGUILayout.Space(4);
                        SubHeader("Auto Shadow Fix", "影補正 (自動)");
                        P(materialEditor, properties, "_FrontMaskStrength", "Front Brightness", "正面の明るさ", "", "");
                        P(materialEditor, properties, "_UpMaskStrength", "Up Brightness", "上向きの明るさ", "", "");
                        P(materialEditor, properties, "_MaskFalloff", "Erase Breadth", "補正の範囲", "", "");
                        P(materialEditor, properties, "_BacklightPreserve", "Backlight Preserve", "逆光時の陰を維持", "", "");
                        P(materialEditor, properties, "_FaceNormalSmoothness", "Normal Smoothing", "法線のならし", "", "");

                        EditorGUILayout.Space(4);
                        SubHeader("Anti-Blowout", "白飛び防止");
                        P(materialEditor, properties, "_DiffuseLightLimit", "Diffuse Light Limit", "ベース明るさ上限", "", "");
                    }
                }
            }

            // -----------------------------------------------------------
            // 4. Outline (アウトライン) - キャラ向けなので優先度高め
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("outline", true, "Outline", "アウトライン (輪郭線)", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawOutlineSetup(materialEditor, properties);
                    }
                }
            }

            // -----------------------------------------------------------
            // 5. Specular and Reflection (ハイライト・MatCapなど)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("specular", false, "Specular and Reflection", "ハイライトと映り込み", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var specMask = FindProperty("_SpecularMask", properties, false);
                        if (specMask != null)
                            materialEditor.TexturePropertySingleLine(Label("Specular Mask (R)", "スペキュラマスク (R)", "", ""), specMask);

                        SubHeader("Primary (Sharp)", "Primary（シャープ）");
                        P(materialEditor, properties, "_SpecularColor", "Color", "色", "", "");
                        P(materialEditor, properties, "_Smoothness", "Smoothness", "なめらかさ", "", "");
                        P(materialEditor, properties, "_SpecularIntensity", "Intensity", "強度", "", "");
                        P(materialEditor, properties, "_PriSpecularLightLimit", "Light Limit", "明るさ上限", "", "");

                        SubHeader("Secondary (Matte)", "Secondary（マット）");
                        P(materialEditor, properties, "_SecSpecularColor", "Color", "色", "", "");
                        P(materialEditor, properties, "_SecSmoothness", "Smoothness", "なめらかさ", "", "");
                        P(materialEditor, properties, "_SecSpecularIntensity", "Intensity", "強度", "", "");
                        P(materialEditor, properties, "_SecSpecularLightLimit", "Light Limit", "明るさ上限", "", "");

                        SubHeader("Anisotropic (Hair / Silk)", "異方性ハイライト (髪 / シルク)");
                        var anisoColorProp = FindProperty("_AnisoColor", properties, false);
                        P(materialEditor, anisoColorProp, "Color (A=0 is Off)", "色 (アルファ0で無効)", "", "アルファ値を0にすると計算自体がスキップされます");
                        if (anisoColorProp != null && anisoColorProp.colorValue.a > 0f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_AnisoThickness", "Thickness", "太さ", "", "");
                                P(materialEditor, properties, "_AnisoOffset", "Position Offset", "位置のズレ", "", "");
                                P(materialEditor, properties, "_AnisoAngle", "Angle", "角度 (向き)", "", "");
                                P(materialEditor, properties, "_AnisoStrandScale", "Strand Scale", "繊維の細かさ", "", "数値を上げるほど毛束が細かくなります");
                                P(materialEditor, properties, "_AnisoStrandStrength", "Strand Strength", "繊維の凹凸感", "", "ハイライトが毛束に沿ってギザギザに割れます");
                                P(materialEditor, properties, "_AnisoStrandDir", "Strand Direction", "繊維の方向", "", "繊維（ノイズ）が流れるUVの方向を回転させます");
                            }
                        }

                        SubHeader("MatCap", "MatCap");
                        var useMatCapProp = FindProperty("_UseMatCap", properties, false);
                        P(materialEditor, useMatCapProp, "Enable MatCap", "MatCapを使う", "", "");
                        if (useMatCapProp != null && useMatCapProp.floatValue > 0.5f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_MatCapBlend", "Blend Mode", "合成モード", "", "");
                                var matcap = FindProperty("_MatCapTex", properties, false);
                                if (matcap != null)
                                    materialEditor.TexturePropertySingleLine(Label("MatCap Texture (RGB)", "MatCapテクスチャ (RGB)", "", ""), matcap);
                                P(materialEditor, properties, "_MatCapColor", "Tint", "色", "", "");
                                P(materialEditor, properties, "_MatCapIntensity", "Intensity", "強度", "", "");
                            }
                        }
                    }
                }
            }

            // -----------------------------------------------------------
            // 6. Emission (発光)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("emission", false, "Emission", "発光", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var useEmissionProp = FindProperty("_UseEmission", properties, false);
                        P(materialEditor, useEmissionProp, "Enable Emission", "発光を有効にする", "", "");
                        
                        if (useEmissionProp != null && useEmissionProp.floatValue > 0.5f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                var emTex = FindProperty("_EmissionMap", properties, false);
                                var emColor = FindProperty("_EmissionColor", properties, false);

                                if (emTex != null && emColor != null)
                                {
                                    materialEditor.TexturePropertySingleLine(
                                        Label("Emission Map & Color", "発光マップと色 (HDR)", "", ""),
                                        emTex, emColor);
                                }
                                
                                P(materialEditor, properties, "_EmissionIntensity", "Intensity", "発光の強度", "", "");
                                materialEditor.LightmapEmissionProperty();
                            }
                        }
                    }
                }
            }

            // -----------------------------------------------------------
            // 7. Optional Effects (ベース以外の質感効果)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("optional", false, "Optional Effects", "追加質感エフェクト", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Glitter", "グリッター");

                        var glitterMask = FindProperty("_GlitterMask", properties, false);
                        if (glitterMask != null)
                            materialEditor.TexturePropertySingleLine(Label("Glitter Mask (R)", "発生マスク (R)", "", "白く塗られた部分にだけグリッターが発生します"), glitterMask);

                        P(materialEditor, properties, "_GlitterColor", "Color (HDR)", "色 (HDR)", "", "HDRで白飛びさせることで画面のBloomやGlareエフェクトを誘発します");
                        var glitterIntProp = FindProperty("_GlitterIntensity", properties, false);
                        P(materialEditor, glitterIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "発光の強さ。ポストエフェクトが反応するまで上げてください");

                        if (glitterIntProp != null && glitterIntProp.floatValue > 0f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_GlitterScale", "Density Scale", "密度", "", "数値を上げるほど粒が細かく密集します");
                                P(materialEditor, properties, "_GlitterSize", "Dot Size", "粒の大きさ", "", "発光の起点となるコアの大きさ");
                                P(materialEditor, properties, "_GlitterTilt", "Normal Tilt", "法線の傾き(ばらつき)", "", "0でモデルの表面に沿い、数値を上げるほどランダムな方向を向いてチラつきます");
                                P(materialEditor, properties, "_GlitterSparsity", "Sparsity", "間引き率", "", "スパンコールの密集度。値が小さいほどまばらになります（推奨:0.5）");
                                P(materialEditor, properties, "_GlitterIridescence", "Iridescence", "虹色強度", "", "1に近づけるほど、視線角度に応じて虹色（ホログラム）に変化します");
                                P(materialEditor, properties, "_GlitterIridescenceShift", "IridescenceShift", "虹色移動", "", "1に近づけるほど、視線角度に応じて虹色が変化します");
                                P(materialEditor, properties, "_GlitterBaseReflection", "Base Reflection", "暗い反射（ベース）", "", "光っていない時のスパンコール自体の存在感（メタリック感）。0～0.1程度推奨");
                            }
                        }

                        SubHeader("SSS (Subsurface)", "SSS（表面下散乱）");
                        P(materialEditor, properties, "_SSSColor", "Color", "色", "", "");
                        var sssIntProp = FindProperty("_SSSIntensity", properties, false);
                        P(materialEditor, sssIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (sssIntProp != null && sssIntProp.floatValue > 0f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_SSSPower", "Falloff", "減衰", "", "");
                                P(materialEditor, properties, "_SSSDistortion", "Distortion", "歪み", "", "");
                            }
                        }

                        SubHeader("Peach Fuzz (Soft Edge Sheen)", "Peach Fuzz（縁の柔らかい光沢）");
                        P(materialEditor, properties, "_FuzzColor", "Color", "色", "", "");
                        var fuzzIntProp = FindProperty("_FuzzIntensity", properties, false);
                        P(materialEditor, fuzzIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (fuzzIntProp != null && fuzzIntProp.floatValue > 0f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                P(materialEditor, properties, "_FuzzPower", "Width", "幅", "", "");
                        }

                        SubHeader("Rim Light", "Rim Light（リムライト）");
                        P(materialEditor, properties, "_RimColor", "Color", "色", "", "");
                        var rimIntProp = FindProperty("_RimIntensity", properties, false);
                        P(materialEditor, rimIntProp, "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (rimIntProp != null && rimIntProp.floatValue > 0f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                                P(materialEditor, properties, "_RimThickness", "Thickness", "太さ", "", "");
                        }
                    }
                }
            }

            // -----------------------------------------------------------
            // 8. Special Effects (演出系のエフェクト)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("special_effects", false, "Special Effects", "特殊エフェクト", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("Dissolve", "消失エフェクト");
                        var useDissolveProp = FindProperty("_UseDissolve", properties, false);
                        P(materialEditor, useDissolveProp, "Enable Dissolve", "Enable Dissolve", "", "ディゾルブ（消失）エフェクトを有効にします");
                        
                        if (useDissolveProp != null && useDissolveProp.floatValue > 0.5f)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_DissolveAmount", "Dissolve Amount", "Dissolve Amount", "", "0で完全表示、1で完全消失");
                                P(materialEditor, properties, "_DissolveInvert", "Invert Dissolve", "Invert Dissolve", "", "チェックを入れると消失方向（条件）が逆転します");
                                var typeProp = FindProperty("_DissolveType", properties, false);
                                P(materialEditor, typeProp, "Dissolve Axis", "Dissolve Axis", "", "None=テクスチャのみ, WorldY=空間のY座標, LocalY=モデルのY座標");
                                
                                if (typeProp != null && typeProp.floatValue > 0.5f) // None以外
                                {
                                    P(materialEditor, properties, "_DissolveStartY", "Start Y (Height)", "Start Y (Height)", "", "フェードが始まるY座標");
                                    P(materialEditor, properties, "_DissolveEndY", "End Y (Height)", "End Y (Height)", "", "フェードが終わるY座標");
                                }

                                EditorGUILayout.Space(2);
                                var disTex = FindProperty("_DissolveTex", properties, false);
                                if (disTex != null)
                                {
                                    materialEditor.TexturePropertySingleLine(Label("Dissolve Noise", "Dissolve Noise", "", "境界を揺らすためのノイズテクスチャ"), disTex);
                                }
                                P(materialEditor, properties, "_DissolveNoiseScale", "Noise Scale", "Noise Scale", "", "ノイズの細かさ");
                                P(materialEditor, properties, "_DissolveNoiseStrength", "Noise Strength", "Noise Strength", "", "ノイズによる境界の揺れ幅");

                                EditorGUILayout.Space(2);
                                P(materialEditor, properties, "_DissolveEdgeColor", "Edge Outer Color (HDR)", "エッジ外側の色 (HDR)", "", "消失の最前線の輝き");
                                P(materialEditor, properties, "_DissolveEdgeColor2", "Edge Inner Color (HDR)", "エッジ内側の色 (HDR)", "", "少し内側のグラデーション色");
                                P(materialEditor, properties, "_DissolveEdgeWidth", "Edge Width", "Edge Width", "", "発光する境界線の太さ");
                                P(materialEditor, properties, "_DissolveEdgeStep", "Step Edge (Toon Style)", "エッジの段階化 (Toon調)", "", "チェックを入れるとグラデーションがパキッとした階調（層）になります");
                            }
                        }

                        EditorGUILayout.Space(6);
                        SubHeader("Black Out", "暗転エフェクト");
                        P(materialEditor, properties, "_BlackOut", "Black Out Amount", "暗転率", "", "");
                    }
                }
            }

            // -----------------------------------------------------------
            // 9. Surface Micro Detail (微細な質感)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("detail", false, "Surface Micro Detail", "マイクロディテール", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var noise = FindProperty("_BlueNoiseTex", properties, false);
                        if (noise != null)
                            materialEditor.TexturePropertySingleLine(Label("Micro Grain (Blue Noise)", "グレイン (ブルーノイズ)", "", ""), noise);
                        
                        P(materialEditor, properties, "_GrainIntensity", "Grain Intensity", "グレイン強度", "", "");
                        P(materialEditor, properties, "_GrainScale", "Grain Scale", "グレインのスケール", "", "");
                    }
                }
            }

            // -----------------------------------------------------------
            // 10. Stencil (高度なレンダリング設定)
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("stencil", false, "Stencil", "Stencil (ステンシル)", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, properties, "_StencilRef", "Stencil Ref", "Stencil Ref", "", "ステンシルの参照値 (0-255)");
                        P(materialEditor, properties, "_StencilComp", "Compare Function", "Compare Function", "", "ステンシルテストの比較条件 (Always, Equal, NotEqual など)");
                        P(materialEditor, properties, "_StencilPass", "Pass Operation", "Pass Operation", "", "テスト通過時の処理 (Keep, Replace など)");
                        P(materialEditor, properties, "_StencilFail", "Fail Operation", "Fail Operation", "", "ステンシルテスト失敗時の処理");
                        P(materialEditor, properties, "_StencilZFail", "ZFail Operation", "ZFail Operation", "", "ステンシルテスト成功、かつZテスト失敗時の処理");
                    }
                }
            }

            // -----------------------------------------------------------
            // 11. Advanced Options
            // -----------------------------------------------------------
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("advanced", false, "Advanced Options", "高度な設定", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        materialEditor.EnableInstancingField();
                        materialEditor.DoubleSidedGIField();
                    }
                }
            }
        }

        private void DrawRenderModeSetup(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var surfaceProp = FindProperty("_SurfaceTransparent", properties, false);
            var alphaClipProp = FindProperty("_AlphaClip", properties, false);
            if (surfaceProp == null || alphaClipProp == null) return;

            int currentMode = 0; // 0: Opaque, 1: Cutout, 2: Transparent
            if (surfaceProp.floatValue > 0.5f) currentMode = 2;
            else if (alphaClipProp.floatValue > 0.5f) currentMode = 1;

            EditorGUI.BeginChangeCheck();
            string[] modesJp = { "Opaque (不透明)", "Cutout (くり抜き)", "Transparent (半透明)" };
            string[] modesEn = { "Opaque", "Cutout", "Transparent" };
            
            var lbl = Label("Render Mode (Preset)", "Render Mode (プリセット)", "Sets Queue, Blend, ZWrite automatically", "一括で半透明用の設定に切り替えます");
            int newMode = EditorGUILayout.Popup(lbl, currentMode, _jp ? modesJp : modesEn);

            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Render Mode Setup");
                foreach (Material mat in materialEditor.targets)
                {
                    SetupRenderMode(mat, newMode);
                }
            }
        }

        private void SetupRenderMode(Material mat, int mode)
        {
            switch (mode)
            {
                case 0: // Opaque (不透明)
                    mat.SetFloat("_SurfaceTransparent", 0f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.renderQueue = 2000; // Geometry
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.DisableKeyword("_SURFACE_TRANSPARENT");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    break;
                case 1: // Cutout (くり抜き - 髪や葉っぱ)
                    mat.SetFloat("_SurfaceTransparent", 0f);
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat("_ZWrite", 1f);
                    mat.renderQueue = 2450; // AlphaTest
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.DisableKeyword("_SURFACE_TRANSPARENT");
                    mat.EnableKeyword("_ALPHATEST_ON");
                    break;
                case 2: // Transparent (半透明ブレンド - ほっぺのチークやガラス)
                    mat.SetFloat("_SurfaceTransparent", 1f);
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f); // 半透明なのでZWriteはOff
                    mat.renderQueue = 3000; // Transparent
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.EnableKeyword("_SURFACE_TRANSPARENT");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    break;
            }
        }

        private void DrawOutlineSetup(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var outlineProp = FindProperty("_UseOutline", properties, false);
            if (outlineProp == null) return;

            EditorGUI.BeginChangeCheck();
            
            P(materialEditor, outlineProp, "Enable Outline", "アウトラインを有効にする", "", "");
            var isOutlineOn = outlineProp.floatValue > 0.5f;

            if (isOutlineOn)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    P(materialEditor, properties, "_OutlineColor", "Color", "色", "", "");
                    P(materialEditor, properties, "_OutlineWidth", "Width", "太さ", "", "");
                    var alphaClipProp = FindProperty("_AlphaClip", properties, false);
                    if (alphaClipProp != null && alphaClipProp.floatValue > 0.5f)
                    {
                        EditorGUILayout.Space(2);
                        P(materialEditor, properties, "_OutlineCutoffShift", "Cutoff Shift (Fix)", "透過エッジの補正", "", "毛先などの半透明グラデーション部分で、アウトラインが黒く太く残ってしまう現象を打ち消します");
                    }

                    EditorGUILayout.Space(4);
                    SubHeader("Masking (Stencil)", "マスク処理 (ステンシル)");
                    P(materialEditor, properties, "_OutlineStencilRef", "Stencil Ref", "参照値", "", "本体側のStencil Refと同じ数値を入れます");
                    P(materialEditor, properties, "_OutlineStencilComp", "Compare Function", "比較条件", "", "NotEqualにすると、本体が描画された部分には線が描かれなくなります");
                    P(materialEditor, properties, "_OutlineStencilPass", "Pass Operation", "Pass Operation", "", "テスト通過時の処理 (基本はKeep)");
                    P(materialEditor, properties, "_OutlineStencilFail", "Fail Operation", "Fail Operation", "", "ステンシルテスト失敗時の処理");
                    P(materialEditor, properties, "_OutlineStencilZFail", "ZFail Operation", "ZFail Operation", "", "ステンシルテスト成功、かつZテスト失敗時の処理");
                }
            }

            if (EditorGUI.EndChangeCheck() || !_prefsLoaded)
            {
                foreach (Material mat in materialEditor.targets)
                {
                    if (isOutlineOn) mat.EnableKeyword("_OUTLINE_ON");
                    else mat.DisableKeyword("_OUTLINE_ON");
                }
            }
        }

        private void LoadPrefs()
        {
            if (_prefsLoaded) return;
            _jp = EditorPrefs.GetBool(LangKey, Application.systemLanguage == SystemLanguage.Japanese);
            _showHelp = EditorPrefs.GetBool(HelpKey, true);
            _useCustomUI = EditorPrefs.GetBool(CustomUIKey, true);
            _prefsLoaded = true;
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("EasyPBR / Doll", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                var uiMode = EditorGUILayout.Popup(_useCustomUI ? 0 : 1,
                    _jp ? new[] { "カスタム", "デフォルト" } : new[] { "Custom", "Default" },
                    GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _useCustomUI = uiMode == 0;
                    EditorPrefs.SetBool(CustomUIKey, _useCustomUI);
                }

                EditorGUI.BeginChangeCheck();
                var lang = EditorGUILayout.Popup(_jp ? 1 : 0, new[] { "English", "日本語" }, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _jp = lang == 1;
                    EditorPrefs.SetBool(LangKey, _jp);
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

        private bool Section(string id, bool defaultOpen, string titleEn, string titleJp, string descEn, string descJp)
        {
            var key = KeyPrefix + "fold." + id;
            var open = EditorPrefs.GetBool(key, defaultOpen);

            var rect = EditorGUILayout.GetControlRect(false, 24f);
            var isPro = EditorGUIUtility.isProSkin;
            
            var barColor = isPro ? new Color(0.22f, 0.22f, 0.24f) : new Color(0.78f, 0.78f, 0.80f);
            EditorGUI.DrawRect(rect, barColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.15f));

            var arrowRect = new Rect(rect.x + 6f, rect.y + 3f, 14f, 18f);
            EditorGUI.LabelField(arrowRect, open ? "\u25BC" : "\u25B6");
            var titleRect = new Rect(rect.x + 22f, rect.y + 3f, rect.width - 26f, 18f);
            EditorGUI.LabelField(titleRect, _jp ? titleJp : titleEn, EditorStyles.boldLabel);

            var e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                open = !open;
                EditorPrefs.SetBool(key, open);
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
            var r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.2f));
            EditorGUILayout.LabelField(_jp ? jp : en, EditorStyles.miniBoldLabel);
        }

        private GUIContent Label(string lblEn, string lblJp, string tipEn, string tipJp)
        {
            return new GUIContent(_jp ? lblJp : lblEn, _jp ? tipJp : tipEn);
        }

        // 軽量化: 取得済みのMaterialPropertyを直接渡せるオーバーロード
        private void P(MaterialEditor editor, MaterialProperty prop, string lblEn, string lblJp, string tipEn, string tipJp)
        {
            if (prop == null) return;
            editor.ShaderProperty(prop, Label(lblEn, lblJp, tipEn, tipJp));
        }

        // 既存との互換用 (必要に応じて内部で検索を行う)
        private void P(MaterialEditor editor, MaterialProperty[] props, string name, string lblEn, string lblJp, string tipEn, string tipJp)
        {
            var p = FindProperty(name, props, false);
            P(editor, p, lblEn, lblJp, tipEn, tipJp);
        }
    }
}
