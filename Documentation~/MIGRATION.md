# EasyPBR for URP — マイグレーション（v0.3.7 → v0.4.0）

v0.4.0 は機能追加（曲率 / ベント法線 / ヘアフロー / クリアコート）と ForwardPass の内部リファクタを含む。**破壊的変更は SSS マップのみ**。それ以外の新機能はすべて既定 OFF で、既存マテリアルの見た目を変えない（一部の既定値変更を除く）。

## 破壊的変更

### `_SSSMask` → `_SSSMap`（プロパティ名・チャンネル構成の変更）

SSS を「厚みスカラ 1 枚」から「厚み＋透過方向の RGBA 1 枚」に統合した。

| | 旧（〜0.3.7） | 新（0.4.0〜） |
| :--- | :--- | :--- |
| プロパティ | `_SSSMask` | `_SSSMap` |
| チャンネル | R = 厚み | **RGB = 接線空間の透過方向 / A = 厚み** |
| ベイカー | `EasyPbrThicknessBaker` | `EasyPbrSssBaker` |
| シェーダー | `CalculateSSS(detailNormalWS, …)` | `CalculateSSS(sssTransWS, …)` |

**対応手順**
1. 旧 `_SSSMask` を参照していた箇所はもう存在しない。プロジェクト全体を `_SSSMask` で grep し、残骸が無いことを確認する。
2. **SSS は焼き直しが必要**。厚みのチャンネルが R から A へ移り、RGB に透過方向が入るため、旧 `_SSSMask` テクスチャはそのままでは使えない。Baking セクションの **SSS** で再ベイクする（→ [USAGE](USAGE.md)）。
3. 焼くと `_SSSMap` へ自動アサインされ、`_SSSIntensity` が 0 のとき自動で 1 に有効化される。

> 透過方向は接線空間なのでスキン変形に追従する。タンジェントの無いメッシュは方向が幾何法線へフォールバック（厚み A は有効）。

## 既定値の変更（新規マテリアルのみ影響）

| プロパティ | 旧既定 | 新既定 | 備考 |
| :--- | :---: | :---: | :--- |
| `_OcclusionStrength` | 1.0 | **0.0** | 新規マテリアルは AO 既定 OFF |
| `_CavityStrength` | 1.0 | **0.0** | 新規マテリアルは Cavity 既定 OFF |

既存マテリアルは**保存値を維持**するため見た目は変わらない。新規マテリアルでは、AO / Cavity をベイクすると自動で Strength が 1 に立つ（焼かない限り OFF）。

## 追加された新機能（すべて既定 OFF・非破壊）

焼く／有効化しない限り既存マテリアルに影響しない。

| 機能 | プロパティ（Strength 等） | 既定 | 有効化 |
| :--- | :--- | :---: | :--- |
| 曲率 | `_CurvatureMap` / `_CurvatureStrength` | 0 | ベイクで自動 1 |
| ベント法線 | `_BentNormalMap` / `_BentNormalStrength` | 0 | ベイクで自動 1 |
| ヘアフロー | `_HairFlowMap` / `_HairFlowStrength` | 0 | ベイクで自動 1 |
| クリアコート | `_ClearcoatMask` / `_ClearcoatStrength` ほか | 0 | 手動で Strength を上げる |

新規シェーダーキーワードは追加していない（すべて uniform 動的分岐）。よって**バリアント数は不変**で、SRP Batcher のバッチングに新たな分断要因は増えない（→ [VARIANTS](VARIANTS.md) / [SRP_BATCHER](SRP_BATCHER.md)）。

## ForwardPass リファクタ（公開 API 不変）

frag の責務分割と `CalculateSingleLight` の引数整理を行ったが、**マテリアルプロパティ・描画結果は不変**。利用者側の対応は不要。シェーダーを改造・流用している場合のみ、内部構成の変更（`DollSurfaceTypes.hlsl` / `DollSurface.hlsl` の追加、`CalculateSingleLight` の `DollLighting.hlsl` への移動とシグネチャ変更）に注意する（→ [ARCHITECTURE](ARCHITECTURE.md)）。

## バージョン表記

`_SSSMask` リネーム（破壊的）と内部アーキテクチャ変更を含むため **0.4.0** を推奨。0.3.x 系に留める場合は、本ページの破壊的変更（SSS の再ベイク要）を必ず明記すること。
