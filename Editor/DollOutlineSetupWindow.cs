// =============================================================================
//  DollOutlineSetupWindow.cs
//  DollOutlineFeature を UniversalRendererData に追加 / 削除 / 有効無効する
//  セットアップ用 EditorWindow。
//
//  Doll の Outline は独自 LightMode（"DollOutline"）のため、この Feature を
//  Renderer に追加しないと描画されない（その代わり ForwardLit のバッチングは良好）。
//  描画とロジックは EasyShaderCore の FeatureSetupWindowBase / FeatureSetup に
//  委譲（アクティブな URP Asset からの Renderer Data 自動収集・手動 ObjectField・
//  Render Graph Compatibility Mode 警告に対応）。ここではタイトルと Feature
//  エントリの宣言のみを行う。
// =============================================================================
using UnityEditor;
using UnityEngine;
using Origuma.EasyShaderCore.Editor;

namespace Origuma.EasyPBR.URP.Editor
{
    public class DollOutlineSetupWindow : FeatureSetupWindowBase
    {
        [MenuItem("Window/Origuma/Doll Outline Setup")]
        public static void Open()
        {
            var window = GetWindow<DollOutlineSetupWindow>(false, "Doll Outline Setup");
            window.minSize = new Vector2(420, 260);
            window.Show();
        }

        protected override string HeaderLabel => "Doll Outline Feature セットアップ";

        protected override string Description =>
            "Doll のアウトラインは独自パス（LightMode = \"DollOutline\"）で描画されます。" +
            "対象の Universal Renderer Data にこの Feature を追加してください。" +
            "追加しない場合、ForwardLit は素でバッチングされますがアウトラインは表示されません。";

        protected override FeatureEntry[] Entries => s_Entries;

        private static readonly FeatureEntry[] s_Entries =
        {
            new FeatureEntry(typeof(DollOutlineFeature), "Doll Outline",
                "アウトライン描画に必須"),
        };
    }
}
