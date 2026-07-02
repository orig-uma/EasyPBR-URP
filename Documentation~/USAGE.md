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
| Custom | **タブ分割 UI**（下記）＋プロパティ検索 |
| Default | Unity 標準のプロパティ一覧 |

Custom UI は最上部の**タブバー**（基本 / 陰・影 / ライト / スペキュラ / 質感 / 演出 / Baking）でページを切り替える。選択中のタブは記憶される。タブバー直上の**検索ボックス**に入力すると、タブを横断して英語の表示名・プロパティ名に部分一致する項目がフラットに列挙される（どのタブにあるか探す必要がない。検索を消すとタブ表示に戻る）。

**シェーダーバリアントを生成するプロパティには ⚡ マーク**が付く（混在すると SRP Batcher のバッチが分断される目印 → [SRP_BATCHER](SRP_BATCHER.md)）。マップ系（Bent Normal / Curvature / SSS / Hair Flow / Clearcoat 等）はすべて uniform 動的分岐で **⚡ は付かない**（バリアント非増）。

## パラメータ（Custom UI）

タブとセクションの並び・項目名は、Custom UI に表示されるラベルと一致させている。

| タブ > セクション | 主な項目 |
| :--- | :--- |
| 基本 > Surface Options | Render Mode (Preset)、Cull、ZWrite、ZTest、Source Blend、Destination Blend、Alpha Blend (Transparent)、Alpha Clipping、Alpha Cutoff、Shadow Cutoff Bias、Stencil（Ref / Compare Function / Pass / Fail / ZFail Operation） |
| 基本 > Base Core | Base Map、Color Correction（Hue Shift / Saturation / Value Multiplier）、Detail Map、Normal Map、Detail Normal Map |
| 基本 > Emission | Enable Emission、Emission Map & Color、Intensity |
| 陰・影 > Shade | Shading Style、Light Wrap、Toon Threshold / Toon Softness、Shadow Color、Shadow Hue Shift / Shadow Saturation、2nd Shadow（Color / Threshold / Softness）、Cast Shadow Color、Shade Normal（Map / Strength） |
| 陰・影 > Self Shadow | Receive Shadow Mask、Receive Shadow Strength、Self Shadow Mode、Receiver Normal Bias、Shadow Softness、Shadow Edge Dither |
| 陰・影 > Face | Face SDF Shadow（Enable / Map / Flip Forward / Softness / External Shadow Mix / Blend Normal Min / Max）、Auto Face Shadow Fix（Front / Up Brightness、Mask Falloff） |
| ライト | Light Conditioning（Light Color Influence / Saturation Limit / Min Brightness / Condition Additional Lights）、Fill Light（Color / Intensity / Pitch / Yaw / Shade Side Only）、Indirect Light（Flatten / Intensity / Tint）、Anti-Blowout（Diffuse Light Limit、Additional Light Blend） |
| スペキュラ | Specular Model（BlinnPhong / GGX）、Specular Anti-Aliasing、Fresnel (F0)、Specular Mask、Primary（Color / Smoothness / Intensity / Light Limit）、Secondary（Color / Smoothness / Intensity / Light Limit）、Stylize（Toon Specular / Threshold / Softness、Specular Shade Dimming）、Environment Reflection、Anisotropic（Thickness / Position Offset / Angle / Strand Scale / Strength / Direction、Sub Highlight、Hair Flow Map / Strength）、MatCap（Blend Mode / Texture / Tint / Intensity / Light Influence） |
| 質感 > Coat and Glitter | Clearcoat（Mask / Strength / Smoothness / Refl Strength）＋Iridescence（Intensity / Thickness / Shift）、Glitter（Mask / Color / Intensity / Density Scale / Dot Size / Normal Tilt / Sparsity / Iridescence / Iridescence Shift / Base Reflection） |
| 質感 > Skin and Edge | Skin Scatter（Color / Intensity / Width / Curvature Mask）、SSS（Map（RGB=透過方向 / A=厚み）/ Color / Intensity / Falloff / Distortion）、Peach Fuzz（Color / Intensity / Width）、Rim Light（Color / Intensity / Thickness）、Grain（Intensity / Scale） |
| 質感 > Baked Maps | Occlusion（AO Map / Strength）、Bent Normal（Map / Strength）、Cavity（Map / Strength）、Curvature（Map / Strength） |
| 演出 | Outline（Enable / Color / Albedo Blend / Width / Cutoff Shift / Masking (Stencil)）、Dissolve（Amount / Invert / Axis / Start Y / End Y / Noise / Edge Outer / Inner Color / Edge Width / Step Edge）、Black Out |
| Baking | Source Root（Hierarchy 選択から自動補完）、Bake All、AO / Bent Normal / Shade Normal / Cavity / Curvature / SSS / Hair Flow / Face SDF Shadow（各 Foldout に Resolution・Smooth・Blur 等 → 対応スロットへ自動アサイン）、Blue Noise、Advanced（GPU Instancing、Double-Sided GI） |

