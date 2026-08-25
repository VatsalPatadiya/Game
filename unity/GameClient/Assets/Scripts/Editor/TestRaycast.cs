using UnityEditor;
using UnityEngine;
using GameClient.Presentation.Board;

public static class TestRaycast
{
    public static void Test()
    {
        var go = new GameObject("Tile");
        go.transform.position = Vector3.zero;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        
        var hits = Physics2D.OverlapPointAll(Vector2.zero);
        Debug.Log("RAYCAST_TEST_HITS: " + hits.Length);
    }
}
