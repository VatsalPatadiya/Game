using UnityEngine;
using UnityEditor;

public static class DebugTile3D {
    public static void Dump() {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile3D.prefab");
        Debug.Log("Prefab: " + prefab.name);
        foreach (var c in prefab.GetComponentsInChildren<Component>(true)) {
            if (c == null) continue;
            Debug.Log(" - " + c.GetType().Name + " on " + c.gameObject.name);
            if (c is MeshRenderer mr && mr.sharedMaterial != null) {
                Debug.Log("   -> Material: " + mr.sharedMaterial.name);
            }
            if (c is SpriteRenderer sr && sr.sprite != null) {
                Debug.Log("   -> Sprite: " + sr.sprite.name);
            }
        }
    }
}
