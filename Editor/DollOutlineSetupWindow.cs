// =============================================================================
//  DollOutlineSetupWindow.cs
//  DollOutlineFeature を任意の UniversalRendererData に追加 / 削除 / 有効無効する
//  セットアップ用 EditorWindow。
//
//  Doll の Outline は独自 LightMode（"DollOutline"）のため、この Feature を
//  Renderer に追加しないと描画されない（その代わり ForwardLit のバッチングは良好）。
//  自動検出は環境差（複数 Renderer / 品質設定）で事故りやすいので、対象を
//  ObjectField で明示的に割り当てる方式にしている。
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Origuma.EasyPBR.URP.Editor
{
    public class DollOutlineSetupWindow : EditorWindow
    {
        private const string FeatureName = "Doll Outline";
        private ScriptableRendererData _rendererData;

        [MenuItem("Window/EasyPBR/Doll Outline Setup")]
        public static void Open()
        {
            var window = GetWindow<DollOutlineSetupWindow>(false, "Doll Outline Setup");
            window.minSize = new Vector2(360, 200);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Doll Outline Feature セットアップ", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Doll のアウトラインは独自パス（LightMode = \"DollOutline\"）で描画されます。" +
                "対象の Universal Renderer Data にこの Feature を追加してください。" +
                "追加しない場合、ForwardLit は素でバッチングされますがアウトラインは表示されません。",
                MessageType.Info);

            EditorGUILayout.Space(4);
            _rendererData = (ScriptableRendererData)EditorGUILayout.ObjectField(
                "Universal Renderer Data", _rendererData, typeof(ScriptableRendererData), false);

            if (_rendererData == null)
            {
                EditorGUILayout.HelpBox(
                    "URP Asset が参照している Renderer Data（UniversalRendererData）を割り当ててください。",
                    MessageType.None);
                return;
            }

            EditorGUILayout.Space(6);

            var existing = FindFeature(_rendererData);
            if (existing == null)
            {
                EditorGUILayout.LabelField("状態", "未追加");
                EditorGUILayout.Space(2);
                if (GUILayout.Button("Feature を追加", GUILayout.Height(28)))
                    AddFeature(_rendererData);
            }
            else
            {
                EditorGUILayout.LabelField("状態", existing.isActive ? "追加済み（有効）" : "追加済み（無効）");
                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var active = EditorGUILayout.ToggleLeft("有効", existing.isActive);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(existing, "Toggle Doll Outline Feature");
                        existing.SetActive(active);
                        EditorUtility.SetDirty(_rendererData);
                        AssetDatabase.SaveAssets();
                    }
                }

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Feature を削除", GUILayout.Height(24)))
                    RemoveFeature(_rendererData);
            }
        }

        // ------------------------------------------------------------------
        //  Feature の検索 / 追加 / 削除
        // ------------------------------------------------------------------
        private static DollOutlineFeature FindFeature(ScriptableRendererData data)
        {
            foreach (var f in data.rendererFeatures)
                if (f is DollOutlineFeature dof)
                    return dof;
            return null;
        }

        private static void AddFeature(ScriptableRendererData data)
        {
            var feature = ScriptableObject.CreateInstance<DollOutlineFeature>();
            feature.name = FeatureName;

            Undo.RegisterCreatedObjectUndo(feature, "Add Doll Outline Feature");
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(data);
            so.Update();
            var listProp = so.FindProperty("m_RendererFeatures");
            var mapProp  = so.FindProperty("m_RendererFeatureMap");

            int idx = listProp.arraySize;
            listProp.arraySize = idx + 1;
            listProp.GetArrayElementAtIndex(idx).objectReferenceValue = feature;
            mapProp.arraySize = idx + 1;
            mapProp.GetArrayElementAtIndex(idx).longValue = localId;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EasyPBR] Doll Outline Feature を追加しました: {AssetDatabase.GetAssetPath(data)}");
        }

        private static void RemoveFeature(ScriptableRendererData data)
        {
            var so = new SerializedObject(data);
            so.Update();
            var listProp = so.FindProperty("m_RendererFeatures");
            var mapProp  = so.FindProperty("m_RendererFeatureMap");

            var toDestroy = new List<Object>();
            for (int i = listProp.arraySize - 1; i >= 0; i--)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue is DollOutlineFeature feat)
                {
                    listProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    listProp.DeleteArrayElementAtIndex(i);
                    if (i < mapProp.arraySize) mapProp.DeleteArrayElementAtIndex(i);
                    toDestroy.Add(feat);
                }
            }

            so.ApplyModifiedProperties();
            foreach (var feat in toDestroy)
                Undo.DestroyObjectImmediate(feat);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EasyPBR] Doll Outline Feature を削除しました: {AssetDatabase.GetAssetPath(data)}");
        }
    }
}
