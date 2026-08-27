using UnityEditor;
using UnityEngine;

// Generates a normal map from CardSpriteGenerator's exact rounded-rect SDF
// gradient, so a plain cube mesh reads as a carved/beveled tile under real
// URP lighting - no hand-authored bevel geometry, no external texture.
public static class CardNormalMapGenerator
{
    private const string Directory = "Assets/Textures/Card";
    private const string TextureName = "card_rounded_rect_normal";
    private const int Size = 256;
    // How far the fake bevel reaches inward from the edge, in SDF units
    // (SDF is in [-1,1] space, so 0.08 is a modest, tile-proportional bevel).
    private const float BevelWidth = 0.08f;
    private const float BevelStrength = 1.5f;

    public static void Generate()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

        float texel = 1f / Size * 2f;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size * 2f - 1f;
                float v = (y + 0.5f) / Size * 2f - 1f;

                float dist = CardSpriteGenerator.RoundedRectSdf(u, v);
                float dxPlus = CardSpriteGenerator.RoundedRectSdf(u + texel, v);
                float dxMinus = CardSpriteGenerator.RoundedRectSdf(u - texel, v);
                float dyPlus = CardSpriteGenerator.RoundedRectSdf(u, v + texel);
                float dyMinus = CardSpriteGenerator.RoundedRectSdf(u, v - texel);
                float gradX = (dxPlus - dxMinus) / (2f * texel);
                float gradY = (dyPlus - dyMinus) / (2f * texel);

                // Only bevel a thin band just inside the edge (dist in
                // [-BevelWidth, 0]); flat everywhere else (interior face,
                // and outside the shape where alpha will be 0 anyway).
                float bevelT = Mathf.Clamp01((dist + BevelWidth) / BevelWidth);
                float slope = (1f - bevelT) * BevelStrength;

                var normal = new Vector3(-gradX * slope, -gradY * slope, 1f).normalized;
                var encoded = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f);
                texture.SetPixel(x, y, encoded);
            }
        }
        texture.Apply();

        string path = Directory + "/" + TextureName + ".png";
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.NormalMap;
        importer.SaveAndReimport();

        AssetDatabase.SaveAssets();
        Debug.Log("CARD_NORMAL_MAP_GENERATOR_DONE");
    }
}
