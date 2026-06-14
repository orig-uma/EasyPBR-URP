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

            // ===== 1. Surface Options =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("surface", true, "Surface Options", "サーフェス設定", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, properties, "_Cull", "Cull", "Cull", "", "描画する面 (Off / Front / Back)");
                        P(materialEditor, properties, "_ZWrite", "ZWrite", "ZWrite", "", "深度バッファへの書き込み (On / Off)");
                        P(materialEditor, properties, "_ZTest", "ZTest", "ZTest", "", "深度テストの条件 (LEqual: 通常, Always: 常に前面に描画 など)");
                        P(materialEditor, properties, "_SrcBlend", "Source Blend", "Source Blend", "", "背景と合成する際の元カラーの係数");
                        P(materialEditor, properties, "_DstBlend", "Destination Blend", "Destination Blend", "", "背景と合成する際の背景カラーの係数");

                        EditorGUILayout.Space(4);
                        materialEditor.RenderQueueField();
                        
                        EditorGUILayout.Space(4);
                        P(materialEditor, properties, "_SurfaceTransparent", "Output Alpha (_SURFACE_TRANSPARENT)", "Output Alpha (_SURFACE_TRANSPARENT)", "", "アルファ値を出力するかどうか");
                        P(materialEditor, properties, "_AlphaClip", "Alpha Clipping (_ALPHATEST_ON)", "Alpha Clipping (_ALPHATEST_ON)", "", "アルファ値によるピクセルの破棄");

                        if (PropOn(properties, "_AlphaClip"))
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_Cutoff", "Alpha Cutoff", "Alpha Cutoff", "", "クリッピングの閾値");
                            }
                        }
                    }
                }
            }

            // ===== 1.5. Stencil Options =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("stencil", false, "Stencil", "Stencil", "", ""))
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

            // ===== 2. Base Core =====
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
                    }
                }
            }

            // ===== 3. Auto Shadow Fix =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("shadowfix", true, "Auto Shadow Fix", "影補正", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, properties, "_FrontMaskStrength", "Front Brightness", "正面の明るさ", "", "");
                        P(materialEditor, properties, "_UpMaskStrength", "Up Brightness", "上向きの明るさ", "", "");
                        P(materialEditor, properties, "_MaskFalloff", "Erase Breadth", "補正の範囲", "", "");
                        P(materialEditor, properties, "_BacklightPreserve", "Backlight Preserve", "逆光時の陰を維持", "", "");
                        P(materialEditor, properties, "_FaceNormalSmoothness", "Normal Smoothing", "法線のならし", "", "");
                    }
                }
            }

            // ===== 4. Light and Shadow =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("light", true, "Light and Shadow", "ライトと影", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, properties, "_ShadingStyle", "Shading Style", "シェーディング", "", "");
                        P(materialEditor, properties, "_ShadowColor", "Shadow Color", "影の色", "", "");

                        var recvMask = FindProperty("_ReceiveShadowMask", properties, false);
                        if (recvMask != null)
                            materialEditor.TexturePropertySingleLine(Label("Receive Shadow Mask (R)", "落ち影マスク (R)", "", ""), recvMask);
                        
                        P(materialEditor, properties, "_ReceiveShadowStrength", "Receive Strength", "落ち影の強さ", "", "");
                        P(materialEditor, properties, "_ShadowMapSoftness", "Shadow Softness", "落ち影のソフトさ", "", "");
                        P(materialEditor, properties, "_ShadowDither", "Shadow Dither", "影のディザ", "", "");
                        P(materialEditor, properties, "_HalfLambertWrap", "Light Wrap", "ライトラップ", "", "");

                        if (IsToon(properties))
                        {
                            EditorGUILayout.Space(2);
                            P(materialEditor, properties, "_ToonStep", "Toon Threshold", "トゥーン境界位置", "", "");
                            P(materialEditor, properties, "_ToonFeather", "Toon Softness", "トゥーン境界の柔らかさ", "", "");
                        }

                        SubHeader("Anti-Blowout", "白飛び防止");
                        P(materialEditor, properties, "_DiffuseLightLimit", "Diffuse Light Limit", "ベース明るさ上限", "", "");
                    }
                }
            }

            // ===== 5. Surface Micro Detail =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("detail", false, "Surface Micro Detail", "表面の質感", "", ""))
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

            // ===== 6. Specular and Reflection =====
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

                        SubHeader("MatCap", "MatCap");
                        P(materialEditor, properties, "_UseMatCap", "Enable MatCap", "MatCapを使う", "", "");
                        if (PropOn(properties, "_UseMatCap"))
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

            // ===== 7. Emission =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("emission", false, "Emission", "発光", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, properties, "_UseEmission", "Enable Emission", "発光を有効にする", "", "");
                        
                        if (PropOn(properties, "_UseEmission"))
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

            // ===== 8. Dissolve =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("dissolve", false, "Dissolve", "Dissolve (消失エフェクト)", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        P(materialEditor, properties, "_UseDissolve", "Enable Dissolve", "Enable Dissolve", "", "ディゾルブ（消失）エフェクトを有効にします");
                        
                        if (PropOn(properties, "_UseDissolve"))
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_DissolveAmount", "Dissolve Amount", "Dissolve Amount", "", "0で完全表示、1で完全消失");
                                P(materialEditor, properties, "_DissolveInvert", "Invert Dissolve", "Invert Dissolve", "", "チェックを入れると消失方向（条件）が逆転します");
                                P(materialEditor, properties, "_DissolveType", "Dissolve Axis", "Dissolve Axis", "", "None=テクスチャのみ, WorldY=空間のY座標, LocalY=モデルのY座標");
                                
                                var typeProp = FindProperty("_DissolveType", properties, false);
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
                                P(materialEditor, properties, "_DissolveEdgeColor", "Edge Burn Color (HDR)", "Edge Burn Color (HDR)", "", "境界が燃えるような発光色");
                                P(materialEditor, properties, "_DissolveEdgeWidth", "Edge Width", "Edge Width", "", "発光する境界線の太さ");
                            }
                        }
                    }
                }
            }

            // ===== 9. Optional Effects =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("optional", false, "Optional Effects", "追加効果", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        SubHeader("SSS (Subsurface)", "SSS（表面下散乱）");
                        P(materialEditor, properties, "_SSSColor", "Color", "色", "", "");
                        P(materialEditor, properties, "_SSSIntensity", "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (PropPositive(properties, "_SSSIntensity"))
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                P(materialEditor, properties, "_SSSPower", "Falloff", "減衰", "", "");
                                P(materialEditor, properties, "_SSSDistortion", "Distortion", "歪み", "", "");
                            }
                        }

                        SubHeader("Peach Fuzz (Soft Edge Sheen)", "Peach Fuzz（縁の柔らかい光沢）");
                        P(materialEditor, properties, "_FuzzColor", "Color", "色", "", "");
                        P(materialEditor, properties, "_FuzzIntensity", "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (PropPositive(properties, "_FuzzIntensity"))
                        {
                            using (new EditorGUI.IndentLevelScope())
                                P(materialEditor, properties, "_FuzzPower", "Width", "幅", "", "");
                        }

                        SubHeader("Rim Light", "Rim Light（リムライト）");
                        P(materialEditor, properties, "_RimColor", "Color", "色", "", "");
                        P(materialEditor, properties, "_RimIntensity", "Intensity (0 = Off)", "強度 (0でOFF)", "", "");
                        if (PropPositive(properties, "_RimIntensity"))
                        {
                            using (new EditorGUI.IndentLevelScope())
                                P(materialEditor, properties, "_RimPower", "Thickness", "太さ", "", "");
                        }
                    }
                }
            }

            // ===== 10. Advanced =====
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Section("advanced", false, "Advanced Options", "高度な設定", "", ""))
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        // RenderQueue は Surface Options に移動したため、ここは標準オプションのみ
                        materialEditor.EnableInstancingField();
                        materialEditor.DoubleSidedGIField();
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // UI ヘルパー群
        // ---------------------------------------------------------------------
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

        private void P(MaterialEditor editor, MaterialProperty[] props, string name,
               string lblEn, string lblJp, string tipEn, string tipJp)
        {
            var p = FindProperty(name, props, false);
            if (p == null) return;
            editor.ShaderProperty(p, Label(lblEn, lblJp, tipEn, tipJp));
        }

        private static bool PropOn(MaterialProperty[] props, string name)
        {
            var p = FindProperty(name, props, false);
            return p != null && p.floatValue > 0.5f;
        }

        private static bool PropPositive(MaterialProperty[] props, string name)
        {
            var p = FindProperty(name, props, false);
            return p != null && p.floatValue > 0f;
        }

        private static bool IsToon(MaterialProperty[] props)
        {
            var p = FindProperty("_ShadingStyle", props, false);
            return p != null && p.floatValue >= 0.5f; 
        }
    }
}
