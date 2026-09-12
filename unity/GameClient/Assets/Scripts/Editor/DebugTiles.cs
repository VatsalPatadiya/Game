using UnityEngine;
using UnityEditor;

public static class DebugTiles
{
    public static void Run()
    {
        string path = "Assets/Sprites/Tiles/Tile_1.png";
        AssetDatabase.ImportAsset(path);
        
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            Debug.Log("Importer found, type: " + importer.textureType);
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        else
        {
            Debug.LogError("No importer found for " + path);
        }

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        Debug.Log("Assets found at " + path + ": " + allAssets.Length);
        foreach (var asset in allAssets)
        {
            Debug.Log(" - " + (asset != null ? asset.GetType().Name : "null") + " : " + (asset != null ? asset.name : "null"));
        }
    }
}
