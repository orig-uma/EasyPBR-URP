# Changelog

[Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) 形式。[Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [Unreleased]

## [0.3.5] - 2026-06-24

### Added
- `DepthOnly` / `DepthNormals` パス（`Passes/DepthOnlyPass.hlsl` / `Passes/DepthNormalsPass.hlsl`）。Forward の Depth Prepass / Depth Priming、Forward+ の深度生成、SSAO / Decal 用の法線生成に対応。Alpha Clip / Dissolve も反映。
- Forward+（Clustered）対応。`_FORWARD_PLUS` を multi_compile に追加し、クラスタに含まれない追加ディレクショナルライトを専用ループで処理。`USE_CLUSTER_LIGHT_LOOP`（6.1+）/ `USE_FORWARD_PLUS`（6.0）の両対応。
- テント 5x5 PCF（`PCF (Tent)`）。決定論的・ノイズなしの自己影モード（ライブ配信向け）。
- `_ShadowCutoffBias`（Shadow Cutoff Bias）。影だけ少し太めのアルファで落とし、毛先のアルファ縁が閾値を行き来する ON/OFF チラつきを抑制。

### Changed
- 自己影プロパティを `_ShadowQuality`（Off / PCF / PCSS）から `_ShadowMode`（Off / PCF (Tent) / PCF (Vogel) / PCSS）へ再編。キーワードも `_SHADOWQUALITY_*` → `_SHADOWMODE_*` にリネーム。`DollShaderGUI` は KeywordEnum を使わず手動同期（`SetShadowModeKeyword` / `ValidateMaterial` で stale・リネーム耐性を確保）。
- 既定値を調整: `_SpecularModel` を GGX に、`_HalfLambertWrap` を 0.0 に、`_GlitterTilt` を 0.8 に。`_ShadowMode` の既定は PCF (Tent)。
- 既定の Surface（Render Mode）を Cutout から **Opaque** に変更（`_AlphaClip` 既定を 0 に、SubShader Tags を `RenderType=Opaque` / `Queue=Geometry` に）。既存マテリアルは保存値を維持。
- Outline パスの LightMode を `SRPDefaultUnlit` から独自タグ `DollOutline` に変更。URP の既定不透明描画から外れることで ForwardLit と交互描画されず、**ForwardLit のバッチング分断を解消**。**アウトラインの表示には `DollOutlineFeature` の追加が必要**（Setup Window 参照）。ShaderGUI は Outline 有効時に Window への導線を表示。
- `_MainTex` に `[MainTexture]` 属性を付与。
- Dissolve のノイズサンプルを `sampler_MainTex` から `sampler_LinearRepeat` へ変更。MainTex 未使用時（深度パス等）に sampler がストリッピングされる問題を回避。
- `ForwardPass.hlsl` で `Core.hlsl` を明示 include（`USE_CLUSTER_LIGHT_LOOP` / `GetNormalizedScreenSpaceUV` / `_FORWARD_PLUS`→`_CLUSTER_LIGHT_LOOP` 互換 shim を 6.0 でも確実に供給）。追加ライトのスクリーン UV を `GetNormalizedScreenSpaceUV` 経由に変更。
- シェーダーバリアントを削減し、マテリアル混在時の SRP Batcher バッチング分断を抑制。マテリアル間で値が割れやすい 3 キーワードを廃止して動的化:
  - `_SHADINGSTYLE_TOON` → `_ShadingStyle`（uniform）の動的分岐（Property を `[Enum]` 化）。
  - `_SPECULARMODEL_BLINNPHONG` / `_GGX` → `_SpecularModel`（uniform）の `UNITY_BRANCH` 動的分岐（CBUFFER に `_SpecularModel` を追加、Property を `[Enum]` 化）。
  - `_SURFACE_TRANSPARENT` → 廃止。アルファ出力を常時 `albedo.a` に（不透明/Cutout はブレンド側で無視）。Property を `[ToggleUI]` 化。
  - `DollShaderGUI.ValidateMaterial` で旧キーワードを既存マテリアルから除去。
  - ForwardLit の理論バリアント数は 384 → 48（実ビルドは概ね 1/4 以下）に減少。

## [0.3.4] - 2026-06-21

### Changed
- HLSL を `Common/`（汎用ライブラリ）と `Doll/`（キャラ固有ポリシー層）に再構成。BRDF・エフェクト・高品質シャドウの計算本体を純粋関数として切り出し。
- ファイル配置を整理（`Doll/` 配下に Pass・Input・Lighting・Shadows を集約、`EasyPBR_*` を `Doll*` へリネーム）。
- 公開 API（`GetCastShadow` / `CalculateDualLobeSpecular` 等）と描画挙動は維持。

### Added
- `Documentation~/ARCHITECTURE.md`（内部構成・設計方針の解説）。
- `Documentation~/SHADOWS.md`（影モードの制御ガイドと推奨設定）。
- `Documentation~/SRP_BATCHER.md`（SRP Batcher を効かせるための指針）。
- `Documentation~/OUTLINE.md`（アウトラインの描画方式とセットアップ）。
- `DollOutlineFeature`（RendererFeature）と `Doll Outline Setup` Window（`Window > EasyPBR > Doll Outline Setup`）。アウトラインを独自パスとして後段でまとめて描画し、対象 Renderer への追加/削除/有効無効を Window から行える。
- カスタム Inspector で、シェーダーバリアントを生成するプロパティに ⚡ マークと凡例・ツールチップ注記を表示。
- カスタム Inspector から GitHub 上のドキュメント（影モードガイド / SRP Batcher ガイド）へ飛べるリンクを追加。

## [0.3.3] - 2026-06-21

### Added
- 高品質セルフシャドウ（`_ShadowQuality`）。メインライト専用。
  - **PCF**: スクリーン空間回転 Vogel ディスクによる連続ペナンブラ（既定）
  - **PCSS**: ブロッカー探索によるコンタクトハードニング（接地は鋭く・遠方は柔らかく）
- 受け側ノーマルオフセット（`_ReceiverNormalBias`）。シャドウアクネ（縞ノイズ）を抑制。
- スペキュラモデル切り替え（`_SpecularModel`: BlinnPhong / GGX）。GGX は Schlick Fresnel・Smith 可視性込みの Cook-Torrance。`_SpecularF0` を追加。
- 異方性ハイライトの第 2 バンド（`_AnisoSecColor` / `_AnisoSecThickness` / `_AnisoSecOffset`）。主＋副の 2 段ハイライト。
- `Runtime/Shaders/DollShadows.hlsl`（メインライト高品質シャドウサンプラ）。
- `DollShaderGUI`: ブルーノイズ専用セクション（影ディザ・グレイン共通サンプルである旨を Help で表示）。
- SSSコントロールマップ対応

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

## [0.3.2] - 2026-06-20
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
