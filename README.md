# EasyPBR for URP

`Origuma/EasyPBR_URP/Doll`

Universal Render Pipeline (URP) 向けの、PBR と トゥーンを両立させたキャラクター用シェーダーです。
フィギュアや人形のような「質感のあるハイライト」と「平面的で破綻しない陰影」を同時に狙っています。

## 特徴

- **Toon / Smooth の2モード** をマテリアルから切り替え可能
- **顔の自己陰を自動で除去**（マスクテクスチャ不要）。鼻・頬の凹凸が作る汚い影を、法線平滑化＋プロシージャルマスクで抑制
- **落ち影(shadow map)と陰影(NdotL)を分離合成** し、トゥーン境界に出やすいマッハバンド（縞）を回避。ブルーノイズディザで量子化バンドも分解
- **Dual-Lobe スペキュラ + エネルギー保存**（鋭い／柔らかいの2ローブ）
- **追加効果**（既定OFF・Intensity 0 で無効）: SSS / Rim Light / Peach Fuzz / MatCap
- **追加効果の実行時スキップ**: SSS / Rim / Peach Fuzz は Intensity 0 のとき GPU 計算を省略。これらは keyword 化していないため、効果ごとの material variant は増えない（Toon / MatCap / Alpha Clip や URP のライト・影用 `multi_compile` による variant は通常の URP Forward 系と同程度に存在する）

## 動作環境

- Unity 6000.3 以降
- Universal RP 17.3.0 以降

## インストール

### Package Manager から Git URL で追加

Unity の `Window > Package Manager > +（左上）> Add package from git URL...` に以下を入力します。

```
https://github.com/orig-uma/EasyPBR-URP.git
```

特定バージョンを固定する場合は末尾にタグを付けます。

```
https://github.com/orig-uma/EasyPBR-URP.git#0.1.0
```

### ローカル / Embedded

このリポジトリをプロジェクトの `Packages/com.origuma.easypbr-urp` に配置すると、埋め込みパッケージとして認識されます。

## 使い方

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を選択。
2. `Base Map` にアルベドテクスチャを設定。
3. `Shading Style` で `Toon` / `Smooth` を選択。

### 主なパラメータ

| グループ | 内容 |
| --- | --- |
| Base Core | アルベド・色・Alpha Clip・カリング |
| Auto Face Shadow Fix | 顔の自己陰を消すマスク（正面/上向きの明るさ、逆光の陰の維持など） |
| Light and Shadow | シェーディングモード、影の色、落ち影のソフトさ／ディザ、トゥーン境界 |
| Surface Micro Detail | ブルーノイズによる微細な質感ノイズ |
| Specular and Reflection | Dual-Lobe スペキュラ、MatCap |
| Optional Effects | SSS / Peach Fuzz / Rim（いずれも Intensity 0 で OFF） |

> 任意効果（SSS / Peach Fuzz）は初期状態では OFF です。必要に応じて Intensity を上げてください。

## ライセンス

[MIT License](LICENSE.md)

## 作者

Origuma — https://github.com/orig-uma
