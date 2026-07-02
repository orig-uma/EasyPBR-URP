// =============================================================================
//  DollBakingPanel.cs
// -----------------------------------------------------------------------------
//  マテリアル Inspector の「Baking」セクションを描く自己完結パネル。
//  描画は ShaderGuiKit に、ベイク本体は各 Baker クラスに委譲する（薄い UI 層）。
//  運用が楽: 選択キャラから Root を自動補完 → 1 ボタンで焼いて自動アサイン。
//  Root 配下で同じマテリアルを使う複数メッシュ/サブメッシュを 1 枚に焼く。
//  マテリアル複数選択時は選択中の全マテリアルに対して実行。
// =============================================================================
using System;
using UnityEditor;
using UnityEngine;

namespace Origuma.EasyPBR.URP.Editor
{
    public class DollBakingPanel
    {
        private GameObject _bakeRoot;
        private bool _aoOpen, _sdfOpen, _cavityOpen, _curvatureOpen, _bentOpen, _hairFlowOpen, _sssOpen, _shadeNormalOpen;

        private EasyPbrAoBaker.Settings        _aoSettings        = EasyPbrAoBaker.Default;
        private EasyPbrFaceSdfBaker.Settings   _sdfSettings       = EasyPbrFaceSdfBaker.Default;
        private EasyPbrCavityBaker.Settings    _cavitySettings    = EasyPbrCavityBaker.Default;
        private EasyPbrCurvatureBaker.Settings _curvatureSettings = EasyPbrCurvatureBaker.Default;
        private EasyPbrBentNormalBaker.Settings _bentSettings      = EasyPbrBentNormalBaker.Default;
        private EasyPbrHairFlowBaker.Settings  _hairFlowSettings   = EasyPbrHairFlowBaker.Default;
        private EasyPbrSssBaker.Settings      _sssSettings       = EasyPbrSssBaker.Default;
        private EasyPbrShadeNormalBaker.Settings _shadeNormalSettings = EasyPbrShadeNormalBaker.Default;

        private static readonly int[] s_BakeResEn = { 512, 1024, 2048 };
        private static readonly string[] s_BakeResLabels = { "512", "1024", "2048" };

        private ShaderGuiKit _kit; // Draw 中だけ有効

        public void Draw(MaterialEditor materialEditor, ShaderGuiKit kit)
        {
            _kit = kit;
            var material = materialEditor.target as Material;
            if (material == null) return;
            bool jp = kit.Jp;

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!kit.Section("baking", false, "Baking (Map Generator)", "ベイク（マップ生成）", "", ""))
                    return;

