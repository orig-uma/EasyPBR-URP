using UnityEditor;
using UnityEngine;

namespace Origuma.EasyPBR.URP.Editor
{
    /// <summary>
    /// "Origuma/EasyPBR_URP/Doll" 用のカスタムマテリアルインスペクター。
    ///
    /// 利用者視点の工夫:
    ///  - 言語切替（English / 日本語）、カスタム/デフォルト UI 切替、
    ///    「説明の表示」トグルを上部に配置。設定は EditorPrefs に保存され、
    ///    全マテリアルで共有される（日本語環境では初期ON）。
    ///  - 各プロパティにローカライズしたラベルとツールチップ（ホバー説明）を付与。
    ///  - 機能ごとの折りたたみセクション。開閉状態も EditorPrefs に保存。
    ///  - 文脈警告（例: ブルーノイズ未設定で Grain/Dither を使っている等）。
    ///
    /// キーワード(_ALPHATEST_ON / _SHADINGSTYLE_* / _MATCAP_ON / _MATCAPBLEND_*)の
    /// 切り替えは [Toggle]/[KeywordEnum] ドロワーが行うため、ShaderProperty 経由で
    /// 描画する。手動の SetKeyword は不要。
    /// </summary>
    public class DollShaderGUI : ShaderGUI
    {
        const string KeyPrefix = "Origuma.EasyPBR.URP.Doll.";
        const string LangKey = KeyPrefix + "lang.jp";
        const string HelpKey = KeyPrefix + "show.help";
        const string CustomUIKey = KeyPrefix + "use.custom.ui";

        bool _jp;
        bool _showHelp;
        bool _useCustomUI = true;
        bool _prefsLoaded;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            LoadPrefs();
            DrawToolbar();

            if (!_useCustomUI)
            {
                base.OnGUI(materialEditor, properties);
                return;
            }

            // ===== Base Core =====
            if (Section("base", true, "Base Core", "基本設定",
                "Albedo, color, transparency and culling.",
                "アルベド・色・透過・カリングといった基本の設定です。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var mainTex = FindProperty("_MainTex", properties, false);
                    var baseColor = FindProperty("_BaseColor", properties, false);
                    if (mainTex != null)
                    {
                        materialEditor.TexturePropertySingleLine(
                            Label("Base Map (RGB / Alpha)", "ベースマップ (RGB/Alpha)",
                                  "Albedo texture. Its alpha is used for Alpha Clipping.",
                                  "アルベド。アルファは Alpha Clipping に使われます。"),
                            mainTex, baseColor);
                        materialEditor.TextureScaleOffsetProperty(mainTex);
                    }

                    P(materialEditor, properties, "_AlphaClip",
                        "Alpha Clipping", "アルファクリッピング",
                        "Discard pixels whose alpha is below Cutoff (hair, eyelashes, etc.).",
                        "アルファが Cutoff 未満のピクセルを破棄します（髪・睫毛など）。");
                    if (PropOn(properties, "_AlphaClip"))
                        P(materialEditor, properties, "_Cutoff",
                            "Alpha Cutoff", "アルファカットオフ",
                            "Threshold used by Alpha Clipping.",
                            "Alpha Clipping のしきい値。");

                    P(materialEditor, properties, "_Cull",
                        "Cull Mode", "カリング",
                        "Which faces to skip. Off draws both sides (for thin meshes).",
                        "描画しない面。Off で両面描画（薄いメッシュ向け）。");
                }
            }

            // ===== Auto Shadow Fix =====
            if (Section("shadowfix", true, "Auto Shadow Fix", "影補正",
                "Automatically reduces harsh self-shadows on front/up-facing areas (e.g. nose and cheek bumps on the face). No mask texture required.",
                "正面・上向きの面でできる余計な影を自動で目立たなくします（例: 顔の鼻や頬の凹凸）。マスクは不要です。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    P(materialEditor, properties, "_FrontMaskStrength",
                        "Front Brightness", "正面の明るさ",
                        "Brightens surfaces pointing toward the model's front.",
                        "モデルの正面を向いた面を明るくします。");
                    P(materialEditor, properties, "_UpMaskStrength",
                        "Up Brightness", "上向きの明るさ",
                        "Brightens upward-facing surfaces.",
                        "上を向いた面を明るくします。");
                    P(materialEditor, properties, "_MaskFalloff",
                        "Erase Breadth", "補正の範囲",
                        "Higher values focus the effect on the most front/up facing areas.",
                        "大きいほど、正面・上向きの面に絞って効きます。");
                    P(materialEditor, properties, "_BacklightPreserve",
                        "Backlight Preserve", "逆光時の陰を維持",
                        "Keeps the shadows when the light is behind the model.",
                        "光源が背後にあるとき、陰を残します。");
                    P(materialEditor, properties, "_FaceNormalSmoothness",
                        "Normal Smoothing", "法線のならし",
                        "Flattens normals so mesh bumps (e.g. on the face) don't cast harsh shadows.",
                        "法線をならして、ポリゴンの凹凸（例: 顔の鼻や頬）が余計な影を作らないようにします。");
                }
            }

