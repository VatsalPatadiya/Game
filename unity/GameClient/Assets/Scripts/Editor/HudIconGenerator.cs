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
            ("icon_lock", LockSdf),
            ("icon_back", BackChevronSdf),
            ("icon_menu", MenuSdf),
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

    // Crossing "shuffle" arrows: two diagonal bars through the centre, each
    // ending in a ">" arrowhead at ONE of its two ends (both pointing
    // rightward - upper-right and lower-right), not both. That asymmetry is
    // what reads as directional arrows instead of a symmetric X/prohibition
    // glyph (the failure mode of two earlier attempts: plain crossed bars,
    // then a fully symmetric circular loop).
    private static float ShuffleSdf(float u, float v)
    {
        // Shorter bars + bigger arrowheads than the first attempt - the
        // arrowheads need to visually dominate the crossing point, not read
        // as a minor detail on otherwise-plain crossed bars.
        const float halfLength = 0.38f;
        float bar1 = DiagonalBarSdf(u, v, 1f, halfLength);
        float bar2 = DiagonalBarSdf(u, v, -1f, halfLength);
        float head1 = ChevronTipSdf(u, v, angleDegrees: 45f, tipDistance: halfLength);
        float head2 = ChevronTipSdf(u, v, angleDegrees: -45f, tipDistance: halfLength);
        return Mathf.Min(Mathf.Min(bar1, bar2), Mathf.Min(head1, head2));
    }

    private static float DiagonalBarSdf(float u, float v, float sign, float halfLength)
    {
        float angle = sign * Mathf.PI / 4f;
        float cos = Mathf.Cos(-angle), sin = Mathf.Sin(-angle);
        float ru = u * cos - v * sin;
        float rv = u * sin + v * cos;
        return BoxSdf(ru, rv, halfLength, 0.09f);
    }

    // A small ">" arrowhead whose tip sits `tipDistance` from the origin
    // along `angleDegrees`, pointing further outward in that same direction -
    // two short arms folded back from the tip at +-150 degrees.
    private static float ChevronTipSdf(float u, float v, float angleDegrees, float tipDistance)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float tipX = Mathf.Cos(rad) * tipDistance, tipY = Mathf.Sin(rad) * tipDistance;
        float armA = RayArmSdf(u, v, tipX, tipY, angleDegrees + 150f, 0.40f, 0.14f);
        float armB = RayArmSdf(u, v, tipX, tipY, angleDegrees - 150f, 0.40f, 0.14f);
        return Mathf.Min(armA, armB);
    }

    private static float RayArmSdf(float u, float v, float originX, float originY, float angleDegrees, float length, float thickness)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(-rad), sin = Mathf.Sin(-rad);
        float du = u - originX, dv = v - originY;
        float ru = du * cos - dv * sin;
        float rv = du * sin + dv * cos;
        return BoxSdf(ru - length * 0.5f, rv, length * 0.5f, thickness * 0.5f);
    }

    // Padlock silhouette: a shackle ring (open at the bottom, where it meets
    // the body) unioned with a rounded body box. No keyhole detail - this
    // only ever renders at small corner-badge scale where it wouldn't read.
    private static float LockSdf(float u, float v)
    {
        const float shackleCy = 0.16f, shackleR = 0.30f, shackleThickness = 0.11f;
        float r = Mathf.Sqrt(u * u + (v - shackleCy) * (v - shackleCy));
        float theta = Mathf.Atan2(v - shackleCy, u) * Mathf.Rad2Deg;
        if (theta < 0f) theta += 360f;

        float shackle = Mathf.Abs(r - shackleR) - shackleThickness;
        float distToBottom = Mathf.Abs(Mathf.DeltaAngle(theta, 270f));
        const float gapHalf = 40f;
        if (distToBottom < gapHalf)
            shackle += (gapHalf - distToBottom) * 0.02f;

        float body = BoxSdf(u, v + 0.30f, 0.38f, 0.32f);
        return Mathf.Min(shackle, body);
    }

    // Left-pointing chevron ("<"): two bars meeting at the tip on the left,
    // same diagonal-bar technique as the shuffle icon's crossed bars.
    private static float BackChevronSdf(float u, float v)
    {
        float upper = ChevronArmSdf(u, v, tipY: 0f, sign: 1f);
        float lower = ChevronArmSdf(u, v, tipY: 0f, sign: -1f);
        return Mathf.Min(upper, lower);
    }

    private static float ChevronArmSdf(float u, float v, float tipY, float sign)
    {
        const float tipX = -0.42f, armLength = 0.62f, thickness = 0.15f;
        float angle = sign * Mathf.PI / 4f; // 45 degrees, opening to the right
        float cos = Mathf.Cos(-angle), sin = Mathf.Sin(-angle);
        float ru = (u - tipX) * cos - (v - tipY) * sin;
        float rv = (u - tipX) * sin + (v - tipY) * cos;
        return BoxSdf(ru - armLength * 0.5f, rv, armLength * 0.5f, thickness * 0.5f);
    }

    // Hamburger menu: three evenly-spaced horizontal bars.
    private static float MenuSdf(float u, float v)
    {
        const float halfW = 0.62f, halfH = 0.09f, spacing = 0.36f;
        float top = BoxSdf(u, v - spacing, halfW, halfH);
        float mid = BoxSdf(u, v, halfW, halfH);
        float bottom = BoxSdf(u, v + spacing, halfW, halfH);
        return Mathf.Min(Mathf.Min(top, mid), bottom);
    }
}