                using (new EditorGUI.IndentLevelScope())
                {
                    if (_bakeRoot == null && Selection.activeGameObject != null)
                        _bakeRoot = Selection.activeGameObject;

                    _bakeRoot = (GameObject)EditorGUILayout.ObjectField(
                        kit.Label("Source Root",
                            "Root GameObject. ALL meshes under it that use this material are baked into one texture (handles a material shared across multiple meshes). Auto-filled from the Hierarchy selection",
                            "Root の GameObject。配下でこのマテリアルを使う全メッシュを1枚に焼く（1マテリアルを複数メッシュで共有していてもOK）。Hierarchy の選択から自動補完"),
                        _bakeRoot, typeof(GameObject), true);

                    if (_bakeRoot == null)
                        EditorGUILayout.HelpBox(
                            jp ? "Source Root を指定してください（Hierarchy でキャラを選択すると自動で入ります）。"
                               : "Assign a Source Root (selecting the character in the Hierarchy auto-fills it).",
                            MessageType.Info);

                    int matCount = materialEditor.targets.Length;
                    if (matCount > 1)
                        EditorGUILayout.HelpBox(
                            jp ? $"{matCount} 個のマテリアルを選択中。ベイクは選択中の全マテリアルに対して実行されます。"
                               : $"{matCount} materials selected. Baking runs for ALL of them.",
                            MessageType.Info);

                    using (new EditorGUI.DisabledScope(_bakeRoot == null))
                    {
                        // --- Bake All（共通マップの一括ベイク）---
                        EditorGUILayout.Space(2);
                        if (GUILayout.Button(jp ? "共通マップを一括ベイク（AO / Bent / Shade Normal / Cavity / Curvature / SSS）"
                                                : "Bake All Common Maps (AO / Bent / Shade Normal / Cavity / Curvature / SSS)",
                                GUILayout.Height(28)))
                            BakeAllTargets(materialEditor, m =>
                                EasyPbrAoBaker.Bake(_bakeRoot, m, _aoSettings)
                                & EasyPbrBentNormalBaker.Bake(_bakeRoot, m, _bentSettings)
                                & EasyPbrShadeNormalBaker.Bake(_bakeRoot, m, _shadeNormalSettings)
                                & EasyPbrCavityBaker.Bake(_bakeRoot, m, _cavitySettings)
                                & EasyPbrCurvatureBaker.Bake(_bakeRoot, m, _curvatureSettings)
                                & EasyPbrSssBaker.Bake(_bakeRoot, m, _sssSettings));
                        EditorGUILayout.HelpBox(
                            jp ? "各 Foldout の現在の設定で 6 種を順に焼く。Hair Flow（髪）と Face SDF（顔）は対象マテリアルで個別に焼くこと。"
                               : "Bakes the 6 common maps in order using each foldout's current settings. Bake Hair Flow (hair) and Face SDF (face) individually on those materials.",
                            MessageType.None);
                        EditorGUILayout.Space(2);

                        // --- Ambient Occlusion ---
                        _aoOpen = EditorGUILayout.Foldout(_aoOpen, jp ? "Ambient Occlusion（→ Occlusion Map）" : "Ambient Occlusion (→ Occlusion Map)", true);
                        if (_aoOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _aoSettings.resolution = ResField(_aoSettings.resolution);
                                _aoSettings.rayCount    = EditorGUILayout.IntSlider(kit.Label("Samples", "Rays per vertex", "頂点あたりのレイ数"), _aoSettings.rayCount, 16, 256);
                                _aoSettings.maxDistance = EditorGUILayout.Slider(kit.Label("Max Distance", "Occlusion reach (m). Smaller = local cavity", "遮蔽の届く距離(m)。小さいほど局所的"), _aoSettings.maxDistance, 0.02f, 3.0f);
                                _aoSettings.intensity   = EditorGUILayout.Slider(kit.Label("Intensity", "AO strength", "AO の強さ"), _aoSettings.intensity, 0.1f, 2.0f);
                                _aoSettings.enclosedCutoff = EditorGUILayout.Slider(kit.Label("Ignore Enclosed", "Snap near-fully-occluded faces to white (removes black patches)", "ほぼ完全遮蔽の面を白へ（黒つぶれ除去）"), _aoSettings.enclosedCutoff, 0.5f, 1.0f);
                                _aoSettings.floor       = EditorGUILayout.Slider(kit.Label("Floor", "Lift dark areas", "暗部の下限"), _aoSettings.floor, 0.0f, 0.5f);
                                _aoSettings.smooth      = EditorGUILayout.IntSlider(kit.Label("Smooth", "Reduce facets", "ファセット低減"), _aoSettings.smooth, 0, 8);
                                _aoSettings.blur        = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _aoSettings.blur, 0, 4);
                                if (BakeButton(jp ? "AO をベイク" : "Bake AO"))
                                    BakeAllTargets(materialEditor, m => EasyPbrAoBaker.Bake(_bakeRoot, m, _aoSettings));
                            }

                        // --- Bent Normal ---
                        _bentOpen = EditorGUILayout.Foldout(_bentOpen, jp ? "Bent Normal（→ Bent Normal Map）" : "Bent Normal (→ Bent Normal Map)", true);
                        if (_bentOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _bentSettings.resolution  = ResField(_bentSettings.resolution);
                                _bentSettings.rayCount    = EditorGUILayout.IntSlider(kit.Label("Samples", "Rays per vertex", "頂点あたりのレイ数"), _bentSettings.rayCount, 16, 256);
                                _bentSettings.maxDistance = EditorGUILayout.Slider(kit.Label("Max Distance", "Occlusion reach (m)", "遮蔽の届く距離(m)"), _bentSettings.maxDistance, 0.02f, 3.0f);
                                _bentSettings.strength    = EditorGUILayout.Slider(kit.Label("Strength", "0=geometric normal, 1=fully open", "0=幾何法線 / 1=開いた方向へ"), _bentSettings.strength, 0.0f, 1.0f);
                                _bentSettings.smooth      = EditorGUILayout.IntSlider(kit.Label("Smooth", "Reduce facets", "ファセット低減"), _bentSettings.smooth, 0, 8);
                                _bentSettings.blur        = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _bentSettings.blur, 0, 4);
                                if (BakeButton(jp ? "Bent Normal をベイク" : "Bake Bent Normal"))
                                    BakeAllTargets(materialEditor, m => EasyPbrBentNormalBaker.Bake(_bakeRoot, m, _bentSettings));
                                EditorGUILayout.HelpBox(
                                    jp ? "接線空間で焼く(スキン追従)。アンビエント/SH を幾何法線の代わりにこの方向で評価すると、くぼみの陰が方向まで正しくなる。AO(強度)と併用。タンジェント必須。"
                                       : "Tangent space (follows skinning). Evaluate ambient/SH along this instead of the geometric normal. Pairs with AO. Requires tangents.",
                                    MessageType.None);
                            }

