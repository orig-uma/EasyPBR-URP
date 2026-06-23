# EasyPBR for URP — アウトラインの設定

アウトライン（背面法線押し出し）の描画方式と、有効化に必要なセットアップをまとめる。

## なぜ RendererFeature が必要か

URP の不透明描画は `UniversalForward` と `SRPDefaultUnlit` を**同じ描画パスでまとめて処理**する。アウトラインを `SRPDefaultUnlit` に置くと、オブジェクト単位で `[本体][輪郭][本体][輪郭]…` と交互に描かれ、毎回別パスになるため **ForwardLit が SRP Batcher でまとまらない**（アウトライン未使用のマテリアルでも、パスが存在するだけで分断が起きる）。

そこで本シェーダーの Outline パスは独自 LightMode タグ **`DollOutline`** を持つ。URP は既定でこれを描かないため：

- **ForwardLit は素でバッチングされる**（アウトラインに邪魔されない）。
- アウトラインは `DollOutlineFeature` が**別の描画パスとしてまとめて**描くので、アウトライン同士もバッチされる。

トレードオフとして、アウトラインの表示には Renderer への Feature 追加が必要になる。詳細な背景は [SRP_BATCHER](SRP_BATCHER.md)。

## セットアップ

1. メニューから **`Window > EasyPBR > Doll Outline Setup`** を開く。
2. **Universal Renderer Data**（URP Asset が参照している Renderer）を割り当てる。
3. **「Feature を追加」** を押す。

これでアウトライン（`_UseOutline` が ON のマテリアル）が描画される。マテリアル側の Inspector でアウトラインを有効にすると、同じ Window への導線（ボタン）も表示される。

- 一時的に切りたいときは Window の **「有効」トグル**で無効化（削除せず温存）。
- 不要になったら **「Feature を削除」** で除去。
- 描画タイミングは Feature の `Injection Point`（既定 `AfterRenderingOpaques`）で調整できる。

## 注意

- **Render Graph 前提**（URP 17 / Unity 6）。Render Graph Compatibility Mode では動作しない。
- 複数の Renderer Data（品質設定ごと等）を使う場合は、**それぞれに対して**追加する。
- Feature を追加していないと、`_UseOutline` を ON にしてもアウトラインは出ない（本体は正常に描画される）。
