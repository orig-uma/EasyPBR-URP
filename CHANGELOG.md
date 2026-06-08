# Changelog

このパッケージの変更点を記録します。フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

## [0.1.0] - 2026-06-08

### Added

- 初回リリース。`Origuma/EasyPBR_URP/Doll` シェーダー（ForwardLit + ShadowCaster）。
- Toon / Smooth の2シェーディングモード。
- 顔の自己陰を自動で消す Auto Face Shadow Fix（法線平滑化 + プロシージャルマスク）。
- 落ち影と陰影を分離合成する仕組み（トゥーン境界のマッハバンド回避、ブルーノイズディザ）。
- Dual-Lobe スペキュラ + エネルギー保存。
- 任意効果（既定OFF）: SSS / Rim Light / Peach Fuzz / MatCap。
- 無効効果を uniform 分岐でスキップする軽量化（バリアント増なし）。
