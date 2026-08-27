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
        TileMaterialGenerator.Generate();
        TileMeshGenerator.Generate();
        FoodModelGenerator.Generate();
        DataAssetGenerator.Generate();
        FeltBackgroundGenerator.Generate();
        GameSceneBuilder3D.Build();
        Debug.Log("REGENERATE_ALL_DONE");
    }
}
