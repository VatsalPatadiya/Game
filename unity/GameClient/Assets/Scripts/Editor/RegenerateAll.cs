using UnityEngine;

public static class RegenerateAll
{
    public static void Run()
    {
        TileIconGenerator.Generate();
        HudIconGenerator.Generate();
        DataAssetGenerator.Generate();
        TilePrefabGenerator.Generate();
        GameSceneBuilder.Build();
        Debug.Log("REGENERATE_ALL_DONE");
    }
}