> **クリアコート**: 下地の陰影を一切暗くしない**加算専用**の薄い光沢層。瞳・唇・爪のツヤ向け。視点依存のフレネルで斜めほど艶が強まり、Iridescence（薄膜の虹色）を少量乗せるとカメラを振ったとき色がうっすら回る（AR 映え）。マスク（`_ClearcoatMask`）で艶の置き場を限定する（Cavity / Curvature を挿せば「くぼみに艶を溜める」「稜線で光らせる」も可。陰影は増えない）。瞳の目安: Smoothness 0.92〜0.97、Iridescence Intensity 0.2〜0.4。

> **影色の色相・彩度（Shadow Hue Shift / Saturation）**: 陰側のベースカラーを HSV で補正してから Shadow Color を乗算する。物理的に正しい「暗くなるだけの影」より、色相が少し転がり彩度が残る影のほうが絵として豊かに見える（肌の陰なら Hue Shift を負側＝赤紫方向に 0.02〜0.06、Saturation 1.1〜1.3 が目安）。マスク・テクスチャ不要のプロシージャル制御で、既定値（0 / 1）のときは HSV 変換ごとスキップされる。

> **スペキュラのスタイライズ（Toon Specular / Shade Dimming）**: **Toon Specular** はスペキュラのトーンマップ後輝度をしきい値で切り、縁のパキッとした様式的ハイライトにする（内側のグラデーションは保持、fwidth による最低 1px の AA 付き）。0〜1 で連続 ⇄ トゥーンをブレンドでき、HDR カラーと組み合わせると「形はパキッと・輝度は Bloom 越え」の強いハイライトが作れる。Threshold を低くすると広い Secondary ローブも生き残る。**Specular Shade Dimming** は陰ランプ（1影・2影）に入った面のハイライトを沈める。落ち影（shadow map）では従来から消えるが、角度ベースの陰の中でもハイライトを消したいアニメ的に厳密な絵作り向け（1 で完全消灯）。両方とも全ライト（メイン＋追加）に適用され、既定 0 で素通し。

> **落ち影の色分離（Cast Shadow Color）**: 落ち影（shadow map）を角度の陰とは別の色で塗る。実写・映像の照明では落ち影は環境光（空・壁）だけに照らされるため陰よりも色相が転ぶことが多く、落ち影だけをわずかに寒色へ振ると首元・前髪の落ち影に情報量が出て映像的な画になる。色は Shadow Hue Shift / Saturation の補正を陰と共有した上で Cast Shadow Color を乗算。顔 SDF 領域では External Shadow Mix の設定に従って落ち影成分だけが塗り分けられる。A=0（既定）で従来どおり陰と同色。

