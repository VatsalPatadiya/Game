using System.IO;
using GameClient.Data;
using GameClient.Presentation;
using GameClient.Presentation.Board3D;
using GameClient.Presentation.HUD3D;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameSceneBuilder3D
{
    private static readonly Color BoardGreen = new Color(42f / 255f, 61f / 255f, 48f / 255f, 1f);
    private static readonly Color DarkHudText = new Color(40f / 255f, 46f / 255f, 36f / 255f, 1f);

    // BoardView3D.FitCameraToBoard backs the camera off to ~18 world units
    // to frame the fixed TurtleShapeBuilder layout (9 cols x 4 rows at
    // _cellWidth=0.57/_cellHeight=0.85, see BoardView3D's own distance
    // formula). HUD elements sized in the same tile-comparable world units
    // (traySlotSize=0.5, button scale=0.45) need to sit at roughly that same
    // distance from camera to read at the intended scale relative to the
    // board - placing them at the old distance=6 made them ~3x too large.
    private const float HudDistance = 18f;
    private const float PopupDistance = 11f; // closer than HudDistance so the modal reads larger, in front of the board

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 40f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BoardGreen;
        cameraGO.tag = "MainCamera";
        // Editor batch mode has no real display, so Camera.aspect defaults
        // to some arbitrary (non-portrait) value here. Every
        // PositionInFrontOfCamera call below bakes a viewport->world
        // conversion that depends on this aspect - left at the default, any
        // off-center viewport X (buttons at 0.3/0.7; only dead-center 0.5
        // elements are aspect-independent) ends up positioned for the wrong
        // screen shape and lands outside the real device's view frustum.
        // Pin it to this project's target portrait resolution so the baked
        // positions match what actually renders on-device.
        camera.aspect = 1080f / 2340f;

        var lightGO = new GameObject("Key Light", typeof(Light));
        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        light.shadows = LightShadows.Soft;
        light.intensity = 1.1f;

        var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CardBody.mat");
        RequireNotNull(cardMaterial, "Assets/Materials/CardBody.mat as Material");

        var boardGO = new GameObject("Board", typeof(BoardView3D));
        var boardView = boardGO.GetComponent<BoardView3D>();
        var tilePrefab = AssetDatabase.LoadAssetAtPath<TileView3D>("Assets/Prefabs/Tile3D.prefab");
        RequireNotNull(tilePrefab, "Assets/Prefabs/Tile3D.prefab as TileView3D");
        SetField(boardView, "_tilePrefab", tilePrefab);

        var tileSet = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
        RequireNotNull(tileSet, "Assets/Data/DefaultTileSet.asset as TileSetAsset");
        SetField(boardView, "_tileSet", tileSet);
        SetField(boardView, "_camera", camera);

        var inputGO = new GameObject("TileInputController3D", typeof(TileInputController3D));
        var inputController = inputGO.GetComponent<TileInputController3D>();

        var gameControllerGO = new GameObject("GameController", typeof(GameController));
        var gameController = gameControllerGO.GetComponent<GameController>();
        SetField(gameController, "_boardView", boardView);

        SetField(inputController, "_targetCamera", camera);
        SetField(inputController, "_gameController", gameController);

        // ------------------
        // Score text
        // ------------------
        var scoreGO = new GameObject("ScoreText", typeof(TextMeshPro), typeof(ScoreDisplay3D));
        var scoreText = scoreGO.GetComponent<TextMeshPro>();
        scoreText.text = "Score: 0";
        scoreText.color = Color.white; // floats directly on BoardGreen, unlike titleText/messageText which sit on the light Panel
        scoreText.fontSize = 1.2f;
        scoreText.alignment = TextAlignmentOptions.Center;
        var scoreDisplay = scoreGO.GetComponent<ScoreDisplay3D>();
        SetField(scoreDisplay, "_scoreText", scoreText);
        SetField(scoreDisplay, "_gameController", gameController);
        PositionInFrontOfCamera(scoreGO.transform, camera, new Vector2(0.5f, 0.92f), HudDistance);

        // ------------------
        // Control bar (hint/undo/shuffle)
        // ------------------
        var hintIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_hint.png");
        var undoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_undo.png");
        var shuffleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_shuffle.png");
        RequireNotNull(hintIcon, "Assets/Textures/HudIcons/icon_hint.png as Sprite");
        RequireNotNull(undoIcon, "Assets/Textures/HudIcons/icon_undo.png as Sprite");
        RequireNotNull(shuffleIcon, "Assets/Textures/HudIcons/icon_shuffle.png as Sprite");

        CreateHudButton3D(camera, cardMaterial, new Vector2(0.3f, 0.08f), gameController, typeof(ShuffleButton3D), shuffleIcon);
        CreateHudButton3D(camera, cardMaterial, new Vector2(0.5f, 0.08f), gameController, typeof(HintButton3D), hintIcon);
        CreateHudButton3D(camera, cardMaterial, new Vector2(0.7f, 0.08f), gameController, typeof(UndoButton3D), undoIcon);

        // ------------------
        // Tray - row of fixed 3D slots in front of the board
        // ------------------
        const int traySlotCount = 4;
        const float traySlotSize = 0.5f;
        const float traySlotSpacing = 0.55f;

        var trayRootGO = new GameObject("TrayRoot", typeof(TrayView3D));
        var trayView = trayRootGO.GetComponent<TrayView3D>();
        PositionInFrontOfCamera(trayRootGO.transform, camera, new Vector2(0.5f, 0.8f), HudDistance);

        var anchors = new Transform[traySlotCount];
        float startX = -(traySlotCount - 1) * traySlotSpacing / 2f;
        for (int i = 0; i < traySlotCount; i++)
        {
            var anchorGO = new GameObject("Slot" + i);
            anchorGO.transform.SetParent(trayRootGO.transform, false);
            anchorGO.transform.localPosition = new Vector3(startX + i * traySlotSpacing, 0f, 0f);
            anchors[i] = anchorGO.transform;
        }

        var traySlotPrefab = BuildTraySlotPrefab(cardMaterial, traySlotSize);
        SetField(trayView, "traySlotPrefab", traySlotPrefab);
        SetField(trayView, "tileSet", tileSet);
        SetFieldArray(trayView, "slotAnchors", anchors);
        SetField(gameController, "_trayView", trayView);

        // ------------------
        // Game over popup
        // ------------------
        var popupGO = new GameObject("GameOverPopup", typeof(GameOverPopup3D));
        PositionInFrontOfCamera(popupGO.transform, camera, new Vector2(0.5f, 0.5f), PopupDistance);

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Panel";
        panel.transform.SetParent(popupGO.transform, false);
        panel.transform.localScale = new Vector3(3f, 2f, 0.15f);
        panel.GetComponent<MeshRenderer>().sharedMaterial = cardMaterial;
        Object.DestroyImmediate(panel.GetComponent<BoxCollider>());

        var titleGO = new GameObject("Title", typeof(TextMeshPro));
        titleGO.transform.SetParent(popupGO.transform, false);
        titleGO.transform.localPosition = new Vector3(0f, 0.6f, -0.1f);
        var titleText = titleGO.GetComponent<TextMeshPro>();
        titleText.fontSize = 1.1f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = DarkHudText;

        var messageGO = new GameObject("Message", typeof(TextMeshPro));
        messageGO.transform.SetParent(popupGO.transform, false);
        messageGO.transform.localPosition = new Vector3(0f, 0.1f, -0.1f);
        var messageText = messageGO.GetComponent<TextMeshPro>();
        messageText.fontSize = 0.66f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = DarkHudText;

        var restartGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        restartGO.name = "RestartButton";
        restartGO.transform.SetParent(popupGO.transform, false);
        restartGO.transform.localPosition = new Vector3(0f, -0.6f, -0.1f);
        restartGO.transform.localScale = new Vector3(1.4f, 0.5f, 0.15f);
        restartGO.GetComponent<MeshRenderer>().sharedMaterial = cardMaterial;
        var restartButton = restartGO.AddComponent<PressScaleButton3D>();
        SetField(restartButton, "_targetCamera", camera);

        var restartTextGO = new GameObject("Text", typeof(TextMeshPro));
        restartTextGO.transform.SetParent(restartGO.transform, false);
        restartTextGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        var restartText = restartTextGO.GetComponent<TextMeshPro>();
        restartText.text = "Restart";
        restartText.fontSize = 0.66f;
        restartText.alignment = TextAlignmentOptions.Center;
        restartText.color = DarkHudText; // sits on the white cardMaterial restart button face, same contrast issue as the HUD icons/badges

        var gameOverPopup = popupGO.GetComponent<GameOverPopup3D>();
        SetField(gameOverPopup, "restartButton", restartButton);
        SetField(gameOverPopup, "titleText", titleText);
        SetField(gameOverPopup, "messageText", messageText);
        SetField(gameController, "_gameOverPopup", gameOverPopup);
        popupGO.SetActive(false); // hidden by default

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");

        Debug.Log("GAME_SCENE_BUILDER_3D_DONE");
    }

    // Places an element at a fixed viewport position, a fixed distance in
    // front of the camera - the 3D-scene replacement for the 2D Canvas's
    // screen-anchored RectTransforms, so HUD elements stay in the same
    // screen location regardless of board size/camera distance. Parented to
    // the camera so it stays true after BoardView3D.FitCameraToBoard moves
    // the camera at runtime (Build() only positions relative to the camera's
    // build-time pose at the world origin - without parenting, a runtime
    // camera move leaves every HUD element behind, out of registration).
    private static void PositionInFrontOfCamera(Transform target, Camera camera, Vector2 viewportPos, float distance)
    {
        target.position = camera.ViewportToWorldPoint(new Vector3(viewportPos.x, viewportPos.y, distance));
        target.rotation = camera.transform.rotation;
        target.SetParent(camera.transform, true);
    }

    private static void CreateHudButton3D(
        Camera camera, Material cardMaterial, Vector2 viewportPos, GameController gameController,
        System.Type hudComponentType, Sprite iconSprite)
    {
        var buttonGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        buttonGO.name = hudComponentType.Name;
        buttonGO.transform.localScale = new Vector3(0.45f, 0.45f, 0.15f);
        PositionInFrontOfCamera(buttonGO.transform, camera, viewportPos, HudDistance);
        buttonGO.GetComponent<MeshRenderer>().sharedMaterial = cardMaterial;

        var pressButton = buttonGO.AddComponent<PressScaleButton3D>();
        SetField(pressButton, "_targetCamera", camera);

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        iconGO.transform.SetParent(buttonGO.transform, false);
        Object.DestroyImmediate(iconGO.GetComponent<MeshCollider>());
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.6f);
        iconGO.transform.localScale = Vector3.one * 0.6f;
        var iconMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        URPMaterialUtil.SetTransparent(iconMaterial);
        iconMaterial.SetTexture("_BaseMap", iconSprite.texture);
        iconMaterial.SetColor("_BaseColor", DarkHudText); // the glyph pixels are opaque white with alpha shaping - untinted, they're invisible against the white button face. ControlButtonUsesDisplay3D seeds its MeshRendererTint with the same color so this doesn't get reset to white on the first SetRemaining() call.
        // Must be saved as a real asset, like TileMeshGenerator's TileIcon.mat -
        // a transparent material that only ever exists embedded in the scene
        // (never an AssetDatabase asset) renders its alpha-cutout shape as a
        // solid opaque quad on-device, even though it looks correct in the
        // Editor (confirmed by comparison: TileIcon.mat's glyphs render fine,
        // this one didn't until saved the same way).
        Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(iconMaterial, "Assets/Materials/HudIcon_" + hudComponentType.Name + ".mat");
        iconGO.GetComponent<MeshRenderer>().material = iconMaterial;

        var badgeGO = new GameObject("BadgeText", typeof(TextMeshPro));
        badgeGO.transform.SetParent(buttonGO.transform, false);
        badgeGO.transform.localPosition = new Vector3(0.35f, 0.35f, -0.7f);
        var badgeText = badgeGO.GetComponent<TextMeshPro>();
        badgeText.text = "3";
        badgeText.fontSize = 0.6f;
        badgeText.color = DarkHudText; // sits directly on the white button face, same as the icon - white text there is invisible
        badgeText.alignment = TextAlignmentOptions.Center;

        var usesDisplay = buttonGO.AddComponent<ControlButtonUsesDisplay3D>();
        SetField(usesDisplay, "_button", pressButton);
        SetField(usesDisplay, "_faceRenderer", buttonGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_iconRenderer", iconGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_badgeText", badgeText);

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        SetField(hudComponent, "_button", pressButton);
        SetField(hudComponent, "_usesDisplay", usesDisplay);
        SetField(hudComponent, "_gameController", gameController);
    }

    private static GameObject BuildTraySlotPrefab(Material cardMaterial, float size)
    {
        var root = new GameObject("TraySlot3D");
        var content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(content.transform, false);
        body.transform.localScale = new Vector3(size, size, 0.1f);
        body.GetComponent<MeshRenderer>().sharedMaterial = cardMaterial;
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        iconGO.transform.SetParent(content.transform, false);
        Object.DestroyImmediate(iconGO.GetComponent<MeshCollider>());
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        iconGO.transform.localScale = Vector3.one * size * CardStyle.IconSizeRatio;
        var iconMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        URPMaterialUtil.SetTransparent(iconMaterial);
        // Saved as a real asset for the same reason as HudIcon_*.mat above -
        // an embedded-only transparent material renders its alpha cutout as a
        // solid opaque quad on-device.
        Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(iconMaterial, "Assets/Materials/TrayIcon.mat");
        iconGO.GetComponent<MeshRenderer>().material = iconMaterial;
        iconGO.SetActive(false); // TraySlotView3D.SetEmpty() keeps it disabled until a tile fills the slot

        var slotView = root.AddComponent<TraySlotView3D>();
        SetField(slotView, "_content", content.transform);
        SetField(slotView, "_bodyRenderer", body.GetComponent<MeshRenderer>());
        SetField(slotView, "_iconRenderer", iconGO.GetComponent<MeshRenderer>());

        Directory.CreateDirectory("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/TraySlot3D.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void SetField(Object target, string fieldName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFieldArray(Object target, string fieldName, Object[] values)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireNotNull(Object value, string description)
    {
        if (value == null) throw new System.Exception("GAME_SCENE_BUILDER_3D_MISSING: " + description);
    }

    private static void RequireNotNull(SerializedProperty value, string description)
    {
        if (value == null) throw new System.Exception("GAME_SCENE_BUILDER_3D_MISSING_PROPERTY: " + description);
    }
}
