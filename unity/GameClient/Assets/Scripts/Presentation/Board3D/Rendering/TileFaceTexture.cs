using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    // Builds the tile face at an arbitrary width:height so a PORTRAIT tile still
    // gets a uniform-width jade frame. (A square texture stretched onto a tall
    // tile makes the top/bottom border thicker than the sides.) The SDF works in
    // isotropic pixel space, so equal pixel insets map to equal world insets when
    // the texture aspect matches the tile aspect. Frame fractions are of WIDTH.
    public static class TileFaceTexture
    {
        public static Texture2D Build(int width, int height, Color ivoryTop, Color ivoryBottom, Color jade,
                                      float framePadding, float frameThickness, float cornerRadius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true)
            {
                name = "TileFace",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float pad = framePadding * width;
            float half = frameThickness * width * 0.5f;
            float rad = cornerRadius * width;
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            float hx = width * 0.5f - pad;   // frame outer half-extent, px
            float hy = height * 0.5f - pad;

            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                Color baseCol = Color.Lerp(ivoryBottom, ivoryTop, v);
                for (int x = 0; x < width; x++)
                {
                    float px = Mathf.Abs(x - cx);
                    float py = Mathf.Abs(y - cy);
                    float qx = px - (hx - rad);
                    float qy = py - (hy - rad);
                    float ax = Mathf.Max(qx, 0f);
                    float ay = Mathf.Max(qy, 0f);
                    // isotropic rounded-rect SDF in pixels, <0 inside the frame outline
                    float d = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
                    float band = 1f - SmoothStep01(half - 1f, half + 1f, Mathf.Abs(d)); // ~1px feather
                    tex.SetPixel(x, y, Color.Lerp(baseCol, jade, band));
                }
            }
            tex.Apply(updateMipmaps: true);
            return tex;
        }

        // GLSL-style smoothstep: 0 below edge0, 1 above edge1, smooth in between.
        // (Unity's Mathf.SmoothStep is a smoothed lerp between edge0 and edge1, not this.)
        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
