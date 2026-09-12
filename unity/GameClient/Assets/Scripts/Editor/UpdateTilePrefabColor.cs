using UnityEngine;
using UnityEditor;

public static class UpdateTilePrefabColor {
    public static void Update() {
        var prefabPath = "Assets/Prefabs/Tile3D.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;
        
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var view = instance.GetComponent<GameClient.Presentation.Board3D.TileView3D>();
        
        if (view != null) {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_blockedCardColor").colorValue = new Color(0.95f, 0.95f, 0.95f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        
        Debug.Log("Tile3D prefab color updated!");
    }
}
