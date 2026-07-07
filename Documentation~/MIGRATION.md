# EasyPBR for URP — マイグレーション

## v0.5.x → v0.6.0

**破壊的変更あり**。共通基盤（Common HLSL / Baker 群 / ShaderGuiKit）を新パッケージ `com.origuma.easyshader-core` へ移管した。

### 必要な作業

1. **EasyShaderCore は自動で導入される**（`com.origuma.easyshader-core` >= 0.2.0）。EasyPBR 0.6.0 以降は、Package Manager で本パッケージを追加した直後（同一エディタセッション内・再起動不要）に Installer が Core を自動インストールするため、**手動インストールは不要**（git が必要）。手動で先に入れても問題ない。自動導入に失敗した場合のみ手動手順つきの案内ウィンドウが表示される
2. **ユーザーシェーダーが EasyPBR の Common を直接 include していた場合**、パスを修正する:

   ```hlsl
   // 旧
   #include "Packages/com.origuma.easypbr-urp/Runtime/Shaders/Common/Common.hlsl"
   // 新
   #include "Packages/com.origuma.easyshader-core/Runtime/Shaders/Common/Common.hlsl"
   ```

3. **ユーザーの Editor 拡張が Baker / ShaderGuiKit を参照していた場合**、名前空間を `Origuma.EasyPBR.URP.Editor` → `Origuma.EasyShaderCore.Editor` に変更し、asmdef の参照を `Origuma.EasyShaderCore.Editor` に切り替える（Baker 群は `public` になったため InternalsVisibleTo は不要）

Doll シェーダー・マテリアルへの影響はない（.meta / GUID は移管元のまま維持しており、テクスチャ・マテリアル参照は壊れない。Doll 内部の include は修正済み）。

## v0.4.0 → v0.5.0

**破壊的変更なし・移行作業不要**。プロパティの削除・リネーム・既定値変更は無い。新機能（2nd Shadow / Cast Shadow Color / Shadow Hue Shift / Skin Scatter / Fill Light / Light Conditioning / Indirect Light / Shade Normal / Toon Specular / Outline Albedo Blend）はすべて既定で素通し（OFF）で、既存マテリアルの見た目を変えない。新規シェーダーキーワードも無い（すべて uniform 動的分岐 → [VARIANTS](VARIANTS.md)）。

挙動が変わるのは以下の 2 点のみ:

- **間接光がわずかに明るくなる場合がある**: 従来は「直接光＋間接光」の合算に Diffuse Light Limit のクランプが掛かっており、直接光が上限（既定 1.0）に達すると間接光の寄与が丸ごと消えていた不具合を修正した。直接光が上限に達していて、かつシーンに環境光があるマテリアルでは、環境光のぶんだけ明るくなる。従来の見た目へ寄せるには Indirect Intensity（Light and Shadow > Indirect Light）を下げる。
- **スライダー上限の拡張（保存値は不変・UI のみ）**: `_ReflectionStrength` 1.0 → 2.0。あわせて Specular（Primary / Secondary）/ Rim / Peach Fuzz / MatCap のカラーが HDR 対応になった（既存の保存値はそのまま）。

## v0.3.7 → v0.4.0

v0.4.0 は機能追加（曲率 / ベント法線 / ヘアフロー / クリアコート）と ForwardPass の内部リファクタを含む。**破壊的変更は SSS マップのみ**。それ以外の新機能はすべて既定 OFF で、既存マテリアルの見た目を変えない（一部の既定値変更を除く）。

### 破壊的変更

#### `_SSSMask` → `_SSSMap`（プロパティ名・チャンネル構成の変更）

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

### 既定値の変更（新規マテリアルのみ影響）

| プロパティ | 旧既定 | 新既定 | 備考 |
| :--- | :---: | :---: | :--- |
| `_OcclusionStrength` | 1.0 | **0.0** | 新規マテリアルは AO 既定 OFF |
| `_CavityStrength` | 1.0 | **0.0** | 新規マテリアルは Cavity 既定 OFF |

既存マテリアルは**保存値を維持**するため見た目は変わらない。新規マテリアルでは、AO / Cavity をベイクすると自動で Strength が 1 に立つ（焼かない限り OFF）。

### 追加された新機能（すべて既定 OFF・非破壊）

焼く／有効化しない限り既存マテリアルに影響しない。

| 機能 | プロパティ（Strength 等） | 既定 | 有効化 |
| :--- | :--- | :---: | :--- |
| 曲率 | `_CurvatureMap` / `_CurvatureStrength` | 0 | ベイクで自動 1 |
| ベント法線 | `_BentNormalMap` / `_BentNormalStrength` | 0 | ベイクで自動 1 |
| ヘアフロー | `_HairFlowMap` / `_HairFlowStrength` | 0 | ベイクで自動 1 |
| クリアコート | `_ClearcoatMask` / `_ClearcoatStrength` ほか | 0 | 手動で Strength を上げる |

新規シェーダーキーワードは追加していない（すべて uniform 動的分岐）。よって**バリアント数は不変**で、SRP Batcher のバッチングに新たな分断要因は増えない（→ [VARIANTS](VARIANTS.md) / [SRP_BATCHER](SRP_BATCHER.md)）。

### ForwardPass リファクタ（公開 API 不変）

frag の責務分割と `CalculateSingleLight` の引数整理を行ったが、**マテリアルプロパティ・描画結果は不変**。利用者側の対応は不要。シェーダーを改造・流用している場合のみ、内部構成の変更（`DollSurfaceTypes.hlsl` / `DollSurface.hlsl` の追加、`CalculateSingleLight` の `DollLighting.hlsl` への移動とシグネチャ変更）に注意する（→ [ARCHITECTURE](ARCHITECTURE.md)）。

### バージョン表記

`_SSSMask` リネーム（破壊的）と内部アーキテクチャ変更を含むため **0.4.0** を推奨。0.3.x 系に留める場合は、本ページの破壊的変更（SSS の再ベイク要）を必ず明記すること。