                        // --- Shade Normal ---
                        _shadeNormalOpen = EditorGUILayout.Foldout(_shadeNormalOpen, jp ? "Shade Normal（→ Shade Normal Map）" : "Shade Normal (→ Shade Normal Map)", true);
                        if (_shadeNormalOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _shadeNormalSettings.resolution = ResField(_shadeNormalSettings.resolution);
                                _shadeNormalSettings.smoothIterations = EditorGUILayout.IntSlider(kit.Label("Smooth Normals", "Laplacian smoothing iterations on welded vertex normals. Higher = softer, cleaner shade gradation", "位置溶接した頂点法線のラプラシアン平滑化回数。高いほど陰のグラデーションが滑らかで綺麗に"), _shadeNormalSettings.smoothIterations, 0, 64);
                                _shadeNormalSettings.blur = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _shadeNormalSettings.blur, 0, 4);
                                if (BakeButton(jp ? "Shade Normal をベイク" : "Bake Shade Normal"))
                                    BakeAllTargets(materialEditor, m => EasyPbrShadeNormalBaker.Bake(_bakeRoot, m, _shadeNormalSettings));
                                EditorGUILayout.HelpBox(
                                    jp ? "平滑化した法線を接線空間に焼き、拡散の陰ランプだけをこの法線で駆動する（スペキュラ・リム・SSSはディテール法線のまま）。シワ・ファセット起伏で陰のグラデーションが汚く割れるのを防ぎ、陰の輪郭を一本の綺麗な曲線として通す。UV継ぎ目・硬エッジは位置溶接して平滑化するので継ぎ目で陰が割れない。レイ不要で高速。服・髪で特に効く。"
                                       : "Bakes smoothed normals into tangent space; only the diffuse shade ramp uses them (specular, rim and SSS keep the detail normal). Stops wrinkles and facets from breaking the shade gradation, so the shade boundary reads as one clean curve. Vertices are position-welded before smoothing so UV seams and hard edges don't split the shade. No rays, fast. Most visible on clothes and hair.",
                                    MessageType.None);
                            }

