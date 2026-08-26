using UnityEngine;
using static ProceduralSpriteGenerator;

public static class HudIconGenerator
{
    private const string Directory = "Assets/Textures/HudIcons";
    private const int Size = 128;

    public static void Generate()
    {
        ProceduralSpriteGenerator.Generate(Directory, Size, 3f, new (string, System.Func<float, float, float>)[]
        {
            ("icon_hint", LightbulbSdf),
            ("icon_undo", UndoSdf),
            ("icon_shuffle", ShuffleSdf),
        });

        Debug.Log("HUD_ICON_GENERATOR_DONE");
    }

    private static float LightbulbSdf(float u, float v)
    {
        float head = CircleSdf(u, v, 0f, 0.14f, 0.46f);
        float baseBox = BoxSdf(u, v + 0.52f, 0.17f, 0.2f);
        return Mathf.Min(head, baseBox);
    }

    private static float UndoSdf(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float theta = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
        if (theta < 0f) theta += 360f;

        float ring = Mathf.Abs(r - 0.5f) - 0.13f;
        float arc = ring + AngleGapPenalty(theta, 40f, 320f);

        float capAngleRad = 40f * Mathf.Deg2Rad;
        float capX = Mathf.Cos(capAngleRad) * 0.5f;
        float capY = Mathf.Sin(capAngleRad) * 0.5f;
        float cap = CircleSdf(u, v, capX, capY, 0.18f);

        return Mathf.Min(arc, cap);
    }

    // Shape is visible for theta in [rangeStart, rangeEnd]; the gap is the short
    // arc on the other side (rangeEnd -> 360/0 -> rangeStart), which is where the
    // undo icon's "opening" reads as a directional break in the ring.
    private static float AngleGapPenalty(float theta, float rangeStart, float rangeEnd)
    {
        if (theta >= rangeStart && theta <= rangeEnd)
            return 0f;
        float distToStart = Mathf.Abs(Mathf.DeltaAngle(theta, rangeStart));
        float distToEnd = Mathf.Abs(Mathf.DeltaAngle(theta, rangeEnd));
        return Mathf.Min(distToStart, distToEnd) * 0.012f;
    }

    private static float ShuffleSdf(float u, float v)
    {
        float diag1 = DiagonalBarSdf(u, v, 1f);
        float diag2 = DiagonalBarSdf(u, v, -1f);
        float dots = Mathf.Min(
            Mathf.Min(CircleSdf(u, v, 0.55f, 0.55f, 0.14f), CircleSdf(u, v, -0.55f, -0.55f, 0.14f)),
            Mathf.Min(CircleSdf(u, v, 0.55f, -0.55f, 0.14f), CircleSdf(u, v, -0.55f, 0.55f, 0.14f)));
        return Mathf.Min(Mathf.Min(diag1, diag2), dots);
    }

    private static float DiagonalBarSdf(float u, float v, float sign)
    {
        float angle = sign * Mathf.PI / 4f;
        float cos = Mathf.Cos(-angle), sin = Mathf.Sin(-angle);
        float ru = u * cos - v * sin;
        float rv = u * sin + v * cos;
        return BoxSdf(ru, rv, 0.72f, 0.09f);
    }
}
