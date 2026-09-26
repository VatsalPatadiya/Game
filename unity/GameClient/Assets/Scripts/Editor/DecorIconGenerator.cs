using UnityEngine;
using static ProceduralSpriteGenerator;

// Faint background decoration (mockup's `.leaf` silhouettes) - a single leaf
// shape baked once as a sprite; GameSceneBuilder3D places 3 low-opacity
// copies of it on both HUD screens, matching the mockup's corner flourishes.
public static class DecorIconGenerator
{
    private const string Directory = "Assets/Textures/Decor";
    private const int Size = 256;

    public static void Generate()
    {
        ProceduralSpriteGenerator.Generate(Directory, Size, 3f, new (string, System.Func<float, float, float>)[]
        {
            ("leaf", LeafSdf),
        });

        Debug.Log("DECOR_ICON_GENERATOR_DONE");
    }

    // Vesica (lens) shape from two circles offset horizontally: the boundary
    // crossings land on the vertical axis, giving pointed top/bottom tips -
    // a leaf/paisley silhouette, taller than it is wide (~1.5:1, close to the
    // mockup's 100:140 viewBox), without needing a hand-authored outline.
    private static float LeafSdf(float u, float v)
    {
        const float r = 0.52f, o = 0.19f;
        float c1 = CircleSdf(u, v, -o, 0f, r);
        float c2 = CircleSdf(u, v, o, 0f, r);
        return Mathf.Max(c1, c2);
    }
}
