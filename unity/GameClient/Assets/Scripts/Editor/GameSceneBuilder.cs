using System.IO;
using GameClient.Data;
using GameClient.Presentation;
using GameClient.Presentation.Board;
using GameClient.Presentation.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameSceneBuilder
{
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4f; // Fit 4x2 grid in portrait
        camera.transform.position = new Vector3(1.5f, -0.5f, -10f); // Center on 4x2 grid
        cameraGO.tag = "MainCamera";

        // Add EventSystem for UI clicks
        var eventSystemGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var boardGO = new GameObject("Board", typeof(BoardView));
        var boardView = boardGO.GetComponent<BoardView>();
        var tilePrefab = AssetDatabase.LoadAssetAtPath<TileView>("Assets/Prefabs/Tile.prefab");
        RequireNotNull(tilePrefab, "Assets/Prefabs/Tile.prefab as TileView");
        SetField(boardView, "_tilePrefab", tilePrefab);

        var inputGO = new GameObject("TileInputController", typeof(TileInputController));
        var inputController = inputGO.GetComponent<TileInputController>();

        var gameControllerGO = new GameObject("GameController", typeof(GameController));
        var gameController = gameControllerGO.GetComponent<GameController>();
        var levelAsset = AssetDatabase.LoadAssetAtPath<LevelShapeAsset>("Assets/Data/SmallTestLevel.asset");
        RequireNotNull(levelAsset, "Assets/Data/SmallTestLevel.asset");
        SetField(gameController, "_boardView", boardView);
        SetField(gameController, "_levelShape", levelAsset);

        SetField(inputController, "_targetCamera", camera);
        SetField(inputController, "_gameController", gameController);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scoreGO = new GameObject("ScoreText", typeof(Text), typeof(ScoreDisplay));
        scoreGO.transform.SetParent(canvasGO.transform, false);
        var scoreText = scoreGO.GetComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 32;
        scoreText.text = "Score: 0";
        var scoreRect = scoreGO.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0f, 1f);
        scoreRect.anchorMax = new Vector2(0f, 1f);
        scoreRect.pivot = new Vector2(0f, 1f);
        scoreRect.anchoredPosition = new Vector2(20f, -20f);
        scoreRect.sizeDelta = new Vector2(300f, 50f);
        var scoreDisplay = scoreGO.GetComponent<ScoreDisplay>();
        SetField(scoreDisplay, "_scoreText", scoreText);
        SetField(scoreDisplay, "_gameController", gameController);

        CreateHudButton(canvasGO.transform, "HintButton", new Vector2(20f, 20f), gameController,
            typeof(HintButton), "_button", "Hint");
        CreateHudButton(canvasGO.transform, "UndoButton", new Vector2(140f, 20f), gameController,
            typeof(UndoButton), "_button", "Undo");
        CreateHudButton(canvasGO.transform, "ShuffleButton", new Vector2(260f, 20f), gameController,
            typeof(ShuffleButton), "_button", "Shuffle");

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");

        Debug.Log("GAME_SCENE_BUILDER_DONE");
    }

    private static void CreateHudButton(
        Transform parent, string name, Vector2 anchoredPosition, GameController gameController,
        System.Type hudComponentType, string buttonFieldName, string label)
    {
        var buttonGO = new GameObject(name, typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(100f, 88f);

        var labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(buttonGO.transform, false);
        var text = labelGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.text = label;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        var button = buttonGO.GetComponent<Button>();
        SetField(hudComponent, buttonFieldName, button);
        SetField(hudComponent, "_gameController", gameController);
    }

    private static void SetField(Object target, string fieldName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireNotNull(Object value, string description)
    {
        if (value == null)
            throw new System.Exception("GAME_SCENE_BUILDER_MISSING: " + description);
    }

    private static void RequireNotNull(SerializedProperty value, string description)
    {
        if (value == null)
            throw new System.Exception("GAME_SCENE_BUILDER_MISSING_PROPERTY: " + description);
    }
}
