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
    private static readonly Color BoardGreen = new Color(42f / 255f, 61f / 255f, 48f / 255f, 1f);
    private static readonly Color CardOffWhite = new Color(0.969f, 0.957f, 0.922f, 1f);
    private static readonly Color DarkHudText = new Color(40f / 255f, 46f / 255f, 36f / 255f, 1f);
    private static readonly Color BadgeTerracotta = new Color(0.69f, 0.26f, 0.16f, 1f);

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f; // Fit 6x4 pyramid
        camera.transform.position = new Vector3(2.5f, -1.5f, -10f); // Center on 6x4 grid
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BoardGreen;
        cameraGO.tag = "MainCamera";

        // Add EventSystem for UI clicks
        var eventSystemGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var boardGO = new GameObject("Board", typeof(BoardView));
        var boardView = boardGO.GetComponent<BoardView>();
        var tilePrefab = AssetDatabase.LoadAssetAtPath<TileView>("Assets/Prefabs/Tile.prefab");
        RequireNotNull(tilePrefab, "Assets/Prefabs/Tile.prefab as TileView");
        SetField(boardView, "_tilePrefab", tilePrefab);

        var tileSet = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
        RequireNotNull(tileSet, "Assets/Data/DefaultTileSet.asset as TileSetAsset");
        SetField(boardView, "_tileSet", tileSet);
        SetField(boardView, "_camera", camera);

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

        // ------------------
        // Score pill
        // ------------------
        var scorePillGO = new GameObject("ScorePill", typeof(Image));
        scorePillGO.transform.SetParent(canvasGO.transform, false);
        var scorePillImage = scorePillGO.GetComponent<Image>();
        scorePillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scorePillImage.type = Image.Type.Sliced;
        scorePillImage.color = CardOffWhite;
        var scorePillRect = scorePillGO.GetComponent<RectTransform>();
        scorePillRect.anchorMin = new Vector2(0f, 1f);
        scorePillRect.anchorMax = new Vector2(0f, 1f);
        scorePillRect.pivot = new Vector2(0f, 1f);
        scorePillRect.anchoredPosition = new Vector2(20f, -20f);
        scorePillRect.sizeDelta = new Vector2(220f, 56f);

        var scoreGO = new GameObject("ScoreText", typeof(Text), typeof(ScoreDisplay));
        scoreGO.transform.SetParent(scorePillGO.transform, false);
        var scoreText = scoreGO.GetComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 28;
        scoreText.color = DarkHudText;
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.text = "Score: 0";
        var scoreRect = scoreGO.GetComponent<RectTransform>();
        scoreRect.anchorMin = Vector2.zero;
        scoreRect.anchorMax = Vector2.one;
        scoreRect.offsetMin = Vector2.zero;
        scoreRect.offsetMax = Vector2.zero;
        var scoreDisplay = scoreGO.GetComponent<ScoreDisplay>();
        SetField(scoreDisplay, "_scoreText", scoreText);
        SetField(scoreDisplay, "_gameController", gameController);

        // ------------------
        // Control bar
        // ------------------
        var hintIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_hint.png");
        var undoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_undo.png");
        var shuffleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_shuffle.png");
        RequireNotNull(hintIcon, "Assets/Textures/HudIcons/icon_hint.png as Sprite");
        RequireNotNull(undoIcon, "Assets/Textures/HudIcons/icon_undo.png as Sprite");
        RequireNotNull(shuffleIcon, "Assets/Textures/HudIcons/icon_shuffle.png as Sprite");

        CreateHudButton(canvasGO.transform, "ShuffleButton", new Vector2(-120f, 60f), gameController,
            typeof(ShuffleButton), shuffleIcon);
        CreateHudButton(canvasGO.transform, "HintButton", new Vector2(0f, 60f), gameController,
            typeof(HintButton), hintIcon);
        CreateHudButton(canvasGO.transform, "UndoButton", new Vector2(120f, 60f), gameController,
            typeof(UndoButton), undoIcon);

        // ------------------
        // Build Tray UI
        // ------------------
        var trayGO = new GameObject("TrayPanel", typeof(Image), typeof(HorizontalLayoutGroup), typeof(TrayView));
        trayGO.transform.SetParent(canvasGO.transform, false);
        var trayImage = trayGO.GetComponent<Image>();
        trayImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        trayImage.type = Image.Type.Sliced;
        trayImage.color = new Color(0.11f, 0.16f, 0.13f, 0.55f);
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

        var shadowLayer = CreateCardLayer(traySlotPrefab.transform, "Shadow", CardStyle.ShadowSizeRatio, CardStyle.ShadowColor);
        shadowLayer.rectTransform.anchoredPosition = new Vector2(4f, -4f);

        var glowLayer = CreateCardLayer(traySlotPrefab.transform, "SelectionGlow", CardStyle.GlowSizeRatio, CardStyle.GlowColor);
        var accentLayer = CreateCardLayer(traySlotPrefab.transform, "AccentBorder", CardStyle.AccentSizeRatio, CardStyle.AccentDefaultColor);
        var cardLayer = CreateCardLayer(traySlotPrefab.transform, "Card", CardStyle.CardSizeRatio, CardStyle.CardColor);

        var slotIconGO = new GameObject("Icon", typeof(Image));
        slotIconGO.transform.SetParent(traySlotPrefab.transform, false);
        var slotIconImage = slotIconGO.GetComponent<Image>();
        slotIconImage.type = Image.Type.Simple;
        var iconInset = (1f - CardStyle.IconSizeRatio) / 2f;
        var slotIconRect = slotIconGO.GetComponent<RectTransform>();
        slotIconRect.anchorMin = new Vector2(iconInset, iconInset);
        slotIconRect.anchorMax = new Vector2(1f - iconInset, 1f - iconInset);
        slotIconRect.offsetMin = Vector2.zero;
        slotIconRect.offsetMax = Vector2.zero;

        var traySlotView = traySlotPrefab.AddComponent<TraySlotView>();
        SetField(traySlotView, "_shadowImage", shadowLayer);
        SetField(traySlotView, "_glowImage", glowLayer);
        SetField(traySlotView, "_accentImage", accentLayer);
        SetField(traySlotView, "_cardImage", cardLayer);
        SetField(traySlotView, "_iconImage", slotIconImage);

        SetField(trayView, "layoutGroup", trayLayout);
        SetField(trayView, "traySlotPrefab", traySlotPrefab);
        SetField(trayView, "tileSet", tileSet);
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
        System.Type hudComponentType, Sprite iconSprite)
    {
        const float buttonSize = 84f;
        var knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        var buttonGO = new GameObject(name, typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(buttonSize, buttonSize);

        var faceImage = buttonGO.GetComponent<Image>();
        faceImage.sprite = knobSprite;
        faceImage.type = Image.Type.Simple;
        faceImage.color = CardOffWhite;

        var iconGO = new GameObject("Icon", typeof(Image));
        iconGO.transform.SetParent(buttonGO.transform, false);
        var iconImage = iconGO.GetComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.type = Image.Type.Simple;
        iconImage.color = DarkHudText;
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.24f, 0.24f);
        iconRect.anchorMax = new Vector2(0.76f, 0.76f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        var badgeGO = new GameObject("Badge", typeof(Image));
        badgeGO.transform.SetParent(buttonGO.transform, false);
        var badgeImage = badgeGO.GetComponent<Image>();
        badgeImage.sprite = knobSprite;
        badgeImage.type = Image.Type.Simple;
        badgeImage.color = BadgeTerracotta;
        var badgeRect = badgeGO.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(-4f, -4f);
        badgeRect.sizeDelta = new Vector2(30f, 30f);

        var badgeTextGO = new GameObject("BadgeText", typeof(Text));
        badgeTextGO.transform.SetParent(badgeGO.transform, false);
        var badgeText = badgeTextGO.GetComponent<Text>();
        badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        badgeText.fontSize = 18;
        badgeText.alignment = TextAnchor.MiddleCenter;
        badgeText.color = Color.white;
        badgeText.text = "3";
        var badgeTextRect = badgeTextGO.GetComponent<RectTransform>();
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.offsetMin = Vector2.zero;
        badgeTextRect.offsetMax = Vector2.zero;

        var usesDisplay = buttonGO.AddComponent<ControlButtonUsesDisplay>();
        var button = buttonGO.GetComponent<Button>();
        SetField(usesDisplay, "_button", button);
        SetField(usesDisplay, "_faceImage", faceImage);
        SetField(usesDisplay, "_iconImage", iconImage);
        SetField(usesDisplay, "_badgeBackground", badgeImage);
        SetField(usesDisplay, "_badgeText", badgeText);

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        SetField(hudComponent, "_button", button);
        SetField(hudComponent, "_usesDisplay", usesDisplay);
        SetField(hudComponent, "_gameController", gameController);
    }

    private static Image CreateCardLayer(Transform parent, string name, float sizeRatio, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        var inset = (1f - sizeRatio) / 2f;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(inset, inset);
        rect.anchorMax = new Vector2(1f - inset, 1f - inset);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
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
