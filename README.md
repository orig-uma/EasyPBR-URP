# EasyPBR for URP

シェーダー名: `Origuma/EasyPBR_URP/Doll`

URP 向けのキャラクターシェーダー。PBR 系の質感表現とトゥーン陰影を同一マテリアルで切り替えられる。
フィギュア・人形向けのハイライト表現と、アニメ調陰影の両立を想定している。

## インストール

### Package Manager（Git URL）

`Window > Package Manager > + > Add package from git URL...` に以下を入力する。

```
https://github.com/orig-uma/EasyPBR-URP.git
```

特定バージョンを指定する場合:

```
https://github.com/orig-uma/EasyPBR-URP.git#v0.3.3
```

### Embedded

`Packages/com.origuma.easypbr-urp` に配置すると embedded package として認識される。

## 動作環境

* Unity 2022.2 以降（Forward+ / Cluster Light Loop）、または Unity 6 (6000.x)
* Universal RP 14.0 以降

## 設計方針

* **パラメータ**
  Rim Light / Peach Fuzz / Anisotropic は `Thickness`（0.0〜1.0）等の直感的な値で指定する。
* **顔影**
  顔用マスクテクスチャは不要。プロシージャルマスクにより自己陰を抑制する（逆光時は陰を維持）。
* **セルフシャドウ**
  落ち影と陰影を分離して合成する。落ち影はメインライト専用に PCF / PCSS で高品質化でき、追加ライトの影は URP 標準のままにして多灯時の負荷を抑える。
* **スペキュラ**
  Dual-Lobe を基本に、Specular Model で軽量な Blinn-Phong と物理ベースの GGX（Fresnel）を切り替えられる。
* **半透明**
  Render Mode プリセット（Opaque / Cutout / Transparent）で Render Queue、Blend Mode、ZWrite を一括設定する。
* **任意効果**
  SSS / Rim / Peach Fuzz / Grain / MatCap / Glitter / Anisotropic は既定 OFF または Intensity 0 で GPU 計算をスキップする。
* **ブルーノイズ**
  1 枚のテクスチャを影エッジのディザ（Self Shadow Quality: Off）とグレイン（法線の微細揺らぎ）で共通サンプルする。

## 機能

| 項目 | 内容 |
| :--- | :--- |
| Shading Style | Toon / Smooth の切り替え |
| Base Core | Base Map、HSV 色調補正（Color Correction）、Detail Map（RGBA ブレンド）、Normal Map |
| Auto Face Shadow Fix | マスク不要の顔自己陰抑制（プロシージャルマスク） |
| Self Shadow | 落ち影（Shadow map）と陰影（NdotL）の分離合成。Self Shadow Quality（Off / PCF / PCSS）、Receiver Normal Bias、Shadow Dither |
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

## ファイル構成

| パス | 役割 |
| :--- | :--- |
| `Runtime/Shaders/Doll.shader` | Properties、Pass 定義 |
| `Runtime/Shaders/EasyPBR_Input.hlsl` | 共通変数・テクスチャ（CBUFFER） |
| `Runtime/Shaders/EasyPBR_Effects.hlsl` | Dissolve、MatCap、Emission |
| `Runtime/Shaders/DollLighting.hlsl` | ライティング統合（顔影 / Toon、Dual-Lobe、SSS / Rim / Fuzz、Anisotropic、Glitter） |
| `Runtime/Shaders/DollShadows.hlsl` | メインライト高品質セルフシャドウ（PCF / PCSS） |
| `Runtime/Shaders/Doll_ForwardPass.hlsl` | ForwardLit パス |
| `Runtime/Shaders/Doll_ShadowPass.hlsl` | ShadowCaster パス |
| `Runtime/Shaders/Doll_OutlinePass.hlsl` | Outline パス |
| `Runtime/Textures/BlueNoise_RGB_256.png` | Grain / Shadow Dither 用 |
| `Runtime/Textures/dissolve_noise.png` | Dissolve ノイズ |
| `Editor/DollShaderGUI.cs` | カスタムインスペクター |

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
* PCSS のブロッカー探索は `_MainLightShadowmapTexture` を point sampler で読むため、環境によっては `sampler_PointClamp` の宣言が必要になる（`DollShadows.hlsl` 内のコメント参照）。

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

Origuma — https://github.com/orig-uma
