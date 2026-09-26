using System.IO;
using UnityEditor;
using UnityEngine;

// Procedurally draws the "Celestial Tiles Mahjong" brand mark - a circular seal
// (echoing LevelStartScreen3D's circular level badge) containing an arched
// gate silhouette (echoing that same screen's arched double-door), rendered
// in the project's own established palette (TileMaterialGenerator's Ivory/
// Jade, GameSceneBuilder3D's GoldChrome) so it reads as part of the same
// world as the rest of the game rather than a bolted-on logo. Same per-pixel
// signed-distance-field technique as TileFaceTexture.Build, just composing
// circle/rect SDFs instead of a single rounded rect.
public static class LogoGenerator
{
    private static readonly Color IvoryTop = new Color(0.957f, 0.945f, 0.906f);
    private static readonly Color IvoryBottom = new Color(0.925f, 0.906f, 0.855f);
    private static readonly Color Jade = new Color(0.141f, 0.200f, 0.259f);
    private static readonly Color JadeDeep = new Color(0.090f, 0.135f, 0.180f); // background gradient's dark end
    private static readonly Color GoldChrome = new Color(0.85f, 0.65f, 0.25f);

    private const int Size = 1024;

    [MenuItem("Tools/Branding/Generate Celestial Tiles Mahjong Logo")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures/Branding");

        // Full mark on a filled jade disc - used directly as the legacy/
        // round app icon and as the splash lockup's mark.
        var markOnJade = BuildMark(Size, includeBackground: true);
        WritePng(markOnJade, "Assets/Textures/Branding/Logo_Mark.png");
        Object.DestroyImmediate(markOnJade);

        // Foreground-only (transparent background) + solid background layer,
        // per Android's adaptive icon format (each composited by the OS).
        var foreground = BuildMark(Size, includeBackground: false);
        WritePng(foreground, "Assets/Textures/Branding/Icon_Foreground.png");
        Object.DestroyImmediate(foreground);

        var background = BuildBackground(Size);
        WritePng(background, "Assets/Textures/Branding/Icon_Background.png");
        Object.DestroyImmediate(background);

        AssetDatabase.Refresh();

        foreach (var path in new[]
        {
            "Assets/Textures/Branding/Logo_Mark.png",
            "Assets/Textures/Branding/Icon_Foreground.png",
            "Assets/Textures/Branding/Icon_Background.png",
        })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }

