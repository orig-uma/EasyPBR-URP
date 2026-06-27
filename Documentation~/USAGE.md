# EasyPBR for URP — 使い方

## 基本手順

1. マテリアルを作成し、シェーダーに `Origuma/EasyPBR_URP/Doll` を指定する
2. Surface Options > Render Mode で Opaque / Cutout / Transparent を選択する（既定 Opaque）
3. Base Map にアルベドテクスチャを割り当てる
4. Shading Style で Toon / Smooth を選択する

- **セルフシャドウ**のモード選択と推奨設定は [SHADOWS](SHADOWS.md) を参照。
- **アウトライン**を使うには `DollOutlineFeature` を Renderer に追加する（[OUTLINE](OUTLINE.md)）。
- **旧バージョンからの移行**（特に SSS マップの変更）は [MIGRATION](MIGRATION.md) を参照。

## インスペクター

`DollShaderGUI` により Custom / Default UI を切り替えられる。

| モード | 表示 |
| :--- | :--- |
| Custom | セクション分け UI |
| Default | Unity 標準のプロパティ一覧 |

カスタム Inspector では、**シェーダーバリアントを生成するプロパティに ⚡ マーク**が付く（混在すると SRP Batcher のバッチが分断される目印 → [SRP_BATCHER](SRP_BATCHER.md)）。新規のマップ系（Bent Normal / Curvature / SSS / Hair Flow / Clearcoat）はすべて uniform 動的分岐で **⚡ は付かない**（バリアント非増）。

## パラメータ（Custom UI）

セクションの並びと項目名は、Custom UI に表示されるラベルと一致させている。

| セクション | 主な項目 |
| :--- | :--- |
| Surface Options | Render Mode (Preset)、Cull、ZWrite、ZTest、Source Blend、Destination Blend、Alpha Blend (Transparent)、Alpha Clipping、Alpha Cutoff、Shadow Cutoff Bias、Stencil（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Base Core | Base Map、Color Correction（Hue Shift / Saturation / Value Multiplier）、Detail Map、Normal Map、Detail Normal Map |
| Light and Shadow | Shading Style、Shadow Color、Face SDF Shadow（Enable / Map / Flip Forward / Softness / External Shadow Mix / Front Blend / Front Fade）、Receive Shadow Mask、Receive Shadow Strength、Self Shadow Mode、Shadow Softness、Receiver Normal Bias、Shadow Edge Dither、Light Wrap、Toon Threshold / Toon Softness、Auto Face Shadow Fix（Front / Up Brightness、Mask Falloff）、Anti-Blowout（Diffuse Light Limit、Additional Light Blend）、Bent Normal（Map / Strength：間接光・反射の方向補正） |
| Specular and Reflection | Specular Model（BlinnPhong / GGX）、Specular Anti-Aliasing、Fresnel (F0)、Specular Mask、Primary（Color / Smoothness / Intensity / Primary Light Limit）、Secondary（Color / Smoothness / Intensity / Secondary Light Limit）、Environment Reflection、Clearcoat（Mask / Strength / Smoothness / Refl Strength）＋Iridescence（Intensity / Thickness / Shift）、Anisotropic（Thickness / Position Offset / Angle / Strand Scale / Strand Strength / Strand Direction、Sub Highlight、Hair Flow Map / Strength）、MatCap（Blend Mode / Texture / Tint / Intensity / Light Influence） |
| Outline | Enable Outline、Color、Width、Cutoff Shift、Masking (Stencil)（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| Emission | Enable Emission、Emission Map & Color、Intensity |
| Optional Effects | Glitter（Mask / Color / Intensity / Density Scale / Dot Size / Normal Tilt / Sparsity / Iridescence / Iridescence Shift / Base Reflection）、SSS（Map（RGB=透過方向 / A=厚み）/ Color / Intensity / Falloff / Distortion）、Peach Fuzz（Color / Intensity / Width）、Rim Light（Color / Intensity / Thickness）、Grain（Intensity / Scale）、Curvature（Map / Strength）、Occlusion（AO Map / Strength）、Cavity（Map / Strength） |
| Special Effects | Dissolve（Amount / Invert / Axis / Start Y / End Y / Noise / Edge Outer Color / Edge Inner Color / Edge Width / Step Edge）、Black Out |
| Blue Noise | Blue Noise Texture（影ディザ・グレイン共通） |
| Advanced Options | GPU Instancing、Double-Sided GI |
| Baking (Map Generator) | Source Root（Hierarchy 選択から自動補完）、AO / Bent Normal / Cavity / Curvature / SSS / Hair Flow / Face SDF Shadow（各 Foldout に Resolution・Smooth・Blur 等 → 対応スロットへ自動アサイン） |

