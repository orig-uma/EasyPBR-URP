# EasyPBR for URP

シェーダー名: `Origuma/EasyPBR_URP/Doll`

複雑な照明環境でもキャラクターが自然に馴染む、PBRベースのURP向けキャラクターシェーダーです。
セットアップの容易さと、3Dライブ等での運用しやすさに特化して設計しています。

## 特徴

インスペクター上の数値制御による手軽なセットアップと、各種コントロールマップを用いた局所制御に対応しています。

* **物理ベースの質感と影:** GGXなどのBRDFを用いた光の反射と、PCFやPCSSによる高品質なセルフシャドウを搭載しています。
* **ハイブリッドなパラメータ制御:** 顔や瞳に落ちる不要なセルフシャドウの除外や、各エフェクトの強度をスライダーで設定可能です。各種コントロールマップ（マスク）を用いた局所的な適用もサポートしています。
* **ビルトインエフェクト:** 異方性ハイライト、グリッター（スパンコール）、ディゾルブ（消失）などを標準搭載しています。
* **運用サポート機能:** 多灯環境での白飛びを防ぐ輝度リミッター（Anti-Blowout）や、演出用の一括暗転（Black Out）機能を搭載しています。
* **SRP Batcher を意識した設計:** 3Dライブ等での多人数同時描画を前提に、**静的に分けた方が得な処理だけをバリアントに残し**、それ以外はキーワードをやめて動的分岐で吸収。マテリアル混在時もバッチが分断されにくくしています（→ [SRP_BATCHER](Documentation~/SRP_BATCHER.md)）。

## インストール

### Package Manager（Git URL）

`Window > Package Manager > + > Add package from git URL...` に以下を入力する。

```
https://github.com/orig-uma/EasyPBR-URP.git
```

特定バージョンを指定する場合:

```
https://github.com/orig-uma/EasyPBR-URP.git#v0.3.5
```

### Embedded

`Packages/com.origuma.easypbr-urp` に配置すると embedded package として認識される。

## 動作環境

* Unity 6 (6000.3) 以降
* Universal RP 17.3 以降
* Render Graph 有効（既定）。Render Graph Compatibility Mode ではアウトライン用の `DollOutlineFeature` が動作しません

## 機能

| 項目 | 内容 |
| :--- | :--- |
| Self Shadow（中核） | メインライト専用の高品質セルフシャドウ。Self Shadow Mode（Off / PCF (Tent) / PCF (Vogel) / PCSS、コンタクトハードニング）、Receiver Normal Bias、Shadow Dither、Shadow Cutoff Bias。落ち影（Shadow map）と陰影（NdotL）を分離合成し、マスク無しで顔がクリーンに出る |
| Shading Style | Toon / Smooth の切り替え |
| Base Core | Base Map、HSV 色調補正（Color Correction）、Detail Map（RGBA ブレンド）、Normal Map |
| Auto Face Shadow Fix | 落ち影/陰の独立した調整軸。マスク不要のプロシージャルマスクで正面を明るく戻す。Off 時の主要調整・PCF 時の追い込み用。既定 OFF |
| Specular | Dual-Lobe（Primary / Secondary）。Blinn-Phong / GGX（Schlick Fresnel・Smith 可視性）を切り替え |
| Anisotropic Highlight | 髪・シルク向け異方性ハイライト。2 バンド（主 / 副）、Strand パラメータ |
| MatCap | Add / Multiply |
| Dissolve | Edge Outer / Inner 2 色、Edge Width、Step Edge、Axis（None / WorldY / LocalY） |
| Glitter | マスク付きスパンコール。Iridescence、Sparsity、Base Reflection |
| Outline | 背面法線拡張。Alpha Clip / Dissolve 同期。Outline 専用 Stencil。描画には `DollOutlineFeature` が必要（→ [OUTLINE](Documentation~/OUTLINE.md)） |
| Black Out | 最終色の暗転 |
| Optional | SSS / Rim Light / Peach Fuzz / Grain（既定 OFF、Intensity 0 で計算スキップ） |
| Map Generator（Editor） | DCC 不要のマップベイク（AO / Cavity / Thickness / Face SDF 4ch）。詳細 → [ARCHITECTURE](Documentation~/ARCHITECTURE.md) |
| Emission | Emission Map、HDR Color、Intensity |
| Anti-Blowout | Diffuse / Specular の輝度上限、追加ライト合成（Add / Max） |
| Stencil | ForwardLit / Outline それぞれ独立設定 |

## 使い方

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を指定する
2. Surface Options > Render Mode で Opaque / Cutout / Transparent を選択する（既定 Opaque）
3. Base Map にアルベドテクスチャを割り当てる
4. Shading Style で Toon / Smooth を選択する

- **セルフシャドウ**のモード選択と推奨設定 → [SHADOWS](Documentation~/SHADOWS.md)
- **アウトライン**を使うには `DollOutlineFeature` を Renderer に追加 → [OUTLINE](Documentation~/OUTLINE.md)
- **インスペクター構成・全パラメータ・マップベイク** → [USAGE](Documentation~/USAGE.md)

## ドキュメント

| ドキュメント | 内容 |
| :--- | :--- |
| [USAGE](Documentation~/USAGE.md) | 使い方・インスペクター・パラメータ一覧 |
| [SHADOWS](Documentation~/SHADOWS.md) | 影モードの制御ガイドと推奨設定 |
| [OUTLINE](Documentation~/OUTLINE.md) | アウトラインの描画方式とセットアップ |
| [SRP_BATCHER](Documentation~/SRP_BATCHER.md) | SRP Batcher を効かせるための指針 |
| [VARIANTS](Documentation~/VARIANTS.md) | シェーダーバリアント（キーワード）一覧 |
| [ARCHITECTURE](Documentation~/ARCHITECTURE.md) | 内部構成・設計方針（Pass / ライブラリ構成） |

## ライセンス

[MIT License](LICENSE.md)

## 作者

Origuma — [https://github.com/orig-uma](https://github.com/orig-uma)
