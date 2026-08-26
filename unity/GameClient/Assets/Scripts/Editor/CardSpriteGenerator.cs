using UnityEditor;
using UnityEngine;
using static ProceduralSpriteGenerator;

// Generates the single rounded-rect sprite every card layer (board tiles,
// tray slots, popup card, restart button) renders through. Previously every
// card layer used Unity's built-in "UI/Skin/UISprite.psd" — an opaque asset
// whose corner radius and border proportions aren't controllable, which is
// why the tile card's corner rounding and accent/card size balance couldn't
// be verified against the spec's exact numeric targets. This sprite's
// corner radius is derived directly from CardStyle's constants instead.
public static class CardSpriteGenerator
{
    private const string Directory = "Assets/Textures/Card";
    private const string SpriteName = "card_rounded_rect";

    public static void Generate()
    {
        ProceduralSpriteGenerator.Generate(Directory, CardStyle.CardSpriteTextureSize, 2f,
            new (string, System.Func<float, float, float>)[] { (SpriteName, RoundedRectSdf) });

        string path = Directory + "/" + SpriteName + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.spriteBorder = new Vector4(
            CardStyle.CardSpriteSourceBorderPx, CardStyle.CardSpriteSourceBorderPx,
            CardStyle.CardSpriteSourceBorderPx, CardStyle.CardSpriteSourceBorderPx);
        importer.SaveAndReimport();

        Debug.Log("CARD_SPRITE_GENERATOR_DONE");
    }

    private static float RoundedRectSdf(float u, float v)
    {
        const float halfSize = 1f;
        const float radius = CardStyle.CardSpriteCornerRadiusNormalized;
        float qx = Mathf.Abs(u) - halfSize + radius;
        float qy = Mathf.Abs(v) - halfSize + radius;
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - radius;
    }
}