                        // --- Cavity ---
                        _cavityOpen = EditorGUILayout.Foldout(_cavityOpen, jp ? "Cavity（→ Cavity Map）" : "Cavity (→ Cavity Map)", true);
                        if (_cavityOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _cavitySettings.resolution = ResField(_cavitySettings.resolution);
                                _cavitySettings.intensity  = EditorGUILayout.Slider(kit.Label("Intensity", "Crease darkening strength", "くぼみの暗化の強さ"), _cavitySettings.intensity, 0.5f, 20.0f);
                                _cavitySettings.smooth     = EditorGUILayout.IntSlider(kit.Label("Smooth", "Reduce facets", "ファセット低減"), _cavitySettings.smooth, 0, 8);
                                _cavitySettings.blur       = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _cavitySettings.blur, 0, 4);
                                if (BakeButton(jp ? "Cavity をベイク" : "Bake Cavity"))
                                    BakeAllTargets(materialEditor, m => EasyPbrCavityBaker.Bake(_bakeRoot, m, _cavitySettings));
                            }

                        // --- Curvature ---
                        _curvatureOpen = EditorGUILayout.Foldout(_curvatureOpen, jp ? "Curvature（→ Curvature Map）" : "Curvature (→ Curvature Map)", true);
                        if (_curvatureOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _curvatureSettings.resolution = ResField(_curvatureSettings.resolution);
                                _curvatureSettings.intensity  = EditorGUILayout.Slider(kit.Label("Intensity", "Convex/concave contrast", "凹凸コントラストの強さ"), _curvatureSettings.intensity, 0.5f, 20.0f);
                                _curvatureSettings.smooth     = EditorGUILayout.IntSlider(kit.Label("Smooth", "Reduce facets", "ファセット低減"), _curvatureSettings.smooth, 0, 8);
                                _curvatureSettings.blur       = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _curvatureSettings.blur, 0, 4);
                                if (BakeButton(jp ? "Curvature をベイク" : "Bake Curvature"))
                                    BakeAllTargets(materialEditor, m => EasyPbrCurvatureBaker.Bake(_bakeRoot, m, _curvatureSettings));
                                EditorGUILayout.HelpBox(
                                    jp ? "0.5=平坦 / 明=凸(稜線) / 暗=凹(くぼみ)。1枚で稜線・くぼみ両方のマスクが取れる。Cavity の上位互換だが併用も可。"
                                       : "0.5=flat / bright=convex (ridge) / dark=concave (cavity). One map gives both masks. Supersedes Cavity but can coexist.",
                                    MessageType.None);
                            }

                        // --- SSS ---
                        _sssOpen = EditorGUILayout.Foldout(_sssOpen, jp ? "SSS（→ SSS Map）" : "SSS (→ SSS Map)", true);
                        if (_sssOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _sssSettings.resolution   = ResField(_sssSettings.resolution);
                                _sssSettings.rayCount    = EditorGUILayout.IntSlider(kit.Label("Samples", "Inward rays per vertex", "内向きレイ数"), _sssSettings.rayCount, 16, 128);
                                _sssSettings.maxDistance = EditorGUILayout.Slider(kit.Label("Max Distance", "Thickness considered fully opaque at this depth (m)", "この深さで完全に厚い扱い(m)"), _sssSettings.maxDistance, 0.02f, 1.0f);
                                _sssSettings.intensity   = EditorGUILayout.Slider(kit.Label("Intensity", "Thin-area boost", "薄い部分の強調"), _sssSettings.intensity, 0.5f, 3.0f);
                                _sssSettings.smooth      = EditorGUILayout.IntSlider(kit.Label("Smooth", "Reduce facets", "ファセット低減"), _sssSettings.smooth, 0, 8);
                                _sssSettings.blur        = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _sssSettings.blur, 0, 4);
                                if (BakeButton(jp ? "SSS をベイク" : "Bake SSS"))
                                    BakeAllTargets(materialEditor, m => EasyPbrSssBaker.Bake(_bakeRoot, m, _sssSettings));
                            }

                        // --- Hair Flow ---
                        _hairFlowOpen = EditorGUILayout.Foldout(_hairFlowOpen, jp ? "Hair Flow（→ Hair Flow Map・髪マテリアル）" : "Hair Flow (→ Hair Flow Map, hair material)", true);
                        if (_hairFlowOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _hairFlowSettings.resolution   = ResField(_hairFlowSettings.resolution);
                                _hairFlowSettings.useCurvature = EditorGUILayout.Toggle(kit.Label("Curvature Mode", "Use min-normal-change dir (sculpted hair). Off=longest-edge (hair cards)", "最小法線変化方向（彫刻髪）。OFF=最長エッジ（カード髪）"), _hairFlowSettings.useCurvature);
                                _hairFlowSettings.smooth       = EditorGUILayout.IntSlider(kit.Label("Smooth", "Orientation smoothing", "毛流れ平滑化"), _hairFlowSettings.smooth, 0, 8);
                                _hairFlowSettings.blur         = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _hairFlowSettings.blur, 0, 4);
                                if (BakeButton(jp ? "Hair Flow をベイク" : "Bake Hair Flow"))
                                    BakeAllTargets(materialEditor, m => EasyPbrHairFlowBaker.Bake(_bakeRoot, m, _hairFlowSettings));
                                EditorGUILayout.HelpBox(
                                    jp ? "髪マテリアルで焼く。形状から毛流れを推定し、毛束ごと・ミラーUV・流れに沿わないUVでも天使の輪を安定させる。倍角エンコードなのでミラー継ぎ目で割れない。_AnisoAngle は全体オフセットとして併用可。"
                                       : "Bake on hair material. Shape-based flow stabilizes the highlight across chunks/mirrored/packed UVs. Double-angle encoded (no mirror-seam break). _AnisoAngle still works as a global offset.",
                                    MessageType.None);
                            }

                        // --- Face SDF Shadow ---
                        _sdfOpen = EditorGUILayout.Foldout(_sdfOpen, jp ? "Face SDF Shadow（→ Face SDF Map・顔マテリアル）" : "Face SDF Shadow (→ Face SDF Map, face material)", true);
                        if (_sdfOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _sdfSettings.resolution = ResField(_sdfSettings.resolution);
                                _sdfSettings.flipForward   = EditorGUILayout.Toggle(kit.Label("Flip Forward", "Enable if the face looks along -Z", "顔が-Z向きならON"), _sdfSettings.flipForward);
                                _sdfSettings.angleSteps    = EditorGUILayout.IntSlider(kit.Label("Angle Steps", "Sweep resolution", "スイープ分割数"), _sdfSettings.angleSteps, 30, 180);
                                _sdfSettings.useCastShadow = EditorGUILayout.Toggle(kit.Label("Cast Shadow", "Include nose/brow cast shadows", "鼻・眉の落ち影を含める"), _sdfSettings.useCastShadow);
                                using (new EditorGUI.DisabledScope(!_sdfSettings.useCastShadow))
                                    _sdfSettings.castDistance = EditorGUILayout.Slider(kit.Label("Cast Distance", "Cast ray length (m)", "落ち影レイ長(m)"), _sdfSettings.castDistance, 0.02f, 0.5f);
                                _sdfSettings.smooth = EditorGUILayout.IntSlider(kit.Label("Smooth", "Vertex smoothing", "頂点平滑化"), _sdfSettings.smooth, 0, 6);
                                _sdfSettings.blur   = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _sdfSettings.blur, 0, 4);
                                if (BakeButton(jp ? "顔 SDF をベイク" : "Bake Face SDF"))
                                    BakeAllTargets(materialEditor, m => EasyPbrFaceSdfBaker.Bake(_bakeRoot, m, _sdfSettings));
                                EditorGUILayout.HelpBox(
                                    jp ? "顔マテリアルで焼き、Light and Shadow の Face SDF Shadow を有効化して使う。R/G/B/A=右/左/上/下の4chで焼くので左右非対称の顔もOK。"
                                       : "Bake on the face material, then enable Face SDF Shadow under Light and Shadow. Bakes 4 channels (R/G/B/A = right/left/up/down) so asymmetric faces work.",
                                    MessageType.None);
                            }

                    }

                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(
                        jp ? "生成 PNG はマテリアル隣の Baked/ に保存され、該当スロットへ自動アサインされます（非破壊・再ベイク可）。書き込みは編集中マテリアルのサブメッシュのみ。"
                           : "PNGs are saved next to the material in Baked/ and auto-assigned to the matching slot (non-destructive, re-bakeable). Only the edited material's submeshes are written.",
                        MessageType.None);
                }
            }
        }

        private static bool BakeButton(string label)
        {
            EditorGUILayout.Space(2);
            return GUILayout.Button(label, GUILayout.Height(24));
        }

        // 解像度ポップアップ（512 / 1024 / 2048）。4 ベイカー共通。
        private int ResField(int current)
        {
            int idx = Mathf.Max(0, Array.IndexOf(s_BakeResEn, current));
            idx = EditorGUILayout.Popup(_kit.Label("Resolution", "Output texture size", "出力テクスチャの解像度"),
                idx, s_BakeResLabels);
            return s_BakeResEn[Mathf.Clamp(idx, 0, s_BakeResEn.Length - 1)];
        }

        // 選択中のマテリアル全部にベイクを実行（マルチ編集対応）。
        private void BakeAllTargets(MaterialEditor editor, Func<Material, bool> bakeOne)
        {
            int ok = 0, total = 0;
            foreach (var o in editor.targets)
            {
                if (!(o is Material m)) continue;
                total++;
                if (bakeOne(m)) ok++;
            }
            if (total > 1)
                EditorUtility.DisplayDialog("EasyPBR Baker",
                    _kit.Jp ? $"{total} 個のマテリアル中 {ok} 個にベイクしました（詳細は Console）。"
                            : $"Baked {ok} of {total} selected materials (see Console).", "OK");
        }
    }
}
