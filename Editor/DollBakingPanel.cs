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
        private bool _aoOpen, _sdfOpen, _cavityOpen, _curvatureOpen, _thicknessOpen;

        private EasyPbrAoBaker.Settings        _aoSettings        = EasyPbrAoBaker.Default;
        private EasyPbrFaceSdfBaker.Settings   _sdfSettings       = EasyPbrFaceSdfBaker.Default;
        private EasyPbrCavityBaker.Settings    _cavitySettings    = EasyPbrCavityBaker.Default;
        private EasyPbrCurvatureBaker.Settings _curvatureSettings = EasyPbrCurvatureBaker.Default;
        private EasyPbrThicknessBaker.Settings _thicknessSettings = EasyPbrThicknessBaker.Default;

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

                        // --- Thickness (SSS) ---
                        _thicknessOpen = EditorGUILayout.Foldout(_thicknessOpen, jp ? "Thickness（SSS）（→ SSS Mask）" : "Thickness (SSS) (→ SSS Mask)", true);
                        if (_thicknessOpen)
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _thicknessSettings.resolution = ResField(_thicknessSettings.resolution);
                                _thicknessSettings.rayCount    = EditorGUILayout.IntSlider(kit.Label("Samples", "Inward rays per vertex", "内向きレイ数"), _thicknessSettings.rayCount, 16, 128);
                                _thicknessSettings.maxDistance = EditorGUILayout.Slider(kit.Label("Max Distance", "Thickness considered fully opaque at this depth (m)", "この深さで完全に厚い扱い(m)"), _thicknessSettings.maxDistance, 0.02f, 1.0f);
                                _thicknessSettings.intensity   = EditorGUILayout.Slider(kit.Label("Intensity", "Thin-area boost", "薄い部分の強調"), _thicknessSettings.intensity, 0.5f, 3.0f);
                                _thicknessSettings.smooth      = EditorGUILayout.IntSlider(kit.Label("Smooth", "Reduce facets", "ファセット低減"), _thicknessSettings.smooth, 0, 8);
                                _thicknessSettings.blur        = EditorGUILayout.IntSlider(kit.Label("Blur", "Texture blur", "ブラー"), _thicknessSettings.blur, 0, 4);
                                if (BakeButton(jp ? "Thickness をベイク" : "Bake Thickness"))
                                    BakeAllTargets(materialEditor, m => EasyPbrThicknessBaker.Bake(_bakeRoot, m, _thicknessSettings));
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
                                    jp ? "顔マテリアルで焼き、Light and Shadow の Face SDF Shadow を有効化して使う。R=右光/G=左光の2chで焼くので左右非対称の顔もOK。"
                                       : "Bake on the face material, then enable Face SDF Shadow under Light and Shadow. Bakes 2 channels (R=right, G=left) so asymmetric faces work.",
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
