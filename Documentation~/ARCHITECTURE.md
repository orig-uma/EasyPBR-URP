# EasyPBR for URP — アーキテクチャ

ライブラリの内部構成と技術的な設計方針を解説する。利用者向けの導入・パラメータ説明は [README](../README.md) を参照。

## レンダリング設計方針

シェーダーの描画における中核的なアプローチと仕様は以下の通り。

* **高品質セルフシャドウ（中核）**
  メインライト専用に、スクリーン空間回転 Vogel ディスクの PCF とブロッカー探索による PCSS（コンタクトハードニング）を実装。低解像度シャドウマップ由来のアクネ・ジャギーを発生源で除去し、顔のような曲面でも**マスクテクスチャ無しでクリーンな落ち影**を生成する。追加ライトの影は URP 標準のままにして多灯時の負荷を抑え、落ち影（Shadow map）と陰影（NdotL）は分離して合成する。
* **顔影（独立した調整軸）**
  「正しいが描きたくない」落ち影・陰を、マスクテクスチャ無しのプロシージャルマスクで正面/上向きの面に限って明るく戻す（逆光時は陰を維持）。アクネを消す Receiver Normal Bias とは目的が逆（誤った影 vs 正しい影）の別軸。Quality: Off 時の主要な調整手段となる。
* **スペキュラとマテリアルモデル**
  Dual-Lobe を基本とし、Specular Model で軽量な Blinn-Phong と物理ベースの GGX（Schlick Fresnel・Smith 可視性込みの Cook-Torrance）をパス内で切り替える。
* **ブルーノイズの共通化**
  1 枚のテクスチャを、影エッジのディザリング（Self Shadow Mode: Off）とグレイン（法線の微細揺らぎ）で共通サンプルし、テクスチャフェッチを節約する。

## ライブラリの分離方針

切り分けの軸は **機能ではなく依存の方向**である。汎用ライブラリ（計算本体）を `Common/` に純粋関数として切り出し、`Doll` 固有の方針はポリシー層に集約することで、他シェーダーへの流用を容易にする。

### ディレクトリ構成

```text
Runtime/
  DollOutlineFeature.cs         アウトライン描画 RendererFeature（独自 LightMode "DollOutline"）
  Shaders/
    Doll/                       Doll シェーダー（流用しない固有実装）
      Doll.shader               Properties、Pass 定義
      DollInput.hlsl            共通変数・テクスチャ（CBUFFER）
      DollEffects.hlsl          Dissolve / MatCap / Emission のポリシー層
      DollLighting.hlsl         ライティング統合のポリシー層
      DollShadows.hlsl          メインライト高品質セルフシャドウのラッパー
      Passes/
        ForwardPass.hlsl        ForwardLit パス
        ShadowPass.hlsl         ShadowCaster パス
        DepthOnlyPass.hlsl      DepthOnly パス
        DepthNormalsPass.hlsl   DepthNormals パス
        OutlinePass.hlsl        Outline パス
    Common/                     汎用ライブラリ（流用する価値のある純粋関数）
      Common.hlsl               アンブレラ
      Common_Math.hlsl
      Common_Color.hlsl
      Common_Sampling.hlsl
      BRDF/
      Effects/
      URP/
  Compute/                      Dissolve エッジ算出 Compute Shader
  Scripts/                      Dissolve VFX 制御スクリプト（asmdef）
  VFX/                          Dissolve VFX Graph
  Textures/                     BlueNoise / Ramp / Dissolve ノイズ
Editor/
  DollShaderGUI.cs              カスタムインスペクター
  DollOutlineSetupWindow.cs     Outline Feature の追加/削除 Window
```

## ファイル構成

| パス | 役割 |
| :--- | :--- |
| `Runtime/Shaders/Doll/Doll.shader` | Properties、Pass 定義 |
| `Runtime/Shaders/Doll/DollInput.hlsl` | 共通変数・テクスチャ（CBUFFER） |
| `Runtime/Shaders/Doll/DollEffects.hlsl` | Dissolve / MatCap / Emission のポリシー層（薄いラッパー）。計算本体は `Common/` へ委譲 |
| `Runtime/Shaders/Doll/DollLighting.hlsl` | ライティング統合のポリシー層（顔影、Toon / Specular / Shadow のキーワード解決、互換ラッパー）。計算本体は `Common/` へ委譲 |
| `Runtime/Shaders/Doll/DollShadows.hlsl` | メインライト高品質セルフシャドウのラッパー（PCF / PCSS）。実装は `Common/URP/Shadow_HQ_URP.hlsl` |
| `Runtime/Shaders/Doll/Passes/ForwardPass.hlsl` | ForwardLit パス |
| `Runtime/Shaders/Doll/Passes/ShadowPass.hlsl` | ShadowCaster パス |
| `Runtime/Shaders/Doll/Passes/DepthOnlyPass.hlsl` | DepthOnly パス |
| `Runtime/Shaders/Doll/Passes/DepthNormalsPass.hlsl` | DepthNormals パス |
| `Runtime/Shaders/Doll/Passes/OutlinePass.hlsl` | Outline パス（LightMode = `DollOutline`） |
| `Runtime/DollOutlineFeature.cs` | `DollOutline` パスを後段でまとめて描く RendererFeature（ForwardLit のバッチング維持） |
| `Runtime/Shaders/Common/` | キーワード・マテリアルプロパティに非依存の汎用ライブラリ（後述） |
| `Runtime/Textures/BlueNoise_RGB_256.png` | Grain / Shadow Dither 用 |
| `Runtime/Textures/DissolveNoise.png` | Dissolve ノイズ |
| `Editor/DollShaderGUI.cs` | カスタムインスペクター |

