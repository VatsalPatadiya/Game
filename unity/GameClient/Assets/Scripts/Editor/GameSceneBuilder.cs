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
        camera.orthographicSize = 5f; // Fit 6x4 pyramid
        camera.transform.position = new Vector3(2.5f, -1.5f, -10f); // Center on 6x4 grid
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
        SetField(gameController, "_boardView", boardView);

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

        CreateHudButton(canvasGO.transform, "ShuffleButton", new Vector2(-120f, 60f), gameController,
            typeof(ShuffleButton), "_button", "🔀");
        CreateHudButton(canvasGO.transform, "HintButton", new Vector2(0f, 60f), gameController,
            typeof(HintButton), "_button", "💡");
        CreateHudButton(canvasGO.transform, "UndoButton", new Vector2(120f, 60f), gameController,
            typeof(UndoButton), "_button", "↩");

        // ------------------
        // Build Tray UI
        // ------------------
        var trayGO = new GameObject("TrayPanel", typeof(Image), typeof(HorizontalLayoutGroup), typeof(TrayView));
        trayGO.transform.SetParent(canvasGO.transform, false);
        var trayImage = trayGO.GetComponent<Image>();
        trayImage.color = new Color(0, 0, 0, 0.5f);
        var trayRect = trayGO.GetComponent<RectTransform>();
        trayRect.anchorMin = new Vector2(0.5f, 1f);
        trayRect.anchorMax = new Vector2(0.5f, 1f);
        trayRect.pivot = new Vector2(0.5f, 1f);
        trayRect.anchoredPosition = new Vector2(0, -80f);
        trayRect.sizeDelta = new Vector2(400f, 100f);

        var trayLayout = trayGO.GetComponent<HorizontalLayoutGroup>();
        trayLayout.childAlignment = TextAnchor.MiddleCenter;
        trayLayout.childControlWidth = false;
        trayLayout.childControlHeight = false;
        trayLayout.spacing = 10f;

        var trayView = trayGO.GetComponent<TrayView>();
        var traySlotPrefab = new GameObject("TraySlot", typeof(RectTransform));
        var slotRect = traySlotPrefab.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(80f, 80f);

        var shadowGO = new GameObject("Shadow", typeof(Image));
        shadowGO.transform.SetParent(traySlotPrefab.transform, false);
        var shadowImage = shadowGO.GetComponent<Image>();
        shadowImage.color = new Color(0, 0, 0, 0.4f);
        var shadowRect = shadowGO.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.anchoredPosition = new Vector2(5f, -5f);

        var bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(traySlotPrefab.transform, false);
        var bgImage = bgGO.GetComponent<Image>();
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var iconGO = new GameObject("Icon", typeof(Image));
        iconGO.transform.SetParent(traySlotPrefab.transform, false);
        var iconImage = iconGO.GetComponent<Image>();
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        var slotTextGO = new GameObject("Text", typeof(Text));
        slotTextGO.transform.SetParent(traySlotPrefab.transform, false);
        var slotText = slotTextGO.GetComponent<Text>();
        slotText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        slotText.fontSize = 40;
        slotText.color = Color.black;
        slotText.alignment = TextAnchor.MiddleCenter;
        var slotTextRect = slotTextGO.GetComponent<RectTransform>();
        slotTextRect.anchorMin = Vector2.zero;
        slotTextRect.anchorMax = Vector2.one;
        slotTextRect.offsetMin = Vector2.zero;
        slotTextRect.offsetMax = Vector2.zero;
        
        SetField(trayView, "layoutGroup", trayLayout);
        SetField(trayView, "traySlotPrefab", traySlotPrefab);
        SetField(gameController, "_trayView", trayView);

        // ------------------
        // Build Game Over UI
        // ------------------
        var gameOverGO = new GameObject("GameOverPanel", typeof(Image), typeof(GameOverPopup));
        gameOverGO.transform.SetParent(canvasGO.transform, false);
        var goImage = gameOverGO.GetComponent<Image>();
        goImage.color = new Color(0, 0, 0, 0.8f);
        var goRect = gameOverGO.GetComponent<RectTransform>();
        goRect.anchorMin = Vector2.zero;
        goRect.anchorMax = Vector2.one;
        goRect.offsetMin = Vector2.zero;
        goRect.offsetMax = Vector2.zero;

        var goTextGO = new GameObject("Text", typeof(Text));
        goTextGO.transform.SetParent(gameOverGO.transform, false);
        var goText = goTextGO.GetComponent<Text>();
        goText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goText.fontSize = 60;
        goText.text = "GAME OVER";
        goText.alignment = TextAnchor.MiddleCenter;
        var goTextRect = goTextGO.GetComponent<RectTransform>();
        goTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        goTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        goTextRect.anchoredPosition = new Vector2(0, 100f);
        goTextRect.sizeDelta = new Vector2(400, 100);

        var restartBtnGO = new GameObject("RestartButton", typeof(Image), typeof(Button));
        restartBtnGO.transform.SetParent(gameOverGO.transform, false);
        var restartBtnRect = restartBtnGO.GetComponent<RectTransform>();
        restartBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchoredPosition = new Vector2(0, -50f);
        restartBtnRect.sizeDelta = new Vector2(200, 60);
        
        var restartTextGO = new GameObject("Text", typeof(Text));
        restartTextGO.transform.SetParent(restartBtnGO.transform, false);
        var restartText = restartTextGO.GetComponent<Text>();
        restartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartText.fontSize = 30;
        restartText.color = Color.black;
        restartText.text = "Restart";
        restartText.alignment = TextAnchor.MiddleCenter;
        var restartTextRect = restartTextGO.GetComponent<RectTransform>();
        restartTextRect.anchorMin = Vector2.zero;
        restartTextRect.anchorMax = Vector2.one;
        restartTextRect.offsetMin = Vector2.zero;
        restartTextRect.offsetMax = Vector2.zero;

        var gameOverPopup = gameOverGO.GetComponent<GameOverPopup>();
        SetField(gameOverPopup, "restartButton", restartBtnGO.GetComponent<Button>());
        SetField(gameController, "_gameOverPopup", gameOverPopup);
        gameOverGO.SetActive(false); // Hidden by default

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
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(80f, 80f);

        var labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(buttonGO.transform, false);
        var text = labelGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 40;
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
