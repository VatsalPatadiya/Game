using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ProceduralSpriteGenerator
{
    public static void Generate(
        string directory, int size, float aaWidthPixels, (string Name, Func<float, float, float> Sdf)[] shapes)
    {
        Directory.CreateDirectory(directory);
        float aaWidth = aaWidthPixels / size * 2f;

        foreach (var shape in shapes)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float dist = shape.Sdf(u, v);
                    float alpha = Mathf.Clamp01(0.5f - dist / aaWidth);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            string path = directory + "/" + shape.Name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = size;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
    }

    public static float CircleSdf(float u, float v, float cx, float cy, float r)
    {
        float dx = u - cx, dy = v - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    public static float BoxSdf(float u, float v, float halfW, float halfH)
    {
        return Mathf.Max(Mathf.Abs(u) - halfW, Mathf.Abs(v) - halfH);
    }
}
