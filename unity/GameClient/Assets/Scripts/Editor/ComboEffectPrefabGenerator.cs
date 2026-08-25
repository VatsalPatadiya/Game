using System.IO;
using GameClient.Presentation.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ComboEffectPrefabGenerator
{
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("ComboEffectFX", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 40f);

        var text = root.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.yellow;
        text.text = "+0";

        var comboEffect = root.AddComponent<ComboEffect>();
        var serialized = new SerializedObject(comboEffect);
        serialized.FindProperty("_label").objectReferenceValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/ComboEffectFX.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("COMBO_EFFECT_PREFAB_GENERATOR_DONE");
    }
}
