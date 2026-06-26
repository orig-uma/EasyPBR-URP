# EasyPBR for URP — シェーダーバリアント（キーワード）

バリアントを生むキーワードと、それを切り替えるマテリアルプロパティの対応。バッチングへの影響は [SRP_BATCHER](SRP_BATCHER.md) を参照。

## 機能キーワード（`shader_feature_local` — マテリアルの設定で切り替え）

| キーワード | 状態数 | 対応プロパティ（UI ラベル） | 対象パス |
| :--- | :---: | :--- | :--- |
| `_ALPHATEST_ON` | 2 | `_AlphaClip`（Alpha Clipping） | ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Outline |
| `_SHADOWMODE_OFF` / `_TENTPCF` / `_VOGELPCF` / `_PCSS` | 4 | `_ShadowMode`（Self Shadow Mode: Off / PCF (Tent) / PCF (Vogel) / PCSS） | ForwardLit |
| `_DISSOLVE_ON` | 2 | `_UseDissolve`（Enable Dissolve） | ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Outline |
| `_DISSOLVETYPE_NONE` / `_WORLDY` / `_LOCALY` | 3 | `_DissolveType`（Dissolve Axis: None / WorldY / LocalY） | ForwardLit / ShadowCaster / DepthOnly / DepthNormals / Outline |
| `_OUTLINE_ON` | 2 | `_UseOutline`（Enable Outline） | Outline |

> MatCap / Emission / Color Correction は keyword を廃止し、`_UseMatCap` / `_UseEmission` / `_UseColorCorrection`（Float）による `UNITY_BRANCH` の動的分岐にしている。無効時はテクスチャサンプルごとスキップされ、バリアントは増えない。**環境反射（`_ReflectionStrength`）も同様の uniform 動的分岐**で、0 のとき cube サンプルごとスキップされバリアントを増やさない。Specular AA / Occlusion / Detail Normal は常時計算（または既定テクスチャで無影響）でキーワードを持たない。**顔 SDF シャドウ（`_UseFaceSDF` 他）も uniform 動的分岐**（OFF 時はサンプルごとスキップ）でバリアント非増。ベイカーは Editor 専用ツールでランタイム・バリアントに影響しない。
>
> Shading Style（Smooth / Toon）/ Specular Model（BlinnPhong / GGX）/ Alpha Blend（Transparent）も同様に keyword を廃止し、`_ShadingStyle` / `_SpecularModel` の uniform 動的分岐、および Alpha 出力の常時化に移行した（0.3.5）。これらはマテリアル間で値が割れやすく、keyword 分岐のままだと**同時描画時に SRP Batcher のバッチが分断される**ため。分岐自体は軽量（threshold vs ramp / 関数選択 / 1 行）なので、バリアント削減のメリットが上回る。

## システムキーワード（`multi_compile` — URP が常に全て生成）

| キーワードセット | 状態数 |
| :--- | :---: |
| `_MAIN_LIGHT_SHADOWS` / `_CASCADE` / `_SCREEN` | 4 |
| `_ADDITIONAL_LIGHTS_VERTEX` / `_ADDITIONAL_LIGHTS` | 3 |
| `_FORWARD_PLUS` / `_CLUSTER_LIGHT_LOOP` | 3 |
| `_ADDITIONAL_LIGHT_SHADOWS` | 2 |
| `_SHADOWS_SOFT` | 2 |
| `_CASTING_PUNCTUAL_LIGHT_SHADOW`（ShadowCaster） | 2 |

## バリアント数（理論上の最大）

| Pass | 機能（`shader_feature`） | システム（`multi_compile`） | 合計 |
| :--- | ---: | ---: | ---: |
| ForwardLit | 2·4·2·3 = **48** | 4·3·3·2·2 = **144** | **6,912** |
| ShadowCaster | 2·2·3 = **12** | 2 | **24** |
| DepthOnly | 2·2·3 = **12** | — | **12** |
| DepthNormals | 2·2·3 = **12** | — | **12** |
| Outline | 2·2·2·3 = **24** | — | **24** |
| **総計** | | | **6,984** |

> `shader_feature_local` はプロジェクト内のマテリアルが実際に使う組み合わせのみビルドに含まれる（1 マテリアルは機能キーワードの 1 通りを選ぶだけ）。一方 `multi_compile` は常に全展開されるため、**実ビルドのバリアント数は概ね「使用中の機能組み合わせ数 × システム 144（ForwardLit）」程度**に収まり、上の理論最大には達しない。
>
> バリアントを生成するプロパティは、カスタム Inspector 上で **⚡ マーク**で明示される。これらの値が同時描画されるマテリアル間で割れると SRP Batcher のバッチが分断される。バッチングを効かせる指針は [SRP_BATCHER](SRP_BATCHER.md) を参照。