> **シェーディング法線（Shade Normal）**: ベイクした平滑化法線で**拡散の陰ランプだけ**を駆動する（スペキュラ・リム・SSS はディテール法線のまま＝質感のディテールは保持）。シワ・ファセット・細かい起伏が陰のグラデーションに入り込んで汚く割れるのを防ぎ、陰の輪郭を一本の綺麗な曲線として通す。ソフトなグラデーション基調の絵作りでは、滑らかなランプほどメッシュの起伏が目立つため特に効果が大きい。ベイクは位置溶接（UV 継ぎ目・硬エッジで分割された頂点を同一点として扱う）の上でラプラシアン平滑化するため、継ぎ目で陰が割れない。Smooth Normals（平滑化回数）を上げるほど大きな起伏まで無視される。服・髪で顕著、レイ不要で高速。既定 OFF（bump / Strength 0）。

> **キャラ用ライト整形（Light Conditioning）**: メインライトの色をキャラの可読性側へ整形する防御層。Anti-Blowout（上限）と対になる「下限と色の防御」で、3 本とも既定値では完全素通し。**Light Color Influence** はライト色の影響度（下げると同輝度の白色光として扱われ、原色のステージ照明でもキャラの色設計が保たれる。0.6〜0.8 で「照明の雰囲気は残しつつ肌色が死なない」バランス）。**Light Saturation Limit** はライト彩度の上限（色相は保持したまま彩度だけ抑える。深い赤・青の単色照明対策）。**Light Min Brightness** はライト輝度の下限（暗所でもキャラが完全黒に沈まない。意図的な暗転は Black Out を使う）。既定ではメインライトのみに適用され、追加ライト（ステージの色物ライト）はそのまま乗る。**Condition Additional Lights** を ON にすると追加ライトにも Color Influence / Saturation Limit が適用される（原色スポットが肌に直撃するケースの防御）。Min Brightness だけは常にメインライト限定——ポイントライト 1 灯ごとに下限を持たせると、届いていない場所まで灯数ぶん持ち上がってしまうため。

> **フィルライト（Fill Light / 照り返し）**: 指定方向からのバウンス光を陰側に注ぐプロシージャルなフィルライト。床からの暖色の照り返し（Pitch -90 付近）、空や壁面からの寒色の回り込みなどを、シーンにライトを追加せず 1 マテリアルで完結できる。Half-Lambert で柔らかく回り込み、**Shade Side Only** で主光の陰側だけに限定（1・既定＝照り返しらしい見た目。0 で全面に乗る第 2 フィルライト）。メインライトの明るさから独立した加算光なので、暗いシーンでも設定した強さで発色し、陰の中に方向性のあるグラデーションが生まれる。既定 OFF（Intensity 0 で計算ごとスキップ）。

> **間接光の整形（Indirect Light）**: ライトプローブ / アンビエントの SH をキャラ向けに整える。**Flatten** は SH の方向成分を潰して定数項（平均環境光）へ寄せるスライダーで、会場 GI が方向性を持つときに顔へ出る明暗ムラを消し、キャラ全体を均一なアンビエントで包む（セル画的な安定感）。ベイク済み Bent Normal による「間接光の方向補正」とはトレードオフの関係——物理的な正しさを足すのが Bent Normal、様式的な均一さに寄せるのが Flatten。**Intensity / Tint** で間接光の寄与と色味を調整できる（メインライトには影響しない）。3 本とも既定値（0 / 1 / 白）で素通し。間接光は Anti-Blowout（Diffuse Light Limit）の**外側**で加算される——上限は直接光にのみ適用され、間接光の上限管理は Intensity が担う（合算クランプだと直接光が上限に達した時点で間接光が消えるため）。

