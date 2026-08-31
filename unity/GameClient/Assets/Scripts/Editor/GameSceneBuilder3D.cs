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
    private static readonly Color CreamHudText = new Color(0.96f, 0.93f, 0.84f, 1f); // light text on wood/bronze chrome

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
        camera.backgroundColor = new Color(0.098f, 0.184f, 0.145f); // dark felt edge, in case the felt quad doesn't reach a frame corner
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

        // Tiles are only ~a couple units deep, but the camera sits ~18 world
        // units back (see HudDistance/BoardView3D.FitCameraToBoard above) -
        // URP's default shadow distance is too short to cover the whole
        // tilted board at that camera distance, so raised tiles would cast no
        // shadow onto the tiles below.
        var urp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
                  UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        // URPSetup pins 15f for Galaxy A50 perf, but tiles sit ~18u from the camera so 15 shows no tile shadows; raise just enough for the stacked board to receive shadows. Verify FPS on the low-end test device.
        if (urp != null) urp.shadowDistance = 30f;

        var lightGO = new GameObject("Key Light", typeof(Light));
        var light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(55f, -35f, 0f); // top-left key
        light.intensity = 1.05f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.65f;   // soft, not black

        var fillGO = new GameObject("Fill Light", typeof(Light));
        var fill = fillGO.GetComponent<Light>();
        fill.type = LightType.Directional;
        fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
        fill.intensity = 0.35f;
        fill.shadows = LightShadows.None;

        // Warm ambient so the ivory tiles read warm and shadowed tiles don't go
        // muddy against the felt.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.5f, 0.45f);

        // Felt-table backdrop: a large quad behind the board (a bit past the
        // deepest tile layer) so the board sits on a warm, vignetted table
        // instead of floating in the flat camera colour. Double-sided material
        // (Felt.mat _Cull=0), so the runtime camera tilt never culls it.
        var feltMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Felt.mat");
        RequireNotNull(feltMat, "Assets/Materials/Felt.mat as Material (run FeltBackgroundGenerator first)");
        var feltGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        feltGO.name = "FeltBackground";
        Object.DestroyImmediate(feltGO.GetComponent<Collider>());
        feltGO.transform.position = new Vector3(0f, 0f, 9f); // well behind the HUD (~z3-4) so it never occludes the score bar / buttons
        feltGO.transform.rotation = Quaternion.identity;
        feltGO.transform.localScale = new Vector3(60f, 60f, 1f);
        var feltRenderer = feltGO.GetComponent<MeshRenderer>();
        feltRenderer.sharedMaterial = feltMat;
        feltRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        feltRenderer.receiveShadows = true;

        var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CardBody.mat");
        RequireNotNull(cardMaterial, "Assets/Materials/CardBody.mat as Material");
        var woodMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Wood.mat");
        RequireNotNull(woodMaterial, "Assets/Materials/Wood.mat as Material (run WoodUiGenerator first)");
        var bronzeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bronze.mat");
        RequireNotNull(bronzeMaterial, "Assets/Materials/Bronze.mat as Material (run WoodUiGenerator first)");

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
        // Progress bar (score-driven gold fill) - replaces the score plaque
        // ------------------
        const float TrackWidth = 2.6f;   // matches ProgressBar3D._trackWidth default
        const float TrackHeight = 0.34f;

        var scoreRootGO = new GameObject("ProgressBar");
        PositionInFrontOfCamera(scoreRootGO.transform, camera, new Vector2(0.5f, 0.92f), HudDistance);

        // wood track (the empty groove)
        var trackGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trackGO.name = "Track";
        trackGO.transform.SetParent(scoreRootGO.transform, false);
        trackGO.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        trackGO.transform.localScale = new Vector3(TrackWidth + 0.12f, TrackHeight + 0.12f, 0.1f);
        Object.DestroyImmediate(trackGO.GetComponent<BoxCollider>());
        trackGO.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;

        // gold fill (left-anchored, grown by ProgressBar3D)
        var barFillGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        barFillGO.name = "Fill";
        barFillGO.transform.SetParent(scoreRootGO.transform, false);
        barFillGO.transform.localPosition = new Vector3(-TrackWidth * 0.5f, 0f, -0.02f);
        barFillGO.transform.localScale = new Vector3(0f, TrackHeight * 0.72f, 1f);
        Object.DestroyImmediate(barFillGO.GetComponent<Collider>());
        barFillGO.GetComponent<MeshRenderer>().sharedMaterial =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gold.mat");

        // milestone ticks at 1/3 and 2/3
        foreach (float f in new[] { 1f / 3f, 2f / 3f })
        {
            var tick = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tick.name = "Tick";
            tick.transform.SetParent(scoreRootGO.transform, false);
            tick.transform.localPosition = new Vector3(-TrackWidth * 0.5f + TrackWidth * f, 0f, -0.04f);
            tick.transform.localScale = new Vector3(0.02f, TrackHeight * 0.9f, 1f);
            Object.DestroyImmediate(tick.GetComponent<Collider>());
            var tm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tm.SetColor("_BaseColor", new Color(0.2f, 0.13f, 0.06f));
            tick.GetComponent<MeshRenderer>().sharedMaterial = tm;
        }

        var scoreGO = new GameObject("ScoreText", typeof(TextMeshPro));
        scoreGO.transform.SetParent(scoreRootGO.transform, false);
        scoreGO.transform.localPosition = new Vector3(0f, 0f, -0.08f);
        var scoreText = scoreGO.GetComponent<TextMeshPro>();
        scoreText.text = "0";
        scoreText.color = CreamHudText;
        scoreText.fontSize = 0.9f;
        scoreText.alignment = TextAlignmentOptions.Center;

        var progressBar = scoreRootGO.AddComponent<ProgressBar3D>();
        SetField(progressBar, "_fill", barFillGO.transform);
        SetField(progressBar, "_label", scoreText);
        SetField(progressBar, "_gameController", gameController);
        // _maxScore (2000), _trackWidth (2.6), _fillHeight (0.24) use the
        // component's serialized defaults, which match the track built above.

        // ------------------
        // Control bar (hint/undo/shuffle)
        // ------------------
        var hintIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_hint.png");
        var undoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_undo.png");
        var shuffleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_shuffle.png");
        RequireNotNull(hintIcon, "Assets/Textures/HudIcons/icon_hint.png as Sprite");
        RequireNotNull(undoIcon, "Assets/Textures/HudIcons/icon_undo.png as Sprite");
        RequireNotNull(shuffleIcon, "Assets/Textures/HudIcons/icon_shuffle.png as Sprite");

        var badgeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        badgeMaterial.SetColor("_BaseColor", new Color(0.85f, 0.24f, 0.2f, 1f));
        AssetDatabase.CreateAsset(badgeMaterial, "Assets/Materials/HudBadgeRed.mat");

        CreateHudButton3D(camera, bronzeMaterial, badgeMaterial, new Vector2(0.3f, 0.08f), gameController, typeof(ShuffleButton3D), shuffleIcon);
        CreateHudButton3D(camera, bronzeMaterial, badgeMaterial, new Vector2(0.5f, 0.08f), gameController, typeof(HintButton3D), hintIcon);
        CreateHudButton3D(camera, bronzeMaterial, badgeMaterial, new Vector2(0.7f, 0.08f), gameController, typeof(UndoButton3D), undoIcon);

        // ------------------
        // Tray - row of fixed 3D slots in front of the board
        // ------------------
        const int traySlotCount = 4;    // pair-match tray: 4 slots (matches BoardState.MaxTraySize)
        const float traySlotSize = 0.65f;
        const float traySlotSpacing = 0.66f; // snug tiles like the reference (was 0.72, too gappy)

        var trayRootGO = new GameObject("TrayRoot", typeof(TrayView3D));
        var trayView = trayRootGO.GetComponent<TrayView3D>();
        // Tray sits CLOSER to the camera than the board (TrayDistance < the
        // board's fit distance) so it draws in FRONT of the stack instead of
        // being occluded by the tiles. Scaled down by TrayDistance/HudDistance
        // so its on-screen size is unchanged despite the nearer placement.
        const float TrayDistance = 9f;
        PositionInFrontOfCamera(trayRootGO.transform, camera, new Vector2(0.5f, 0.8f), TrayDistance);
        trayRootGO.transform.localScale = Vector3.one * (TrayDistance / HudDistance);

        // Rounded wood frame behind the slots (matches the reference's rounded
        // tray corners). The 4 slots sit on it as warm recessed parts.
        float containerWidth = (traySlotCount - 1) * traySlotSpacing + traySlotSize + 0.34f;
        float frameHeight = traySlotSize + 0.26f;
        var frameMesh = SaveRoundedTrayMesh("Assets/Meshes/TrayFrame.asset", containerWidth, frameHeight, 0.12f, 0.14f);
        var trayContainerGO = new GameObject("TrayContainer", typeof(MeshFilter), typeof(MeshRenderer));
        trayContainerGO.transform.SetParent(trayRootGO.transform, false);
        trayContainerGO.transform.localPosition = new Vector3(0f, 0f, 0.06f); // behind the slots (+Z, away from camera)
        trayContainerGO.GetComponent<MeshFilter>().sharedMesh = frameMesh;
        trayContainerGO.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;

        var anchors = new Transform[traySlotCount];
        float startX = -(traySlotCount - 1) * traySlotSpacing / 2f;
        for (int i = 0; i < traySlotCount; i++)
        {
            var anchorGO = new GameObject("Slot" + i);
            anchorGO.transform.SetParent(trayRootGO.transform, false);
            anchorGO.transform.localPosition = new Vector3(startX + i * traySlotSpacing, 0f, 0f);
            anchors[i] = anchorGO.transform;
        }

        var recessMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrayRecess.mat");
        RequireNotNull(recessMaterial, "Assets/Materials/TrayRecess.mat (run WoodUiGenerator first)");
        var traySlotPrefab = BuildTraySlotPrefab(recessMaterial, traySlotSize);
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
        panel.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;
        Object.DestroyImmediate(panel.GetComponent<BoxCollider>());

        var titleGO = new GameObject("Title", typeof(TextMeshPro));
        titleGO.transform.SetParent(popupGO.transform, false);
        titleGO.transform.localPosition = new Vector3(0f, 0.6f, -0.1f);
        var titleText = titleGO.GetComponent<TextMeshPro>();
        titleText.fontSize = 1.1f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = CreamHudText;

        var messageGO = new GameObject("Message", typeof(TextMeshPro));
        messageGO.transform.SetParent(popupGO.transform, false);
        messageGO.transform.localPosition = new Vector3(0f, 0.1f, -0.1f);
        var messageText = messageGO.GetComponent<TextMeshPro>();
        messageText.fontSize = 0.66f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = CreamHudText;

        var restartGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        restartGO.name = "RestartButton";
        restartGO.transform.SetParent(popupGO.transform, false);
        restartGO.transform.localPosition = new Vector3(0f, -0.6f, -0.1f);
        restartGO.transform.localScale = new Vector3(1.4f, 0.5f, 0.15f);
        restartGO.GetComponent<MeshRenderer>().sharedMaterial = bronzeMaterial;
        var restartButton = restartGO.AddComponent<PressScaleButton3D>();
        SetField(restartButton, "_targetCamera", camera);

        var restartTextGO = new GameObject("Text", typeof(TextMeshPro));
        restartTextGO.transform.SetParent(restartGO.transform, false);
        restartTextGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        var restartText = restartTextGO.GetComponent<TextMeshPro>();
        restartText.text = "Restart";
        restartText.fontSize = 0.66f;
        restartText.alignment = TextAlignmentOptions.Center;
        restartText.color = CreamHudText; // cream on the bronze restart button

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
        Camera camera, Material cardMaterial, Material badgeMaterial, Vector2 viewportPos, GameController gameController,
        System.Type hudComponentType, Sprite iconSprite)
    {
        // Empty, unrotated root: PressScaleButton3D requires a BoxCollider
        // sized to the button's footprint, and every child below (icon,
        // badge) is positioned assuming an unrotated parent - only the
        // "Face" child is rotated, purely for its circular-disc look.
        var buttonGO = new GameObject(hudComponentType.Name);
        PositionInFrontOfCamera(buttonGO.transform, camera, viewportPos, HudDistance);

        var faceGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        faceGO.name = "Face";
        faceGO.transform.SetParent(buttonGO.transform, false);
        faceGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        faceGO.transform.localScale = new Vector3(0.45f, 0.15f, 0.45f);
        Object.DestroyImmediate(faceGO.GetComponent<Collider>());
        faceGO.GetComponent<MeshRenderer>().sharedMaterial = cardMaterial;

        var pressButton = buttonGO.AddComponent<PressScaleButton3D>(); // RequireComponent auto-adds a BoxCollider, default-sized - must be resized to the disc's footprint
        var buttonCollider = buttonGO.GetComponent<BoxCollider>();
        buttonCollider.size = new Vector3(0.45f, 0.45f, 0.15f);
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

        var badgeBgGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        badgeBgGO.name = "BadgeBackground";
        badgeBgGO.transform.SetParent(buttonGO.transform, false);
        badgeBgGO.transform.localPosition = new Vector3(0.32f, 0.32f, -0.66f);
        badgeBgGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        badgeBgGO.transform.localScale = new Vector3(0.17f, 0.05f, 0.17f);
        Object.DestroyImmediate(badgeBgGO.GetComponent<Collider>());
        badgeBgGO.GetComponent<MeshRenderer>().sharedMaterial = badgeMaterial;

        var badgeGO = new GameObject("BadgeText", typeof(TextMeshPro));
        badgeGO.transform.SetParent(buttonGO.transform, false);
        // BadgeBackground is a Cylinder (default half-height 1) scaled to
        // 0.05 and rotated so its height axis is local Z - its front face
        // sits at -0.66 - 0.05 = -0.71, in front of the old -0.7 text
        // position. That embedded the flat text mesh behind the opaque
        // disc's front cap, hiding it entirely (confirmed on-device: badge
        // showed only a bare red circle, no digit). -0.72 clears the front
        // face by the same 0.01 epsilon used elsewhere in this file.
        badgeGO.transform.localPosition = new Vector3(0.32f, 0.32f, -0.72f);
        var badgeText = badgeGO.GetComponent<TextMeshPro>();
        badgeText.text = "3";
        badgeText.fontSize = 0.5f;
        badgeText.color = Color.white; // sits on the red BadgeBackground circle now, not the button face
        badgeText.alignment = TextAlignmentOptions.Center;

        var usesDisplay = buttonGO.AddComponent<ControlButtonUsesDisplay3D>();
        SetField(usesDisplay, "_button", pressButton);
        SetField(usesDisplay, "_faceRenderer", faceGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_iconRenderer", iconGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_badgeText", badgeText);

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        SetField(hudComponent, "_button", pressButton);
        SetField(hudComponent, "_usesDisplay", usesDisplay);
        SetField(hudComponent, "_gameController", gameController);
    }

    // Builds a rounded-rectangle slab mesh and saves it as an asset (so both the
    // in-scene frame and the slot prefab reference a real asset, not a runtime mesh).
    private static Mesh SaveRoundedTrayMesh(string path, float w, float h, float thickness, float radius)
    {
        var mesh = RoundedTileMesh.Build(w, h, thickness, radius, cornerSegments: 6);
        mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
        System.IO.Directory.CreateDirectory("Assets/Meshes");
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static GameObject BuildTraySlotPrefab(Material cardMaterial, float size)
    {
        var root = new GameObject("TraySlot3D");
        var content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);

        // Rounded slot body (rounded corners like the reference); empty = warm
        // recess (cardMaterial), filled swaps to the ivory tile face.
        var slotMesh = SaveRoundedTrayMesh("Assets/Meshes/TraySlot.asset", size * 0.9f, size * 0.9f, 0.05f, size * 0.16f);
        var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(content.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.03f); // slightly toward camera, inset within the tray container
        body.GetComponent<MeshFilter>().sharedMesh = slotMesh;
        body.GetComponent<MeshRenderer>().sharedMaterial = cardMaterial; // recess material passed in

        var foodAnchorGO = new GameObject("FoodAnchor");
        foodAnchorGO.transform.SetParent(content.transform, false);
        foodAnchorGO.transform.localPosition = new Vector3(0f, 0f, -0.08f);
        // Bold symbol filling the tray tile (default scale 1 read tiny/faint on the
        // ivory face); board tiles use ~2.0, tray tiles are smaller so ~1.6 fills them.
        foodAnchorGO.transform.localScale = Vector3.one * 1.6f;

        // Filled slots swap to the ivory tile face so a collected tile looks like a
        // real white tile in the tray (matches the reference); empty slots keep the
        // dark recess (cardMaterial).
        var tileFaceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileBody.mat");
        RequireNotNull(tileFaceMaterial, "Assets/Materials/TileBody.mat (run TileMaterialGenerator first)");

        var slotView = root.AddComponent<TraySlotView3D>();
        SetField(slotView, "_content", content.transform);
        SetField(slotView, "_bodyRenderer", body.GetComponent<MeshRenderer>());
        SetField(slotView, "_foodAnchor", foodAnchorGO.transform);
        SetField(slotView, "_emptyMaterial", cardMaterial);
        SetField(slotView, "_filledMaterial", tileFaceMaterial);

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
