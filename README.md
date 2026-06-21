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

## インストール

### Package Manager（Git URL）

`Window > Package Manager > + > Add package from git URL...` に以下を入力する。

```
https://github.com/orig-uma/EasyPBR-URP.git
```

特定バージョンを指定する場合:

```
https://github.com/orig-uma/EasyPBR-URP.git#v0.3.4
```

### Embedded

`Packages/com.origuma.easypbr-urp` に配置すると embedded package として認識される。

## 動作環境

* Unity 6 (6000.x) 以降
* Universal RP 14.0 以降

## 機能

| 項目 | 内容 |
| :--- | :--- |
| Self Shadow（中核） | メインライト専用の高品質セルフシャドウ。Self Shadow Quality（Off / PCF / PCSS、コンタクトハードニング）、Receiver Normal Bias、Shadow Dither。落ち影（Shadow map）と陰影（NdotL）を分離合成し、マスク無しで顔がクリーンに出る |
| Shading Style | Toon / Smooth の切り替え |
| Base Core | Base Map、HSV 色調補正（Color Correction）、Detail Map（RGBA ブレンド）、Normal Map |
| Auto Face Shadow Fix | 落ち影/陰の独立した調整軸。マスク不要のプロシージャルマスクで正面を明るく戻す。Off 時の主要調整・PCF 時の追い込み用。既定 OFF |
| Specular | Dual-Lobe（Primary / Secondary）。Blinn-Phong / GGX（Schlick Fresnel・Smith 可視性）を切り替え |
| Anisotropic Highlight | 髪・シルク向け異方性ハイライト。2 バンド（主 / 副）、Strand パラメータ |
| MatCap | Add / Multiply |
| Dissolve | Edge Outer / Inner 2 色、Edge Width、Step Edge、Axis（None / WorldY / LocalY） |
| Glitter | マスク付きスパンコール。Iridescence、Sparsity、Base Reflection |
| Outline | 背面法線拡張。Alpha Clip / Dissolve 同期。Outline 専用 Stencil |
| Black Out | 最終色の暗転 |
| Optional | SSS / Rim Light / Peach Fuzz / Grain（既定 OFF、Intensity 0 で計算スキップ） |
| Emission | Emission Map、HDR Color、Intensity |
| Anti-Blowout | Diffuse / Specular の輝度上限、追加ライト合成（Add / Max） |
| Stencil | ForwardLit / Outline それぞれ独立設定 |

## Pass

| Pass | LightMode | 用途 |
| :--- | :--- | :--- |
| ForwardLit | UniversalForward | メイン描画 |
| ShadowCaster | ShadowCaster | 落ち影の生成 |
| Outline | SRPDefaultUnlit | 輪郭線 |

## シェーダーバリアント（キーワード）

バリアントを生むキーワードと、それを切り替えるマテリアルプロパティの対応。

### 機能キーワード（`shader_feature_local` — マテリアルの設定で切り替え）

| キーワード | 状態数 | 対応プロパティ（UI ラベル） | 対象パス |
| :--- | :---: | :--- | :--- |
| `_ALPHATEST_ON` | 2 | `_AlphaClip`（Alpha Clipping） | ForwardLit / ShadowCaster / Outline |
| `_SURFACE_TRANSPARENT` | 2 | `_SurfaceTransparent`（Alpha Blend (Transparent)） | ForwardLit |
| `_SHADINGSTYLE_TOON` | 2 | `_ShadingStyle`（Shading Style: Smooth / Toon） | ForwardLit |
| `_SPECULARMODEL_BLINNPHONG` / `_GGX` | 2 | `_SpecularModel`（Specular Model: BlinnPhong / GGX） | ForwardLit |
| `_SHADOWQUALITY_OFF` / `_PCF` / `_PCSS` | 3 | `_ShadowQuality`（Self Shadow Quality: Off / PCF / PCSS） | ForwardLit |
| `_DISSOLVE_ON` | 2 | `_UseDissolve`（Enable Dissolve） | ForwardLit / ShadowCaster / Outline |
| `_DISSOLVETYPE_NONE` / `_WORLDY` / `_LOCALY` | 3 | `_DissolveType`（Dissolve Axis: None / WorldY / LocalY） | ForwardLit / ShadowCaster / Outline |
| `_OUTLINE_ON` | 2 | `_UseOutline`（Enable Outline） | Outline |

> MatCap / Emission / Color Correction は keyword を廃止し、`_UseMatCap` / `_UseEmission` / `_UseColorCorrection`（Float）による `UNITY_BRANCH` の動的分岐にしている。無効時はテクスチャサンプルごとスキップされ、バリアントは増えない。

### システムキーワード（`multi_compile` — URP が常に全て生成）

| キーワードセット | 状態数 |
| :--- | :---: |
| `_MAIN_LIGHT_SHADOWS` / `_CASCADE` / `_SCREEN` | 4 |
| `_ADDITIONAL_LIGHTS_VERTEX` / `_ADDITIONAL_LIGHTS` | 3 |
| `_CLUSTER_LIGHT_LOOP` | 2 |
| `_ADDITIONAL_LIGHT_SHADOWS` | 2 |
| `_SHADOWS_SOFT` | 2 |
| `_CASTING_PUNCTUAL_LIGHT_SHADOW`（ShadowCaster） | 2 |

### バリアント数（理論上の最大）

