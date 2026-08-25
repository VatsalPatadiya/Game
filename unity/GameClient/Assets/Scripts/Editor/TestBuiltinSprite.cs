using UnityEditor;
using UnityEngine;

public static class TestBuiltinSprite
{
    public static void Test()
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Debug.Log("SPRITE_TEST: " + (sprite != null ? sprite.name : "NULL"));
    }
}
