using UnityEditor;
using UnityEngine;
using GameClient.Presentation.HUD3D;

// One-off surgical patch: wires TraySlotView3D's new _landingPuffMaterial field
// on the already-baked TraySlot3D.prefab, without running a full scene
// regenerate (GameSceneBuilder3D.Build() rebuilds the whole board/scene, which
// is out of scope for this asset-only change).
public static class PatchTraySlotLandingPuff
{
    [MenuItem("Tools/Mahjong/Patch TraySlot Landing Puff")]
    public static void Patch()
    {
        const string prefabPath = "Assets/Prefabs/TraySlot3D.prefab";
        var glowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MatchParticleGlow.mat");
        if (glowMaterial == null)
            throw new System.Exception("PATCH_MISSING_MATERIAL: Assets/Materials/MatchParticleGlow.mat");

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var slotView = root.GetComponent<TraySlotView3D>();
        if (slotView == null)
            throw new System.Exception("PATCH_MISSING_COMPONENT: TraySlotView3D on " + prefabPath);

        var serialized = new SerializedObject(slotView);
        var property = serialized.FindProperty("_landingPuffMaterial");
        if (property == null)
            throw new System.Exception("PATCH_MISSING_FIELD: _landingPuffMaterial");
        property.objectReferenceValue = glowMaterial;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("PATCH_TRAYSLOT_LANDING_PUFF_DONE");
    }
}