| Pass | 機能（`shader_feature`） | システム（`multi_compile`） | 合計 |
| :--- | ---: | ---: | ---: |
| ForwardLit | 2·2·2·2·3·2·3 = **288** | 4·3·2·2·2 = **96** | **27,648** |
| ShadowCaster | 2·2·3 = **12** | 2 | **24** |
| Outline | 2·2·2·3 = **24** | — | **24** |
| **総計** | | | **27,696** |

> `shader_feature_local` はプロジェクト内のマテリアルが実際に使う組み合わせのみビルドに含まれる（1 マテリアルは機能キーワードの 1 通りを選ぶだけ）。一方 `multi_compile` は常に全展開されるため、**実ビルドのバリアント数は概ね「使用中の機能組み合わせ数 × システム 96（ForwardLit）」程度**に収まり、上の理論最大には達しない。

## ファイル構成・ライブラリ構成

ディレクトリ構成、ポリシー層と汎用ライブラリ（`Common/`）の分離方針、include 順、他シェーダーへの流用例など、技術的な設計仕様については [Documentation~/ARCHITECTURE.md](Documentation~/ARCHITECTURE.md) を参照。

## 使い方

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を指定する
2. Surface Options > Render Mode で Opaque / Cutout / Transparent を選択する
3. Base Map にアルベドテクスチャを割り当てる
4. Shading Style で Toon / Smooth を選択する

### セルフシャドウ品質（Self Shadow Quality）

落ち影の精度を 3 段階から選べる。いずれもメインライト（ディレクショナル）にのみ適用され、追加ライトの影は URP 標準のまま描画される。

| モード | 内容 | 想定用途 |
| :--- | :--- | :--- |
| Off | URP 標準サンプリング | モバイル / 最軽量 |
| PCF | スクリーン空間回転 Vogel ディスクによる連続ペナンブラ | PC / スタンドアロン VR の常用（推奨） |
| PCSS | ブロッカー探索によるコンタクトハードニング（接地は鋭く・遠方は柔らかく） | 据置機 / PC、寄りのカット |

* **Shadow Softness** はペナンブラ幅（PCF / PCSS のカーネル半径）を兼ねる。
* **Receiver Normal Bias** は受け側のノーマルオフセット量。縞状のシャドウアクネが出る場合に上げる。上げ過ぎると影が痩せる。
* Receiver Normal Bias を使う場合は、URP Asset 側の Light の **Normal Bias を 0〜0.3 程度に下げる**と二重バイアスによる影の浮き（ピーターパン）を防げる。
* PCSS のブロッカー探索は `_MainLightShadowmapTexture` を point sampler で読むため、環境によっては `sampler_PointClamp` の宣言が必要になる（`Common/URP/Shadow_HQ_URP.hlsl` 内のコメント参照）。

### インスペクター

`DollShaderGUI` により Custom / Default UI を切り替えられる。

| モード | 表示 |
| :--- | :--- |
| Custom | セクション分け UI |
| Default | Unity 標準のプロパティ一覧 |

### パラメータ（Custom UI）

セクションの並びと項目名は、Custom UI に表示されるラベルと一致させている。

| セクション | 主な項目 |
| :--- | :--- |
| Surface Options | Render Mode (Preset)、Cull、ZWrite、ZTest、Source Blend、Destination Blend、Alpha Blend (Transparent)、Alpha Clipping、Alpha Cutoff、Stencil（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Base Core | Base Map、Color Correction（Hue Shift / Saturation / Value Multiplier）、Detail Map、Normal Map |
| Light and Shadow | Shading Style、Shadow Color、Receive Shadow Mask、Receive Shadow Strength、Self Shadow Quality、Shadow Softness、Receiver Normal Bias、Shadow Edge Dither、Light Wrap、Toon Threshold / Toon Softness、Auto Face Shadow Fix（Front / Up Brightness、Mask Falloff）、Anti-Blowout（Diffuse Light Limit、Additional Light Blend） |
| Specular and Reflection | Specular Model（BlinnPhong / GGX）、Fresnel (F0)、Specular Mask、Primary（Color / Smoothness / Intensity / Primary Light Limit）、Secondary（Color / Smoothness / Intensity / Secondary Light Limit）、Anisotropic（Thickness / Position Offset / Angle / Strand Scale / Strand Strength / Strand Direction、Sub Highlight）、MatCap（Blend Mode / Texture / Tint / Intensity） |
| Outline | Enable Outline、Color、Width、Cutoff Shift、Masking (Stencil)（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Emission | Enable Emission、Emission Map & Color、Intensity |
| Optional Effects | Glitter（Mask / Color / Intensity / Density Scale / Dot Size / Normal Tilt / Sparsity / Iridescence / Iridescence Shift / Base Reflection）、SSS（Color / Intensity / Falloff / Distortion）、Peach Fuzz（Color / Intensity / Width）、Rim Light（Color / Intensity / Thickness）、Grain（Intensity / Scale） |
| Special Effects | Dissolve（Amount / Invert / Axis / Start Y / End Y / Noise / Edge Outer Color / Edge Inner Color / Edge Width / Step Edge）、Black Out |
| Blue Noise | Blue Noise Texture（影ディザ・グレイン共通） |
| Advanced Options | GPU Instancing、Double-Sided GI |

## ライセンス

[MIT License](LICENSE.md)

## 作者

Origuma — [https://github.com/orig-uma](https://github.com/orig-uma)
