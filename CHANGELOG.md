# Changelog

[Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) 形式。[Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [Unreleased]

## [0.4.0] - 2026-06-28

> **破壊的変更を含む**（`_SSSMask` → `_SSSMap` のプロパティ名・チャンネル構成変更）。移行は [MIGRATION](MIGRATION.md) を参照。
> 公開 API のうち見た目に影響するのは SSS のみ。ForwardPass の内部リファクタはマテリアルプロパティ・描画結果ともに不変。新規シェーダーキーワードは追加していない（すべて uniform 動的分岐）。

### Added
- **曲率マップ（Curvature Map）**（`EasyPbrCurvatureBaker` → `_CurvatureMap` / `_CurvatureStrength`）。隣接頂点の法線関係から**符号付き**曲率を算出（レイ不要）。0.5=平坦 / 明=凸（稜線）/ 暗=凹（くぼみ）。1 枚で稜線・くぼみ両マスクが取れる Cavity の上位互換。凸で `finalSpecular *= (1 + curvRidge)`、凹で albedo を暗化。ベイク時 `clearValue: 0.5`、Strength 自動 1。**新規キーワードなし。**
- **ベント法線マップ（Bent Normal Map）**（`EasyPbrBentNormalBaker` → `_BentNormalMap` / `_BentNormalStrength`、**RGBA**）。AO と同じ半球レイで「開いている平均方向」を求め接線空間に焼く。RGB=接線空間ベント法線、A=開き具合（可視率）。`SampleSH(bentNormalWS)` で間接光の評価方向を補正し、くぼみのアンビエントを方向まで正しくする。環境反射には**解析スペキュラ遮蔽**（`SpecularOcclusion`＝Lagarde/Frostbite 近似、`DollEffects.hlsl`）＋ A チャンネルによる**方向スペキュラ遮蔽**を適用。接線空間ゆえスキン変形に追従。**新規キーワードなし。**
- **ヘアフローマップ（Hair Flow Map）**（`EasyPbrHairFlowBaker` → `_HairFlowMap` / `_HairFlowStrength`）。形状（構造テンソル＝最長エッジ／曲率トグル）から毛流れ軸を推定。向きの無い軸を**倍角エンコード**（RG=cos2θ/sin2θ）し、B に信頼度を焼く。`PrecomputeAnisoTangent` に統合し、信頼度の高い箇所では単一グローバル角の代わりに**ピクセルごとの焼き角**で接線を駆動。ミラーUV・流れに沿わないUVでの天使の輪の破綻を解消。**新規キーワードなし。**
- **クリアコート＋イリデッセンス**（`Common/BRDF/BRDF_Clearcoat.hlsl`: `CalculateClearcoat` / `ClearcoatIridescence` / `IridescenceTint`）。**加算専用**（下地の陰影・アルベドに非干渉＝黒ずませない）。コート法線は幾何法線（平滑）で艶をクリーンに走らせ、視点依存のフレネルで斜めほど強まる。薄膜の虹色は視点角で位相が動く（ARでカメラを振ると色が回る）。瞳・唇・爪のツヤ向け。プロパティ: `_ClearcoatMask` / `_ClearcoatStrength` / `_ClearcoatSmoothness` / `_ClearcoatReflStrength` / `_IridescenceIntensity` / `_IridescenceThickness` / `_IridescenceShift`。**新規キーワードなし。**
- `EasyPbrBakeCore`: チャンネル別クリア値（`clearValueG` / `clearValueB` / `clearValueA`）に対応（接線空間マップ等で背景を neutral にできる）。

### Changed
- **SSS マップ刷新（破壊的変更）**: `_SSSMask`（R のみ・厚み）→ `_SSSMap`（**RGBA**: RGB=接線空間の透過方向、A=厚み）。ベイカーを `EasyPbrThicknessBaker` → `EasyPbrSssBaker` に置換。`CalculateSSS` の歪み軸を `detailNormalWS` から焼いた透過方向 `sssTransWS` に変更し、耳の縁・小鼻・指など「薄さの抜ける向きが法線とずれる」箇所で透過グローが正しい向きに出る。詳細は [MIGRATION](MIGRATION.md)。
- **既定値変更**: `_OcclusionStrength` / `_CavityStrength` を 1.0 → **0.0**（新規マテリアルは既定 OFF。既存マテリアルは保存値を維持）。
- **環境反射の cube フェッチ共有**: 下地反射とクリアコート反射が **1 回の cube サンプル**を共有（`EasyPBR_SampleEnvironment` を 1 回呼び、下地・コートで重みだけ別適用）。下地反射の挙動は従来と同一。
- **ForwardPass リファクタ（挙動不変）**: frag を責務分割し、`CalculateSingleLight` の引数肥大を解消。新規 `DollSurfaceTypes.hlsl`（`DollSurfaceData` 構造体）／ `DollSurface.hlsl`（`GatherSurface` / `ComputeFaceSDF` / `ApplyEnvironmentAndCoat` / `ApplyPostEffects`）を追加し、`CalculateSingleLight` を `DollLighting.hlsl` へ移動して `DollSurfaceData` を受け取る形に集約。frag は約 474 行 → 約 145 行。計算式・分岐条件・見た目は不変。詳細は [ARCHITECTURE](ARCHITECTURE.md)。
- **Baking パネル順**: AO → Bent Normal → Cavity → Curvature → SSS → Hair Flow → Face SDF。

### Removed
- `EasyPbrThicknessBaker.cs` を削除（`EasyPbrSssBaker` に置換）。
- `_SSSMask` プロパティを削除（`_SSSMap` にリネーム）。

## [0.3.7] - 2026-06-26

### Added
- GUIに未実装だった`_FaceSDFBlendNormalMin` と `_FaceSDFShadowMax` を追加

### Changed
- `_FaceSDFShadowMix` のデフォルト値を調整

### Removed
- 使用していないプロパティの削除

## [0.3.6] - 2026-06-26

### Added
- **ディテールノーマルマップ**（`_DetailNormalMap` / `_DetailNormalScale`）。汎用タイリングの微細ノーマルで肌のキメ・布の織りを足す。Detail Map のタイリングを共有し、whiteout ブレンドでベース法線に重ねる。`bump`（既定）で無影響＝モデル別オーサリング不要（CC0 タイリング素材でOK）。
- **Geometric Specular Anti-Aliasing**（`_SpecularAA`）。法線の画面内分散から実効ラフネスを上げ、大型 LED・激しいモーション時のハイライトのチラつき（ジャギ）を発生源で抑える。デュアルローブスペキュラ（GGX / Blinn-Phong 双方）に適用。`Common/BRDF/BRDF_GGX.hlsl` に `ComputeSpecularAAVariance` / `ApplySpecularAA` を追加。分散は frag で 1 回だけ算出（導関数は均一制御フロー）。**新規キーワードなし（uniform 動的分岐）。**
- **ライト連動 MatCap**（`_MatCapLightInfluence`）。メインライトの画面内方向に MatCap のサンプリングを回転させ、焼かれた映り込みをステージ照明に反応させる。`Common/Effects/Fx_MatCap.hlsl` に `GetMatCapUVLightAligned` を追加。0 で従来のビュー固定。
- **オクルージョンマップ**（`_OcclusionMap` / `_OcclusionStrength`）。ベイクした AO（R チャンネル）で拡散光を沈める。白（既定）で無効。
- **キャビティマップ**（`_CavityMap` / `_CavityStrength`）。細かいくぼみ（しわ・継ぎ目）の暗化（R チャンネル）。広域 AO とは別軸で重ねられる。白（既定）で無効。
- **環境反射（Reflection Probe）**（本体 Doll: `_ReflectionStrength`）。シーンの Reflection Probe を表面に反射させる汎用 PBR スペキュラ反射。瞳・エナメル・小物がステージ環境に反応する。ぼけは Primary Smoothness、縁の重みは Fresnel(F0) を流用し、Occlusion マップ・Specular Mask・**地平線オクルージョン**（反射ベクトルが面の裏へ潜るぶんを減衰）で整える。`Common/URP/Reflection_URP.hlsl` に `EasyPBR_SampleEnvironment` / `EasyPBR_EnvironmentReflection` を追加（URP 結合層）。0 で cube サンプルごとスキップ・既定 OFF。**新規キーワードなし（uniform 動的分岐）。**
- **ベイカー（マップ生成ツール）**（Editor 限定・`Editor/Baking/`、UI は `Editor/DollBakingPanel.cs` → `DollShaderGUI` の Baking セクション）。DCC 不要でメッシュからマップを焼く。マテリアル Inspector の **Baking** セクションから、選択中キャラの **Source Root（GameObject）を自動補完 → 1 ボタンで焼いて自動アサイン**（非破壊・再ベイク可）。共通土台 `EasyPbrBakeCore.RunBake`＝Root 配下で対象マテリアルを使う **複数 Renderer / サブメッシュをまとめて 1 枚に焼く**（1 マテリアルを複数メッシュで共有していても OK）。遮蔽計算は全パーツを遮蔽源にしつつ、書き込みは**編集中マテリアルのサブメッシュのみ**。頂点値 → UV 空間 CPU 累積ラスタライズ → ダイレート → ブラー → 保存(Linear) → アサイン。**焼くと対応機能を自動で有効化**（AO/Cavity の Strength、SSS の Intensity、顔 SDF の `_UseFaceSDF` を OFF なら ON に）して即座に見た目へ反映。`RunBake` は最大 **RGBA 4 チャンネル**（`computeR` / `computeG` / `computeB` / `computeA`）に対応。
  - **`EasyPbrAoBaker`**（→ `_OcclusionMap`）: 一時 MeshCollider への半球レイで頂点 AO を算出。平滑化 / ブラー / Floor / **Ignore Enclosed**（密着面・反転法線・内部メッシュ由来の黒つぶれを白へ戻す）。
  - **`EasyPbrCavityBaker`**（→ `_CavityMap` / `_CavityStrength`）: 隣接頂点の法線方向の偏りから凹（くぼみ）を検出してしわ・継ぎ目を細かく暗化。広域 AO とは別軸。レイ不要で高速。
  - **`EasyPbrThicknessBaker`**（→ `_SSSMask`）: 内向き半球レイで出口までの距離＝厚みを測り、薄い部位（耳・鼻・指）ほど白＝SSS 強に。
  - **`EasyPbrFaceSdfBaker`**（→ `_FaceSDFMap`）: 正面から各ローカル軸へ 180° スイープし、各点が陰に入る光角度を 0..1 で記録（鼻・眉の落ち影を Cast Shadow レイで考慮）。**RGBA 4 チャンネル**（**R=右 / G=左 / B=上 / A=下**）で焼くため、ランタイムは UV ミラー不要＝**左右非対称の顔（傷跡・マーク等）にも対応**し、上下方向の光にも追従。ベイク結果はチャンネル混色を避けるため無圧縮 Linear インポート。
- **顔 SDF シャドウ（ランタイム）**（`_UseFaceSDF` / `_FaceSDFMap` / `_FaceSDFFlip` / `_FaceSDFSoftness` / `_FaceSDFShadowMix` / `_FaceSDFFrontBlend` / `_FaceSDFFrontFade`）。ベイクした 4ch SDF でメインライトの顔影を駆動し、光に合わせて**滑らかに動く**（シャドウマップ非依存＝アクネ・シマー・ガタつき無し。3D ライブのモーション安定向け）。メインライト方向を顔ローカル（Forward / Up / Right）へ投影し、右・左・上・下の **ウェイト付き平均**で 4 チャンネルを合成した SDF 値と `frontness` を比較。SDF 時は自己影マップを顔に使わず（`_FaceSDFShadowMix` で外部落ち影のみ任意合成）、エッジは `fwidth` ベースで常に AA。UV ミラー不要・**左右非対称の顔に対応**。正面横切りの継ぎ目は `_FaceSDFFrontBlend`（左右クロスフェード）＋ `_FaceSDFFrontFade`（正面ほど影を弱め切り替わりを隠す）で解消。**新規キーワードなし（uniform 動的分岐）。**
- **ドキュメント**: `Documentation~/ARCHITECTURE.md` に Editor ベイク構成（`EasyPbrBakeCore` + 4 Baker）と Face SDF 4ch の解説を追加。`Documentation~/USAGE.md` の Baking セクションを全マップ種別・Source Root 表記に更新。`Documentation~/SHADOWS.md` に Face SDF Shadow 節を追加。README に Map Generator の概要行を追加。

### Changed
- `_SpecularAA` の既定値を **1.0（ON）** とした。スペキュラ AA は静止時の見た目をほぼ変えずモーション時のチラつきのみを抑えるため既定で有効化。既存マテリアルにも適用される（チラつき低減方向の変化）。OFF にするには 0 に設定。
- `CalculateSingleLight` / `CalculateDualLobeSpecular`（`DualLobeSpecularGGX` / `DualLobeSpecularBlinn`）にスペキュラ AA 分散を渡す引数を追加。フラグメント側以外の呼び出しは無し。

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
- `Documentation~/USAGE.md`（使い方・インスペクター・パラメータ一覧）/ `Documentation~/VARIANTS.md`（シェーダーバリアント一覧）。README から詳細・重複を移設し、README はリンク集に整理。
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
