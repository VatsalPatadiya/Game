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
        // bevelStrength / sheenStrength default to 0 so the plain gradient+frame
        // behaviour (and its tests) are unchanged; the premium tile passes them >0
        // to add a raised lacquered edge (bevel rim) and a soft top-left specular
        // pool (sheen), per premium-ui-guidelines s5.
        public static Texture2D Build(int width, int height, Color ivoryTop, Color ivoryBottom, Color jade,
                                      float framePadding, float frameThickness, float cornerRadius,
                                      float bevelStrength = 0f, float sheenStrength = 0f)
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

            // Outer silhouette used for the bevel rim (no frame padding).
            float ohx = width * 0.5f - 1f;
            float ohy = height * 0.5f - 1f;
            float orad = cornerRadius * width;
            float bevelPx = 0.06f * width;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Diagonal directional highlight: lightest at TOP-LEFT, warmer/
                    // darker toward BOTTOM-RIGHT. Unity texture y=0 is the BOTTOM,
                    // so "top" is high y; g=0 at (x=0, y=height-1).
                    float gx = x / (float)(width - 1);
                    float gy = 1f - y / (float)(height - 1);
                    float g = Mathf.Clamp01((gx + gy) * 0.5f);
                    Color baseCol = Color.Lerp(ivoryTop, ivoryBottom, g);

                    // Jade frame band (unchanged rounded-rect SDF outline).
                    float px = Mathf.Abs(x - cx);
                    float py = Mathf.Abs(y - cy);
                    float qx = px - (hx - rad);
                    float qy = py - (hy - rad);
                    float ax = Mathf.Max(qx, 0f);
                    float ay = Mathf.Max(qy, 0f);
                    float d = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
                    float band = 1f - SmoothStep01(half - 1f, half + 1f, Mathf.Abs(d));
                    Color col = Color.Lerp(baseCol, jade, band);

                    // Bevel rim: brighten edges facing top-left, darken edges facing
                    // bottom-right, only near the silhouette - reads as a raised
                    // lacquered edge with real thickness.
                    if (bevelStrength > 0f)
                    {
                        float dO = OuterSdf(x, y, cx, cy, ohx, ohy, orad);
                        float rim = SmoothStep01(-bevelPx, 0f, dO) * (1f - SmoothStep01(0f, 1f, dO));
                        if (rim > 0f)
                        {
                            // outward normal via finite difference of the silhouette SDF
                            float nx = OuterSdf(x + 1, y, cx, cy, ohx, ohy, orad) - OuterSdf(x - 1, y, cx, cy, ohx, ohy, orad);
                            float ny = OuterSdf(x, y + 1, cx, cy, ohx, ohy, orad) - OuterSdf(x, y - 1, cx, cy, ohx, ohy, orad);
                            float nlen = Mathf.Sqrt(nx * nx + ny * ny) + 1e-5f;
                            nx /= nlen; ny /= nlen;
                            float ndl = nx * -0.7071f + ny * 0.7071f; // light from top-left (+y is top)
                            float shade = ndl * rim * bevelStrength;
                            col.r = Mathf.Clamp01(col.r + shade);
                            col.g = Mathf.Clamp01(col.g + shade);
                            col.b = Mathf.Clamp01(col.b + shade * 0.9f); // slightly warm
                        }
                    }

                    // Soft top-left specular pool.
                    if (sheenStrength > 0f)
                    {
                        float su = x / (float)(width - 1);
                        float sv = 1f - y / (float)(height - 1);
                        float sd = Mathf.Sqrt((su - 0.28f) * (su - 0.28f) + (sv - 0.80f) * (sv - 0.80f));
                        float sheen = SmoothStep01(0.55f, 0f, sd) * sheenStrength;
                        col.r = Mathf.Clamp01(col.r + sheen);
                        col.g = Mathf.Clamp01(col.g + sheen);
                        col.b = Mathf.Clamp01(col.b + sheen);
                    }

                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(updateMipmaps: true);
            return tex;
        }

        private static float OuterSdf(int x, int y, float cx, float cy, float hx, float hy, float rad)
        {
            float px = Mathf.Abs(x - cx) - (hx - rad);
            float py = Mathf.Abs(y - cy) - (hy - rad);
            float ax = Mathf.Max(px, 0f);
            float ay = Mathf.Max(py, 0f);
            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(px, py), 0f) - rad;
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