        Debug.Log("LOGO_GENERATOR_DONE");
    }

    private static void WritePng(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }

    private static Texture2D BuildBackground(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float maxDist = Mathf.Sqrt(cx * cx + cy * cy);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / maxDist;
                Color col = Color.Lerp(Jade, JadeDeep, d);
                col.a = 1f;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    // includeBackground=true draws the radial jade backdrop first (for a
    // standalone "logo on its own field" use, e.g. splash/legacy icon).
    // includeBackground=false leaves everything outside the mark transparent
    // (for the Android adaptive icon foreground layer, composited over
    // BuildBackground by the OS).
    private static Texture2D BuildMark(int size, bool includeBackground)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;

        // Outer seal ring (thin gold circle) and its jade fill, echoing the
        // in-game LevelBadge's circular disc treatment.
        float ringOuterR = size * 0.46f;
        float ringInnerR = size * 0.435f;
        float sealFillR = size * 0.42f;

        // Arched gate silhouette (door body + domed top), echoing
        // LevelStartScreen3D's arched double-door. Built as the SDF union
        // (min) of a rounded rectangle "door body" and a circle "dome",
        // the dome's radius equal to the door's half-width so the two meet
        // with a continuous tangent (a classic architectural arch profile).
        float doorHalfW = size * 0.155f;
        float doorTopY = cy + size * 0.06f;   // where the dome springs from
        float doorBottomY = cy - size * 0.24f; // door's flat bottom edge
        float doorCenterX = cx;
        float domeCenterY = doorTopY;
        float domeR = doorHalfW;

        // Small gold keystone accent at the dome's apex, echoing the small
        // circular level badge that floats above the in-game door.
        float keystoneCx = cx;
        float keystoneCy = domeCenterY + domeR + size * 0.045f;
        float keystoneR = size * 0.018f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dxCenter = x - cx;
                float dyCenter = y - cy;
                float distFromCenter = Mathf.Sqrt(dxCenter * dxCenter + dyCenter * dyCenter);

                Color col;
                float alpha;

                if (includeBackground)
                {
                    col = Jade;
                    alpha = 1f;
                }
                else
                {
                    col = Color.clear;
                    alpha = 0f;
                }

                // Seal fill (ivory disc) + gold ring, only within the outer ring radius.
                if (distFromCenter <= ringOuterR)
                {
                    float gy = 1f - (y / (float)(size - 1));
                    Color ivory = Color.Lerp(IvoryTop, IvoryBottom, gy);
                    col = ivory;
                    alpha = 1f;

                    float ringBand = 1f - SmoothStep01(1.5f, 3.5f, Mathf.Abs(distFromCenter - (ringOuterR + ringInnerR) * 0.5f) - (ringOuterR - ringInnerR) * 0.5f);
                    col = Color.Lerp(col, GoldChrome, Mathf.Clamp01(ringBand));

                    float outerAA = 1f - SmoothStep01(ringOuterR - 1.5f, ringOuterR + 1.5f, distFromCenter);
                    alpha *= outerAA;
                }
                else if (includeBackground)
                {
                    alpha = 1f; // outside the ring stays plain jade backdrop
                }

                // Door body: rounded rect, SDF.
                float rx = Mathf.Abs(x - doorCenterX) - doorHalfW;
                float ryTop = doorTopY - y;      // >0 below the spring line
                float ryBottom = y - doorBottomY; // >0 above the floor line
                float doorSdf;
                if (y <= doorTopY)
                {
                    float qx = Mathf.Max(rx, 0f);
                    float qyTop = Mathf.Max(-ryBottom, 0f);
                    doorSdf = Mathf.Sqrt(qx * qx + qyTop * qyTop) + Mathf.Min(Mathf.Max(rx, -ryBottom), 0f);
                }
                else
                {
                    doorSdf = 9999f; // above the spring line, the dome SDF below takes over
                }

                // Dome: circle SDF, only relevant above the spring line.
                float ddx = x - doorCenterX;
                float ddy = y - domeCenterY;
                float domeSdf = Mathf.Sqrt(ddx * ddx + ddy * ddy) - domeR;

                float archSdf = (y > doorTopY) ? domeSdf : Mathf.Min(doorSdf, domeSdf);

                float archBand = 1f - SmoothStep01(-1.5f, 1.5f, archSdf);
                if (archBand > 0f)
                {
                    Color gy2Col = Color.Lerp(Jade, JadeDeep, Mathf.Clamp01(1f - (y / (float)(size - 1))));
                    col = Color.Lerp(col, gy2Col, archBand);
                    alpha = Mathf.Max(alpha, archBand);
                }

                // Thin gold keyline tracing the arch silhouette.
                float keylineBand = SmoothStep01(0f, 2f, 2f - Mathf.Abs(archSdf));
                if (keylineBand > 0f && archSdf < 6f)
                {
                    col = Color.Lerp(col, GoldChrome, keylineBand * 0.8f);
                }

                // Keystone accent dot.
                float kdx = x - keystoneCx;
                float kdy = y - keystoneCy;
                float keystoneSdf = Mathf.Sqrt(kdx * kdx + kdy * kdy) - keystoneR;
                float keystoneBand = 1f - SmoothStep01(-1.5f, 1.5f, keystoneSdf);
                if (keystoneBand > 0f)
                {
                    col = Color.Lerp(col, GoldChrome, keystoneBand);
                    alpha = Mathf.Max(alpha, keystoneBand);
                }

                col.a = alpha;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return tex;
    }

    private static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
