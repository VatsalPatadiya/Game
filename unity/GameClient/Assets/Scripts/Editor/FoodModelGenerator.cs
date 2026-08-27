using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Builds the food-icon prefab set from the Kenney food-kit OBJ models
// (Assets/Models/Food/*.obj, all sharing one texture atlas). Each tile value
// (mod 26, per TileVisual's existing convention) gets its own distinct food
// model instead of a flat icon+accent-color combo.
public static class FoodModelGenerator
{
    public static readonly string[] FoodNames =
    {
        "apple", "banana", "orange", "strawberry", "watermelon", "pineapple",
        "grapes", "cherries", "lemon", "pear", "avocado", "tomato", "carrot",
        "corn", "broccoli", "mushroom", "egg", "cheese", "croissant", "donut",
        "cookie", "cupcake", "muffin", "pizza", "burger", "hot-dog",
    };

    private const float TargetSize = 0.41f; // world-unit bounding-sphere diameter each model is normalized to, sized to sit clearly within a tile face

    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Materials");
        var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Food/Textures/colormap.png");
        if (atlasTexture == null) throw new System.Exception("FOOD_MODEL_GENERATOR_MISSING: colormap.png");

        var foodMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        foodMaterial.SetTexture("_BaseMap", atlasTexture);
        foodMaterial.SetFloat("_Smoothness", 0.25f);
        URPMaterialUtil.SetTransparent(foodMaterial); // TileView3D/TraySlotView3D fade food-model alpha in/out during deal-in, tap-away, and clear animations
        AssetDatabase.CreateAsset(foodMaterial, "Assets/Materials/FoodAtlas.mat");

        Directory.CreateDirectory("Assets/Prefabs/Food");
        foreach (var name in FoodNames)
        {
            var modelPath = "Assets/Models/Food/" + name + ".obj";
            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (sourceModel == null) throw new System.Exception("FOOD_MODEL_GENERATOR_MISSING_MODEL: " + modelPath);

            var instance = Object.Instantiate(sourceModel);
            instance.name = name;

            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
                renderer.sharedMaterial = foodMaterial;

            var bounds = ComputeBounds(instance);
            float diameter = bounds.size.magnitude;
            float scale = diameter > 0.0001f ? TargetSize / diameter : 1f;

            var root = new GameObject(name);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = -bounds.center * scale;
            root.transform.localScale = Vector3.one * scale;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Food/" + name + ".prefab");
            Object.DestroyImmediate(root);
            if (prefab == null) throw new System.Exception("FOOD_MODEL_GENERATOR_SAVE_FAILED: " + name);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("FOOD_MODEL_GENERATOR_DONE");
    }

    private static Bounds ComputeBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1))
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
