using UnityEngine;
using static ProceduralSpriteGenerator;

public static class HudIconGenerator
{
    private const string Directory = "Assets/Textures/HudIcons";
    private const int Size = 256; // higher res so the SDF glyphs stay crisp at on-screen size (round-2 fix 4)

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
    // Magnifying glass for Hint: A ring with a diagonal handle.
    private static float LightbulbSdf(float u, float v)
    {
        // Round head (solid)
        float head = Mathf.Sqrt(u * u + (v - 0.15f) * (v - 0.15f)) - 0.35f;

        // Narrower base box, generously overlapping the head
        float baseBox = BoxSdf(u, v + 0.20f, 0.18f, 0.20f);

        return Mathf.Min(head, baseBox);
    }

    // U-Turn backward arrow for Undo.
    private static float UndoSdf(float u, float v)
    {
        return BackChevronSdf(u, v);
    }

    // Two horizontal opposite arrows for Shuffle
    private static float ShuffleSdf(float u, float v)
    {
        float thickness = 0.1f;
        // Top arrow pointing right
        float topBar = BoxSdf(u + 0.05f, v - 0.25f, 0.35f, thickness);
        float topHead = TriangleSdf(u, v, 0.5f, 0.25f, 0f, 0.25f, 0.2f);
        
        // Bottom arrow pointing left
        float botBar = BoxSdf(u - 0.05f, v + 0.25f, 0.35f, thickness);
        float botHead = TriangleSdf(u, v, -0.5f, -0.25f, 180f, 0.25f, 0.2f);

        return Mathf.Min(Mathf.Min(topBar, topHead), Mathf.Min(botBar, botHead));
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
