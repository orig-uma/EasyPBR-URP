// =============================================================================
//  EasyPbrThicknessBaker.cs  (Editor only)
// -----------------------------------------------------------------------------
//  Thickness (SSS Mask) マップのベイク。内向き半球レイで厚みを測定。
// =============================================================================
using UnityEngine;

namespace Origuma.EasyPBR.URP.Editor
{
    internal static class EasyPbrThicknessBaker
    {
        public struct Settings
        {
            public int   resolution;
            public int   rayCount;
            public float maxDistance;
            public float intensity;
            public int   dilate;
            public int   smooth;
            public int   blur;
        }

        public static Settings Default => new Settings
        {
            resolution = 1024, rayCount = 48, maxDistance = 0.3f, intensity = 1.0f,
            dilate = 4, smooth = 2, blur = 1
        };

        public static bool Bake(GameObject root, Material material, Settings s)
            => EasyPbrBakeCore.RunBake(root, material, s.resolution, s.smooth, s.dilate, s.blur,
                "Thickness", "_SSSMask", "_SSSIntensity", needsCollider: true,
                (r, m) => ComputeVertexThickness(r.transform, m, s));

        private static float[] ComputeVertexThickness(Transform xf, Mesh mesh, Settings s)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            int n = verts.Length;
            var result = new float[n];
            int mask = 1 << EasyPbrBakeCore.BakeLayer;
            Vector3[] hemi = EasyPbrBakeCore.BuildHemisphere(s.rayCount);

            for (int i = 0; i < n; i++)
            {
                Vector3 nLocal   = norms.Length == n ? norms[i] : Vector3.up;
                Vector3 normalWS = xf.TransformDirection(nLocal).normalized;
                Vector3 inward   = -normalWS;
                Vector3 originIn = xf.TransformPoint(verts[i]) - normalWS * 1e-3f;
                EasyPbrBakeCore.Basis(inward, out Vector3 t, out Vector3 b);

                float acc = 0f;
                for (int r = 0; r < hemi.Length; r++)
                {
                    Vector3 h = hemi[r];
                    Vector3 dir = (t * h.x + b * h.y + inward * h.z).normalized;
                    acc += Physics.Raycast(originIn, dir, out RaycastHit hit, s.maxDistance, mask)
                        ? hit.distance
                        : s.maxDistance;
                }
                float avg  = acc / Mathf.Max(1, hemi.Length);
                float thin = Mathf.Clamp01((1.0f - avg / Mathf.Max(1e-4f, s.maxDistance)) * s.intensity);
                result[i] = thin;
            }
            return result;
        }
    }
}
