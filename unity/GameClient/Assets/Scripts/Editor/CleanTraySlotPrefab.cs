using UnityEngine;
using UnityEditor;

public static class CleanTraySlotPrefab {
    public static void Clean() {
        var prefabPath = "Assets/Prefabs/TraySlot3D.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;
        
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        
        // Find and destroy the old Body mesh
        var body = instance.transform.Find("Content/Body");
        if (body != null) {
            Object.DestroyImmediate(body.gameObject);
        }
        
        // Reset the Content scale to 1.0 (it was 1.6)
        var content = instance.transform.Find("Content");
        if (content != null) {
            content.localScale = Vector3.one;
        }
        
        // Find the TraySlotView3D and clear the _bodyRenderer reference
        var view = instance.GetComponent<GameClient.Presentation.HUD3D.TraySlotView3D>();
        if (view != null) {
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_bodyRenderer").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        
        Debug.Log("TraySlot3D cleaned!");
    }
}
