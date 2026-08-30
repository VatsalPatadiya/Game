using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMeshGenerator
{
    private const float CardThickness = 0.18f; // real Z depth, replaces the 2D drop-shadow trick

    public static void Generate()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("Tile3D");

        float w = CardStyle.CardSizeRatio * CardStyle.CardAspectRatio;
        float h = CardStyle.CardSizeRatio;
        float cornerRadius = w * 0.16f;

        var body = new GameObject("CardBody", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root.transform, false);

        var tileMesh = RoundedTileMesh.Build(w, h, CardThickness, cornerRadius, cornerSegments: 6);
        // Persist the generated mesh as a standalone asset BEFORE saving the
        // prefab, so the saved prefab's MeshFilter references a real asset on
        // disk rather than an unsaved runtime Mesh (AddObjectToAsset after
        // SaveAsPrefabAsset does not reliably re-link the already-serialized
        // prefab reference, leaving the tile invisible).
        System.IO.Directory.CreateDirectory("Assets/Meshes");
        const string meshPath = "Assets/Meshes/RoundedTile.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh != null)
            AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(tileMesh, meshPath);
        AssetDatabase.SaveAssets();
        var persistedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        body.GetComponent<MeshFilter>().sharedMesh = persistedMesh;

        var bodyRenderer = body.GetComponent<MeshRenderer>();
        var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileBody.mat");
        if (cardMaterial == null)
            throw new System.Exception("TILE_MESH_GENERATOR_MISSING_MATERIAL: run TileMaterialGenerator first");
        bodyRenderer.sharedMaterial = cardMaterial;
        bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        bodyRenderer.receiveShadows = true;

        var foodAnchorGO = new GameObject("FoodAnchor");
        foodAnchorGO.transform.SetParent(root.transform, false);
        foodAnchorGO.transform.localPosition = new Vector3(0f, 0f, -(CardThickness / 2f + 0.02f));
        foodAnchorGO.transform.localScale = Vector3.one * 2.0f; // bigger symbol: the clean (frameless) face leaves more room, matching the reference's large symbols

        // Soft drop shadow: a quad behind the tile body (toward the felt, +Z),
        // nudged down-right so it reads under a top-left key light. Extends past
        // the tile silhouette so its edges peek out as a contact shadow.
        var shadowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileShadow.mat");
        if (shadowMat == null)
            throw new System.Exception("TILE_MESH_GENERATOR_MISSING_SHADOW: run TileMaterialGenerator first");
        var shadowGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadowGO.name = "DropShadow";
        Object.DestroyImmediate(shadowGO.GetComponent<Collider>());
        shadowGO.transform.SetParent(root.transform, false);
        shadowGO.transform.localPosition = new Vector3(0.05f, -0.07f, CardThickness / 2f + 0.03f);
        shadowGO.transform.localScale = new Vector3(w * 1.24f, h * 1.2f, 1f);
        var shadowRenderer = shadowGO.GetComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = shadowMat;
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;

        var tileView = root.AddComponent<TileView3D>(); // auto-adds a BoxCollider to root via [RequireComponent]
        var collider = root.GetComponent<BoxCollider>();
        collider.size = new Vector3(
            CardStyle.CardSizeRatio * CardStyle.CardAspectRatio,
            CardStyle.CardSizeRatio,
            CardThickness);
        collider.center = Vector3.zero;

        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;
        serialized.FindProperty("_bodyCollider").objectReferenceValue = collider;
        serialized.FindProperty("_foodAnchor").objectReferenceValue = foodAnchorGO.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile3D.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_MESH_GENERATOR_DONE");
    }
}
