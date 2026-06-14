---

### CHANGELOG.md

```markdown
# Changelog

このパッケージの変更点を記録します。フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [0.2.0] - 2026-06-15

### Added
- **アーキテクチャの刷新**: 今後の拡張性・保守性向上のため、シェーダー本体を機能単位のHLSLファイル（`EasyPBR_Input.hlsl`, `EasyPBR_Effects.hlsl`, `Doll_FaceLogic.hlsl`など）にモジュール分割。
- **Render Mode のプリセット機能**: マテリアルインスペクター（`DollShaderGUI`）にて、Opaque / Cutout / Transparent を切り替えた際に `Queue`、`Blend`、`ZWrite` などの複雑な半透明設定をワンクリックで一括セットアップする機能を追加。
- **リッチなDissolve（消失エフェクト）の拡張**:
- アウター（最前線）とインナー（焦げ）の2色設定に対応し、アルベドを直接焦げ色に塗り潰す高度な表現を追加。
- アニメ・グリッチ表現に最適な、エッジのパキッとした段階化（Step Edge）機能を追加。

### Changed
- **Rim Light / Peach Fuzz の UX 改善**: 設定値を数式の「べき乗(Power)」から、直感的な「太さ(Thickness)」の指定(0.0〜1.0)へと変更。スライダーを右に動かすほど光の線が太くなるように計算式を逆転。
- **URP 14+ (Forward+) への対応**: 最新のURP仕様に合わせ、追加ライトループ周りのマクロ（`_FORWARD_PLUS` から `_CLUSTER_LIGHT_LOOP` へ移行）およびダミーの `InputData` 構築を修正。

## [0.1.0] - 2026-06-08

### Changed
- ライセンスを MIT に変更（source-available から緩和）。

### Added
- 初回リリース。`Origuma/EasyPBR_URP/Doll` シェーダー（ForwardLit + ShadowCaster）。
- Toon / Smooth の2シェーディングモード。
- 顔の自己陰を自動で消す Auto Face Shadow Fix（法線平滑化 + プロシージャルマスク）。
- 落ち影と陰影を分離合成する仕組み（トゥーン境界のマッハバンド回避、ブルーノイズディザ）。
- Dual-Lobe スペキュラ + エネルギー保存。
- 任意効果（既定OFF）: SSS / Rim Light / Peach Fuzz / MatCap。
- 任意効果（SSS / Rim / Peach Fuzz）の Intensity 0 時は uniform 分岐で GPU 計算をスキップ（効果ごとの keyword variant は増やさない設計）。
- ブルーノイズテクスチャ `BlueNoise_RGB_256.png` を同梱（grain / shadow dither 用）。
- カスタムマテリアルインスペクター `DollShaderGUI`（機能別の折りたたみ表示・依存項目の出し分け）。