            // ===== Light and Shadow =====
            if (Section("light", true, "Light and Shadow", "ライトと影",
                "Shading mode, and how cast/received shadows look.",
                "シェーディングの種類と、影の見え方を調整します。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    P(materialEditor, properties, "_ShadingStyle",
                        "Shading Style", "シェーディング",
                        "Smooth = soft gradient shading. Toon = hard two-tone boundary.",
                        "Smooth = なめらかな陰影 / Toon = 二値的なトゥーン境界。");
                    P(materialEditor, properties, "_ShadowColor",
                        "Shadow Color", "影の色",
                        "Color tint applied to shaded areas. Black simply darkens.",
                        "陰部分に乗る色。黒なら単純に暗くなります。");

                    var recvMask = FindProperty("_ReceiveShadowMask", properties, false);
                    if (recvMask != null)
                        materialEditor.TexturePropertySingleLine(
                            Label("Receive Shadow Mask (R)", "落ち影マスク (R)",
                                  "R channel controls how much cast shadow each area receives.",
                                  "Rチャンネルで、各部位が落ち影をどれだけ受けるか制御します。"),
                            recvMask);
                    P(materialEditor, properties, "_ReceiveShadowStrength",
                        "Receive Strength", "落ち影の強さ",
                        "How strongly cast shadows darken the surface.",
                        "落ち影が表面を暗くする強さ。");
                    P(materialEditor, properties, "_ShadowMapSoftness",
                        "Shadow Softness", "落ち影のソフトさ",
                        "Softens the cast-shadow edge to hide shadow-map stair-stepping.",
                        "落ち影の境界をぼかし、シャドウマップの階段状ノイズを隠します。");
                    P(materialEditor, properties, "_ShadowDither",
                        "Shadow Dither", "影のディザ",
                        "Breaks up shadow banding with blue noise. Requires a Blue Noise texture (see Surface Micro Detail).",
                        "ブルーノイズで影の縞を分解します。ブルーノイズの設定が必要です（Surface Micro Detail）。");
                    P(materialEditor, properties, "_HalfLambertWrap",
                        "Light Wrap", "ライトラップ",
                        "Lifts the shaded side for softer, wrapped shading.",
                        "陰側を持ち上げ、陰影を柔らかくします。");

                    if (IsToon(properties))
                    {
                        EditorGUILayout.Space(2);
                        P(materialEditor, properties, "_ToonStep",
                            "Toon Threshold", "トゥーン境界位置",
                            "Position of the light/shadow boundary (Toon mode).",
                            "明暗境界の位置（Toonモード）。");
                        P(materialEditor, properties, "_ToonFeather",
                            "Toon Softness", "トゥーン境界の柔らかさ",
                            "Softness of the toon boundary.",
                            "トゥーン境界の柔らかさ。");
                    }

                    // ベースカラー用の白飛び防止
                    SubHeader("Anti-Blowout", "白飛び防止");
                    P(materialEditor, properties, "_DiffuseLightLimit",
                        "Diffuse Light Limit", "ベース明るさ上限",
                        "Limits maximum brightness of skin/clothes to prevent blowout under strong lights.",
                        "肌や服の明るさ上限。強いライト環境下での白飛びを防止します。");
                }
            }

            // ===== Surface Micro Detail =====
            if (Section("detail", false, "Surface Micro Detail", "表面の質感",
                "Subtle grain via blue noise. The same texture is also used for shadow dithering.",
                "ブルーノイズで表面に細かな質感を加えます。影のディザにも同じテクスチャを使います。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var noise = FindProperty("_BlueNoiseTex", properties, false);
                    if (noise != null)
                        materialEditor.TexturePropertySingleLine(
                            Label("Micro Grain (Blue Noise)", "グレイン (ブルーノイズ)",
                                  "Blue noise texture for grain and shadow dither. A texture is bundled with this package.",
                                  "grain と影ディザ用のブルーノイズ。本パッケージに同梱テクスチャがあります。"),
                            noise);
                    P(materialEditor, properties, "_GrainIntensity",
                        "Grain Intensity", "グレイン強度",
                        "Amount of micro normal perturbation.",
                        "法線を揺らす量。");
                    P(materialEditor, properties, "_GrainScale",
                        "Grain Scale", "グレインのスケール",
                        "Tiling of the grain pattern.",
                        "グレインのタイリング。");

                    DrawBlueNoiseWarning(properties, noise);
                }
            }

            // ===== Specular and Reflection =====
            if (Section("specular", false, "Specular and Reflection", "ハイライトと映り込み",
                "Dual-lobe specular highlights and optional MatCap.",
                "2種類のハイライトと、MatCapによる擬似的な映り込みです。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var specMask = FindProperty("_SpecularMask", properties, false);
                    if (specMask != null)
                        materialEditor.TexturePropertySingleLine(
                            Label("Specular Mask (R)", "スペキュラマスク (R)",
                                  "R channel masks where specular appears.",
                                  "Rチャンネルでスペキュラの出る場所を制御します。"),
                            specMask);

                    SubHeader("Primary (Sharp)", "Primary（シャープ）");
                    P(materialEditor, properties, "_SpecularColor", "Color", "色",
                        "Primary (sharp) highlight color.", "鋭いハイライトの色。");
                    P(materialEditor, properties, "_Smoothness", "Smoothness", "なめらかさ",
                        "Higher = sharper and smaller highlight.", "大きいほど鋭く小さいハイライト。");
                    P(materialEditor, properties, "_SpecularIntensity", "Intensity", "強度",
                        "Primary highlight strength.", "鋭いハイライトの強さ。");
                    // Primary用リミッター
                    P(materialEditor, properties, "_PriSpecularLightLimit", "Light Limit", "明るさ上限",
                        "Limits maximum highlight brightness to control bloom.", "明るさの上限。過剰なブルーム（発光）を抑えます。");

                    SubHeader("Secondary (Matte)", "Secondary（マット）");
                    P(materialEditor, properties, "_SecSpecularColor", "Color", "色",
                        "Secondary (broad, matte) highlight color.", "広く柔らかいハイライトの色。");
                    P(materialEditor, properties, "_SecSmoothness", "Smoothness", "なめらかさ",
                        "Lower = broader, softer highlight.", "小さいほど広く柔らかいハイライト。");
                    P(materialEditor, properties, "_SecSpecularIntensity", "Intensity", "強度",
                        "Secondary highlight strength.", "柔らかいハイライトの強さ。");
                    // Secondary用リミッター
                    P(materialEditor, properties, "_SecSpecularLightLimit", "Light Limit", "明るさ上限",
                        "Limits maximum brightness to prevent sweaty/plastic look.", "明るさの上限。過剰なテカリ（汗だく感）を抑えます。");

                    SubHeader("MatCap", "MatCap");
                    P(materialEditor, properties, "_UseMatCap",
                        "Enable MatCap", "MatCapを使う",
                        "Pseudo reflection sampled from the view-space normal.",
                        "ビュー空間の法線でテクスチャを引く擬似反射。");
                    if (PropOn(properties, "_UseMatCap"))
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            P(materialEditor, properties, "_MatCapBlend",
                                "Blend Mode", "合成モード",
                                "Add = adds gloss. Multiply = tints/shades.",
                                "Add = 光沢を加算 / Multiply = 乗算で陰影付け。");
                            var matcap = FindProperty("_MatCapTex", properties, false);
                            if (matcap != null)
                                materialEditor.TexturePropertySingleLine(
                                    Label("MatCap Texture (RGB)", "MatCapテクスチャ (RGB)",
                                          "Sphere-mapped reflection texture.",
                                          "球状にマッピングされる映り込みテクスチャ。"),
                                    matcap);
                            P(materialEditor, properties, "_MatCapColor", "Tint", "色",
                                "MatCap color tint.", "MatCapの色味。");
                            P(materialEditor, properties, "_MatCapIntensity", "Intensity", "強度",
                                "MatCap strength.", "MatCapの強さ。");
                        }
                    }
                }
            }

            // ===== Optional Effects =====
            if (Section("optional", false, "Optional Effects", "追加効果",
                "Extra effects. Set Intensity to 0 to fully disable (its cost is also skipped).",
                "オン/オフできる追加の効果です。強度を0にすると無効になり、負荷もかかりません。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    SubHeader("SSS (Subsurface)", "SSS（表面下散乱）");
                    P(materialEditor, properties, "_SSSColor", "Color", "色",
                        "Transmission tint. White uses the light color as-is; reddish gives a skin look.",
                        "透過の色。白なら光源色そのまま、赤系で肌の透け感。");
                    P(materialEditor, properties, "_SSSIntensity", "Intensity (0 = Off)", "強度 (0でOFF)",
                        "Subsurface scattering strength.", "表面下散乱の強さ。");
                    if (PropPositive(properties, "_SSSIntensity"))
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            P(materialEditor, properties, "_SSSPower", "Falloff", "減衰",
                                "Backlight falloff sharpness.", "逆光の減衰の鋭さ。");
                            P(materialEditor, properties, "_SSSDistortion", "Distortion", "歪み",
                                "Distorts the transmission direction by the normal.", "法線で透過方向を歪ませます。");
                        }
                    }

                    SubHeader("Peach Fuzz (Soft Edge Sheen)", "Peach Fuzz（縁の柔らかい光沢）");
                    P(materialEditor, properties, "_FuzzColor", "Color", "色",
                        "Soft edge sheen color (velvet-like).", "縁の柔らかい光沢の色（ベルベット風）。");
                    P(materialEditor, properties, "_FuzzIntensity", "Intensity (0 = Off)", "強度 (0でOFF)",
                        "Peach fuzz strength.", "ピーチファズの強さ。");
                    if (PropPositive(properties, "_FuzzIntensity"))
                    {
                        using (new EditorGUI.IndentLevelScope())
                            P(materialEditor, properties, "_FuzzPower", "Width", "幅",
                                "Width of the soft edge sheen.", "縁の光沢の幅。");
                    }

                    SubHeader("Rim Light", "Rim Light（リムライト）");
                    P(materialEditor, properties, "_RimColor", "Color", "色",
                        "Rim (edge) light color.", "リム（輪郭）の光の色。");
                    P(materialEditor, properties, "_RimIntensity", "Intensity (0 = Off)", "強度 (0でOFF)",
                        "Rim light strength.", "リムライトの強さ。");
                    if (PropPositive(properties, "_RimIntensity"))
                    {
                        using (new EditorGUI.IndentLevelScope())
                            P(materialEditor, properties, "_RimPower", "Thickness", "太さ",
                                "Thickness of the rim band.", "リムの太さ。");
                    }
                }
            }

            // ===== Advanced =====
            if (Section("advanced", false, "Advanced Options", "高度な設定",
                "Standard rendering options.",
                "Unity標準のレンダリング設定です。"))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    materialEditor.RenderQueueField();
                    materialEditor.EnableInstancingField();
                    materialEditor.DoubleSidedGIField();
                }
            }
        }

        // ---------------------------------------------------------------------
        // UI ヘルパー
        // ---------------------------------------------------------------------

        void LoadPrefs()
        {
            if (_prefsLoaded) return;
            _jp = EditorPrefs.GetBool(LangKey, Application.systemLanguage == SystemLanguage.Japanese);
            _showHelp = EditorPrefs.GetBool(HelpKey, true);
            _useCustomUI = EditorPrefs.GetBool(CustomUIKey, true);
            _prefsLoaded = true;
        }

        // 上部の言語切替・説明表示トグル
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("EasyPBR / Doll", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                int uiMode = EditorGUILayout.Popup(_useCustomUI ? 0 : 1,
                    _jp ? new[] { "カスタム", "デフォルト" } : new[] { "Custom", "Default" },
                    GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _useCustomUI = uiMode == 0;
                    EditorPrefs.SetBool(CustomUIKey, _useCustomUI);
                }

                EditorGUI.BeginChangeCheck();
                int lang = EditorGUILayout.Popup(_jp ? 1 : 0, new[] { "English", "日本語" }, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _jp = lang == 1;
                    EditorPrefs.SetBool(LangKey, _jp);
                }

                EditorGUI.BeginChangeCheck();
                bool help = GUILayout.Toggle(_showHelp, _jp ? "説明" : "Help", "Button", GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    _showHelp = help;
                    EditorPrefs.SetBool(HelpKey, help);
                }
            }
        }

        // 折りたたみセクション。色付きのヘッダーバーで視認性を上げる。
        // バー全体クリックで開閉し、状態は EditorPrefs に保存。開いていて説明ONなら概要を表示。
        bool Section(string id, bool defaultOpen, string titleEn, string titleJp, string descEn, string descJp)
        {
            string key = KeyPrefix + "fold." + id;
            bool open = EditorPrefs.GetBool(key, defaultOpen);

            EditorGUILayout.Space(6);

            Rect rect = EditorGUILayout.GetControlRect(false, 24f);
            bool isPro = EditorGUIUtility.isProSkin;
            Color barColor = isPro ? new Color(0.26f, 0.26f, 0.28f) : new Color(0.74f, 0.74f, 0.76f);
            EditorGUI.DrawRect(rect, barColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.25f));

            var arrowRect = new Rect(rect.x + 6f, rect.y + 3f, 14f, 18f);
            EditorGUI.LabelField(arrowRect, open ? "\u25BC" : "\u25B6");
            var titleRect = new Rect(rect.x + 22f, rect.y + 3f, rect.width - 26f, 18f);
            EditorGUI.LabelField(titleRect, _jp ? titleJp : titleEn, EditorStyles.boldLabel);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                open = !open;
                EditorPrefs.SetBool(key, open);
                e.Use();
            }

            if (open && _showHelp)
                EditorGUILayout.HelpBox(_jp ? descJp : descEn, MessageType.None);

            return open;
        }

        // 小見出し（区切り線 + 太字ラベル）。セクション内のグループ分けを見やすくする。
        void SubHeader(string en, string jp)
        {
            EditorGUILayout.Space(4);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.LabelField(_jp ? jp : en, EditorStyles.miniBoldLabel);
        }

        // ローカライズ済み GUIContent（ラベル＋ツールチップ）を生成
        GUIContent Label(string lblEn, string lblJp, string tipEn, string tipJp)
        {
            return new GUIContent(_jp ? lblJp : lblEn, _jp ? tipJp : tipEn);
        }

        // プロパティが存在すれば、ローカライズラベルで標準ドロワー描画
        void P(MaterialEditor editor, MaterialProperty[] props, string name,
               string lblEn, string lblJp, string tipEn, string tipJp)
        {
            var p = FindProperty(name, props, false);
            if (p == null) return;
            editor.ShaderProperty(p, Label(lblEn, lblJp, tipEn, tipJp));
        }

        // ブルーノイズ未設定なのに Grain/Dither を使っている場合の警告
        void DrawBlueNoiseWarning(MaterialProperty[] props, MaterialProperty noise)
        {
            bool assigned = noise != null && noise.textureValue != null;
            if (assigned) return;

            bool usesNoise = PropPositive(props, "_GrainIntensity") || PropPositive(props, "_ShadowDither");
            if (!usesNoise) return;

            EditorGUILayout.HelpBox(
                _jp
                    ? "Blue Noise が未設定です。Grain / Shadow Dither はブルーノイズが無いと効きません。本パッケージ同梱の BlueNoise_RGB_256 を割り当ててください。"
                    : "No Blue Noise texture assigned. Grain / Shadow Dither will not work without one. Assign the bundled BlueNoise_RGB_256.",
                MessageType.Warning);
        }

        // ---------------------------------------------------------------------
        // 値ヘルパー（キーワード更新の遅延を避けるため float 値で判定）
        // ---------------------------------------------------------------------

        static bool PropOn(MaterialProperty[] props, string name)
        {
            var p = FindProperty(name, props, false);
            return p != null && p.floatValue > 0.5f;
        }

        static bool PropPositive(MaterialProperty[] props, string name)
        {
            var p = FindProperty(name, props, false);
            return p != null && p.floatValue > 0f;
        }

        static bool IsToon(MaterialProperty[] props)
        {
            var p = FindProperty("_ShadingStyle", props, false);
            return p != null && p.floatValue >= 0.5f; // KeywordEnum(Smooth=0, Toon=1)
        }
    }
}
