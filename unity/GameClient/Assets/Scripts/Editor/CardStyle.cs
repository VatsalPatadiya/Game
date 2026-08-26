using UnityEngine;

// Single source of truth for what a "tile card" looks like, shared by the
// world-space Tile prefab (TilePrefabGenerator, SpriteRenderer-based) and the
// UI tray slot prefab (GameSceneBuilder, Image-based) so the two can never
// visually drift apart. Ratios are fractions of the tile's full footprint
// (world units for the board tile, normalized 0-1 anchors for the tray slot).
public static class CardStyle
{
    // Accent border width = (AccentSizeRatio - CardSizeRatio) / 2 = 4% of tile
    // width, matching the thin uniform border seen in reference footage
    // (docs/superpowers/specs/2026-08-26-pyramid-shape-and-tray-correction.md)
    // more closely than the earlier 8%. Card area share of (accent+card)
    // combined area is ~82% — the dominant, clearly-majority color.
    public const float ShadowSizeRatio = 0.82f;
    public const float GlowSizeRatio = 1.0f;
    public const float AccentSizeRatio = 0.84f;
    public const float CardSizeRatio = 0.76f;
    public const float IconSizeRatio = 0.5f;

    // Width:height of the board tile card - a portrait rectangle like the
    // classic mahjong tile silhouette (and the reference screenshot),
    // instead of the square card used through round 5. Every *SizeRatio
    // above still scales the tile's HEIGHT axis; width is that value times
    // this ratio (see TilePrefabGenerator).
    public const float CardAspectRatio = 0.8f;

    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.3f);
    public static readonly Color GlowColor = new Color(1f, 0.85f, 0.2f, 0f);
    public static readonly Color AccentDefaultColor = new Color(0.69f, 0.26f, 0.16f, 1f);
    public static readonly Color CardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
    public static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.12f);

    // Procedural rounded-rect card sprite (CardSpriteGenerator): source texture
    // is 256px with a border of 51.2px, giving a corner radius equal to 20% of
    // a full 1.0-ratio footprint at PPU=256 (world-space rendering). UI Image
    // layers don't share that PPU system, so they scale this same source
    // border via Image.pixelsPerUnitMultiplier, computed per element as
    // CardSpriteSourceBorderPx / (containerSizePx * CornerRadiusRatio).
    public const int CardSpriteTextureSize = 256;
    public const float CardSpriteCornerRadiusNormalized = 0.4f; // in [-1,1] SDF space, box half-width=1
    public const float CardSpriteSourceBorderPx = 51.2f;
    public const float CornerRadiusRatio = 0.2f; // target corner as % of an element's own on-screen width
}
