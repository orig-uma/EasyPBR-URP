# Changelog

[Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) 形式。[Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [Unreleased]

## [0.7.1] - 2026-08-24

### Fixed

- **本体 Editor asmdef の `versionDefines` 式 `[0.3.0,)` が Unity に無効と判定され（`ExpressionNotValidException`）、Editor アセンブリごとコンパイルされず Inspector のカスタム UI が出なかった問題を修正。** Unity の式は開区間を受け付けず、素の `0.3.0` が「0.3.0 以上」を意味する。0.7.0 で入れた Core 最低バージョン連動の意図はそのまま。

## [0.7.0] - 2026-08-23

### Fixed

- **旧 EasyShaderCore が入ったまま本パッケージを更新すると、Core が更新されず本体 Editor がコンパイルエラーになる問題を修正。** Installer は「Core が存在するか」しか見ておらず、0.7.0 が要求する Core 0.3.0 の新 API（`BlackOutController` / ベイカー拡張）が無い 0.2.0 のままでも無音だった。Installer に必要最低バージョン（0.3.0）の比較を入れ、古ければピン留め URL（`#v0.3.1`）へ差し替える。本体 Editor asmdef の `versionDefines` も `0.3.0`（= 0.3.0 以上）に揃え、古い Core では本体を除外してコンパイルエラーを出さず Installer が走れるようにした。

### Added
- **暗転をキャラ単位で駆動する `BlackOutController` が使えるようになった**（EasyShaderCore に新設。T-364）。`_BlackOut` は Doll / EasyToon Idol で同名・同義なので Doll でもそのまま使える（Play = マテリアルインスタンス / Edit = 非破壊 MPB・Timeline の Animation Track 対応）。**`DollLiveDirector` の Black Out override と同じキャラで併用しないこと**（同じプロパティを奪い合う）。Dissolve のときと同様、いずれ Controller 側へ一本化する余地がある。
- **顔 SDF ベイクに距離場ブレンド整形（DF Blend）を追加**（Baking > Face SDF Shadow、既定 ON。T-346）。頂点スイープの生の出力は影境界の等値線にポリゴン割りと法線ノイズがそのまま出て線がガタつく。手描き SDF ツールの本質工程（白黒マスク → 距離場変換 → ブレンド）を Core ベイカーが画像空間で内蔵し、等値線を距離幾何で丸め直すことで**外部ツール無しで滑らかな線**を焼けるようにした。丸め半径は Line Softness（texel）で調整。4ch の意味・ランタイム・シェーダーは不変（**再ベイクするだけで品質が上がる**。従来出力は DF Blend OFF）。実体は EasyShaderCore の `EasyPbrFaceSdfBaker`（`Settings.dfBlend` / `dfSpread`）。
- **顔 SDF ベイクに X Axis Tilt を追加**（Baking > Face SDF Shadow、既定 0・-45〜45 度）。左右（R/G）チャンネルのスイープ光に仰角を与えて焼く。水平スイープ前提だと顎下〜首の境界が実際のライト（通常は上方から）とずれ、モデルによっては首まわりの影が SDF によって不自然になるため。上下（B/A）チャンネルとランタイムの合成式は不変で、**シェーダー変更なし・新規キーワードなし**（ベイク時のみの調整）。既定 0 で従来と同一の焼き上がり。実体は EasyShaderCore の `EasyPbrFaceSdfBaker`（`Settings.xAxisTilt`）。

### Changed (Breaking)
- **DollLiveDirector の Dissolve override を削除**。Dissolve のランタイム制御は EasyShaderCore の `DissolveController` に一本化した（マテリアルへ直接書く経路を Controller 1 点へ集約するため。2 キャラの入れ替わり演出は新設の `DissolveSwapController`）。`overrideDissolve` / `dissolveAmount` フィールドと `SetDissolve()` API を削除。**これらを使用していた場合は `DissolveController`（対象キャラのルートに追加し `amount` を駆動）への移行が必要**（詳細は EasyShaderCore の `Documentation~/VFX_DISSOLVE.md`）。

### Changed
- **詳細（Advanced）タブを新設し、Advanced Options を Baking タブから移動**（T-354。EasyToon Idol と同時・タブ構成同一の原則）。ベイクと高度な設定（GPU Instancing / Double Sided GI）は別物という指摘への対応。8 タブ（4 列 × 2 段）になり、Blue Noise はベイク素材なので Baking タブに残る。foldout の節 id（v2.advanced）は不変。
- **アウトラインを演出（FX）タブ → 基本タブへ移動**（Doll GUI。EasyToon Idol と同時・同じ棚 = タブ構成同一の原則）。輪郭線はマテリアルごとの恒久設定＝キャラの基本の見た目であって、時間で変化する演出（ディゾルブ / 暗転）とは性質が違うため。演出タブにはディゾルブ系だけが残る。foldout の節 id（v2.outline）は不変＝開閉状態は引き継がれる。
- **セルフシャドウ（PCF (Vogel) / PCSS）の毎タップ sincos を除去**（EasyShaderCore 側の `VogelDisk` 位相回転化による。Doll 側のシェーダー変更なし・見た目不変）。実測（fxc / D3D11・ForwardLit フラグメント）: Vogel PCF **1,412 → 1,404 命令**、PCSS **1,474 → 1,463 命令**。数値等価の変形（加法定理）なので影のパターンは 1 ビットも変わらない。
- `DollInput.hlsl` / `Passes/OutlinePass.hlsl` から UTF-8 BOM を除去（コンパイル結果不変。BOM を受け付けない外部 HLSL ツールとの相互運用のため）。

### Fixed
- **暗転（Black Out）が輪郭線に掛かっていなかったのを修正**（T-361。EasyToon Idol への輸入時に判明）。本体だけに掛けていたため、**暗転しきったキャラの輪郭線だけが明るく残って宙に浮いていた**。輪郭パスにも同じ `_BlackOut` を掛けるようにした（ディゾルブが輪郭も切っているのと同じ理屈）。`_BlackOut` を使っていない材質（既定 0）は不変。
- **顔 SDF: 光が真後ろ（および真正面）を通るとき左右チャンネルが段差で入れ替わる不具合を修正**（`Runtime/Shaders/Doll/DollSurface.hlsl` の `ComputeFaceSDF`）。4ch の重み（`max(0, ±dirX)` / `max(0, ±dirY)`）は顔 Forward 軸まわりの**方位だけ**で決まるため、光が Forward 軸上を通る瞬間は両成分が同時に 0 へ落ちて方位が定まらず、無限小の符号で R↔G が瞬時に入れ替わっていた。真正面は顔全面が光るので見えないが、真後ろは陰の遷移帯（`|f - sdf| < soft`）に入るため「急に左右が切り替わる」段差として出る（既定の Softness 0.5 では顔の広い範囲が遷移帯に入るため顕著）。横成分の長さ `lateral = |sin(光と顔 Forward のなす角)|` を方位の確からしさとし、軸から約 14.5°（`lateral < 0.25`）の内側では 4ch の平均（方位に依らない値）へ `smoothstep` でフェードして連続化した。この錐の内側は「全面が光る／全面が陰る」領域なので通常の絵は変わらない。**新規プロパティ・キーワードなし・再ベイク不要。**
- **Package Manager からの追加直後にも EasyShaderCore の自動インストールが走るように修正**: 本体 Editor asmdef（`Origuma.EasyPBR.URP.Editor`）を versionDefines + defineConstraints（シンボル `EASYSHADERCORE_PRESENT`）で Core 不在時にコンパイル対象から除外した。従来は Core 不在時のコンパイルエラーでドメインリロードが完了せず、PM 追加直後に `InitializeOnLoad`（Installer）が走らないため、エディタを再起動するまで Core が自動導入されなかった。除外により PM 追加直後（同一エディタセッション内・再起動不要）に Installer が走り、ゼロクリックで Core が導入される。

## [0.6.0]

> **破壊的変更**（→ [MIGRATION](Documentation~/MIGRATION.md)）: 共通基盤を新パッケージ `com.origuma.easyshader-core` へ移管した。EasyShaderCore は初回エディタ起動時に**自動でインストールされる**（失敗時は案内ウィンドウ）。

### Changed (Breaking)

- `Runtime/Shaders/Common/**`（BRDF / Effects / URP / 純粋関数 HLSL）を `com.origuma.easyshader-core` へ移管。HLSL の include パスが `Packages/com.origuma.easypbr-urp/Runtime/Shaders/Common/...` → `Packages/com.origuma.easyshader-core/Runtime/Shaders/Common/...` に変わった（Doll 内部は修正済み。**ユーザーシェーダーが EasyPBR の Common を直接 include していた場合はパス修正が必要**）
- `Editor/Baking/**`（EasyPbr*Baker / EasyPbrBakeCore）と `Editor/ShaderGuiKit.cs` を EasyShaderCore へ移管。名前空間が `Origuma.EasyPBR.URP.Editor` → `Origuma.EasyShaderCore.Editor` に変わり、Baker 群は `internal` → `public` に
- `Editor/AssemblyInfo.cs`（InternalsVisibleTo）を削除（不要になったため）
- EasyShaderCore を初回エディタ起動時に自動インストールする仕組みを追加（package.json の dependencies には宣言しない。宣言すると UPM がレジストリ解決に失敗し git URL からの単体インストール自体ができなくなるため）
- `Editor/MaterialReplacerWindow.cs` を EasyShaderCore へ移管。メニューが `Window > EasyPBR > Material Replacer` → `Window > Origuma > Material Replacer` に変わった（旧メニューパスは廃止）
- `DollOutlineSetupWindow` を EasyShaderCore の `FeatureSetupWindowBase` ベースに刷新（アクティブな URP Asset からの Renderer Data 自動収集・Compatibility Mode 警告に対応）。メニューが `Window > EasyPBR > Doll Outline Setup` → `Window > Origuma > Doll Outline Setup` に変わった（Window メニューの占有を Origuma 1 枠に集約）

## [0.5.0] - 2026-07-02

> **破壊的変更なし・移行作業不要**（→ [MIGRATION](MIGRATION.md)）。新機能はすべて既定で素通し（OFF）、新規シェーダーキーワードなし（すべて uniform 動的分岐・バリアント数不変）。挙動変化は「間接光がクランプに食われる不具合の修正（環境光のあるシーンでわずかに明るくなる）」と「一部スライダー上限の拡張・カラーの HDR 化（保存値不変）」のみ。
> テーマは**ライティングコアの強化と運用支援**: 陰の階調（2影・落ち影色分離・色相制御・散乱）、照明環境からの防御（ライト整形・間接光整形）、陰の形の整形（シェーディング法線）、陰への加光（フィルライト）、ライブ演出のランタイム制御。

### Added
- **ライブ演出コンポーネント（DollLiveDirector）**（`Runtime/Scripts/DollLiveDirector.cs`、Add Component > EasyPBR > Doll Live Director）。配下の Doll マテリアルの演出系プロパティ（Black Out / Dissolve Amount / Fill Light の色・強度）をキャラ単位で一括制御する。各グループは Override トグルが ON のあいだだけ上書きし、OFF でマテリアルの元値へ復元。Timeline / Animation からはフィールド直キーで駆動でき（専用トラック・追加依存なし）、スクリプト API（`SetBlackOut` / `SetDissolve` / `SetFill` / `ClearOverrides`）も持つ。**SRP Batcher 維持の設計**: Play 中はマテリアルインスタンス経由で値を書き（MPB はレンダラーをバッチから外すため不使用）、Edit モードのプレビューのみ非破壊の MaterialPropertyBlock を使用（→ SRP_BATCHER.md に指針を追記）。
- **マテリアル一括置換ユーティリティ（Material Replacer）**（`Editor/MaterialReplacerWindow.cs`、`Window > EasyPBR > Material Replacer`）。対象オブジェクト配下の全 Renderer のマテリアルを、指定フォルダ内の同名マテリアルへ一括で差し替える（Undo 可・`sharedMaterials` のスロット参照のみ変更＝アセット非破壊・置換済みスロットはスキップ）。元モデルのマテリアルと同名で EasyPBR 版を用意しておくことで、モデル一式の移行をワンクリック化する導線。
- **Bake All（共通マップの一括ベイク）**（Baking パネル先頭）。AO / Bent Normal / Shade Normal / Cavity / Curvature / SSS の 6 種を、各 Foldout の現在の設定のまま順に実行。Hair Flow / Face SDF は対象マテリアルが限られるため対象外（個別に焼く）。
- **GPU Instancing の注意表示**（Advanced Options）。Enable Instancing が ON のとき「SkinnedMeshRenderer には効かず、SRP Batcher から外れる」警告を表示（キャラ用途では通常 OFF を推奨）。
- **影色の色相・彩度制御（Shadow Hue Shift / Saturation）**（`_ShadowHueShift` / `_ShadowSaturation`）。陰側のベースカラーを HSV 補正してから Shadow Color を乗算し、「ただ暗い影」を色相が転がり彩度が残る影にする。マスク不要のプロシージャル制御。既定値（0 / 1）のとき HSV 変換ごとスキップ。**新規キーワードなし（uniform 動的分岐）。**
- **アウトラインのアルベド連動色（Outline Albedo Blend）**（`_OutlineAlbedoBlend`）。輪郭線の色を「その場のアルベド × Outline Color」側へブレンドする。髪には髪系統・肌には肌系統の線が自動で付き、固定単色より馴染む（Outline Color は乗算色として働くため暗めを推奨）。アルベドは Outline パスで既にサンプル済みのため追加コストは lerp 1 回。0（既定）で従来の固定色。
- **落ち影の色分離（Cast Shadow Color）**（`_CastShadowColor`（A=Enable））。落ち影（shadow map）を角度の陰とは独立した色で塗る（落ち影だけ寒色へ振る等の映像的な塗り分け）。色は Hue Shift / Saturation 補正を陰と共有した上で乗算（`DollSurfaceData.castShadowAlbedo` として事前計算）。有効時は角度の陰（litMask / SDF）と落ち影（castShadow / External Shadow Mix）を分離して合成し、A=0（既定）では従来の合成式をそのまま使用（見た目完全互換）。**新規キーワードなし（uniform 動的分岐）。**
- **フィルライト（Fill Light / 照り返し）**（`_FillColor`（HDR）/ `_FillIntensity` / `_FillPitch` / `_FillYaw` / `_FillShadeOnly`）。指定方向（ワールド空間の Pitch / Yaw）からのバウンス光を陰側に注ぐプロシージャルなフィルライト。Half-Lambert（wrap 0.5）で柔らかく回り込み、Shade Side Only で主光の陰側に限定（既定 1）。メインライトの寄与から独立した加算光（`CalculateSingleLight` に `fillIntensity` 引数を追加。メインライト呼び出しのみ有効・追加ライトは 0）。床の照り返し・空からの回り込み等をシーンのライト追加なしで構成できる。0 で計算ごとスキップ・既定 OFF。**新規キーワードなし（uniform 動的分岐）。**
- **シェーディング法線（Shade Normal）**（`EasyPbrShadeNormalBaker` → `_ShadeNormalMap` / `_ShadeNormalStrength`）。頂点法線を**位置溶接**（UV 継ぎ目・硬エッジで分割された頂点を量子化位置でグループ化）した隣接グラフ上でラプラシアン平滑化し、接線空間に焼く（法線マップと同一の符号化・スキン変形追従・レイ不要）。ランタイムは**拡散の陰ランプだけ**をこの法線で駆動し（スペキュラ・リム・SSS はディテール法線のまま）、シワ・ファセット起伏で陰のグラデーションが汚く割れるのを防いで陰の輪郭を一本の綺麗な曲線として通す。滑らかなランプほど起伏が目立つソフトグラデーション基調の絵作りで特に有効。ベイクで Strength 自動 1。0 でサンプルごとスキップ・既定 OFF。**新規キーワードなし（uniform 動的分岐）。**
- **キャラ用ライト整形（Light Conditioning）**（`_LightColorInfluence` / `_LightSaturationLimit` / `_LightMinBrightness`、`Common/Common_Color.hlsl`: `ConditionLightColor`）。メインライトの色をキャラの可読性側へ整形する防御層で、Anti-Blowout（上限）と対になる「下限と色の防御」。Light Color Influence（0 で同輝度の白色光扱い＝原色照明でもキャラの色設計を保持）、Light Saturation Limit（色相を保持したまま彩度を上限で抑える）、Light Min Brightness（輝度下限。暗所でも完全黒に沈まない）。`_ConditionAdditionalLights`（Toggle・既定 OFF）で追加ライトにも Color Influence / Saturation Limit を適用可（Min Brightness は灯数ぶん持ち上がるのを防ぐためメインライト限定）。既定値（1 / 1 / 0 / OFF）で完全素通し・整形ごとスキップ。**新規キーワードなし（uniform 動的分岐）。**
- **スペキュラのスタイライズ（Toon Specular / Shade Dimming）**（`_ToonSpecular` / `_ToonSpecularStep` / `_ToonSpecularFeather` / `_SpecularShadeInfluence`）。Toon Specular はスペキュラのトーンマップ後輝度（`lum/(1+lum)`）をしきい値で切り、縁のパキッとした様式的ハイライトにする（内側のグラデーション保持・fwidth で最低 1px の AA・0〜1 で連続とブレンド・両ローブ共通）。Specular Shade Dimming は陰ランプ（1影・2影の `min(finalShade, shade2)`）に入った面のスペキュラを減衰（落ち影はローブ内で従来から適用済み。角度ベースの陰でも消すアニメ的に厳密な絵作り向け）。全ライトに適用。いずれも 0 で整形ごとスキップ・既定 OFF。**新規キーワードなし（uniform 動的分岐）。**
- **間接光の整形（Indirect Light）**（`_IndirectFlatten` / `_IndirectIntensity` / `_IndirectTint`）。ライトプローブ / アンビエントの SH をキャラ向けに整える。Flatten は SH の方向成分を潰して定数項（`SampleSH(0)`＝平均環境光）へ寄せ、会場 GI の方向ムラが顔に出るのを防いでキャラ全体を均一なアンビエントで包む（Bent Normal の方向補正とはトレードオフ）。Intensity / Tint で間接光の寄与と色味を調整（メインライト非干渉）。既定値（0 / 1 / 白）で素通し。Flatten 0 のとき 2 回目の SH 評価ごとスキップ。**新規キーワードなし（uniform 動的分岐）。**
- **2影（2nd Shadow）**（`_Shadow2Color`（A=Enable）/ `_Shadow2Step` / `_Shadow2Feather`）。1影より深い位置に第2の陰ランプを重ねるアニメ塗りの 2 段構成（明 → 1影 → 2影）。光角度ベースで駆動し、落ち影（shadow map）は 1影のまま。顔 SDF 領域では SDF 由来の連続値で駆動し、SDF が消した法線由来の陰バンドを顔に再発させない。色は Hue Shift / Saturation 補正を 1影と共有した上で `_Shadow2Color` を乗算（`DollSurfaceData.shadow2Albedo` として事前計算）。境界は `ToonRamp`（smoothstep + fwidth AA）を Toon / Smooth 共通で使用。Skin Scatter 有効時は 1影・2影両方の境界に散乱が乗る。A=0 で 2影ランプごとスキップ・既定 OFF。**新規キーワードなし（uniform 動的分岐）。**
- **スキンスキャッタ（Skin Scatter）**（`_SkinScatterColor` / `_SkinScatterIntensity` / `_SkinScatterWidth` / `_SkinScatterCurvatureMask`、`Common/BRDF/BRDF_Diffuse.hlsl`: `ApplyTerminatorScatter`）。明暗境界（ターミネータ）に散乱色を滲ませる pre-integrated skin scattering の近似。バンドは最終陰影値（`finalShade`）の遷移域から取るため、トゥーンランプ・落ち影ペナンブラ・顔 SDF 境界のいずれにも同じ式で乗る。Curvature Mask でベイク済み `_CurvatureMap` と連動し、薄い・曲率の高い部位（耳・鼻・指）ほど強く散乱（0 で均一・曲率未使用時も動作）。マスク不要。0 で計算ごとスキップ・既定 OFF。**新規キーワードなし（uniform 動的分岐）。**

### Changed
- **インスペクターをタブ分割 UI に刷新**: 縦一列だった 11 セクションを最上部の固定タブバー（基本 / 陰・影 / ライト / スペキュラ / 質感 / 演出 / Baking）でページ分割し、縦の長さと「どこに何があるか」を同時に解消。選択タブは記憶される。あわせて**プロパティ検索**を追加——検索ボックスに入力するとタブを横断して表示名・プロパティ名に部分一致する項目をフラットに列挙する。肥大していた Light and Shadow は「陰・影（階調 / セルフシャドウ / 顔）」と「ライト（整形 / フィル / 間接光 / 白飛び防止）」へ、Optional Effects は「質感（コートとグリッター / 肌と縁 / ベイクマップ）」へ再編成。タブ内セクションは既定で展開。
- **ベイク出力を同名上書きに変更**: 生成 PNG のファイル名を固定（従来は `GenerateUniqueAssetPath` により再ベイクのたび `_1, _2…` と連番で増加）。同名ファイルを上書きするため `Baked/` が肥大しない。GUID が維持されるので、アサイン済みの参照はそのまま新しい内容に更新される（以前の結果へ戻すには焼き直すかバージョン管理で戻す）。
- **ハイライト系カラーの HDR 化**: `_SpecularColor`（Primary）/ `_SecSpecularColor`（Secondary）/ `_RimColor` / `_FuzzColor` / `_MatCapColor` に `[HDR]` を付与（既存の保存値は不変・UI のみ拡張）。`_ReflectionStrength` の上限を 1.0 → **2.0** に拡張（1 超は様式的なブースト）。ハイライトを 1 超の輝度にして Bloom を誘発する様式的な強い反射表現を全ハイライト系（Specular / Rim / Peach Fuzz / MatCap / 環境反射。Aniso / Glitter / Dissolve は従来から HDR）で構成可能に。
- **陰色の事前計算（挙動不変の最適化）**: 陰側の最終色（Shadow Color 乗算後）を `GatherSurface` でライト非依存に 1 回だけ算出し `DollSurfaceData.shadowAlbedo` に保持（従来はライトごとに算出）。`DollLighting.hlsl` の未使用になった `GetShadedAlbedo` ラッパーを削除（汎用 `ShadedAlbedo` は `Common/BRDF/BRDF_Diffuse.hlsl` に残置）。

### Fixed
- **SRP Batcher 互換性の穴を修正**: `_UseOutline` / `_ShadowMode` / `_OutlineStencilRef` / `_OutlineStencilComp` / `_OutlineStencilPass` / `_OutlineStencilFail` / `_OutlineStencilZFail` の 7 プロパティが `UnityPerMaterial` CBUFFER に未登録だった（通常側の `_StencilRef` 等は登録済みで非対称だった）。これらは ShaderLab の Stencil ブロックや KeywordEnum が参照し HLSL からは直接読まないが、CBUFFER に含めないと SRP Batcher が incompatible 判定になりうるため追加（挙動・見た目は不変）。これで Properties の全プロパティが CBUFFER / テクスチャに網羅登録された。
- **間接光が Anti-Blowout に食われて無効化される問題を修正**: 従来は「直接光＋間接光」の合算に Diffuse Light Limit（既定 1.0）の輝度クランプを掛けていたため、標準的な強度 1 のライト下では間接光の寄与が丸ごと削られていた（Indirect Light の調整も見た目に反映されない）。直接光のみをクランプし間接光をその後に加算する形へ変更。**挙動変化**: 直接光が上限に達しているシーンでは、環境光のぶんだけ従来よりわずかに明るくなる（間接光側の上限管理は Indirect Intensity で行う）。
- `package.json` の `version` が `0.3.7` のまま v0.4.0 リリースに追従していなかったのを修正。

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
