# Changelog

[Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) 形式。[Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [Unreleased]

## [0.3.3] - 2026-06-29

### Added
- 高品質セルフシャドウ（`_ShadowQuality`）。メインライト専用。
  - **PCF**: スクリーン空間回転 Vogel ディスクによる連続ペナンブラ（既定）
  - **PCSS**: ブロッカー探索によるコンタクトハードニング（接地は鋭く・遠方は柔らかく）
- 受け側ノーマルオフセット（`_ReceiverNormalBias`）。シャドウアクネ（縞ノイズ）を抑制。
- スペキュラモデル切り替え（`_SpecularModel`: BlinnPhong / GGX）。GGX は Schlick Fresnel・Smith 可視性込みの Cook-Torrance。`_SpecularF0` を追加。
- 異方性ハイライトの第 2 バンド（`_AnisoSecColor` / `_AnisoSecThickness` / `_AnisoSecOffset`）。主＋副の 2 段ハイライト。
- `Runtime/Shaders/DollShadows.hlsl`（メインライト高品質シャドウサンプラ）。
- `DollShaderGUI`: 初期値と異なるプロパティ行に ↺ リセットボタン（項目単位でシェーダー既定値に復帰）。
- `DollShaderGUI`: ブルーノイズ専用セクション（影ディザ・グレイン共通サンプルである旨を Help で表示）。

### Changed
- 落ち影をピクセル単位のシャドウ座標で算出（頂点補間誤差を排除）。
- `GetCastShadow`: PCF / PCSS 時は UV 連動ディザと再量子化をバイパス（ザラつき除去）。
- `CalculateDualLobeSpecular` / `CalculateAnisotropicSpecular`: モデル分岐・第 2 バンドに対応（既定値では従来と同一の見た目）。
- `DollShaderGUI`: Light and Shadow / Specular and Reflection を再構成（品質・モデルに応じて項目を出し分け）。
- 追加ライトの影は従来どおり URP 標準（多灯時の負荷を考慮）。
- Auto Face Shadow Fix の既定値を無効化（`_FrontMaskStrength` / `_UpMaskStrength` = 0）。
- `GetProceduralMask`: 逆光時の陰維持を常時有効化（`_BacklightPreserve` 相当を固定）。
- 拡散光の法線は常に `detailNormalWS` を使用（法線平滑化なし）。
- ブルーノイズ（`_BlueNoiseTex`）とグレイン（`_GrainIntensity` / `_GrainScale`）を Properties / GUI 上で分離。グレインは Optional Effects へ移動。
- GUI調整
- スペキュラ / 異方性の既定値を調整。

### Removed
- `_BacklightPreserve` / `_FaceNormalSmoothness` プロパティと `GetFaceSmoothedNormal`（UI・CBUFFER から削除。逆光維持は内部固定）。

## [0.3.2] - 2026-06-28
### Added
- _AdditionalLightBlendModeの追加。白飛び対策。

## [0.3.1] - 2026-06-18
### Changed
- 処理負荷・ShaderVariantの削減
- GUIの更新

## [0.3.0] - 2026-06-18

### Added

- 異方性ハイライト（Anisotropic Highlight）。髪・シルク向け。Strand Scale / Strength / Direction による繊維表現
- グリッター（Glitter）。マスク付きスパンコール表現。虹色（Iridescence）、間引き、ベース反射
- アウトライン Pass（`Doll_OutlinePass.hlsl`）。Depth フェード、Alpha Clip / Dissolve 同期、Outline 専用 Stencil
- Base Core: Normal Map、HSV 色調補正（Hue Shift / Saturation / Value Multiplier）、Detail Map（RGBA ブレンド）
- Black Out（画面暗転）
- インスペクター UI 切り替え（Custom / Default）
- 同梱テクスチャ `dissolve_noise.png`

### Changed

- `DollShaderGUI`: Base Core / Specular / Optional Effects / Outline / Black Out セクションを追加・更新

## [0.2.1] - 2026-06-15

### Fixed

- シェーダー記述ミスによるコンパイルエラー

## [0.2.0] - 2026-06-15

### Added

- HLSL モジュール分割（`EasyPBR_Input.hlsl`、`EasyPBR_Effects.hlsl`、`EasyPBR_Lighting.hlsl`、`Doll_FaceLogic.hlsl`、`Doll_ForwardPass.hlsl`、`Doll_ShadowPass.hlsl`）
- Render Mode プリセット（Opaque / Cutout / Transparent）
- Dissolve: Outer / Inner 2 色、焦げのアルベド上書き、Step Edge
- Transparent / Emission / Stencil 対応

### Changed

- Rim Light / Peach Fuzz: `Power` → `Thickness`（0.0〜1.0）
- 追加ライトループ: `_FORWARD_PLUS` → `_CLUSTER_LIGHT_LOOP`（URP 14+）

## [0.1.0] - 2026-06-08

### Changed

- ライセンス: source-available → MIT

### Added

- `Origuma/EasyPBR_URP/Doll`（ForwardLit + ShadowCaster）
- Shading Style: Toon / Smooth
- Auto Face Shadow Fix
- 落ち影と陰影の分離、ブルーノイズディザ
- Dual-Lobe スペキュラ
- SSS / Rim Light / Peach Fuzz / MatCap（既定 OFF、Intensity 0 で計算スキップ）
- 同梱テクスチャ `BlueNoise_RGB_256.png`
- カスタムインスペクター `DollShaderGUI`
