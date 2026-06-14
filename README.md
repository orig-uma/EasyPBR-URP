# EasyPBR for URP

`Origuma/EasyPBR_URP/Doll`

Universal Render Pipeline (URP) 向けの、PBRとトゥーン表現を両立させたキャラクター用シェーダーです。
フィギュアや人形のような「質感のあるリッチなハイライト」と「平面的で破綻しないアニメ調の陰影」を同時に狙えるように設計されています。

## インストール

### Package Manager から Git URL で追加

Unity の `Window > Package Manager > +（左上）> Add package from git URL...` に以下を入力します。

```
https://github.com/orig-uma/EasyPBR-URP.git
```

## コンセプト：なぜ "Easy" なのか？ 

EasyPBRはその名の通り、**「誰でも簡単に、迷わず直感的にクオリティの高い絵作りができること」**を最優先（UXファースト）に設計されています。

* **「数式」ではなく「人間の直感」で触れるUI**
  従来のシェーダーでよくある「べき乗（Power）」といった数学的なスライダーを排除。Rim Light や Peach Fuzz などの調整は「太さ（Thickness）」という直感的な概念に変換されており、スライダーを右に動かすほど素直に効果が太くなります。
* **「テクスチャを描く手間」をスキップ**
  キャラクターの顔に綺麗な影を出すために、従来必要だった「顔影用のマスクテクスチャ」を描く必要はありません。数学的な計算（プロシージャル）によって自動で顔の汚い自己陰を消し去るため、テクスチャ1枚を割り当てるだけで即座に美しいキャラクター表現が完成します。
* **「めんどくさい設定」はワンクリックで自動化**
  Unityの半透明設定は、Render Queue、Blend Mode、ZWriteなど、初心者にとって挫折しやすい設定の宝庫です。EasyPBRでは「Render Mode プリセット」を切り替えるだけで、裏側で最適な設定をすべて一括セットアップします。

---

## 特徴 (Features)

* **Toon / Smooth の2モード切替**
  マテリアルごとにパキッとしたアニメ調か、滑らかなPBR調かを切り替え可能です。
* **Auto Face Shadow Fix (顔影の自動補正)**
  マスクテクスチャを用意することなく、鼻や頬の凹凸が作る汚い自己陰を、法線平滑化＋プロシージャルマスクによって自動で抑制します。
* **マッハバンドの回避**
  落ち影(Shadow map)と陰影(NdotL)を分離合成し、トゥーン境界に出やすいマッハバンド（縞模様）を回避。さらにブルーノイズディザで滑らかに馴染ませます。
* **多彩な質感表現 (既定OFF・軽量設計)**
  Dual-Lobe スペキュラ（鋭い/柔らかい2ローブ）による高度なハイライトに加え、SSS / Rim Light / Peach Fuzz / MatCap を搭載。
  *※追加効果は Intensity 0 の時は uniform 分岐により GPU 計算を自動でスキップするため、無駄な負荷や variant の増加を防ぎます。*
* **リッチな Dissolve (消失エフェクト)**
  消失の最前線（高輝度エッジ）と内側（焦げ色）の2色グラデーションに対応。さらにアニメやグリッチ表現に最適な「パキッとした段階化 (Step Edge)」機能により、マグマのように溶ける表現やサイバーチックな消滅を簡単に実装できます。

## ファイル構成 (開発者向け)

今後の拡張性とメンテナンス性を高めるため、機能ごとにHLSLファイルをモジュール分割しています。

* `Doll.shader` : ShaderLabの定義、Propertiesの宣言
* `EasyPBR_Input.hlsl` : SRP Batcher対応の共通変数・テクスチャ宣言
* `EasyPBR_Effects.hlsl` : Dissolve, MatCap, Emission などの汎用エフェクト
* `EasyPBR_Lighting.hlsl` : 汎用的なPBRライティング・質感計算ロジック
* `Doll_FaceLogic.hlsl` : キャラクター特有の顔影消し・Toon処理ロジック
* `Doll_ForwardPass.hlsl` / `Doll_ShadowPass.hlsl` : パスごとのメイン処理

簡易的なシェーダー（Unlitや背景用など）を自作する際も、`EasyPBR_Input.hlsl` 等をインクルードすることで簡単に機能を流用・共有できます。

## 動作環境

* Unity 2022.2 以降 (Forward+ / Cluster Light Loop 対応)
* Unity 6000.3 以降
* Universal RP 14.0 以降

### ローカル / Embedded

このリポジトリをプロジェクトの `Packages/com.origuma.easypbr-urp` に配置すると、埋め込みパッケージとして認識されます。

## 使い方

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を選択。
2. Surface Options 内の Render Mode (プリセット) から、不透明・くり抜き・半透明 の用途に合わせてモードを選択します（自動で各種設定が行われます）。
3. `Base Map` にアルベドテクスチャを設定。
4. `Shading Style` で `Toon` / `Smooth` を選択。

### 主なパラメータ

マテリアルのインスペクターは、機能ごとにグループ化されています。

| グループ (セクション) | 主な設定内容 |
| :--- | :--- |
| **Surface Options** | Render Mode（不透明・くり抜き・半透明の一括セットアップ）、両面描画(Cull)、ZWrite設定など |
| **Stencil** | ステンシルテストの参照値や比較条件の設定 |
| **Base Core** | 基本となるテクスチャ（Base Map）と色（Base Color） |
| **Auto Face Shadow Fix** | 顔の自己陰を消すための補正設定（正面/上向きの明るさ、逆光の陰の維持、法線平滑化） |
| **Light and Shadow** | Toon/Smoothの切替、影の色、落ち影の強さ/ソフトさ/ディザリング、白飛び防止(Light Limit) |
| **Surface Micro Detail** | 肌や布の質感を表現するブルーノイズ（グレイン）の強度とスケール |
| **Specular and Reflection** | 2層のハイライト（Primary: シャープ / Secondary: マット）と、MatCap（擬似反射） |
| **Emission** | 自己発光（HDRカラー、マスクテクスチャ、発光強度） |
| **Dissolve** | 消失エフェクト。2色（Outer/Inner）のエッジカラー、段階化（Step Edge）、消失方向の設定 |
| **Optional Effects** | SSS (表面下散乱)、Peach Fuzz (産毛のような縁の光沢)、Rim Light (リムライト) ※FuzzとRimの「太さ」は `0.0～1.0` で直感的に指定可能 |

## ライセンス

[MIT License](LICENSE.md)

## 作者

Origuma — https://github.com/orig-uma
