# EasyPBR for URP

シェーダー名: `Origuma/EasyPBR_URP/Doll`

URP 向けキャラクターシェーダー。PBR 系の質感表現とトゥーン陰影を同一マテリアルで切り替え可能。
フィギュア・人形向けのハイライト表現と、アニメ調陰影の両立を想定している。

## インストール

### Package Manager（Git URL）

`Window > Package Manager > + > Add package from git URL...` に以下を入力する。

```
https://github.com/orig-uma/EasyPBR-URP.git
```

特定バージョンを指定する場合:

```
https://github.com/orig-uma/EasyPBR-URP.git#v0.3.0
```

### Embedded

`Packages/com.origuma.easypbr-urp` に配置すると embedded package として認識される。

## 設計方針

* **パラメータ**
  Rim Light / Peach Fuzz / Anisotropic は `Thickness`（0.0〜1.0）等の直感的な値で指定する。
* **顔影**
  顔用マスクテクスチャは不要。法線平滑化とプロシージャルマスクにより自己陰を抑制する。
* **半透明**
  Render Mode プリセット（Opaque / Cutout / Transparent）で Render Queue、Blend Mode、ZWrite を一括設定する。
* **任意効果**
  SSS / Rim / Peach Fuzz / MatCap / Glitter / Anisotropic は既定 OFF または Intensity 0 で GPU 計算をスキップする。

## 機能

| 項目 | 内容 |
| :--- | :--- |
| Shading Style | Toon / Smooth |
| Base Core | Base Map、HSV 色調補正、Detail Map、Normal Map |
| Auto Face Shadow Fix | マスクなしの顔自己陰抑制 |
| Shadow | 落ち影（Shadow map）と陰影（NdotL）の分離合成。ブルーノイズディザ |
| Specular | Dual-Lobe（Primary / Secondary） |
| Anisotropic Highlight | 髪・シルク向け異方性ハイライト（Strand パラメータ） |
| MatCap | Add / Multiply |
| Dissolve | Outer / Inner 2 色、Step Edge、Axis（None / WorldY / LocalY） |
| Glitter | マスク付きスパンコール。Iridescence、Sparsity、Base Reflection |
| Outline | 背面法線拡張。Alpha Clip / Dissolve 同期。Outline 専用 Stencil |
| Black Out | 最終色の暗転 |
| Optional | SSS / Rim Light / Peach Fuzz |
| Emission | Emission Map、HDR Color、Intensity |
| Stencil | ForwardLit / Outline それぞれ独立設定 |

## Pass

| Pass | LightMode | 用途 |
| :--- | :--- | :--- |
| ForwardLit | UniversalForward | メイン描画 |
| ShadowCaster | ShadowCaster | 影 |
| Outline | SRPDefaultUnlit | 輪郭線 |

## ファイル構成

| パス | 役割 |
| :--- | :--- |
| `Runtime/Shaders/Doll.shader` | Properties、Pass 定義 |
| `Runtime/Shaders/EasyPBR_Input.hlsl` | 共通変数・テクスチャ |
| `Runtime/Shaders/EasyPBR_Effects.hlsl` | Dissolve、MatCap、Emission |
| `Runtime/Shaders/EasyPBR_Lighting.hlsl` | ライティング、Anisotropic、Glitter |
| `Runtime/Shaders/Doll_FaceLogic.hlsl` | 顔影・Toon |
| `Runtime/Shaders/Doll_ForwardPass.hlsl` | ForwardLit |
| `Runtime/Shaders/Doll_ShadowPass.hlsl` | ShadowCaster |
| `Runtime/Shaders/Doll_OutlinePass.hlsl` | Outline |
| `Runtime/Textures/BlueNoise_RGB_256.png` | Grain / Shadow Dither 用 |
| `Runtime/Textures/dissolve_noise.png` | Dissolve ノイズ |
| `Editor/DollShaderGUI.cs` | カスタムインスペクター |

## 動作環境

* Unity 2022.2 以降（Forward+ / Cluster Light Loop）
* Unity 6000.3 以降
* Universal RP 14.0 以降

## 使い方

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を指定する
2. Surface Options > Render Mode で Opaque / Cutout / Transparent を選択する
3. Base Map にアルベドテクスチャを割り当てる
4. Shading Style で Toon / Smooth を選択する

### インスペクター

`DollShaderGUI` により Custom / Default UI を切り替え可能。

| モード | 表示 |
| :--- | :--- |
| Custom | セクション分け UI |
| Default | Unity 標準のプロパティ一覧 |

### パラメータ（Custom UI）

| セクション | 主な項目 |
| :--- | :--- |
| Surface Options | Render Mode、Cull、ZWrite、ZTest、Blend |
| Stencil | Ref、Compare、Pass / Fail / ZFail |
| Base Core | Base Map、HSV、Detail Map、Normal Map |
| Auto Face Shadow Fix | Front / Up Brightness、Mask Falloff、Backlight Preserve、Normal Smoothing |
| Light and Shadow | Shading Style、Shadow Color、Receive Shadow、Dither、Light Limit |
| Surface Micro Detail | Blue Noise、Grain |
| Specular and Reflection | Dual-Lobe、Anisotropic、MatCap |
| Emission | Emission Map、Color、Intensity |
| Dissolve | Amount、Axis、Edge Color、Step Edge |
| Optional Effects | Glitter、SSS、Peach Fuzz、Rim Light |
| Black Out | Black Out |
| Outline | Enable、Color、Width、Cutoff Shift、Stencil |
| Advanced | GPU Instancing、Double-Sided GI |

## ライセンス

[MIT License](LICENSE.md)

## 作者

Origuma — https://github.com/orig-uma
