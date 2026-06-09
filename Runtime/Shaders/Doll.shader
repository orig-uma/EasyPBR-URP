// =============================================================================
//  Origuma/EasyPBR_URP/Doll
// -----------------------------------------------------------------------------
//  URP (Universal Render Pipeline) Forward 用のキャラクター向けシェーダー。
//  フィギュア/人形のような「PBRの質感」と「トゥーンの平面的な陰影」を
//  両立させることを狙っている。主な特徴は以下:
//
//   - Toon / Smooth の2モードを keyword で切り替え可能
//   - 顔の自己陰（鼻・頬の凹凸が作る汚い影）を自動で消す Procedural Mask
//   - 「落ち影(shadow map)」と「陰影(NdotL)」を分離合成し、トゥーン境界の
//     マッハバンド（縞）を回避
//   - Dual-Lobe スペキュラ + エネルギー保存
//   - SSS / Rim / Peach Fuzz / MatCap など任意効果（既定OFF）
//
//  パス構成: ForwardLit（本体） + ShadowCaster（影を落とすため）。
// =============================================================================
Shader "Origuma/EasyPBR_URP/Doll"
{
    Properties
    {
        // --- 基本パラメータ。アルベド・色・透過・カリング ---------------------
        [Header(Base Core)]
        _MainTex ("Base Map (RGB / Alpha)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        // Alpha Clip を有効にすると _Cutoff 未満のピクセルを破棄（髪・睫毛などの抜き）
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        // --- 顔の自己陰を自動で消す仕組み（マスクテクスチャ不要） -------------
        //  モデルの「正面向き」「上向き」の面ほど明るくして、ポリゴンの凹凸が
        //  作る暗い自己陰を打ち消す。アニメ顔の「鼻の影が出ない」表現の自動版。
        [Header(Auto Face Shadow Fix (No Mask Needed))]
        _FrontMaskStrength ("Front Brightness (Model Facing)", Range(0.0, 1.0)) = 0.8
        _UpMaskStrength ("Up Brightness (Upward Facing)", Range(0.0, 1.0)) = 0.3
        // マスクの効き方のカーブ。大きいほど正面/上向きの面に絞って効く
        _MaskFalloff ("Shadow Erase Breadth", Range(0.1, 10.0)) = 2.0
        // 逆光時はあえて陰を残すための係数（1で逆光の陰を最大限維持）
        _BacklightPreserve ("Backlight Shadow Preserve", Range(0.0, 1.0)) = 1.0 
        [Space(10)]
        // 法線平滑化: マスクが強い面ほど法線を正面へ寄せ、凹凸由来の陰を無効化
        _FaceNormalSmoothness ("Face Normal Smoothing (Hide Bumps)", Range(0.0, 1.0)) = 0.8 

        // --- ライティングと影 ------------------------------------------------
        [Header(Light and Shadow)]
        // Smooth = なめらかな陰影 / Toon = 二値的なトゥーン境界（keyword 切替）
        [KeywordEnum(Smooth, Toon)] _ShadingStyle ("Shading Style", Float) = 0
        // 影部分に乗る色。黒だと素直に暗くなるだけ（色相シフトなし）
        _ShadowColor ("Shadow Color Shift", Color) = (0, 0, 0, 1)
        // R チャンネルで「落ち影をどれだけ受けるか」を部位ごとに制御するマスク
        _ReceiveShadowMask ("Receive Shadow Mask (R=Shadow)", 2D) = "white" {}
        _ReceiveShadowStrength ("Receive Shadow Strength", Range(0.0, 1.0)) = 1.0
        // 落ち影の境界のソフトさ。シャドウマップの階段状ノイズをぼかす
        _ShadowMapSoftness ("Receive Shadow Softness", Range(0.0, 1.0)) = 0.4
        // 落ち影境界にブルーノイズを加えて量子化バンド（縞）を分解する量
        _ShadowDither ("Shadow Edge Dither", Range(0.0, 1.0)) = 0.5
        // Half Lambert の wrap 量。陰側を持ち上げて陰影を柔らかくする（Smooth向け）
        _HalfLambertWrap ("Light Wrap (Smooth Mode)", Range(0.0, 1.0)) = 0.5
        
        [Space(10)]
        // Toon モード時の明暗境界の位置と、境界の柔らかさ
        _ToonStep ("Toon Shadow Threshold", Range(0.0, 1.0)) = 0.5
        _ToonFeather ("Toon Shadow Softness (Blend to PBR)", Range(0.0, 1.0)) = 0.2

        // --- 微細なザラつき（質感ノイズ） ------------------------------------
        //  ブルーノイズで法線を僅かに揺らし、つるんとし過ぎない肌質感を作る。
        //  テクスチャ未指定（既定 grey）の場合は効果ゼロ。
        [Header(Surface Micro Detail)]
        [NoScaleOffset] _BlueNoiseTex ("Micro Grain Pattern (Blue Noise)", 2D) = "grey" {}
        _GrainIntensity ("Grain Intensity", Range(0.0, 1.0)) = 0.2
        _GrainScale ("Grain UV Scale", Float) = 10.0

        // --- スペキュラと映り込み --------------------------------------------
        [Header(Specular and Reflection)]
        _SpecularMask ("Specular Mask (R)", 2D) = "white" {}
        [Space(10)]
        // Primary = 鋭いハイライト / Secondary = 広く柔らかいハイライト（2 ローブ）
        _SpecularColor ("Primary Specular Color (Sharp)", Color) = (1, 1, 1, 1)
        _Smoothness ("Primary Smoothness", Range(0.01, 1.0)) = 0.8
        _SpecularIntensity ("Primary Intensity", Range(0.0, 5.0)) = 1.5
        [Space(10)]
        _SecSpecularColor ("Secondary Specular Color (Matte)", Color) = (1, 1, 1, 1)
        _SecSmoothness ("Secondary Smoothness", Range(0.01, 1.0)) = 0.2
        _SecSpecularIntensity ("Secondary Intensity", Range(0.0, 5.0)) = 0.5
        [Space(10)]
        // MatCap: ビュー空間法線でテクスチャを引く擬似ライティング（球状の映り込み風）
        [Toggle(_MATCAP_ON)] _UseMatCap ("Enable MatCap", Float) = 0
        [KeywordEnum(Add, Multiply)] _MatCapBlend ("MatCap Blend Mode", Float) = 0
        [NoScaleOffset] _MatCapTex ("MatCap Texture (RGB)", 2D) = "black" {}
        _MatCapColor ("MatCap Tint", Color) = (1, 1, 1, 1)
        _MatCapIntensity ("MatCap Intensity", Range(0.0, 5.0)) = 1.0

        // --- 任意効果（Intensity を 0 にすると完全にOFF。既定はすべてOFF寄り） ---
        [Header(Optional Effects (Intensity 0 turns them Off))]
        [Space(4)]
        // SSS: 逆光時に肌が透けるような表面下散乱の擬似表現。
        //  最終色は _SSSColor × light.color。既定の白(1,1,1)は乗算の単位元なので
        //  SSSColor は無影響＝光源色そのままで出る。肌の赤い透け感が欲しければ
        //  赤系(例: 1.0, 0.2, 0.1)に寄せる。
        _SSSColor ("SSS Color (Subsurface)", Color) = (1, 1, 1, 1)
        _SSSIntensity ("SSS Intensity (0 = Off)", Range(0.0, 5.0)) = 0.0
        _SSSPower ("SSS Falloff", Range(0.1, 10.0)) = 4.0
        _SSSDistortion ("SSS Distortion", Range(0.0, 1.0)) = 0.1
        [Space(10)]
        // Peach Fuzz: 輪郭にうっすら乗る産毛のような柔らかい縁の光沢（ベルベット風）
        _FuzzColor ("Peach Fuzz (Soft Edge Sheen) Color", Color) = (1.0, 0.95, 0.9, 1.0)
        _FuzzIntensity ("Peach Fuzz Intensity (0 = Off)", Range(0.0, 5.0)) = 0.0
        _FuzzPower ("Peach Fuzz Width", Range(0.1, 10.0)) = 4.0
        [Space(10)]
        // Rim Light: フレネル（輪郭）に乗る縁の光。立体感・キャラの分離に有効
        _RimColor ("Rim Light Color", Color) = (1, 1, 1, 1)
        _RimIntensity ("Rim Light Intensity (0 = Off)", Range(0.0, 5.0)) = 1.0
        _RimPower ("Rim Light Thickness", Range(0.1, 10.0)) = 3.0
    }

    SubShader
    {
        // TransparentCutout + AlphaTest キューで、Alpha Clip による抜きに対応
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "AlphaTest" 
        }

        // =====================================================================
        //  ForwardLit パス: 実際の見た目（色・陰影・ハイライト等）を出力する本体
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // shader_feature: マテリアル設定に応じて使われた variant だけがビルドに残る
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SHADINGSTYLE_TOON
            #pragma shader_feature_local_fragment _MATCAP_ON
            #pragma shader_feature_local_fragment _MATCAPBLEND_ADD _MATCAPBLEND_MULTIPLY

            // multi_compile: 全 variant を常にビルド。ライト/影など実行時に切り替わるもの用
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AlphaClip;
                half _Cutoff;
                half _Cull;
                
                half _FrontMaskStrength;
                half _UpMaskStrength;
                half _MaskFalloff;
                half _BacklightPreserve;
                half _FaceNormalSmoothness;

                half _ReceiveShadowStrength;
                half _ShadowMapSoftness;
                half _ShadowDither;
                half4 _ShadowColor;
                half _HalfLambertWrap;
                half _ToonStep;
                half _ToonFeather;
                half4 _SpecularColor;
                half _Smoothness;
                half _SpecularIntensity;
                half4 _SSSColor;
                half _SSSIntensity;
                half _SSSPower;
                half _SSSDistortion;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _GrainIntensity;
                half _GrainScale;
                half4 _SecSpecularColor;
                half _SecSmoothness;
                half _SecSpecularIntensity;
                half4 _FuzzColor;
                half _FuzzPower;
                half _FuzzIntensity;
                half _UseMatCap;
                half4 _MatCapColor;
                half _MatCapIntensity;
            CBUFFER_END
            
            // マスク類はすべて _MainTex のサンプラーを共有し、サンプラー枠を節約する
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ReceiveShadowMask);
            TEXTURE2D(_SpecularMask);
            TEXTURE2D(_BlueNoiseTex);
            TEXTURE2D(_MatCapTex);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2; 
                float4 shadowCoord  : TEXCOORD3;
                float3 forwardWS    : TEXCOORD4; // モデルの正面方向（顔マスク用）
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.shadowCoord = GetShadowCoord(vertexInput);
                
                // オブジェクトのローカル +Z をワールドの「正面方向」とみなす。
                // オブジェクト定数なので頂点で正規化しておけば frag 側の正規化が不要。
                output.forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
                
                return output;
            }

            // 1 つのライト（メイン or 追加）に対する寄与色を計算して返す。
            // 各ライトで共通の「ライト非依存」な値（baseProceduralMask / rimFresnel /
            // fuzzFresnel）は frag 側で 1 回だけ算出して渡す（再計算を避ける最適化）。
            half3 CalculateSingleLight(Light light, half3 detailNormalWS, half3 viewDirectionWS, float3 objectForwardWS, half3 baseColor, half receiveShadowMask, half specMask, half ditherValue, float baseProceduralMask, float rimFresnel, float fuzzFresnel)
            {
                // =========================================================================
                // 1. 影消しマスク（顔の自己陰を消すためのマスク）
                //    ライト非依存の土台 baseProceduralMask は frag で算出済み。
                //    ここでは「逆光時は陰を残す」ための光源依存項 backlightFade だけ掛ける。
                // =========================================================================
                float lightToForwardDot = dot(objectForwardWS, light.direction);
                // ライトが正面側にあるほど 1、背後にあるほど 0 へフェード
                float backlightFade = smoothstep(-0.3, 0.2, lightToForwardDot);
                backlightFade = lerp(1.0, backlightFade, _BacklightPreserve);
                float proceduralMask = baseProceduralMask * backlightFade;

                // =========================================================================
                // 2. 法線平滑化 (Normal Flattening)
                //    マスクが強い面ほど法線をモデル正面へ寄せ、鼻や頬の凹凸が作る
                //    汚い自己陰を NdotL の段階で消し去る。
                // =========================================================================
                half3 diffuseNormalWS = normalize(lerp(detailNormalWS, objectForwardWS, proceduralMask * _FaceNormalSmoothness));

                // 陰影用の NdotL は「平滑化した法線」で取る（顔の陰だけツルッとさせる）
                float diffuseNdotL = dot(diffuseNormalWS, light.direction);

                // Half Lambert (Valve 流): NdotL を 0..1 に再マップして陰側を持ち上げ、
                // 陰影を柔らかくする。これが「顔の陰（幾何由来）」の入力になる。
                float halfLambert = saturate((diffuseNdotL + _HalfLambertWrap) / (1.0 + _HalfLambertWrap));

                // =========================================================================
                // 2.5 落ち影（シャドウマップ）専用のソフトランプ
                //  顔の陰(NdotL)とは「分離」して扱うのがポイント。両者を混ぜてから
                //  トゥーン量子化すると、シャドウマップの階段状ノイズがトゥーン境界で
                //  増幅され縞（マッハバンド）になる。ここでは fwidth を使わず、
                //  ブルーノイズのディザでバンドを分解してから滑らかなランプにする。
                // =========================================================================
                float rawShadow = lerp(1.0, light.shadowAttenuation, receiveShadowMask * _ReceiveShadowStrength);
                float ditheredShadow = rawShadow + (ditherValue - 0.5) * _ShadowDither;
                float castShadow = smoothstep(0.5 - _ShadowMapSoftness * 0.5, 0.5 + _ShadowMapSoftness * 0.5, ditheredShadow);
                // 顔の正面（マスクの強い所）では落ち影も消す
                castShadow = lerp(castShadow, 1.0, proceduralMask);

                // =========================================================================
                // 3. 各種ライティングの適用
                // =========================================================================
                // ① Diffuse: 顔の陰のランプは halfLambert のみから作る。
                //    Toon の場合だけ fwidth(halfLambert) で 1px のアンチエイリアス幅を確保。
                //    （fwidth は落ち影には掛けない＝縞の原因を断つ）
                float litMask;
                #if defined(_SHADINGSTYLE_TOON)
                    float softness = max(fwidth(halfLambert), _ToonFeather);
                    litMask = smoothstep(_ToonStep - softness, _ToonStep + softness, halfLambert);
                #else
                    litMask = halfLambert;
                #endif
                litMask = lerp(litMask, 1.0, proceduralMask);

                // 顔の陰(litMask)と落ち影(castShadow)を min で合成（暗い方が勝つ）
                float finalShade = min(litMask, castShadow);
                // 陰では _ShadowColor、明では baseColor へ補間
                half3 diffuseColor = lerp(_ShadowColor.rgb, baseColor, finalShade);

                half3 finalDiffuse = diffuseColor * light.color * light.distanceAttenuation;

                // ② Dual-Lobe Specular (Blinn-Phong を 2 つ重ねる)
                //  ハイライトは「平滑化していない元の法線(detailNormalWS)」で計算する。
                //  → 陰はツルッと、ハイライトには肌や布の質感が残る、という両立が狙い。
                float NdotL_Specular = dot(detailNormalWS, light.direction);
                float3 halfVector = SafeNormalize(light.direction + viewDirectionWS);
                float NdotH = saturate(dot(detailNormalWS, halfVector));

                // smoothness(0..1) を exp2 で鏡面指数へ変換（大きいほど鋭いハイライト）
                float specPower1 = exp2(10.0 * _Smoothness + 1.0);
                float specTerm1 = pow(NdotH, specPower1);
                half3 spec1 = _SpecularColor.rgb * specTerm1 * _SpecularIntensity;

                float specPower2 = exp2(10.0 * _SecSmoothness + 1.0);
                float specTerm2 = pow(NdotH, specPower2);
                half3 spec2 = _SecSpecularColor.rgb * specTerm2 * _SecSpecularIntensity;

                // ライトの裏側ではハイライトを出さない + マスク + 落ち影で減衰
                float specularMaskVal = saturate(NdotL_Specular * 10.0) * specMask * castShadow;
                half3 finalSpecular = (spec1 + spec2) * light.color * light.distanceAttenuation * specularMaskVal;

                // エネルギー保存: 反射が強い箇所はその分ディフューズを落とす（白飛び防止）
                float specLuminance = saturate(dot(finalSpecular, half3(0.299, 0.587, 0.114)));
                finalDiffuse *= (1.0 - specLuminance);

                // ③ SSS (擬似サブサーフェス): 逆光側で光が透ける表現。
                //  backlightDir がライト方向依存のため frag へ巻き上げできない。
                //  Intensity=0（既定OFF）のときは分岐で pow/normalize ごとスキップ。
                half3 finalSSS = half3(0, 0, 0);
                UNITY_BRANCH
                if (_SSSIntensity > 0.0)
                {
                    float3 backlightDir = normalize(light.direction + detailNormalWS * _SSSDistortion);
                    float backlightTerm = pow(saturate(dot(viewDirectionWS, -backlightDir)), _SSSPower);
                    float sssShadow = lerp(0.4, 1.0, castShadow);
                    finalSSS = _SSSColor.rgb * backlightTerm * _SSSIntensity * light.color * light.distanceAttenuation * sssShadow;
                }

                // ④ Rim Light: フレネル項 rimFresnel = (1-NdotV)^power は frag で算出済み。
                //    OFF 時は rimFresnel=0 なので寄与は自動的に 0。
                float rimLightMask = saturate(NdotL_Specular * 5.0) * castShadow;
                half3 finalRim = _RimColor.rgb * rimFresnel * _RimIntensity * light.color * light.distanceAttenuation * rimLightMask;

                // ⑤ Peach Fuzz: 同様に fuzzFresnel は frag で算出済み（縁の柔らかい光沢）
                float fuzzMask = fuzzFresnel * saturate(NdotL_Specular) * castShadow;
                half3 finalFuzz = _FuzzColor.rgb * fuzzMask * _FuzzIntensity * light.color * light.distanceAttenuation;

                return finalDiffuse + finalSpecular + finalSSS + finalRim + finalFuzz;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 補間で長さが崩れるため、ワールド法線はここで正規化し直す
                half3 cleanNormalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 objectForwardWS = input.forwardWS; // 頂点で正規化済み

                // ブルーノイズで法線を微妙に揺らし、質感ノイズ(grain)を与える。
                // grey テクスチャ(既定)だと noiseVec=0 となり効果なし。
                float3 noiseVec = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, input.uv * _GrainScale).rgb * 2.0 - 1.0;
                half3 detailNormalWS = normalize(cleanNormalWS + noiseVec * _GrainIntensity * 0.15);
                
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff); // _Cutoff 未満は描画しない
                #endif

                half receiveShadowMask = SAMPLE_TEXTURE2D(_ReceiveShadowMask, sampler_MainTex, input.uv).r;
                half specMask = SAMPLE_TEXTURE2D(_SpecularMask, sampler_MainTex, input.uv).r;

                // 落ち影の量子化バンドを分解するためのディザ値。
                // UV ではなく「スクリーン座標」基準でサンプルし、模様が表面に
                // 貼り付く(UVロック)のを防ぐ。
                float2 ditherUV = input.positionCS.xy / 64.0;
                half ditherValue = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_MainTex, ditherUV).r;

                // Forward+ のライトループ(LIGHT_LOOP_BEGIN)が参照する入力
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 finalColor = half3(0, 0, 0);
                float4 shadowCoord = input.shadowCoord;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    // スクリーンスペースシャドウ時は画面座標から影をサンプルする
                    shadowCoord = ComputeScreenPos(input.positionCS);
                #endif

                // --- ライト非依存の値を 1 回だけ算出してループに渡す（最適化） -----
                // 顔の影消しマスクの土台（逆光フェードはライト毎に掛ける）
                float frontMask = saturate(dot(cleanNormalWS, objectForwardWS));
                float upMask = saturate(cleanNormalWS.y);
                float baseProceduralMask = smoothstep(0.0, 1.0, saturate(pow(frontMask, _MaskFalloff) * _FrontMaskStrength + pow(upMask, _MaskFalloff) * _UpMaskStrength));

                // Rim / Fuzz のフレネル項はライト非依存。OFF 時は分岐で pow を省く
                // （uniform 条件の分岐なので全ピクセル同一経路＝安価、variant も増えない）
                float NdotV = saturate(dot(detailNormalWS, viewDirectionWS));
                float rimFresnel = 0.0;
                UNITY_BRANCH
                if (_RimIntensity > 0.0)
                {
                    rimFresnel = pow(1.0 - NdotV, _RimPower);
                }
                float fuzzFresnel = 0.0;
                UNITY_BRANCH
                if (_FuzzIntensity > 0.0)
                {
                    fuzzFresnel = pow(saturate(1.0 - NdotV), _FuzzPower);
                }

                // --- メインライト（Directional 1 灯）の寄与 ---
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
                finalColor += CalculateSingleLight(mainLight, detailNormalWS, viewDirectionWS, objectForwardWS, albedo.rgb, receiveShadowMask, specMask, ditherValue, baseProceduralMask, rimFresnel, fuzzFresnel);

                // --- 追加ライト（ポイント/スポット等）の寄与 ---
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        finalColor += CalculateSingleLight(addLight, detailNormalWS, viewDirectionWS, objectForwardWS, albedo.rgb, receiveShadowMask, specMask, ditherValue, baseProceduralMask, rimFresnel, fuzzFresnel);
                    LIGHT_LOOP_END
                #endif

                // 環境光: Spherical Harmonics（Light Probe / Ambient）をアルベドに乗算
                half3 ambient = SampleSH(cleanNormalWS) * albedo.rgb;
                finalColor += ambient;

                // MatCap: ビュー空間法線の XY を UV にしてテクスチャを引く擬似反射
                #if defined(_MATCAP_ON)
                    float3 normalVS = mul((float3x3)GetWorldToViewMatrix(), detailNormalWS);
                    float2 matcapUV = normalVS.xy * 0.5 + 0.5;
                    half3 matcapColor = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MainTex, matcapUV).rgb * _MatCapColor.rgb;
                    
                    #if defined(_MATCAPBLEND_ADD)
                        finalColor += matcapColor * _MatCapIntensity;      // 加算: 光沢を足す
                    #elif defined(_MATCAPBLEND_MULTIPLY)
                        finalColor *= lerp(half3(1.0, 1.0, 1.0), matcapColor, saturate(_MatCapIntensity)); // 乗算: 陰影付け
                    #endif
                #endif

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // =====================================================================
        //  ShadowCaster パス: このオブジェクト自身が「影を落とす」ために
        //  シャドウマップ（深度）へ書き込む。色は出力しない（ColorMask 0）。
        //  必要なのは深度と Alpha Clip だけなので、本体より大幅に軽量。
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}
            
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            // ポイント/スポットライトの影を焼くときに使われる variant
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // Unity バージョンによっては Shadows.hlsl の前に必要
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // ShadowCaster 側で URP が設定するライト情報（ShadowBias 計算に使用）
            float3 _LightDirection;
            float3 _LightPosition;

            // このパスで参照する分だけを CBUFFER に宣言（軽量化）。
            // SRP Batcher のため ForwardLit と「同名・同型」であることが必要。
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AlphaClip;
                half _Cutoff;
                half _Cull;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes 
            { 
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0; 
            };
            
            struct Varyings 
            { 
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0; 
            };
            
            Varyings vert_shadow(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // 影を焼く相手がポイント光なら位置から、平行光なら固定方向から光方向を得る
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // 法線方向にバイアスを掛けてシャドウアクネ（自己交差ノイズ）を抑える
                float3 biasedPositionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
                float4 positionCS = TransformWorldToHClip(biasedPositionWS);

                // ニアクリップ面より手前に飛び出さないよう深度をクランプ
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = input.uv;
                return output;
            }
            
            half4 frag_shadow(Varyings input) : SV_Target 
            { 
                // 色は不要。Alpha Clip の抜きだけ本体と一致させる（影の形を正しくする）
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }

    CustomEditor "Origuma.EasyPBR.URP.Editor.DollShaderGUI"
}
