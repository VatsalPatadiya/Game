using UnityEngine;
using static ProceduralSpriteGenerator;

public static class TileIconGenerator
{
    private const string Directory = "Assets/Textures/Icons";
    private const int Size = 128;

    public static void Generate()
    {
        ProceduralSpriteGenerator.Generate(Directory, Size, 3f, new (string, System.Func<float, float, float>)[]
        {
            ("icon_dots", DotClusterSdf),
            ("icon_flower", FlowerSdf),
            ("icon_star", StarSdf),
            ("icon_diamond", DiamondSdf),
            ("icon_ring", RingSdf),
            ("icon_cross", CrossSdf),
            ("icon_leaf", LeafSdf),
        });

        Debug.Log("TILE_ICON_GENERATOR_DONE");
    }

    private static float DotClusterSdf(float u, float v)
    {
        float best = float.MaxValue;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            float cx = Mathf.Cos(angle) * 0.52f;
            float cy = Mathf.Sin(angle) * 0.52f;
            best = Mathf.Min(best, CircleSdf(u, v, cx, cy, 0.24f));
        }
        return best;
    }

    private static float FlowerSdf(float u, float v)
    {
        float best = CircleSdf(u, v, 0f, 0f, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2f / 5f - Mathf.PI / 2f;
            float cx = Mathf.Cos(angle) * 0.4f;
            float cy = Mathf.Sin(angle) * 0.4f;
            best = Mathf.Min(best, CircleSdf(u, v, cx, cy, 0.32f));
        }
        return best;
    }

    private static float StarSdf(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float theta = Mathf.Atan2(v, u);
        const float outerR = 0.82f;
        const float innerR = 0.36f;
        float lobe = 0.5f + 0.5f * Mathf.Cos(5f * theta);
        float boundary = innerR + (outerR - innerR) * Mathf.Pow(lobe, 2.2f);
        return r - boundary;
    }

    private static float DiamondSdf(float u, float v)
    {
        return Mathf.Abs(u) + Mathf.Abs(v) - 0.78f;
    }

    private static float RingSdf(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        return Mathf.Abs(r - 0.55f) - 0.17f;
    }

    private static float CrossSdf(float u, float v)
    {
        float vertical = BoxSdf(u, v, 0.22f, 0.75f);
        float horizontal = BoxSdf(u, v, 0.75f, 0.22f);
        return Mathf.Min(vertical, horizontal);
    }

    private static float LeafSdf(float u, float v)
    {
        float top = CircleSdf(u, v, 0f, 0.34f, 0.62f);
        float bottom = CircleSdf(u, v, 0f, -0.34f, 0.62f);
        return Mathf.Max(top, bottom);
    }
}
