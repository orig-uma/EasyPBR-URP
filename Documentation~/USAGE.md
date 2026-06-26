# EasyPBR for URP — 使い方

## 基本手順

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を指定する
2. Surface Options > Render Mode で Opaque / Cutout / Transparent を選択する（既定 Opaque）
3. Base Map にアルベドテクスチャを割り当てる
4. Shading Style で Toon / Smooth を選択する

- **セルフシャドウ**のモード選択と推奨設定は [SHADOWS](SHADOWS.md) を参照。
- **アウトライン**を使うには `DollOutlineFeature` を Renderer に追加する（[OUTLINE](OUTLINE.md)）。

## インスペクター

`DollShaderGUI` により Custom / Default UI を切り替えられる。

| モード | 表示 |
| :--- | :--- |
| Custom | セクション分け UI |
| Default | Unity 標準のプロパティ一覧 |

カスタム Inspector では、**シェーダーバリアントを生成するプロパティに ⚡ マーク**が付く（混在すると SRP Batcher のバッチが分断される目印 → [SRP_BATCHER](SRP_BATCHER.md)）。

## パラメータ（Custom UI）

セクションの並びと項目名は、Custom UI に表示されるラベルと一致させている。

| セクション | 主な項目 |
| :--- | :--- |
| Surface Options | Render Mode (Preset)、Cull、ZWrite、ZTest、Source Blend、Destination Blend、Alpha Blend (Transparent)、Alpha Clipping、Alpha Cutoff、Shadow Cutoff Bias、Stencil（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Base Core | Base Map、Color Correction（Hue Shift / Saturation / Value Multiplier）、Detail Map、Normal Map、Detail Normal Map |
| Light and Shadow | Shading Style、Shadow Color、Face SDF Shadow（Enable / Map / Flip Forward / Softness / External Shadow Mix / Front Blend / Front Fade）、Receive Shadow Mask、Receive Shadow Strength、Self Shadow Mode、Shadow Softness、Receiver Normal Bias、Shadow Edge Dither、Light Wrap、Toon Threshold / Toon Softness、Auto Face Shadow Fix（Front / Up Brightness、Mask Falloff）、Anti-Blowout（Diffuse Light Limit、Additional Light Blend） |
| Specular and Reflection | Specular Model（BlinnPhong / GGX）、Specular Anti-Aliasing、Fresnel (F0)、Specular Mask、Primary（Color / Smoothness / Intensity / Primary Light Limit）、Secondary（Color / Smoothness / Intensity / Secondary Light Limit）、Environment Reflection、Anisotropic（Thickness / Position Offset / Angle / Strand Scale / Strand Strength / Strand Direction、Sub Highlight）、MatCap（Blend Mode / Texture / Tint / Intensity / Light Influence） |
| Outline | Enable Outline、Color、Width、Cutoff Shift、Masking (Stencil)（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Emission | Enable Emission、Emission Map & Color、Intensity |
| Optional Effects | Glitter（Mask / Color / Intensity / Density Scale / Dot Size / Normal Tilt / Sparsity / Iridescence / Iridescence Shift / Base Reflection）、SSS（Color / Intensity / Falloff / Distortion）、Peach Fuzz（Color / Intensity / Width）、Rim Light（Color / Intensity / Thickness）、Grain（Intensity / Scale）、Occlusion（AO Map / Strength） |
| Special Effects | Dissolve（Amount / Invert / Axis / Start Y / End Y / Noise / Edge Outer Color / Edge Inner Color / Edge Width / Step Edge）、Black Out |
| Blue Noise | Blue Noise Texture（影ディザ・グレイン共通） |
| Advanced Options | GPU Instancing、Double-Sided GI |
| Baking (Map Generator) | Source Root（Hierarchy 選択から自動補完）、Ambient Occlusion / Cavity / Thickness (SSS) / Face SDF Shadow（各 Foldout に Resolution・Smooth・Blur 等 → 対応スロットへ自動アサイン） |

> **ベイク（マップ生成）**: DCC 不要でメッシュからマップを焼く Editor 機能（内部実装は [ARCHITECTURE](ARCHITECTURE.md) の「ベイク」節）。マテリアル Inspector の Baking セクションで **Source Root**（Root 配下の全メッシュが対象）を指定し、種別ごとに 1 ボタンで焼く。生成 PNG はマテリアル隣の `Baked/` に保存され、該当スロットへ自動アサイン（非破壊・再ベイク可）。書き込みは編集中マテリアルのサブメッシュのみ。マテリアル複数選択時は選択中すべてに実行。

### Baking セクション（マップ種別）

| Foldout | 主なパラメータ | 出力先 | 備考 |
| :--- | :--- | :--- | :--- |
| Ambient Occlusion | Resolution, Samples, Max Distance, Intensity, Ignore Enclosed, Floor, Smooth, Blur | `_OcclusionMap` | 全パーツを遮蔽源にした半球レイ AO |
| Cavity | Resolution, Intensity, Smooth, Blur | `_CavityMap` | くぼみ検出（レイ不要） |
| Thickness (SSS) | Resolution, Samples, Max Distance, Intensity, Smooth, Blur | `_SSSMask` | 薄い部分が白（SSS 強） |
| Face SDF Shadow | Resolution, Flip Forward, Angle Steps, Cast Shadow, Cast Distance, Smooth, Blur | `_FaceSDFMap` | **RGBA 4ch**（下記）。顔マテリアルで焼き、Light and Shadow の Face SDF Shadow を有効化 |

**Face SDF マップ（4 チャンネル）**: `EasyPbrFaceSdfBaker` が **R=右 / G=左 / B=上 / A=下** の 4 方向スイープを 1 枚に焼く。ランタイムはメインライト方向に応じて 4ch を加重ブレンドし顔影を駆動（シャドウマップ非依存・左右非対称の顔も可）。影の挙動は [SHADOWS](SHADOWS.md) を参照。
