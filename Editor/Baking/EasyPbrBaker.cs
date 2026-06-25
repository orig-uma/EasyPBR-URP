// =============================================================================
//  EasyPbrBaker.cs  (Editor only)
// -----------------------------------------------------------------------------
//  メッシュからマップを「焼く」ベイカー群（AO / 顔SDF / キャビティ / 厚み）。
//  DCC 不要で、選択キャラから各マップを生成して該当スロットへ自動アサインする。
//
//  共通土台 RunBake:
//    Root 配下から「対象マテリアルを使う Renderer 全部」を集める →
//    （必要なら）全メッシュの一時 MeshCollider を立てる（全パーツが遮蔽源）→
//    Renderer ごとに頂点値を計算し、対象サブメッシュだけを 1 枚の UV へ累積ラスタライズ →
//    ダイレート → ブラー → 保存(Linear) → マテリアルへアサイン。
//  これにより「1 マテリアルを複数メッシュで共有」していても 1 テクスチャに焼ける。
//
//  ランタイムには一切含まれない（Editor アセンブリ）。
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Origuma.EasyPBR.URP.Editor
{
    internal static class EasyPbrBaker
    {
        private const int BakeLayer = 31; // レイキャスト隔離用（通常未使用の最上位）

        // ===== 設定 =========================================================
        public struct AoSettings
        {
            public int   resolution;
            public int   rayCount;
            public float maxDistance;
            public float intensity;
            public int   dilate;
            public int   smooth;
            public float floor;
            public int   blur;
            public float enclosedCutoff;
        }
        public static AoSettings DefaultAo => new AoSettings
        {
            resolution = 1024, rayCount = 64, maxDistance = 0.25f, intensity = 1.0f,
            dilate = 4, smooth = 2, floor = 0.0f, blur = 1, enclosedCutoff = 0.95f
        };

        public struct SdfSettings
        {
            public int   resolution;
            public int   angleSteps;
            public float ndotlThreshold;
            public bool  useCastShadow;
            public float castDistance;
            public bool  flipForward;
            public int   smooth;
            public int   blur;
            public int   dilate;
        }
        public static SdfSettings DefaultSdf => new SdfSettings
        {
            resolution = 1024, angleSteps = 90, ndotlThreshold = 0.0f,
            useCastShadow = true, castDistance = 0.15f, flipForward = false,
            smooth = 1, blur = 1, dilate = 4
        };

        public struct CavitySettings
        {
            public int   resolution;
            public float intensity;   // くぼみ darkening の強さ
            public int   dilate;
            public int   smooth;
            public int   blur;
        }
        public static CavitySettings DefaultCavity => new CavitySettings
        {
            resolution = 1024, intensity = 4.0f, dilate = 4, smooth = 1, blur = 1
        };

        public struct ThicknessSettings
        {
            public int   resolution;
            public int   rayCount;
            public float maxDistance; // この距離で「厚い」とみなす（薄いほど SSS 強）
            public float intensity;
            public int   dilate;
            public int   smooth;
            public int   blur;
        }
        public static ThicknessSettings DefaultThickness => new ThicknessSettings
        {
            resolution = 1024, rayCount = 48, maxDistance = 0.3f, intensity = 1.0f,
            dilate = 4, smooth = 2, blur = 1
        };

        // ===== 公開エントリ（Root GameObject を渡す）=========================
        public static bool BakeAmbientOcclusion(GameObject root, Material material, AoSettings s)
            => RunBake(root, material, s.resolution, s.smooth, s.dilate, s.blur,
                       "AO", "_OcclusionMap", "_OcclusionStrength", true,
                       (r, m) => ComputeVertexAO(r.transform, m, s));

        // 顔SDFは2チャンネルで焼く: R=右光用 / G=左光用。ランタイムはミラー不要＝
        // 左右非対称の顔（傷跡・マーク等）にも対応。
        public static bool BakeFaceSdf(GameObject root, Material material, SdfSettings s)
            => RunBake(root, material, s.resolution, s.smooth, s.dilate, s.blur,
                       "FaceSDF", "_FaceSDFMap", "_UseFaceSDF", true,  // 焼いたら自動で有効化
                       (r, m) => SdfSweep(r, m, s, +1f),   // R: 右向きスイープ
                       (r, m) => SdfSweep(r, m, s, -1f));  // G: 左向きスイープ

        private static float[] SdfSweep(Renderer r, Mesh m, SdfSettings s, float sign)
        {
            Vector3 fwd   = r.transform.forward * (s.flipForward ? -1f : 1f);
            Vector3 up    = r.transform.up;
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, fwd));
            return ComputeVertexSdf(r.transform, m, fwd, up, right, s, sign);
        }

        public static bool BakeCavity(GameObject root, Material material, CavitySettings s)
            => RunBake(root, material, s.resolution, s.smooth, s.dilate, s.blur,
                       "Cavity", "_CavityMap", "_CavityStrength", false,
                       (r, m) => ComputeVertexCavity(m, s));

        public static bool BakeThickness(GameObject root, Material material, ThicknessSettings s)
            => RunBake(root, material, s.resolution, s.smooth, s.dilate, s.blur,
                       "Thickness", "_SSSMask", "_SSSIntensity", true,
                       (r, m) => ComputeVertexThickness(r.transform, m, s));

        // ===== 共通パイプライン =============================================
        private static bool RunBake(GameObject root, Material material, int res, int smooth, int dilate, int blur,
                                    string suffix, string slot, string strengthProp, bool needsCollider,
                                    Func<Renderer, Mesh, float[]> compute,
                                    Func<Renderer, Mesh, float[]> computeG = null)
        {
            if (root == null || material == null)
            {
                EditorUtility.DisplayDialog("EasyPBR Baker", "Root（GameObject）と Material が必要です。", "OK");
                return false;
            }

            var renderers = GatherRenderers(root, material);
            if (renderers.Count == 0)
            {
                // 複数マテリアル一括ベイク時にダイアログが連発しないよう警告ログに留める。
                Debug.LogWarning($"[EasyPBR Baker] '{root.name}' 配下に '{material.name}' を使う Renderer が無いためスキップ。");
                return false;
            }

            bool prevBackface = Physics.queriesHitBackfaces;
            var temps = new List<GameObject>();
            var usable = new List<Renderer>();
            var meshes = new List<Mesh>();
            var tempFlags = new List<bool>();
            try
            {
                EditorUtility.DisplayProgressBar("EasyPBR Baker", "メッシュを準備中...", 0.05f);
                foreach (var r in renderers)
                {
                    var m = ResolveMesh(r, out bool tmp);
                    if (m == null || m.vertexCount == 0) continue;
                    usable.Add(r); meshes.Add(m); tempFlags.Add(tmp);
                }
                if (usable.Count == 0)
                {
                    EditorUtility.DisplayDialog("EasyPBR Baker",
                        "焼けるメッシュがありません（Read/Write Enabled が無効の可能性）。", "OK");
                    return false;
                }

                if (needsCollider)
                {
                    Physics.queriesHitBackfaces = true;
                    for (int i = 0; i < usable.Count; i++)
                    {
                        var r = usable[i];
                        var go = new GameObject("~EasyPbrBakeCollider") { hideFlags = HideFlags.HideAndDontSave };
                        go.layer = BakeLayer;
                        go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                        go.transform.localScale = r.transform.lossyScale;
                        var col = go.AddComponent<MeshCollider>();
                        col.sharedMesh = meshes[i];
                        temps.Add(go);
                    }
                    Physics.SyncTransforms();
                }

                var px = new Color32[res * res];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                var covered = new bool[res * res];

                for (int i = 0; i < usable.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("EasyPBR Baker",
                        $"計算中... ({i + 1}/{usable.Count})", 0.1f + 0.7f * i / usable.Count);
                    float[] vr = compute(usable[i], meshes[i]);
                    SmoothVertexScalar(meshes[i], vr, smooth);
                    float[] vg = null;
                    if (computeG != null)
                    {
                        vg = computeG(usable[i], meshes[i]);
                        SmoothVertexScalar(meshes[i], vg, smooth);
                    }
                    int[] subs = ResolveSubmeshes(usable[i], material, meshes[i]);
                    RasterizeInto(px, covered, meshes[i], vr, vg, res, subs);
                }

                EditorUtility.DisplayProgressBar("EasyPBR Baker", "仕上げ中...", 0.85f);
                var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
                tex.SetPixels32(px);
                tex.Apply(false, false);
                _coverage = covered;
                Dilate(tex, dilate);
                BlurTexture(tex, blur);

                string meshName = usable.Count == 1 ? ResolveSourceMeshName(usable[0]) : root.name;
                bool ok = SaveAndAssign(tex, material, meshName, suffix, slot, strengthProp);
                UnityEngine.Object.DestroyImmediate(tex);
                return ok;
            }
            finally
            {
                Physics.queriesHitBackfaces = prevBackface;
                foreach (var go in temps) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                for (int i = 0; i < meshes.Count; i++)
                    if (tempFlags[i] && meshes[i] != null) UnityEngine.Object.DestroyImmediate(meshes[i]);
                EditorUtility.ClearProgressBar();
            }
        }

        // Root 配下から、対象マテリアルを使う MeshRenderer / SkinnedMeshRenderer を集める。
        private static List<Renderer> GatherRenderers(GameObject root, Material material)
        {
            var list = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (Array.IndexOf(r.sharedMaterials, material) >= 0) list.Add(r);
            }
            return list;
        }

        // ===== 頂点値の計算 =================================================

        // AO: 半球レイの遮蔽率（白=遮蔽なし）。
        private static float[] ComputeVertexAO(Transform xf, Mesh mesh, AoSettings s)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            int n = verts.Length;
            var result = new float[n];
            int mask = 1 << BakeLayer;
            Vector3[] hemi = BuildHemisphere(s.rayCount);

            for (int i = 0; i < n; i++)
            {
                Vector3 nLocal   = norms.Length == n ? norms[i] : Vector3.up;
                Vector3 originWS = xf.TransformPoint(verts[i]);
                Vector3 normalWS = xf.TransformDirection(nLocal).normalized;
                Vector3 bias     = normalWS * 1e-3f;
                Basis(normalWS, out Vector3 t, out Vector3 b);

                int hits = 0;
                for (int r = 0; r < hemi.Length; r++)
                {
                    Vector3 h = hemi[r];
                    Vector3 dir = (t * h.x + b * h.y + normalWS * h.z).normalized;
                    if (Physics.Raycast(originWS + bias, dir, s.maxDistance, mask)) hits++;
                }
                float occ = (float)hits / Mathf.Max(1, hemi.Length);
                float bright = Mathf.Clamp01(1.0f - occ * s.intensity);
                float toWhite = Mathf.Clamp01(Mathf.InverseLerp(s.enclosedCutoff - 0.05f, s.enclosedCutoff, occ));
                bright = Mathf.Lerp(bright, 1.0f, toWhite);
                result[i] = Mathf.Lerp(s.floor, 1.0f, bright);
            }
            return result;
        }

        // 顔SDF: 各頂点が陰に入る「光の前向き度」を 0..1 で。sweepSign +1=右向き / -1=左向き。
        private static float[] ComputeVertexSdf(Transform xf, Mesh mesh, Vector3 fwd, Vector3 up, Vector3 right,
                                                SdfSettings s, float sweepSign)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            int n = verts.Length;
            var result = new float[n];
            int mask = 1 << BakeLayer;
            int steps = Mathf.Max(2, s.angleSteps);

            for (int i = 0; i < n; i++)
            {
                Vector3 N        = norms.Length == n ? xf.TransformDirection(norms[i]).normalized : up;
                Vector3 originWS = xf.TransformPoint(verts[i]);
                Vector3 bias     = N * 1e-3f;

                float sdf = 0f;
                bool prevLit = true;
                for (int st = 0; st < steps; st++)
                {
                    float th = Mathf.PI * st / (steps - 1);
                    Vector3 L = (fwd * Mathf.Cos(th) + right * (sweepSign * Mathf.Sin(th))).normalized;

                    bool lit = Vector3.Dot(N, L) > s.ndotlThreshold;
                    if (lit && s.useCastShadow)
                        if (Physics.Raycast(originWS + bias, L, s.castDistance, mask)) lit = false;

                    if (st == 0 && !lit) { sdf = 1f; break; }
                    if (prevLit && !lit) { sdf = Mathf.Cos(th) * 0.5f + 0.5f; break; }
                    prevLit = lit;
                }
                result[i] = sdf;
            }
            return result;
        }

        // キャビティ: 隣接頂点が法線側にあるほど凹（くぼみ）→ 暗化（白=平坦/凸、暗=くぼみ）。レイ不要。
        private static float[] ComputeVertexCavity(Mesh mesh, CavitySettings s)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            int n = verts.Length;
            var sum = new float[n];
            var cnt = new int[n];
            int[] tris = mesh.triangles;

            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                AccumCavity(a, b, verts, norms, sum, cnt); AccumCavity(a, c, verts, norms, sum, cnt);
                AccumCavity(b, a, verts, norms, sum, cnt); AccumCavity(b, c, verts, norms, sum, cnt);
                AccumCavity(c, a, verts, norms, sum, cnt); AccumCavity(c, b, verts, norms, sum, cnt);
            }

            var result = new float[n];
            for (int i = 0; i < n; i++)
            {
                float mean = cnt[i] > 0 ? sum[i] / cnt[i] : 0f; // >0 = 凹
                result[i] = 1.0f - Mathf.Clamp01(mean * s.intensity);
            }
            return result;
        }

        private static void AccumCavity(int i, int j, Vector3[] verts, Vector3[] norms, float[] sum, int[] cnt)
        {
            if (norms.Length != verts.Length) return;
            Vector3 d = verts[j] - verts[i];
            float len = d.magnitude;
            if (len < 1e-7f) return;
            sum[i] += Vector3.Dot(d / len, norms[i]);
            cnt[i]++;
        }

        // 厚み(SSS用): 内向きに半球レイを飛ばし、出口までの距離＝厚み。薄いほど高い値（白）。
        private static float[] ComputeVertexThickness(Transform xf, Mesh mesh, ThicknessSettings s)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            int n = verts.Length;
            var result = new float[n];
            int mask = 1 << BakeLayer;
            Vector3[] hemi = BuildHemisphere(s.rayCount);

            for (int i = 0; i < n; i++)
            {
                Vector3 nLocal   = norms.Length == n ? norms[i] : Vector3.up;
                Vector3 normalWS = xf.TransformDirection(nLocal).normalized;
                Vector3 inward   = -normalWS;
                Vector3 originIn = xf.TransformPoint(verts[i]) - normalWS * 1e-3f; // 表面の少し内側
                Basis(inward, out Vector3 t, out Vector3 b);

                float acc = 0f;
                for (int r = 0; r < hemi.Length; r++)
                {
                    Vector3 h = hemi[r];
                    Vector3 dir = (t * h.x + b * h.y + inward * h.z).normalized;
                    acc += Physics.Raycast(originIn, dir, out RaycastHit hit, s.maxDistance, mask)
                        ? hit.distance
                        : s.maxDistance; // 当たらない＝厚い
                }
                float avg  = acc / Mathf.Max(1, hemi.Length);
                float thin = Mathf.Clamp01((1.0f - avg / Mathf.Max(1e-4f, s.maxDistance)) * s.intensity);
                result[i] = thin; // 薄い＝白＝SSS 強
            }
            return result;
        }

        // ===== メッシュ / サブメッシュ =====================================
        private static Mesh ResolveMesh(Renderer renderer, out bool isTemp)
        {
            isTemp = false;
            if (renderer is SkinnedMeshRenderer smr)
            {
                var baked = new Mesh { name = "~bakedPose" };
                smr.BakeMesh(baked, false); // スケールは transform 側で適用（二重スケール回避）
                isTemp = true;
                return baked;
            }
            var mf = renderer.GetComponent<MeshFilter>();
            var shared = mf != null ? mf.sharedMesh : null;
            if (shared != null && !shared.isReadable)
            {
                Debug.LogWarning($"[EasyPBR Baker] '{shared.name}' は Read/Write 無効のためスキップ。インポート設定で有効化してください。");
                return null;
            }
            return shared;
        }

        private static int[] ResolveSubmeshes(Renderer renderer, Material material, Mesh mesh)
        {
            var mats = renderer.sharedMaterials;
            var list = new List<int>();
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (i < mats.Length && mats[i] == material) list.Add(i);
            if (list.Count == 0)
                for (int i = 0; i < mesh.subMeshCount; i++) list.Add(i);
            return list.ToArray();
        }

        private static string ResolveSourceMeshName(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null) return smr.sharedMesh.name;
            var mf = renderer.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : renderer.name;
        }

        // ===== サンプリング基底 ============================================
        private static Vector3[] BuildHemisphere(int count)
        {
            count = Mathf.Max(1, count);
            var dirs = new Vector3[count];
            float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Sqrt((i + 0.5f) / count);
                float r = Mathf.Sqrt(1f - z * z);
                float phi = i * ga;
                dirs[i] = new Vector3(Mathf.Cos(phi) * r, Mathf.Sin(phi) * r, z);
            }
            return dirs;
        }

        private static void Basis(Vector3 n, out Vector3 t, out Vector3 b)
        {
            Vector3 up = Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right;
            t = Vector3.Normalize(Vector3.Cross(up, n));
            b = Vector3.Cross(n, t);
        }

        // ===== ラスタライズ / 後処理 =======================================
        private static bool[] _coverage;

        // 頂点スカラを既存バッファ(px/covered)へ累積ラスタライズ（複数メッシュ対応）。
        //  valueG != null のとき 2 チャンネル（R=value / G=valueG / B=0）で書き込む（顔SDF用）。
        //  null のときはグレースケール（R=G=B=value）。
        private static void RasterizeInto(Color32[] px, bool[] covered, Mesh mesh,
                                          float[] value, float[] valueG, int res, int[] submeshes)
        {
            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length != mesh.vertexCount) return;
            bool two = valueG != null;

            foreach (int sub in submeshes)
            {
                int[] tris = mesh.GetTriangles(sub);
                for (int ti = 0; ti < tris.Length; ti += 3)
                {
                    int i0 = tris[ti], i1 = tris[ti + 1], i2 = tris[ti + 2];
                    Vector2 p0 = uv[i0] * res, p1 = uv[i1] * res, p2 = uv[i2] * res;
                    RasterTriangle(px, covered, res, p0, p1, p2,
                        value[i0], value[i1], value[i2],
                        two ? valueG[i0] : value[i0], two ? valueG[i1] : value[i1], two ? valueG[i2] : value[i2],
                        two);
                }
            }
        }

        private static void RasterTriangle(Color32[] px, bool[] covered, int res,
            Vector2 a, Vector2 b, Vector2 c,
            float va, float vb, float vc, float ga, float gb, float gc, bool two)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, res - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt (Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, res - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, res - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt (Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, res - 1);

            float denom = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(denom) < 1e-9f) return;
            float invDen = 1f / denom;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float w0 = ((b.y - c.y) * (fx - c.x) + (c.x - b.x) * (fy - c.y)) * invDen;
                float w1 = ((c.y - a.y) * (fx - c.x) + (a.x - c.x) * (fy - c.y)) * invDen;
                float w2 = 1f - w0 - w1;
                if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;

                float vr = Mathf.Clamp01(w0 * va + w1 * vb + w2 * vc);
                byte rB = (byte)(vr * 255f + 0.5f);
                int idx = y * res + x;
                if (two)
                {
                    float vg = Mathf.Clamp01(w0 * ga + w1 * gb + w2 * gc);
                    px[idx] = new Color32(rB, (byte)(vg * 255f + 0.5f), 0, 255);
                }
                else
                {
                    px[idx] = new Color32(rB, rB, rB, 255);
                }
                covered[idx] = true;
            }
        }

        private static void Dilate(Texture2D tex, int iterations)
        {
            if (_coverage == null || iterations <= 0) return;
            int res = tex.width;
            var px = tex.GetPixels32();
            var covered = (bool[])_coverage.Clone();

            for (int it = 0; it < iterations; it++)
            {
                var next = (bool[])covered.Clone();
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int idx = y * res + x;
                    if (covered[idx]) continue;
                    for (int dy = -1; dy <= 1 && !next[idx]; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= res || ny >= res) continue;
                        int nIdx = ny * res + nx;
                        if (covered[nIdx]) { px[idx] = px[nIdx]; next[idx] = true; break; }
                    }
                }
                covered = next;
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _coverage = covered;
        }

        // 三角形エッジ近傍で頂点値を平滑化（粗いメッシュのファセット低減。AO/SDF/厚み共通）。
        private static void SmoothVertexScalar(Mesh mesh, float[] v, int iterations)
        {
            if (iterations <= 0) return;
            int n = v.Length;
            int[] tris = mesh.triangles;

            for (int it = 0; it < iterations; it++)
            {
                var sum = new float[n];
                var count = new int[n];
                for (int i = 0; i < n; i++) { sum[i] = v[i]; count[i] = 1; }

                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    sum[a] += v[b] + v[c]; count[a] += 2;
                    sum[b] += v[a] + v[c]; count[b] += 2;
                    sum[c] += v[a] + v[b]; count[c] += 2;
                }
                for (int i = 0; i < n; i++) v[i] = sum[i] / count[i];
            }
        }

        private static void BlurTexture(Texture2D tex, int radius)
        {
            if (radius <= 0 || _coverage == null) { _coverage = null; return; }
            int res = tex.width;
            var src = tex.GetPixels32();
            var dst = (Color32[])src.Clone();
            var cov = _coverage;

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                int idx = y * res + x;
                if (!cov[idx]) continue;
                int accR = 0, accG = 0, accB = 0, num = 0;
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= res || ny >= res) continue;
                    int nIdx = ny * res + nx;
                    if (!cov[nIdx]) continue;
                    accR += src[nIdx].r; accG += src[nIdx].g; accB += src[nIdx].b; num++;
                }
                if (num > 0)
                    dst[idx] = new Color32((byte)(accR / num), (byte)(accG / num), (byte)(accB / num), 255);
            }
            tex.SetPixels32(dst);
            tex.Apply(false, false);
            _coverage = null;
        }

        // ===== 保存 / アサイン =============================================
        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static bool SaveAndAssign(Texture2D tex, Material material, string meshName,
                                          string suffix, string slot, string strengthProp)
        {
            string matPath = AssetDatabase.GetAssetPath(material);
            string dir = string.IsNullOrEmpty(matPath) ? "Assets" : Path.GetDirectoryName(matPath);
            string bakedDir = Path.Combine(dir, "Baked").Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(bakedDir))
                AssetDatabase.CreateFolder(dir, "Baked");

            string baseName = Sanitize($"{meshName}_{material.name}_{suffix}");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{bakedDir}/{baseName}.png");

            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;                                   // リニアデータ
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // データマップは無圧縮（2chSDFのR/G混色防止）
                importer.SaveAndReimport();
            }

            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (imported == null) return false;

            Undo.RecordObject(material, "Assign Baked Map");
            if (slot != null && material.HasProperty(slot)) material.SetTexture(slot, imported);
            if (strengthProp != null && material.HasProperty(strengthProp) && material.GetFloat(strengthProp) <= 0f)
                material.SetFloat(strengthProp, 1f);
            EditorUtility.SetDirty(material);

            string assignNote = (slot != null && material.HasProperty(slot)) ? $"（{slot} に自動アサイン）" : "（保存のみ）";
            // Ping はしない（Project ビューが生成先へジャンプして連続ベイクの邪魔になるため）。
            // 場所は下のログをクリックすればハイライトされる。
            Debug.Log($"[EasyPBR Baker] {suffix} baked → {path} {assignNote}", imported);
            return true;
        }
    }
}
