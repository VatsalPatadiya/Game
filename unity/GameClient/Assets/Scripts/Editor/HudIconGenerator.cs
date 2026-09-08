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

    // Round head + a single narrower base box, GENEROUSLY overlapping the
    // head (not just touching) so it reads as one continuous silhouette.
    // Two earlier attempts both failed for the same reason - a disconnected
    // second piece: v1's two separate thin bars had a sign bug (see below)
    // that placed them ABOVE the head entirely; even with the sign fixed,
    // two thin bars floating near the head still risked reading as "two
    // icons stacked", which is exactly the ambiguity a single fused base
    // avoids. NOTE the sign convention here: Unity's Texture2D.SetPixel is
    // bottom-left-origin, so more POSITIVE v renders toward the TOP of the
    // final sprite and more NEGATIVE v toward the BOTTOM - the opposite of
    // "row 0 = top" image conventions. v1's `v - 0.42` centred the bars at
    // v=+0.42 (top); this uses `v + 0.20` to centre the base at v=-0.20
    // (bottom, below the head).
    private static float LightbulbSdf(float u, float v)
    {
        float head = CircleSdf(u, v, 0f, 0.12f, 0.38f);
        float baseBox = BoxSdf(u, v + 0.20f, 0.16f, 0.28f);
        return Mathf.Min(head, baseBox);
    }

    // Ring with a solid triangular arrowhead fused at the gap - not a plain
    // dot (read as "C" with a period) and not a chevron-outline tip either
    // (two thin V-arms whose hollow interior made it read as a second,
    // disconnected mark floating near the ring, confirmed on an actual
    // device screenshot: the tip sat at the ring's own radius, tangent to
    // the stroke rather than fused into it). A SOLID filled triangle
    // (TriangleSdf, below) whose tip points back toward the ring's centre
    // reads immediately as a classic circular "undo/refresh" arrow.
    private static float UndoSdf(float u, float v)
    {
        const float radius = 0.48f, thickness = 0.14f;
        const float gapStart = 55f, gapEnd = 300f;

        float r = Mathf.Sqrt(u * u + v * v);
        float theta = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
        if (theta < 0f) theta += 360f;

        float ring = Mathf.Abs(r - radius) - thickness;
        float arc = ring + AngleGapPenalty(theta, gapStart, gapEnd);

        float tipAngleRad = gapStart * Mathf.Deg2Rad;
        float tipX = Mathf.Cos(tipAngleRad) * radius;
        float tipY = Mathf.Sin(tipAngleRad) * radius;
        // Points back along the radius toward the centre (gapStart + 180),
        // not outward - an outward-pointing tip left a visible seam where
        // the wide triangle base met the curved ring; inward-pointing lets
        // the base sit flush against the ring's own stroke.
        float triangle = TriangleSdf(u, v, tipX, tipY, gapStart + 180f, 0.30f, 0.24f);

        return Mathf.Min(arc, triangle);
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
    // ending in a solid triangular arrowhead at ONE of its two ends (upper-
    // right and lower-right) - that asymmetry is what reads as directional
    // arrows instead of a symmetric X/prohibition glyph. Previously used a
    // hollow 2-arm "chevron" for the arrowhead (same technique as the old
    // UndoSdf) which, unioned with a bar at 45 degrees, created extra
    // notches where the arms crossed the bar at odd angles - confirmed on
    // an actual device screenshot as a jagged, illegible blob. A single
    // SOLID triangle (TriangleSdf, below) has no interior seams to catch
    // the low sprite resolution's antialiasing, so it stays clean.
    private static float ShuffleSdf(float u, float v)
    {
        const float halfLength = 0.42f;
        const float thickness = 0.075f;
        float bar1 = DiagonalBarSdf(u, v, 1f, halfLength, thickness);
        float bar2 = DiagonalBarSdf(u, v, -1f, halfLength, thickness);

        float tip1X = Mathf.Cos(Mathf.PI / 4f) * halfLength, tip1Y = Mathf.Sin(Mathf.PI / 4f) * halfLength;
        float tip2X = Mathf.Cos(-Mathf.PI / 4f) * halfLength, tip2Y = Mathf.Sin(-Mathf.PI / 4f) * halfLength;
        float head1 = TriangleSdf(u, v, tip1X, tip1Y, 45f, 0.20f, 0.16f);
        float head2 = TriangleSdf(u, v, tip2X, tip2Y, -45f, 0.20f, 0.16f);

        return Mathf.Min(Mathf.Min(bar1, bar2), Mathf.Min(head1, head2));
    }

    private static float DiagonalBarSdf(float u, float v, float sign, float halfLength, float thickness)
    {
        float angle = sign * Mathf.PI / 4f;
        float cos = Mathf.Cos(-angle), sin = Mathf.Sin(-angle);
        float ru = u * cos - v * sin;
        float rv = u * sin + v * cos;
        return BoxSdf(ru, rv, halfLength, thickness);
    }

    // Solid filled triangle: tip at (tipX,tipY) pointing along angleDegrees,
    // base (width 2*halfBaseWidth) sitting `length` back from the tip.
    // Built as the intersection (max) of 3 signed half-plane distances, one
    // per edge - unlike a 2-arm "chevron" (two separate boxes forming a V),
    // this has a genuinely solid interior with no seam between pieces.
    private static float TriangleSdf(float u, float v, float tipX, float tipY, float angleDegrees, float length, float halfBaseWidth)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(-rad), sin = Mathf.Sin(-rad);
        float du = u - tipX, dv = v - tipY;
        float lx = du * cos - dv * sin;
        float ly = du * sin + dv * cos;

        float e1 = EdgeDistance(0f, 0f, -length, halfBaseWidth, lx, ly);
        float e2 = EdgeDistance(-length, halfBaseWidth, -length, -halfBaseWidth, lx, ly);
        float e3 = EdgeDistance(-length, -halfBaseWidth, 0f, 0f, lx, ly);
        return -Mathf.Min(Mathf.Min(e1, e2), e3);
    }

    // Signed distance from point (x,y) to the infinite line through
    // (p1x,p1y)->(p2x,p2y), via the 2D cross product - used by TriangleSdf
    // to build each of the triangle's 3 edges.
    private static float EdgeDistance(float p1x, float p1y, float p2x, float p2y, float x, float y)
    {
        float ex = p2x - p1x, ey = p2y - p1y;
        float px = x - p1x, py = y - p1y;
        float cross = ex * py - ey * px;
        return cross / Mathf.Sqrt(ex * ex + ey * ey);
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