> **2影（2nd Shadow）**: 1影より深い位置に第2の陰ランプを重ねる、アニメ塗りの基本となる 2 段構成。「明 → 1影 → 2影」の 3 階調になり、頬・首元・髪の内側に陰の層が生まれる。しきい値・ぼかし幅は 1影（Toon Threshold / Softness）から独立し、色は Shadow Hue Shift / Saturation の補正を 1影と共有した上で 2nd Shadow Color を乗算する。光の角度ベースで駆動し、**落ち影（shadow map）は 1影のまま**（落ち影が最暗になって絵が重くならない）。顔 SDF 領域では SDF 由来の連続値で駆動するため、SDF が消した法線由来の陰バンドが顔に再発しない。Skin Scatter 有効時は 1影・2影両方の境界に散乱がにじむ。Toon では境界がもう 1 本増える形、Smooth では Softness を上げて深部への柔らかいグラデーションとして使える。A=0 で OFF（既定）。

> **スキンスキャッタ（Skin Scatter）**: 明暗境界（ターミネータ）に散乱色を滲ませる pre-integrated skin scattering の近似。実際の肌は明暗境界で光が表皮下に潜って赤く透けるため、この 1 本で「のっぺりした陰」が血色のある肌に変わる。バンドは最終陰影値（`finalShade`）の遷移域から取るので、Toon のランプ境界・Smooth のグラデーション・落ち影のペナンブラ・顔 SDF の境界のいずれにも同じ設定で乗る。Curvature Mask を上げるとベイク済み曲率マップ（`_CurvatureMap` / Strength > 0 が必要）で耳・鼻・指など薄い部位ほど強く散乱する。マスク・テクスチャ不要（曲率は任意）。肌の目安: Color は既定の赤系のまま Intensity 0.3〜0.6、Width 0.4〜0.7。

## マテリアル一括置換（Material Replacer）

`Window > EasyPBR > Material Replacer` で、対象オブジェクト配下の全 Renderer のマテリアルを、指定フォルダ内の**同名マテリアル**へ一括で差し替えられる（Undo 可・アセット非破壊）。元モデルのマテリアルと同じ名前で EasyPBR 版マテリアルを 1 フォルダに用意しておけば、モデル一式の移行がワンクリックで済む。

## ライブ演出のランタイム制御（DollLiveDirector）

キャラの Root に `DollLiveDirector` コンポーネント（Add Component > EasyPBR > Doll Live Director）を付けると、配下の Doll マテリアルすべてに対して演出系プロパティをまとめて制御できる。

| グループ | 制御対象 | 用途 |
| :--- | :--- | :--- |
| Black Out | `_BlackOut` | 曲間の暗転・カットイン |
| Dissolve | `_DissolveAmount` | 登場・消失演出（マテリアル側で Enable Dissolve が必要。Amount 0 で待機） |
| Fill Light | `_FillColor` / `_FillIntensity` | 曲ごとの照り返し色の切り替え（サビで暖色を注ぐ等） |

- 各グループは **Override トグルが ON のあいだだけ**上書きし、OFF に戻すとマテリアルの元値へ復元する。
- **Timeline / Animation**: 専用トラックは不要。Animation Track で本コンポーネントのフィールド（`blackOut` 等）を直接キー打ちすれば駆動できる。スクリプトからは `SetBlackOut()` / `SetDissolve()` / `SetFill()` / `ClearOverrides()`。
- **SRP Batcher 維持の設計**: Play 中はマテリアルインスタンス経由で値を書く（別マテリアル同士は SRP Batcher でバッチされる）。`MaterialPropertyBlock` はレンダラーをバッチから外すため**使わない**。Edit モードのプレビューだけは非破壊の MaterialPropertyBlock（共有マテリアル資産を汚さない）。詳細 → [SRP_BATCHER](SRP_BATCHER.md)。

