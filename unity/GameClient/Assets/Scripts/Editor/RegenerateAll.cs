using UnityEngine;

public static class RegenerateAll
{
    public static void Run()
    {
        URPSetup.Configure();
        TileIconGenerator.Generate();
        HudIconGenerator.Generate();
        CardSpriteGenerator.Generate();
        CardNormalMapGenerator.Generate();
        CardMaterialGenerator.Generate();
        TileMeshGenerator.Generate();
        DataAssetGenerator.Generate();
        TilePrefabGenerator.Generate();
        GameSceneBuilder.Build();
        Debug.Log("REGENERATE_ALL_DONE");
    }
}
