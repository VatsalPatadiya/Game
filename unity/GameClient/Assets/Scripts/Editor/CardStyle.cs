using UnityEngine;

// Single source of truth for what a "tile card" looks like, shared by the
// world-space Tile prefab (TilePrefabGenerator, SpriteRenderer-based) and the
// UI tray slot prefab (GameSceneBuilder, Image-based) so the two can never
// visually drift apart. Ratios are fractions of the tile's full footprint
// (world units for the board tile, normalized 0-1 anchors for the tray slot).
public static class CardStyle
{
    public const float ShadowSizeRatio = 0.82f;
    public const float GlowSizeRatio = 1.0f;
    public const float AccentSizeRatio = 0.86f;
    public const float CardSizeRatio = 0.78f;
    public const float IconSizeRatio = 0.5f;

    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.3f);
    public static readonly Color GlowColor = new Color(1f, 0.85f, 0.2f, 0f);
    public static readonly Color AccentDefaultColor = new Color(0.69f, 0.26f, 0.16f, 1f);
    public static readonly Color CardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
    public static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.12f);
}