> **ベイク（マップ生成）**: DCC 不要でメッシュからマップを焼く Editor 機能（内部実装は [ARCHITECTURE](ARCHITECTURE.md) の「ベイク」節）。マテリアル Inspector の Baking セクションで **Source Root**（Root 配下の全メッシュが対象）を指定し、種別ごとに 1 ボタンで焼く。生成 PNG はマテリアル隣の `Baked/` に保存され、該当スロットへ自動アサイン。**再ベイクは同名ファイルを上書き**する（連番で増えない。GUID 維持のためアサイン済み参照へ即反映。以前の結果へ戻すには焼き直すかバージョン管理で戻す）。書き込みは編集中マテリアルのサブメッシュのみ。マテリアル複数選択時は選択中すべてに実行。**焼くと対応機能を自動で有効化**（Strength / Intensity を OFF なら ON に）。

### Baking セクション（マップ種別）

**Bake All**: パネル先頭の一括ボタンで、共通 6 種（AO / Bent Normal / Shade Normal / Cavity / Curvature / SSS）を各 Foldout の現在の設定のまま順に焼ける。Hair Flow（髪）と Face SDF（顔）は対象が特定マテリアルに限られるため一括の対象外——それぞれのマテリアルで個別に焼く。

パネルの並び順: **AO → Bent Normal → Shade Normal → Cavity → Curvature → SSS → Hair Flow → Face SDF Shadow**。

| Foldout | 主なパラメータ | 出力先 | 備考 |
| :--- | :--- | :--- | :--- |
| Ambient Occlusion | Resolution, Samples, Max Distance, Intensity, Ignore Enclosed, Floor, Smooth, Blur | `_OcclusionMap` | 全パーツを遮蔽源にした半球レイ AO |
| Bent Normal | Resolution, Samples, Max Distance, Strength, Smooth, Blur | `_BentNormalMap` | RGB=接線空間ベント法線 / A=開き具合。間接光方向の補正＋反射のスペキュラ遮蔽 |
| Shade Normal | Resolution, Smooth Normals, Blur | `_ShadeNormalMap` | 位置溶接＋ラプラシアン平滑化した法線を接線空間に焼く。拡散の陰専用。レイ不要 |
| Cavity | Resolution, Intensity, Smooth, Blur | `_CavityMap` | くぼみ検出（レイ不要） |
| Curvature | Resolution, Intensity, Smooth, Blur | `_CurvatureMap` | 符号付き曲率（0.5=平坦/明=凸/暗=凹）。1 枚で稜線・くぼみ両マスク |
| SSS | Resolution, Samples, Max Distance, Intensity, Smooth, Blur | `_SSSMap` | RGB=透過方向 / A=厚み（薄い＝SSS 強）。旧 Thickness を統合・置換 |
| Hair Flow | Resolution, Curvature Mode, Smooth, Blur | `_HairFlowMap` | 形状から毛流れ軸を推定（倍角＋信頼度）。髪/布の異方性を安定化 |
| Face SDF Shadow | Resolution, Flip Forward, Angle Steps, Cast Shadow, Cast Distance, Smooth, Blur | `_FaceSDFMap` | **RGBA 4ch**（下記）。顔マテリアルで焼き、Light and Shadow の Face SDF Shadow を有効化 |
**Face SDF マップ（4 チャンネル）**: `EasyPbrFaceSdfBaker` が **R=右 / G=左 / B=上 / A=下** の 4 方向スイープを 1 枚に焼く。ランタイムはメインライト方向に応じて 4ch を加重ブレンドし顔影を駆動（シャドウマップ非依存・左右非対称の顔も可）。影の挙動は [SHADOWS](SHADOWS.md) を参照。

> **タンジェント必須のマップ**: Bent Normal / SSS（透過方向）/ Hair Flow は接線空間に焼くため、メッシュにタンジェント（または UV）が必要。無い場合は方向が幾何法線へフォールバックする（厚み等のスカラ成分は有効）。

> **SSS の移行注意**: 旧 `_SSSMask`（R=厚み）は `_SSSMap`（RGBA）に統合された。チャンネル構成が変わるため**再ベイクが必要**（→ [MIGRATION](MIGRATION.md)）。
