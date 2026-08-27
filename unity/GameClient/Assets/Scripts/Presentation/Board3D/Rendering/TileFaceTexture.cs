using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    // Pure albedo generator for the tile face: vertical ivory gradient + an inset
    // jade rounded-rect frame stroke. Fractions (padding/thickness/radius) are in
    // 0..1 of the texture size so the look is resolution-independent.
    public static class TileFaceTexture
    {
        public static Texture2D Build(int size, Color ivoryTop, Color ivoryBottom, Color jade,
                                      float framePadding, float frameThickness, float cornerRadius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "TileFace",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                Color baseCol = Color.Lerp(ivoryBottom, ivoryTop, v);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float d = Mathf.Abs(RoundedRectSdf(u, v, framePadding, cornerRadius));
                    // 1 inside the stroke band, fading to 0 just outside it
                    float half = frameThickness * 0.5f;
                    float band = 1f - Mathf.SmoothStep(half, half + 1.5f / size, d);
                    tex.SetPixel(x, y, Color.Lerp(baseCol, jade, band));
                }
            }
            tex.Apply(updateMipmaps: true);
            return tex;
        }

        // Signed distance (in uv units) to a centred rounded rectangle whose edges
        // sit `padding` in from each side. Negative = inside, positive = outside.
        private static float RoundedRectSdf(float u, float v, float padding, float radius)
        {
            float px = Mathf.Abs(u - 0.5f);
            float py = Mathf.Abs(v - 0.5f);
            float halfExtent = 0.5f - padding;      // rect half-size
            float inner = halfExtent - radius;      // straight-section half-size
            float qx = px - inner;
            float qy = py - inner;
            float ax = Mathf.Max(qx, 0f);
            float ay = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(ax * ax + ay * ay);
            float insideCorner = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + insideCorner - radius;
        }
    }
}
