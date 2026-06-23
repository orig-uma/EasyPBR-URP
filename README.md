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
| DepthOnly | DepthOnly | Depth Prepass / Depth Priming、Forward+ の深度生成 |
| DepthNormals | DepthNormals | Forward+ の Depth Normals Prepass、SSAO / Decal 用の法線生成 |
| Outline | DollOutline | 輪郭線（描画には `DollOutlineFeature` が必要） |

> Outline は独自 LightMode タグ `DollOutline` を使う。URP の既定の不透明描画に含まれないため、ForwardLit と交互描画されず **ForwardLit のバッチングを阻害しない**。描画には `DollOutlineFeature`（RendererFeature）を Renderer に追加する。設定・理由は [Documentation~/OUTLINE.md](Documentation~/OUTLINE.md) を参照。

## シェーダーバリアント（キーワード）

バリアントを生むキーワードと、それを切り替えるマテリアルプロパティの対応。

### 機能キーワード（`shader_feature_local` — マテリアルの設定で切り替え）

| キーワード | 状態数 | 対応プロパティ（UI ラベル） | 対象パス |
| :--- | :---: | :--- | :--- |
| `_ALPHATEST_ON` | 2 | `_AlphaClip`（Alpha Clipping） | ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Outline |
| `_SHADOWMODE_OFF` / `_TENTPCF` / `_VOGELPCF` / `_PCSS` | 4 | `_ShadowMode`（Self Shadow Mode: Off / PCF (Tent) / PCF (Vogel) / PCSS） | ForwardLit |
| `_DISSOLVE_ON` | 2 | `_UseDissolve`（Enable Dissolve） | ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Outline |
| `_DISSOLVETYPE_NONE` / `_WORLDY` / `_LOCALY` | 3 | `_DissolveType`（Dissolve Axis: None / WorldY / LocalY） | ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Outline |
| `_OUTLINE_ON` | 2 | `_UseOutline`（Enable Outline） | Outline |

> MatCap / Emission / Color Correction は keyword を廃止し、`_UseMatCap` / `_UseEmission` / `_UseColorCorrection`（Float）による `UNITY_BRANCH` の動的分岐にしている。無効時はテクスチャサンプルごとスキップされ、バリアントは増えない。
>
> Shading Style（Smooth / Toon）/ Specular Model（BlinnPhong / GGX）/ Alpha Blend（Transparent）も同様に keyword を廃止し、`_ShadingStyle` / `_SpecularModel` の uniform 動的分岐、および Alpha 出力の常時化に移行した（0.3.5）。これらはマテリアル間で値が割れやすく、keyword 分岐のままだと**同時描画時に SRP Batcher のバッチが分断される**ため。分岐自体は軽量（threshold vs ramp / 関数選択 / 1 行）なので、バリアント削減のメリットが上回る。

### システムキーワード（`multi_compile` — URP が常に全て生成）

| キーワードセット | 状態数 |
| :--- | :---: |
| `_MAIN_LIGHT_SHADOWS` / `_CASCADE` / `_SCREEN` | 4 |
| `_ADDITIONAL_LIGHTS_VERTEX` / `_ADDITIONAL_LIGHTS` | 3 |
| `_FORWARD_PLUS` / `_CLUSTER_LIGHT_LOOP` | 3 |
| `_ADDITIONAL_LIGHT_SHADOWS` | 2 |
| `_SHADOWS_SOFT` | 2 |
| `_CASTING_PUNCTUAL_LIGHT_SHADOW`（ShadowCaster） | 2 |

### バリアント数（理論上の最大）

| Pass | 機能（`shader_feature`） | システム（`multi_compile`） | 合計 |
| :--- | ---: | ---: | ---: |
| ForwardLit | 2·4·2·3 = **48** | 4·3·3·2·2 = **144** | **6,912** |
| ShadowCaster | 2·2·3 = **12** | 2 | **24** |
| DepthOnly | 2·2·3 = **12** | — | **12** |
| DepthNormals | 2·2·3 = **12** | — | **12** |
| Outline | 2·2·2·3 = **24** | — | **24** |
| **総計** | | | **6,984** |