## 汎用ライブラリ構成（Common）

`Runtime/Shaders/Common/` に、計算本体を純粋関数として切り出している。
`Doll` 固有の方針はポリシー層（`DollEffects` / `DollLighting` / `DollShadows`）に集約する。

* **層の分離**

  | 層 | 役割 | 含むもの |
  | :--- | :--- | :--- |
  | Common（純粋） | 外部依存なし。入力 → 出力のみ | 数学・色・BRDF・エフェクトの計算本体 |
  | URP（結合） | URP のシャドウグローバルに依存 | 高品質セルフシャドウサンプラ |
  | ポリシー（薄い） | キーワード / プロパティ / キャラ方針 | 顔マスク、キーワード分岐、互換ラッパー |

* **汎用化方針**
  キーワード（`_SHADOWMODE_*` 等）は `bool` 引数化、マテリアルプロパティ（`_ReceiverNormalBias` / `_Dissolve*` 等）と Dissolve のテクスチャサンプリングは呼び出し側へ外出しする。`Doll_` 接頭辞は除去する。
  なお Shading Style / Specular Model は keyword を持たず、`_ShadingStyle` / `_SpecularModel`（uniform）の `UNITY_BRANCH` 動的分岐で解決する（混在マテリアルの SRP Batcher バッチング維持のため。0.3.5）。
* **互換性**
  公開関数名（`GetCastShadow` / `GetLitMask` / `CalculateDualLobeSpecular` / `SampleMainShadowHQ` / `ApplyDissolveClip` 等）と挙動は維持する。各パスのフラグメント側は無改修で動作する。

### Common のファイル

| パス | 役割 |
| :--- | :--- |
| `Common/Common.hlsl` | アンブレラ。これ 1 本で BRDF / Effects の純粋関数を依存順に内包 |
| `Common/Common_Math.hlsl` | `Hash21`、`IGN`、`EasyPBR_Remap`、`Luminance601`、`ApplyLuminanceClamp` |
| `Common/Common_Color.hlsl` | `RgbToHsv`、`HsvToRgb`、`HueToRGB`、`ApplyColorCorrection` |
| `Common/Common_Sampling.hlsl` | `VogelDisk` |
| `Common/BRDF/BRDF_GGX.hlsl` | `D_GGX`、`V_SmithGGX`、`F_Schlick`、`GGXLobe`、`BlinnPhongLobe` |
| `Common/BRDF/BRDF_Specular.hlsl` | `DualLobeSpecularGGX` / `DualLobeSpecularBlinn` |
| `Common/BRDF/BRDF_Diffuse.hlsl` | `HalfLambert`、`ToonRamp`、`ShadeRamp`、`ShadedAlbedo`、`ResolveCastShadow` |
| `Common/BRDF/BRDF_RimFuzz.hlsl` | `GetFresnelTerms`、`CalculateRimLight`、`CalculatePeachFuzz` |
| `Common/BRDF/BRDF_Translucency.hlsl` | `CalculateSSS` |
| `Common/BRDF/BRDF_Anisotropic.hlsl` | `AnisoPrecomp`、`PrecomputeAnisoTangent`、`CalculateAnisotropicSpecular` |
| `Common/BRDF/BRDF_Glitter.hlsl` | `GlitterGeom`、`PrepareGlitter`、`ApplyGlitterLight` |
| `Common/BRDF/BRDF_Detail.hlsl` | `GetGrainNormal` |
| `Common/Effects/Fx_MatCap.hlsl` | `GetMatCapUV`、`ApplyMatCap` |
| `Common/Effects/Fx_Emission.hlsl` | `CalculateEmission` |
| `Common/Effects/Fx_Dissolve.hlsl` | `ResolveDissolve`（`DissolveInput` 構造体・サンプリングは外部） |
| `Common/URP/Shadow_HQ_URP.hlsl` | `EasyPBR_SampleMainShadowHQ`、`EasyPBR_FindBlocker` |

### include 順

```
URP Core.hlsl          ← 必ず最初（PI / TWO_PI / SafeNormalize / UNITY_* を供給）
  └─ Common.hlsl       （Common_* → BRDF_* → Effects_* を依存順に内包）
  └─ DollLighting.hlsl      （Common.hlsl を内部 include）
  └─ DollEffects.hlsl       （Common_Color + Effects を内部 include）
URP Shadows.hlsl
  └─ DollShadows.hlsl       （Common/URP/Shadow_HQ_URP.hlsl を内部 include）
````
