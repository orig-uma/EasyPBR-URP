# Changelog

[Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) 形式。[Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [Unreleased]

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