> `shader_feature_local` はプロジェクト内のマテリアルが実際に使う組み合わせのみビルドに含まれる（1 マテリアルは機能キーワードの 1 通りを選ぶだけ）。一方 `multi_compile` は常に全展開されるため、**実ビルドのバリアント数は概ね「使用中の機能組み合わせ数 × システム 144（ForwardLit）」程度**に収まり、上の理論最大には達しない。
>
> バリアントを生成するプロパティは、カスタム Inspector 上で **⚡ マーク**で明示される。これらの値が同時描画されるマテリアル間で割れると SRP Batcher のバッチが分断される。バッチングを効かせる指針は [Documentation~/SRP_BATCHER.md](Documentation~/SRP_BATCHER.md) を参照。

## ファイル構成・ライブラリ構成

ディレクトリ構成、ポリシー層と汎用ライブラリ（`Common/`）の分離方針、include 順、他シェーダーへの流用例など、技術的な設計仕様については [Documentation~/ARCHITECTURE.md](Documentation~/ARCHITECTURE.md) を参照。

## ドキュメント

| ドキュメント | 内容 |
| :--- | :--- |
| [Documentation~/ARCHITECTURE.md](Documentation~/ARCHITECTURE.md) | 内部構成・設計方針 |
| [Documentation~/SHADOWS.md](Documentation~/SHADOWS.md) | 影モードの制御ガイドと推奨設定 |
| [Documentation~/SRP_BATCHER.md](Documentation~/SRP_BATCHER.md) | SRP Batcher を効かせるための指針 |
| [Documentation~/OUTLINE.md](Documentation~/OUTLINE.md) | アウトラインの描画方式とセットアップ |

## 使い方

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を指定する
2. Surface Options > Render Mode で Opaque / Cutout / Transparent を選択する
3. Base Map にアルベドテクスチャを割り当てる
4. Shading Style で Toon / Smooth を選択する

### セルフシャドウモード（Self Shadow Mode）

落ち影の精度を 4 段階から選べる。いずれもメインライト（ディレクショナル）にのみ適用され、追加ライトの影は URP 標準のまま描画される。モードごとの推奨設定・URP Asset 側の合わせ込みは [Documentation~/SHADOWS.md](Documentation~/SHADOWS.md) を参照。

| モード | 内容 | 想定用途 |
| :--- | :--- | :--- |
| Off | URP 標準サンプリング | モバイル / 最軽量 |
| PCF (Tent) | 決定論的なテント 5x5（ノイズなし・9 フェッチ） | ライブ配信 / 動画。フレーム間でちらつかせたくない用途（既定） |
| PCF (Vogel) | スクリーン空間回転 Vogel ディスクによる連続ペナンブラ（可変半径・微ノイズ） | PC / スタンドアロン VR、広めのソフト影 |
| PCSS | ブロッカー探索によるコンタクトハードニング（接地は鋭く・遠方は柔らかく） | 据置機 / PC、寄りのカット |

* **Shadow Softness** はペナンブラ幅。**PCF (Vogel) のみ**で有効（Tent は固定カーネル、PCSS は接地硬化で自動決定のため不使用）。
* **Receiver Normal Bias** は受け側のノーマルオフセット量。縞状のシャドウアクネが出る場合に上げる。上げ過ぎると影が痩せる。HQ 全モード（Tent / Vogel / PCSS）で有効。
* Receiver Normal Bias を使う場合は、URP Asset 側の Light の **Normal Bias を 0〜0.3 程度に下げる**と二重バイアスによる影の浮き（ピーターパン）を防げる。
* **Shadow Cutoff Bias**（Alpha Clip 時）は、影だけ少し太めのアルファ（低い cutoff）で落とすことで、毛先のアルファ縁が閾値を行き来する ON/OFF チラつきを抑える。0 で前面の cutoff と同じ。
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
| Surface Options | Render Mode (Preset)、Cull、ZWrite、ZTest、Source Blend、Destination Blend、Alpha Blend (Transparent)、Alpha Clipping、Alpha Cutoff、Shadow Cutoff Bias、Stencil（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Base Core | Base Map、Color Correction（Hue Shift / Saturation / Value Multiplier）、Detail Map、Normal Map |
| Light and Shadow | Shading Style、Shadow Color、Receive Shadow Mask、Receive Shadow Strength、Self Shadow Mode、Shadow Softness、Receiver Normal Bias、Shadow Edge Dither、Light Wrap、Toon Threshold / Toon Softness、Auto Face Shadow Fix（Front / Up Brightness、Mask Falloff）、Anti-Blowout（Diffuse Light Limit、Additional Light Blend） |
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
