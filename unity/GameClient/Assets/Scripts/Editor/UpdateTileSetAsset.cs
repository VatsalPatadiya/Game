using GameClient.Data;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class UpdateTileSetAsset
{
    public static void Execute()
    {
        var tileSet = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
        if (tileSet == null)
        {
            Debug.LogError("Could not find DefaultTileSet.asset");
            return;
        }

        var loadedSprites = new List<Sprite>();
        string[] files = Directory.GetFiles("Assets/Sprites/Tiles", "*.png");
        
        foreach (var path in files)
        {
            AssetDatabase.ImportAsset(path);
            
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
            
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach(var asset in allAssets)
            {
                if (asset is Sprite s)
                {
                    loadedSprites.Add(s);
                    break;
                }
            }
        }

        if (loadedSprites.Count > 0)
        {
            loadedSprites.Sort((a, b) => {
                if (a.name.Length == b.name.Length) return a.name.CompareTo(b.name);
                return a.name.Length.CompareTo(b.name.Length);
            });
            
            tileSet.Icons = loadedSprites.ToArray();
            EditorUtility.SetDirty(tileSet);
            AssetDatabase.SaveAssets();
            Debug.Log("Updated TileSetAsset with " + tileSet.Icons.Length + " sprites from SVGs.");
        }
        else
        {
            Debug.LogError("Found 0 sprites! Files found: " + files.Length);
        }
    }
}