> **クリアコート**: 下地の陰影を一切暗くしない**加算専用**の薄い光沢層。瞳・唇・爪のツヤ向け。視点依存のフレネルで斜めほど艶が強まり、Iridescence（薄膜の虹色）を少量乗せるとカメラを振ったとき色がうっすら回る（AR 映え）。マスク（`_ClearcoatMask`）で艶の置き場を限定する（Cavity / Curvature を挿せば「くぼみに艶を溜める」「稜線で光らせる」も可。陰影は増えない）。瞳の目安: Smoothness 0.92〜0.97、Iridescence Intensity 0.2〜0.4。

> **ベイク（マップ生成）**: DCC 不要でメッシュからマップを焼く Editor 機能（内部実装は [ARCHITECTURE](ARCHITECTURE.md) の「ベイク」節）。マテリアル Inspector の Baking セクションで **Source Root**（Root 配下の全メッシュが対象）を指定し、種別ごとに 1 ボタンで焼く。生成 PNG はマテリアル隣の `Baked/` に保存され、該当スロットへ自動アサイン（非破壊・再ベイク可）。書き込みは編集中マテリアルのサブメッシュのみ。マテリアル複数選択時は選択中すべてに実行。**焼くと対応機能を自動で有効化**（Strength / Intensity を OFF なら ON に）。

### Baking セクション（マップ種別）

パネルの並び順: **AO → Bent Normal → Cavity → Curvature → SSS → Hair Flow → Face SDF Shadow**。

| Foldout | 主なパラメータ | 出力先 | 備考 |
| :--- | :--- | :--- | :--- |
| Ambient Occlusion | Resolution, Samples, Max Distance, Intensity, Ignore Enclosed, Floor, Smooth, Blur | `_OcclusionMap` | 全パーツを遮蔽源にした半球レイ AO |
| Bent Normal | Resolution, Samples, Max Distance, Strength, Smooth, Blur | `_BentNormalMap` | RGB=接線空間ベント法線 / A=開き具合。間接光方向の補正＋反射のスペキュラ遮蔽 |
| Cavity | Resolution, Intensity, Smooth, Blur | `_CavityMap` | くぼみ検出（レイ不要） |
| Curvature | Resolution, Intensity, Smooth, Blur | `_CurvatureMap` | 符号付き曲率（0.5=平坦/明=凸/暗=凹）。1 枚で稜線・くぼみ両マスク |
| SSS | Resolution, Samples, Max Distance, Intensity, Smooth, Blur | `_SSSMap` | RGB=透過方向 / A=厚み（薄い＝SSS 強）。旧 Thickness を統合・置換 |
| Hair Flow | Resolution, Curvature Mode, Smooth, Blur | `_HairFlowMap` | 形状から毛流れ軸を推定（倍角＋信頼度）。髪/布の異方性を安定化 |
| Face SDF Shadow | Resolution, Flip Forward, Angle Steps, Cast Shadow, Cast Distance, Smooth, Blur | `_FaceSDFMap` | **RGBA 4ch**（下記）。顔マテリアルで焼き、Light and Shadow の Face SDF Shadow を有効化 |

**Face SDF マップ（4 チャンネル）**: `EasyPbrFaceSdfBaker` が **R=右 / G=左 / B=上 / A=下** の 4 方向スイープを 1 枚に焼く。ランタイムはメインライト方向に応じて 4ch を加重ブレンドし顔影を駆動（シャドウマップ非依存・左右非対称の顔も可）。影の挙動は [SHADOWS](SHADOWS.md) を参照。

> **タンジェント必須のマップ**: Bent Normal / SSS（透過方向）/ Hair Flow は接線空間に焼くため、メッシュにタンジェント（または UV）が必要。無い場合は方向が幾何法線へフォールバックする（厚み等のスカラ成分は有効）。

> **SSS の移行注意**: 旧 `_SSSMask`（R=厚み）は `_SSSMap`（RGBA）に統合された。チャンネル構成が変わるため**再ベイクが必要**（→ [MIGRATION](MIGRATION.md)）。
