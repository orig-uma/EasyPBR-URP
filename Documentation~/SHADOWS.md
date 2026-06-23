# EasyPBR for URP — 影の制御ガイド

メインライト（ディレクショナル）専用の自己影モードの選び方をまとめる。追加ライトの影は URP 標準のまま。

`Self Shadow Mode`（`_ShadowMode`）は**シェーダーバリアントを生成する**（⚡）。使うモードは1〜2種に絞ると有利 → [SRP_BATCHER](SRP_BATCHER.md)。

影の品質は **シェーダー側のモード × URP Asset 側（シャドウ解像度・カスケード・距離）** の両輪で決まる。

## モードと推奨（優先度順）

| モード | 内容 | 推奨と前提 |
| :--- | :--- | :--- |
| **PCF (Tent)**（既定・最推奨） | 決定論的テント 5x5。ノイズなし | 配信・動画で破綻しない。**シャドウ解像度を上げ、カスケードを近距離側に寄せて**くっきり出すのが理想。これで十分なケースが多い |
| **PCF (Vogel)** | 回転 Vogel ディスク。可変ぼかし・微ノイズ | 解像度を**あまり確保できない**時。`Shadow Softness` でぼかして粗を隠しつつ調整 |
| **PCSS** | ブロッカー探索による接地硬化 | URP Asset で**最高品質の影を出せる状態**かつリアル志向の時。負荷は最大。実用上は Tent で足りることが多い |
| **Off** | URP 標準 | PCF でも負荷が厳しい時。`Shadow Edge Dither` と `Auto Face Shadow Fix` で追い込む |

## パラメータの効くモード

| パラメータ | 効くモード | 役割 |
| :--- | :--- | :--- |
| `Receiver Normal Bias` | Tent / Vogel / PCSS | アクネ（縞ノイズ）抑制。上げ過ぎると影が痩せる |
| `Shadow Softness` | **Vogel のみ** | ペナンブラ幅。Tent は固定カーネル、PCSS は接地硬化で自動決定のため不使用 |
| `Shadow Edge Dither` | Off のみ | エッジをブルーノイズでディザし段差を散らす |
| `Shadow Cutoff Bias` | 全モード（Alpha Clip 時） | 影を少し太めに落とし毛先のチラつきを抑える |

## URP Asset 側の合わせ込み

- **Tent / PCSS で近距離をくっきり出す**には、**カスケードシャドウで近距離側に解像度を寄せる**（第 1 カスケードの距離を狭める）こと。シェーダー側のモードだけ上げてもシャドウマップが粗いと頭打ち。
- `Receiver Normal Bias` を使う場合、Light の **Normal Bias を 0〜0.3 程度に下げる**と影の浮き（ピーターパン）を防げる。
- PCSS のブロッカー探索は `_MainLightShadowmapTexture` を point sampler で読む（環境により `sampler_PointClamp` 宣言が必要。`Common/URP/Shadow_HQ_URP.hlsl` 参照）。
