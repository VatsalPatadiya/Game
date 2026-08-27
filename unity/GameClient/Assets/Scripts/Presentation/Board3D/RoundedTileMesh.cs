using System.Collections.Generic;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    // Procedural rounded-rectangle slab, centred at the origin on XY, extruded
    // along Z. Front face at -Z (toward the camera); back at +Z. Pure/testable:
    // no editor or asset dependencies, so EditMode tests can call it directly.
    public static class RoundedTileMesh
    {
        public static Mesh Build(float width, float height, float thickness,
                                 float cornerRadius, int cornerSegments)
        {
            cornerSegments = Mathf.Max(1, cornerSegments);
            cornerRadius = Mathf.Min(cornerRadius, Mathf.Min(width, height) * 0.5f);

            var perimeter = BuildPerimeter(width, height, cornerRadius, cornerSegments);
            int n = perimeter.Count;
            float halfT = thickness * 0.5f;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // FRONT (-Z), triangle fan from centre
            int frontCentre = verts.Count;
            verts.Add(new Vector3(0, 0, -halfT)); normals.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            int frontStart = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = perimeter[i];
                verts.Add(new Vector3(p.x, p.y, -halfT));
                normals.Add(Vector3.back);
                uvs.Add(new Vector2(p.x / width + 0.5f, p.y / height + 0.5f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = frontStart + i;
                int b = frontStart + (i + 1) % n;
                tris.Add(frontCentre); tris.Add(b); tris.Add(a);
            }

            // BACK (+Z)
            int backCentre = verts.Count;
            verts.Add(new Vector3(0, 0, halfT)); normals.Add(Vector3.forward); uvs.Add(new Vector2(0.5f, 0.5f));
            int backStart = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = perimeter[i];
                verts.Add(new Vector3(p.x, p.y, halfT));
                normals.Add(Vector3.forward);
                uvs.Add(new Vector2(p.x / width + 0.5f, p.y / height + 0.5f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = backStart + i;
                int b = backStart + (i + 1) % n;
                tris.Add(backCentre); tris.Add(a); tris.Add(b);
            }

            // SIDE wall
            int sideStart = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = perimeter[i];
                var outward = new Vector3(p.x, p.y, 0f).normalized;
                verts.Add(new Vector3(p.x, p.y, -halfT)); normals.Add(outward); uvs.Add(new Vector2((float)i / n, 0f));
                verts.Add(new Vector3(p.x, p.y, halfT));  normals.Add(outward); uvs.Add(new Vector2((float)i / n, 1f));
            }
            for (int i = 0; i < n; i++)
            {
                int i0 = sideStart + i * 2;
                int i1 = sideStart + i * 2 + 1;
                int j0 = sideStart + ((i + 1) % n) * 2;
                int j1 = sideStart + ((i + 1) % n) * 2 + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(j0);
                tris.Add(j0); tris.Add(i1); tris.Add(j1);
            }

            var mesh = new Mesh { name = "RoundedTile" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // CCW rounded-rect outline, starting at the bottom-right corner arc.
        private static List<Vector2> BuildPerimeter(float w, float h, float r, int seg)
        {
            var pts = new List<Vector2>();
            float hw = w * 0.5f, hh = h * 0.5f;
            var centres = new[]
            {
                new Vector2(hw - r, -hh + r), // bottom-right
                new Vector2(hw - r,  hh - r), // top-right
                new Vector2(-hw + r, hh - r), // top-left
                new Vector2(-hw + r,-hh + r), // bottom-left
            };
            float[] startAng = { -90f, 0f, 90f, 180f };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = (startAng[c] + 90f * s / seg) * Mathf.Deg2Rad;
                    pts.Add(centres[c] + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
            return pts;
        }
    }
}
