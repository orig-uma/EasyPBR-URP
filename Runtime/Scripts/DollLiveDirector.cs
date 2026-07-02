// =============================================================================
//  DollLiveDirector.cs
// -----------------------------------------------------------------------------
//  ライブ演出用の一括コントローラ。配下の Doll マテリアルに対して、演出系
//  プロパティ（Black Out / Dissolve / Fill Light）をキャラ単位でまとめて制御する。
//
//  設計:
//  - Play 中は「マテリアルインスタンス」経由で値を書く。MaterialPropertyBlock は
//    レンダラーを SRP Batcher から外すため使わない（別マテリアル同士は SRP
//    Batcher で問題なくバッチされる → Documentation~/SRP_BATCHER.md）。
//  - Edit モードのプレビューだけは非破壊な MaterialPropertyBlock を使う
//    （共有マテリアル資産を汚さない。プレビュー時のバッチングは無関係）。
//  - Override が OFF のグループはマテリアルの値に一切触れず、OFF に戻すと
//    インスタンス生成時に控えた元値へ復元する。
//  - Timeline / Animation からは本コンポーネントのフィールドをキー打ちすれば
//    そのまま駆動できる（専用トラック不要・パッケージ依存も増えない）。
//
//  注意: Dissolve はキーワード（_DISSOLVE_ON）がマテリアル側で有効なこと。
//        Enable Dissolve を ON にし Amount 0 で運用し、本コンポーネントで
//        Amount を動かすのが想定フロー。
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Origuma.EasyPBR.URP
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("EasyPBR/Doll Live Director")]
    public class DollLiveDirector : MonoBehaviour
    {
        private const string ShaderPrefix = "Origuma/EasyPBR_URP/";

        [Header("Black Out")]
        [Tooltip("ON のあいだ Black Out を上書きする（OFF でマテリアルの元値へ復元）")]
        public bool overrideBlackOut = false;
        [Range(0f, 1f)] public float blackOut = 0f;

        [Header("Dissolve")]
        [Tooltip("ON のあいだ Dissolve Amount を上書きする。マテリアル側で Enable Dissolve が必要")]
        public bool overrideDissolve = false;
        [Range(0f, 1f)] public float dissolveAmount = 0f;

        [Header("Fill Light")]
        [Tooltip("ON のあいだ Fill Light の色と強度を上書きする（曲中の照り返し演出など）")]
        public bool overrideFill = false;
        [ColorUsage(true, true)] public Color fillColor = new Color(0.4f, 0.45f, 0.6f, 1f);
        [Range(0f, 2f)] public float fillIntensity = 0f;

        private static readonly int BlackOutId       = Shader.PropertyToID("_BlackOut");
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int FillColorId      = Shader.PropertyToID("_FillColor");
        private static readonly int FillIntensityId  = Shader.PropertyToID("_FillIntensity");

        // Play: Doll マテリアルのインスタンスと、Override 解除時の復元用の元値。
        private struct TargetMat
        {
            public Material mat;
            public float origBlackOut;
            public float origDissolve;
            public Color origFillColor;
            public float origFillIntensity;
        }

        private readonly List<TargetMat> _targets = new List<TargetMat>();
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;   // Edit モードプレビュー専用
        private bool _instancesReady;

        private void OnEnable()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                RestoreAll();
            }
            else if (_renderers != null)
            {
                foreach (var r in _renderers)
                    if (r != null) r.SetPropertyBlock(null); // プレビューを非破壊に解除
            }
        }

        private void LateUpdate()
        {
            if (_renderers == null || _renderers.Length == 0) return;

            if (Application.isPlaying)
            {
                if (!_instancesReady) CollectInstances();
                ApplyToInstances();
            }
            else
            {
                ApplyEditPreview();
            }
        }

        // ------------------------------------------------------------------
        //  Play: マテリアルインスタンス経由（SRP Batcher 維持）
        // ------------------------------------------------------------------
        private void CollectInstances()
        {
            _targets.Clear();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                bool hasDoll = false;
                var shared = r.sharedMaterials;
                foreach (var m in shared)
                    if (IsDoll(m)) { hasDoll = true; break; }
                if (!hasDoll) continue;

                // .materials アクセスでスロット全体がインスタンス化される（初回のみ）。
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (!IsDoll(m)) continue;
                    _targets.Add(new TargetMat
                    {
                        mat = m,
                        origBlackOut      = m.HasProperty(BlackOutId) ? m.GetFloat(BlackOutId) : 0f,
                        origDissolve      = m.HasProperty(DissolveAmountId) ? m.GetFloat(DissolveAmountId) : 0f,
                        origFillColor     = m.HasProperty(FillColorId) ? m.GetColor(FillColorId) : Color.white,
                        origFillIntensity = m.HasProperty(FillIntensityId) ? m.GetFloat(FillIntensityId) : 0f,
                    });
                }
            }
            _instancesReady = true;
        }

        private void ApplyToInstances()
        {
            foreach (var t in _targets)
            {
                if (t.mat == null) continue;
                t.mat.SetFloat(BlackOutId,       overrideBlackOut ? blackOut       : t.origBlackOut);
                t.mat.SetFloat(DissolveAmountId, overrideDissolve ? dissolveAmount : t.origDissolve);
                t.mat.SetColor(FillColorId,      overrideFill ? fillColor          : t.origFillColor);
                t.mat.SetFloat(FillIntensityId,  overrideFill ? fillIntensity      : t.origFillIntensity);
            }
        }

        private void RestoreAll()
        {
            foreach (var t in _targets)
            {
                if (t.mat == null) continue;
                t.mat.SetFloat(BlackOutId, t.origBlackOut);
                t.mat.SetFloat(DissolveAmountId, t.origDissolve);
                t.mat.SetColor(FillColorId, t.origFillColor);
                t.mat.SetFloat(FillIntensityId, t.origFillIntensity);
            }
        }

        // ------------------------------------------------------------------
        //  Edit: MaterialPropertyBlock プレビュー（非破壊・資産を汚さない）
        // ------------------------------------------------------------------
        private void ApplyEditPreview()
        {
            bool any = overrideBlackOut || overrideDissolve || overrideFill;
            _mpb ??= new MaterialPropertyBlock();

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (!any) { r.SetPropertyBlock(null); continue; }

                bool hasDoll = false;
                foreach (var m in r.sharedMaterials)
                    if (IsDoll(m)) { hasDoll = true; break; }
                if (!hasDoll) continue;

                _mpb.Clear();
                if (overrideBlackOut) _mpb.SetFloat(BlackOutId, blackOut);
                if (overrideDissolve) _mpb.SetFloat(DissolveAmountId, dissolveAmount);
                if (overrideFill)
                {
                    _mpb.SetColor(FillColorId, fillColor);
                    _mpb.SetFloat(FillIntensityId, fillIntensity);
                }
                r.SetPropertyBlock(_mpb);
            }
        }

        private static bool IsDoll(Material m)
            => m != null && m.shader != null && m.shader.name.StartsWith(ShaderPrefix, System.StringComparison.Ordinal);

        // ------------------------------------------------------------------
        //  スクリプト API（Timeline の Animation Track はフィールド直キーで可）
        // ------------------------------------------------------------------
        public void SetBlackOut(float value)  { overrideBlackOut = true; blackOut = Mathf.Clamp01(value); }
        public void SetDissolve(float value)  { overrideDissolve = true; dissolveAmount = Mathf.Clamp01(value); }
        public void SetFill(Color color, float intensity)
        {
            overrideFill = true;
            fillColor = color;
            fillIntensity = Mathf.Max(0f, intensity);
        }
        public void ClearOverrides()
        {
            overrideBlackOut = overrideDissolve = overrideFill = false;
        }
    }
}
