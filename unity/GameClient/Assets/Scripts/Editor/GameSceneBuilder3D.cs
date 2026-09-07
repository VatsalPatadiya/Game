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
    private static readonly Color MutedIconTint = new Color(0.541f, 0.502f, 0.447f, 1f); // #8A8072 - mockup's .chrome-btn.is-locked svg stroke
    private static readonly Color GoldIconTint = new Color(0.95f, 0.78f, 0.30f, 1f); // hint's lightbulb is colored gold, unlike the other buttons' white/cream glyphs

    // BoardView3D.FitCameraToBoard measures ~13.6 world units for the current
    // fixed board layout (confirmed via a runtime diagnostic) - HudDistance
    // must stay safely LESS than that so HUD elements (parented to the camera
    // at this fixed local Z) render in FRONT of the board instead of behind
    // it. This used to be 18, calibrated against a since-changed, larger
    // board size; leaving it at 18 forced BoardView3D._minDistanceForHud to
    // push the camera back further than this board actually needs, which
    // visibly shrank the tiles as an unintended side effect. 11 leaves a
    // ~2.6-unit margin against the measured 13.6 without oversizing it.
    // NOTE: if the board layout ever becomes variable-sized again, a static
    // margin like this is fragile - re-derive HudDistance dynamically from
    // BoardView3D's actual fit distance instead of hardcoding it here.
    private const float HudDistance = 11f;
    private const float PopupDistance = 9f; // closer than HudDistance so the modal reads larger, in front of the board

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
        var hudButtonFaceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HudButtonFace.mat");
        RequireNotNull(hudButtonFaceMaterial, "Assets/Materials/HudButtonFace.mat as Material (run WoodUiGenerator first)");
        var hudButtonFaceLockedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HudButtonFaceLocked.mat");
        RequireNotNull(hudButtonFaceLockedMaterial, "Assets/Materials/HudButtonFaceLocked.mat as Material (run WoodUiGenerator first)");
        var trayBorderMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrayBorder.mat");
        RequireNotNull(trayBorderMaterial, "Assets/Materials/TrayBorder.mat as Material (run WoodUiGenerator first)");

        var boardGO = new GameObject("Board", typeof(BoardView3D));
        var boardView = boardGO.GetComponent<BoardView3D>();
        var tilePrefab = AssetDatabase.LoadAssetAtPath<TileView3D>("Assets/Prefabs/Tile3D.prefab");
        RequireNotNull(tilePrefab, "Assets/Prefabs/Tile3D.prefab as TileView3D");
        SetField(boardView, "_tilePrefab", tilePrefab);

        var tileSet = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
        RequireNotNull(tileSet, "Assets/Data/DefaultTileSet.asset as TileSetAsset");
        SetField(boardView, "_tileSet", tileSet);
        SetField(boardView, "_camera", camera);
        // Keeps BoardView3D's camera-fit distance from ever undercutting where the
        // HUD is anchored (see BoardView3D._minDistanceForHud) - the two constants
        // are set from the same HudDistance value here so they can't drift apart.
        SetFieldFloat(boardView, "_minDistanceForHud", HudDistance);

        var inputGO = new GameObject("TileInputController3D", typeof(TileInputController3D));
        var inputController = inputGO.GetComponent<TileInputController3D>();

        var gameControllerGO = new GameObject("GameController", typeof(GameController));
        var gameController = gameControllerGO.GetComponent<GameController>();
        SetField(gameController, "_boardView", boardView);

        SetField(inputController, "_targetCamera", camera);
        SetField(inputController, "_gameController", gameController);

        // Tray sizing, computed early so the progress bar (below) can match its
        // width exactly - "exact over the tile 4 tile box" per the reference.
        // See the Tray section further down for where these are actually used
        // to build the tray itself.
        const int TraySlotCount = 4;    // pair-match tray: 4 slots (matches BoardState.MaxTraySize)
        const float TraySlotSize = 0.65f;
        const float TraySlotSpacing = 0.66f; // snug tiles like the reference (was 0.72, too gappy)
        float trayContainerWidth = (TraySlotCount - 1) * TraySlotSpacing + TraySlotSize + 0.34f;
        float trayFrameHeight = TraySlotSize + 0.26f;

        // ------------------
        // Progress bar - simplified to just the bar (border/background/fill),
        // no milestone numbers or avatar - width pinned to the tray's width so
        // the two sit as one aligned unit.
        // ------------------
        float TrackWidth = trayContainerWidth;
        const float TrackHeight = 0.34f;
        const float ProgressMaxScore = 2000f; // matches ProgressBar3D._maxScore default

        var scoreRootGO = new GameObject("ProgressBar");
        // 0.875, not 0.92: the mockup puts back/menu on their OWN top row,
        // with the progress bar row below it (CSS .topbar then .progress-row
        // with margin-top:6%) - previously both sat on the same y=0.92 line.
        PositionInFrontOfCamera(scoreRootGO.transform, camera, new Vector2(0.5f, 0.86f), HudDistance);

        // Rounded wood border with a vertical light/dark gradient bake (not a
        // flat colour - see WoodUiGenerator.ApplyVerticalGradientPanel) and a
        // near-transparent wood-tinted background inset within it (10%
        // opacity, so the felt shows through faintly) - same layered-mesh
        // technique as the tray's amber border/dark body. Gold fill sits in
        // front of both. A soft drop shadow (reusing the board tiles'
        // TileShadow.mat) sits behind everything so the whole bar reads as
        // raised off the felt instead of painted flat onto it.
        const float ProgressBorderThickness = 0.06f;
        var progressBorderMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProgressBorder.mat");
        RequireNotNull(progressBorderMaterial, "Assets/Materials/ProgressBorder.mat (run WoodUiGenerator first)");
        var progressBackgroundMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProgressBackground.mat");
        RequireNotNull(progressBackgroundMaterial, "Assets/Materials/ProgressBackground.mat (run WoodUiGenerator first)");
        var tileShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileShadow.mat");
        RequireNotNull(tileShadowMaterial, "Assets/Materials/TileShadow.mat (run TileMaterialGenerator first)");

        var progressShadowGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        progressShadowGO.name = "Shadow";
        Object.DestroyImmediate(progressShadowGO.GetComponent<Collider>());
        progressShadowGO.transform.SetParent(scoreRootGO.transform, false);
        progressShadowGO.transform.localPosition = new Vector3(0.04f, -0.06f, 0.09f); // behind the border (0.06), nudged down-right like the tile shadows
        progressShadowGO.transform.localScale = new Vector3((TrackWidth + 0.12f) * 1.15f, (TrackHeight + 0.12f) * 1.4f, 1f);
        progressShadowGO.GetComponent<MeshRenderer>().sharedMaterial = tileShadowMaterial;
        progressShadowGO.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var progressBorderMesh = SaveRoundedTrayMesh("Assets/Meshes/ProgressBorder.asset",
            TrackWidth + 0.12f, TrackHeight + 0.12f, 0.1f, 0.16f);
        var progressBorderGO = new GameObject("Border", typeof(MeshFilter), typeof(MeshRenderer));
        progressBorderGO.transform.SetParent(scoreRootGO.transform, false);
        progressBorderGO.transform.localPosition = new Vector3(0f, 0f, 0.06f);
        progressBorderGO.GetComponent<MeshFilter>().sharedMesh = progressBorderMesh;
        progressBorderGO.GetComponent<MeshRenderer>().sharedMaterial = progressBorderMaterial;

        var progressBackgroundMesh = SaveRoundedTrayMesh("Assets/Meshes/ProgressBackground.asset",
            TrackWidth + 0.12f - ProgressBorderThickness * 2f, TrackHeight + 0.12f - ProgressBorderThickness * 2f, 0.1f, 0.12f);
        var progressBackgroundGO = new GameObject("Background", typeof(MeshFilter), typeof(MeshRenderer));
        progressBackgroundGO.transform.SetParent(scoreRootGO.transform, false);
        // 0.05, not the previous 0.04: tightened together with Border/Fill/Text
        // below (all now within a 0.03 band instead of spanning 0.14) - this
        // element sits well off-center in the viewport (y=0.875), and a
        // perspective camera visibly shifts children at different local Z by
        // different screen-space amounts even though their local X/Y match
        // (same root cause already documented on the button icon's Z offset
        // below). At 0 score the fill has zero width so this was invisible
        // until a real score exposed the gold fill floating detached from the
        // wood bar instead of sitting flush inside it.
        progressBackgroundGO.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        progressBackgroundGO.GetComponent<MeshFilter>().sharedMesh = progressBackgroundMesh;
        progressBackgroundGO.GetComponent<MeshRenderer>().sharedMaterial = progressBackgroundMaterial;

        // gold fill (left-anchored, grown by ProgressBar3D) - Z tightened to
        // 0.04 (was -0.02, a 0.08 gap from Border) for the same off-center
        // parallax reason as Background above.
        var barFillGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        barFillGO.name = "Fill";
        barFillGO.transform.SetParent(scoreRootGO.transform, false);
        barFillGO.transform.localPosition = new Vector3(-TrackWidth * 0.5f, 0f, 0.04f);
        barFillGO.transform.localScale = new Vector3(0f, TrackHeight * 0.72f, 1f);
        Object.DestroyImmediate(barFillGO.GetComponent<Collider>());
        barFillGO.GetComponent<MeshRenderer>().sharedMaterial =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gold.mat");

        var scoreGO = new GameObject("ScoreText", typeof(TextMeshPro));
        scoreGO.transform.SetParent(scoreRootGO.transform, false);
        scoreGO.transform.localPosition = new Vector3(0f, 0f, 0.03f); // was -0.08, same tightening as Fill/Background above
        var scoreText = scoreGO.GetComponent<TextMeshPro>();
        scoreText.text = "0";
        scoreText.color = CreamHudText;
        scoreText.fontSize = 1.05f; // was 0.9 - larger so it reads clearly centered on the dark track, like the mockup
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.alignment = TextAlignmentOptions.Center;

        var progressBar = scoreRootGO.AddComponent<ProgressBar3D>();
        SetField(progressBar, "_fill", barFillGO.transform);
        SetField(progressBar, "_label", scoreText);
        SetField(progressBar, "_gameController", gameController);
        // _trackWidth must match the TrackWidth actually built above (now tied
        // to the tray's width, not the component's 2.6 default) or the fill's
        // grow-to-the-right math would size itself against the wrong track.
        SetFieldFloat(progressBar, "_trackWidth", TrackWidth);
        // _maxScore (2000) and _fillHeight (0.24) still use the component's
        // serialized defaults.

        // Back / menu chrome flanking the top bar - visual-only for now (no
        // navigation wired), matching the reference's top-bar layout. Same
        // dark-disc-with-amber-ring style as the bottom control buttons.
        var backIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_back.png");
        var menuIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_menu.png");
        RequireNotNull(backIcon, "Assets/Textures/HudIcons/icon_back.png as Sprite (run HudIconGenerator first)");
        RequireNotNull(menuIcon, "Assets/Textures/HudIcons/icon_menu.png as Sprite (run HudIconGenerator first)");
        // Same dark-disc-with-amber-ring style as the bottom control buttons
        // (hudButtonFaceMaterial) - a separate solid-amber style here (tried
        // earlier) made these read as a different button family from the rest
        // of the HUD. One consistent button chrome across all five buttons.
        // y=0.92, not 0.965: leaves a top margin clear of the status-bar area
        // so the discs aren't jammed against the very top edge of the screen.
        var backButtonGO = CreateVisualIconButton3D(camera, hudButtonFaceMaterial, new Vector2(0.09f, 0.92f), "BackButton", backIcon);
        var menuButtonGO = CreateVisualIconButton3D(camera, hudButtonFaceMaterial, new Vector2(0.91f, 0.92f), "MenuButton", menuIcon);

        // ------------------
        // Control bar (hint/undo/shuffle)
        // ------------------
        var hintIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_hint.png");
        var undoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_undo.png");
        var shuffleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_shuffle.png");
        RequireNotNull(hintIcon, "Assets/Textures/HudIcons/icon_hint.png as Sprite");
        RequireNotNull(undoIcon, "Assets/Textures/HudIcons/icon_undo.png as Sprite");
        RequireNotNull(shuffleIcon, "Assets/Textures/HudIcons/icon_shuffle.png as Sprite");

        // Unlit: badges are flat graphic-icon chrome, same reasoning as Bronze.mat -
        // a Lit material here picks up the scene's tile-board lighting and reads as
        // a soft blotch instead of a crisp flat-colored circle.
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        var badgeMaterial = new Material(unlitShader);
        badgeMaterial.SetColor("_BaseColor", new Color(0.761f, 0.231f, 0.188f, 1f)); // #C23B30 - mockup's .action .badge
        AssetDatabase.CreateAsset(badgeMaterial, "Assets/Materials/HudBadgeRed.mat");

        // x spacing widened from 0.3/0.5/0.7 and y raised from 0.08 - both
        // needed room for the ~2.2x bigger buttons below (wider spacing so
        // adjacent badges don't overlap the next disc, higher y so the
        // enlarged level label's offset doesn't get clipped by the bottom
        // system-gesture inset, the same class of off-screen clipping that
        // hit the label's text at the old, lower position).
        // x = 0.17 / 0.5 / 0.83 so the outer buttons' edges line up with the
        // progress bar / tray edges - consistent left/right margins across the
        // whole HUD (they were at 0.2/0.8, a bit narrower than the tray).
        var shuffleButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.17f, 0.15f), gameController, typeof(ShuffleButton3D), shuffleIcon,
            locked: true, lockedFaceMaterial: hudButtonFaceLockedMaterial, lockedLabel: "Lv. 6");
        var hintButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.5f, 0.15f), gameController, typeof(HintButton3D), hintIcon,
            iconColorOverride: GoldIconTint);
        var undoButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.83f, 0.15f), gameController, typeof(UndoButton3D), undoIcon);
        // The control buttons were built at full size (~2.2x) and ran off the
        // screen edges (undo's badge was clipped). Scale the whole button root
        // (face + icon + badge + caption together) down to a mockup-sized disc
        // so all three sit fully on-screen with even spacing.
        foreach (var b in new[] { shuffleButtonGO, hintButtonGO, undoButtonGO })
            b.transform.localScale = Vector3.one * 0.62f;

        // ------------------
        // Tray - row of fixed 3D slots in front of the board. Sizing consts
        // (TraySlotCount/TraySlotSize/TraySlotSpacing) and the derived
        // trayContainerWidth/trayFrameHeight are declared earlier, near the
        // progress bar, so the bar's width can match this tray exactly.
        // ------------------
        var trayRootGO = new GameObject("TrayRoot", typeof(TrayView3D));
        var trayView = trayRootGO.GetComponent<TrayView3D>();
        // Tray sits CLOSER to the camera than the board (TrayDistance < the
        // board's fit distance) so it draws in FRONT of the stack instead of
        // being occluded by the tiles. Scaled down by TrayDistance/HudDistance
        // so its on-screen size is unchanged despite the nearer placement.
        const float TrayDistance = 9f;
        PositionInFrontOfCamera(trayRootGO.transform, camera, new Vector2(0.5f, 0.75f), TrayDistance);
        trayRootGO.transform.localScale = Vector3.one * (TrayDistance / HudDistance);

        // Soft drop shadow (reuses the board tiles' TileShadow.mat) behind the
        // whole tray, so it reads as sitting raised above the felt.
        var traySlotShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileShadow.mat");
        RequireNotNull(traySlotShadowMaterial, "Assets/Materials/TileShadow.mat (run TileMaterialGenerator first)");
        var trayShadowGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        trayShadowGO.name = "Shadow";
        Object.DestroyImmediate(trayShadowGO.GetComponent<Collider>());
        trayShadowGO.transform.SetParent(trayRootGO.transform, false);
        trayShadowGO.transform.localPosition = new Vector3(0.05f, -0.07f, 0.09f);
        trayShadowGO.transform.localScale = new Vector3(trayContainerWidth * 1.15f, trayFrameHeight * 1.4f, 1f);
        trayShadowGO.GetComponent<MeshRenderer>().sharedMaterial = traySlotShadowMaterial;
        trayShadowGO.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Rounded wood frame behind the slots (matches the reference's rounded
        // tray corners). The 4 slots sit on it as warm recessed parts.
        float containerWidth = trayContainerWidth;
        float frameHeight = trayFrameHeight;
        var frameMesh = SaveRoundedTrayMesh("Assets/Meshes/TrayFrame.asset", containerWidth, frameHeight, 0.12f, 0.14f);
        var trayContainerGO = new GameObject("TrayContainer", typeof(MeshFilter), typeof(MeshRenderer));
        trayContainerGO.transform.SetParent(trayRootGO.transform, false);
        trayContainerGO.transform.localPosition = new Vector3(0f, 0f, 0.06f); // behind the slots (+Z, away from camera)
        trayContainerGO.GetComponent<MeshFilter>().sharedMesh = frameMesh;
        trayContainerGO.GetComponent<MeshRenderer>().sharedMaterial = trayBorderMaterial; // amber border - shows as a ring around the smaller dark body below

        // Dark body inset within the amber frame above (smaller + nearer camera,
        // so the amber only shows as a border) - matches the reference's flat
        // dark tray with a colored edge, instead of a uniform wood-grain plank.
        // (recessMaterial is loaded again below for the individual slot cells -
        // loaded here too since this runs before that later declaration.)
        var trayBodyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrayBody.mat");
        RequireNotNull(trayBodyMaterial, "Assets/Materials/TrayBody.mat (run WoodUiGenerator first)");
        const float TrayBorderThickness = 0.07f;
        var trayBodyMesh = SaveRoundedTrayMesh("Assets/Meshes/TrayBody.asset",
            containerWidth - TrayBorderThickness * 2f, frameHeight - TrayBorderThickness * 2f, 0.12f, 0.11f);
        var trayBodyGO = new GameObject("TrayBody", typeof(MeshFilter), typeof(MeshRenderer));
        trayBodyGO.transform.SetParent(trayRootGO.transform, false);
        trayBodyGO.transform.localPosition = new Vector3(0f, 0f, 0.04f); // between the amber frame (0.06) and the slots (~0)
        trayBodyGO.GetComponent<MeshFilter>().sharedMesh = trayBodyMesh;
        trayBodyGO.GetComponent<MeshRenderer>().sharedMaterial = trayBodyMaterial;

        var anchors = new Transform[TraySlotCount];
        float startX = -(TraySlotCount - 1) * TraySlotSpacing / 2f;
        for (int i = 0; i < TraySlotCount; i++)
        {
            var anchorGO = new GameObject("Slot" + i);
            anchorGO.transform.SetParent(trayRootGO.transform, false);
            anchorGO.transform.localPosition = new Vector3(startX + i * TraySlotSpacing, 0f, 0f);
            anchors[i] = anchorGO.transform;
        }

        var recessMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrayRecess.mat");
        RequireNotNull(recessMaterial, "Assets/Materials/TrayRecess.mat (run WoodUiGenerator first)");
        var traySlotPrefab = BuildTraySlotPrefab(recessMaterial, TraySlotSize);
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

        // ------------------
        // Level-start screen (mockup's left phone) - shown first, hides the
        // HUD below until Play is tapped.
        // ------------------
        var hudObjects = new[]
        {
            scoreRootGO, trayRootGO, backButtonGO, menuButtonGO,
            shuffleButtonGO, hintButtonGO, undoButtonGO,
        };
        BuildLevelStartScreen(camera, gameController, hudObjects, hintIcon, undoIcon, shuffleIcon, hudButtonFaceMaterial);

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

    // Soft drop shadow (reuses the board tiles' TileShadow.mat) behind a
    // button, sized relative to its own Face scale - shared by
    // CreateHudButton3D and CreateVisualIconButton3D so every button in the
    // HUD reads as raised off the felt instead of painted flat onto it.
    private static void AddButtonDropShadow(Transform buttonRoot, float faceScale)
    {
        var shadowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileShadow.mat");
        RequireNotNull(shadowMaterial, "Assets/Materials/TileShadow.mat (run TileMaterialGenerator first)");
        var shadowGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadowGO.name = "Shadow";
        Object.DestroyImmediate(shadowGO.GetComponent<Collider>());
        shadowGO.transform.SetParent(buttonRoot, false);
        shadowGO.transform.localPosition = new Vector3(0.05f * faceScale, -0.07f * faceScale, 0.06f);
        shadowGO.transform.localScale = new Vector3(faceScale * 1.35f, faceScale * 1.35f, 1f);
        shadowGO.GetComponent<MeshRenderer>().sharedMaterial = shadowMaterial;
        shadowGO.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static GameObject CreateHudButton3D(
        Camera camera, Material cardMaterial, Material badgeMaterial, Vector2 viewportPos, GameController gameController,
        System.Type hudComponentType, Sprite iconSprite,
        bool locked = false, Material lockedFaceMaterial = null, string lockedLabel = null,
        Color? iconColorOverride = null)
    {
        // Empty, unrotated root: PressScaleButton3D requires a BoxCollider
        // sized to the button's footprint, and every child below (icon,
        // badge) is positioned assuming an unrotated parent.
        var buttonGO = new GameObject(hudComponentType.Name);
        PositionInFrontOfCamera(buttonGO.transform, camera, viewportPos, HudDistance);

        AddButtonDropShadow(buttonGO.transform, faceScale: 0.99f);

        // Flat alpha-cutout quad, not a Cylinder cap: a built-in Cylinder's cap
        // UVs are not a clean radial mapping, so the disc's dark-center/tan-rim
        // gradient rendered as a lopsided blob instead of a symmetric ring even
        // though the source texture is a perfect circle. cardMaterial
        // (HudButtonFace.mat) already bakes its own circular alpha cutout, so
        // this only needs the same flat camera-facing quad treatment already
        // used for the icon/badge/lock glyphs below.
        var faceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceGO.name = "Face";
        faceGO.transform.SetParent(buttonGO.transform, false);
        faceGO.transform.localScale = new Vector3(0.99f, 0.99f, 1f); // 0.45 * 2.2 - buttons enlarged for tap-target size and visual prominence
        Object.DestroyImmediate(faceGO.GetComponent<MeshCollider>());
        // Locked buttons use the dimmed/desaturated Style-A face bake (mockup's
        // .chrome-btn.is-locked) instead of the normal amber-ring face.
        faceGO.GetComponent<MeshRenderer>().sharedMaterial = locked ? lockedFaceMaterial : cardMaterial;

        var pressButton = buttonGO.AddComponent<PressScaleButton3D>(); // RequireComponent auto-adds a BoxCollider, default-sized - must be resized to the disc's footprint
        var buttonCollider = buttonGO.GetComponent<BoxCollider>();
        buttonCollider.size = new Vector3(0.99f, 0.99f, 0.33f);
        SetField(pressButton, "_targetCamera", camera);

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        iconGO.transform.SetParent(buttonGO.transform, false);
        Object.DestroyImmediate(iconGO.GetComponent<MeshCollider>());
        // Real root cause of the whole icon-sizing saga: these buttons sit
        // near the BOTTOM of the screen, far from viewport center - at that
        // far-off-center position, perspective projection
        // means a child positioned at a different local Z than its parent
        // shifts noticeably in screen-space (parallax), not just toward/away
        // from camera. Even the small -0.6 offset used here previously was
        // enough to shift the icon down far enough that its top portion
        // landed outside/below the disc face instead of centered on it -
        // confirmed by deliberately exaggerating the offset to -3 and
        // watching the icon visibly slide down the screen. Zero offset (same
        // Z as the face) removes the parallax entirely; draw-order is instead
        // guaranteed by SetAlwaysOnTop below, not by an actual depth gap.
        // Locked buttons show a "Lv. N" caption inside the same disc, below the
        // icon (see the locked label block below) - shift the icon up a bit so
        // it doesn't collide with that text. Y-only, same Z as the face, so
        // this doesn't reintroduce the Z-parallax bug described above.
        iconGO.transform.localPosition = Vector3.zero; // "Lv. N" now sits below the disc, not inside it (see the locked label block below) - icon no longer needs to make room for it
        // Real root cause of every earlier size/visibility mismatch: this quad
        // used a uniform Vector3.one*scale (including Z), unlike every other
        // working flat quad in this file (e.g. Face), which
        // use (x, y, 1) - a Quad mesh has no Z-extent, but scaling Z to
        // anything other than 1 here visibly clipped off the half of the
        // quad farther from the local origin (confirmed by isolating a
        // solid-color, no-texture version of this material and watching it
        // render as a half-height rectangle instead of a full square, even
        // though Transform/renderer bounds reported the full, correctly
        // centered size). Reference icons fill ~66% of the disc's diameter,
        // and this icon's own PNG content fills ~67% of its own quad, so the
        // quad should be almost the same size as the disc itself.
        // 0.62, not 0.97: the icon glyph fills ~67% of its own PNG, so at 0.97
        // it nearly filled the whole disc (read as "too huge"). At 0.62 the
        // glyph sits ~42% of the disc diameter with padding around it, like the
        // mockup's small line icons inside the button.
        iconGO.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
        var iconMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // unlit: see Bronze.mat comment above
        URPMaterialUtil.SetTransparent(iconMaterial);
        URPMaterialUtil.SetAlwaysOnTop(iconMaterial);
        iconMaterial.SetTexture("_BaseMap", iconSprite.texture);
        var iconTintColor = locked ? MutedIconTint : (iconColorOverride ?? CreamHudText);
        iconMaterial.SetColor("_BaseColor", iconTintColor); // the glyph pixels are opaque white with alpha shaping - untinted, they're invisible against the dark button face. ControlButtonUsesDisplay3D seeds its MeshRendererTint with the same color so this doesn't get reset to white on the first SetRemaining() call.
        // Must be saved as a real asset, like TileMeshGenerator's TileIcon.mat -
        // a transparent material that only ever exists embedded in the scene
        // (never an AssetDatabase asset) renders its alpha-cutout shape as a
        // solid opaque quad on-device, even though it looks correct in the
        // Editor (confirmed by comparison: TileIcon.mat's glyphs render fine,
        // this one didn't until saved the same way).
        Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(iconMaterial, "Assets/Materials/HudIcon_" + hudComponentType.Name + ".mat");
        iconGO.GetComponent<MeshRenderer>().material = iconMaterial;

        TextMeshPro badgeText = null;
        if (!locked)
        {
            var badgeBgGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            badgeBgGO.name = "BadgeBackground";
            badgeBgGO.transform.SetParent(buttonGO.transform, false);
            badgeBgGO.transform.localPosition = new Vector3(0.40f, 0.40f, -0.72f); // pulled in from the naive 0.32*2.2=0.70 so the badge overlaps the disc rim like the reference, instead of floating detached from it
            badgeBgGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            badgeBgGO.transform.localScale = new Vector3(0.37f, 0.11f, 0.37f); // 0.17/0.05 * 2.2
            Object.DestroyImmediate(badgeBgGO.GetComponent<Collider>());
            badgeBgGO.GetComponent<MeshRenderer>().sharedMaterial = badgeMaterial;

            var badgeGO = new GameObject("BadgeText", typeof(TextMeshPro));
            badgeGO.transform.SetParent(buttonGO.transform, false);
            // -0.87, not exactly -0.72: needs to clear BadgeBackground's own
            // front face. Cylinder height scale is 0.11 (default height 2 ->
            // half-height 0.11 after rotation becomes the Z-depth), so its
            // front face sits at -0.72-0.11=-0.83, not -0.72 - missing this
            // by enlarging the badge without correspondingly moving the text
            // is exactly what embedded the "3" behind the now-thicker opaque
            // disc (confirmed on-device: badge showed a bare circle, no
            // digit, right after the button-enlarge pass). -0.87 clears the
            // front face with the same ~0.04 margin used before enlarging.
            badgeGO.transform.localPosition = new Vector3(0.40f, 0.40f, -0.87f);
            badgeText = badgeGO.GetComponent<TextMeshPro>();
            badgeText.text = "3";
            badgeText.fontSize = 1.1f; // 0.5 * 2.2
            badgeText.color = Color.white; // sits on the red BadgeBackground circle now, not the button face
            badgeText.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            // Locked buttons show no badge at all (mockup's .chrome-btn.is-locked
            // has no lock-pip overlay - the dimmed disc face + muted icon alone
            // communicate the locked state) plus a level-gate caption below the
            // disc. Static/visual only - no real level-progression system.
            // "Lv. N" caption sits as plain text directly below the disc - no
            // background pill and not inside the disc either (both tried
            // earlier); matches the reference exactly: bare white text under
            // the button, same treatment as a UI caption under an icon.
            var levelLabelGO = new GameObject("LevelLabel", typeof(TextMeshPro));
            levelLabelGO.transform.SetParent(buttonGO.transform, false);
            levelLabelGO.transform.localPosition = new Vector3(0f, -0.68f, -0.08f); // below the disc (radius ~0.495)
            var levelLabelText = levelLabelGO.GetComponent<TextMeshPro>();
            levelLabelText.text = lockedLabel;
            levelLabelText.fontSize = 0.55f;
            levelLabelText.color = CreamHudText;
            levelLabelText.alignment = TextAlignmentOptions.Center;
        }

        var usesDisplay = buttonGO.AddComponent<ControlButtonUsesDisplay3D>();
        SetField(usesDisplay, "_button", pressButton);
        SetField(usesDisplay, "_faceRenderer", faceGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_iconRenderer", iconGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_badgeText", badgeText);
        SetFieldColor(usesDisplay, "_iconTintColor", iconTintColor);

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        SetField(hudComponent, "_button", pressButton);
        SetField(hudComponent, "_usesDisplay", usesDisplay);
        SetField(hudComponent, "_gameController", gameController);

        return buttonGO;
    }

    // A smaller, non-interactive bronze disc + icon glyph - no
    // PressScaleButton3D/BoxCollider, no badge/lock chrome. For HUD elements
    // that are visual-only for now (BackButton, MenuButton): styled like the
    // full CreateHudButton3D discs so they read as the same chrome family,
    // without the tap-target/state-wiring that a real control button needs.
    private static GameObject CreateVisualIconButton3D(
        Camera camera, Material faceMaterial, Vector2 viewportPos, string name, Sprite iconSprite)
    {
        var buttonGO = new GameObject(name);
        PositionInFrontOfCamera(buttonGO.transform, camera, viewportPos, HudDistance);

        AddButtonDropShadow(buttonGO.transform, faceScale: 0.55f);

        var faceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceGO.name = "Face";
        faceGO.transform.SetParent(buttonGO.transform, false);
        faceGO.transform.localScale = new Vector3(0.55f, 0.55f, 1f); // smaller than the 0.99 control-button discs - secondary chrome
        Object.DestroyImmediate(faceGO.GetComponent<MeshCollider>());
        faceGO.GetComponent<MeshRenderer>().sharedMaterial = faceMaterial;

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        iconGO.transform.SetParent(buttonGO.transform, false);
        Object.DestroyImmediate(iconGO.GetComponent<MeshCollider>());
        iconGO.transform.localPosition = Vector3.zero; // same Z as Face - avoids the parallax bug documented on CreateHudButton3D's Icon
        iconGO.transform.localScale = new Vector3(0.36f, 0.36f, 1f); // ~65% of the face, matching CreateHudButton3D's icon/face ratio
        var iconMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        URPMaterialUtil.SetTransparent(iconMaterial);
        URPMaterialUtil.SetAlwaysOnTop(iconMaterial);
        iconMaterial.SetTexture("_BaseMap", iconSprite.texture);
        iconMaterial.SetColor("_BaseColor", CreamHudText);
        AssetDatabase.CreateAsset(iconMaterial, "Assets/Materials/HudIcon_" + name + ".mat");
        iconGO.GetComponent<MeshRenderer>().material = iconMaterial;

        return buttonGO;
    }

    // Level-start screen (mockup's left phone): "Now playing / Level 6" badge,
    // stars, goal text, carryover hint/undo/shuffle chips, and a big gold Play
    // button. Placed on a camera-parallel plane NEARER than the felt backdrop
    // (world z=9) so it draws in front of it - the felt itself is the screen's
    // background, matching the mockup's felt screen. LevelStartScreen3D hides
    // the passed-in hudObjects until Play is tapped, then reveals them and
    // calls GameController.BeginLevel.
    private static void BuildLevelStartScreen(
        Camera camera, GameController gameController, GameObject[] hudObjects,
        Sprite hintIcon, Sprite undoIcon, Sprite shuffleIcon, Material discFaceMaterial)
    {
        const float D = 7f; // nearer than the felt backdrop (world z=9) so it renders in front

        var root = new GameObject("LevelStartScreen");
        PositionInFrontOfCamera(root.transform, camera, new Vector2(0.5f, 0.5f), D);

        // Full-screen radial-felt backdrop for this screen, parented to the
        // camera and sized to exactly fill the viewport so the baked glow +
        // vignette map screen-to-texture. The shared world felt quad can't do
        // this: it's scale-60, so the camera only ever sees its near-uniform
        // centre, and it's Lit (scene ambient lifts its dark vignette to a flat
        // mid-green). This one is Unlit so the baked darks survive.
        const float BgD = 8f; // behind the content plane (D=7), in front of the world felt (z=9)
        var bgGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgGO.name = "Backdrop";
        Object.DestroyImmediate(bgGO.GetComponent<Collider>());
        bgGO.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, BgD));
        bgGO.transform.rotation = camera.transform.rotation;
        bgGO.transform.SetParent(root.transform, true);
        float bgH = 2f * BgD * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float bgW = bgH * camera.aspect;
        bgGO.transform.localScale = new Vector3(bgW * 1.06f, bgH * 1.06f, 1f); // slight overscan to guarantee full coverage
        var feltTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Felt.png");
        var bgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        bgMat.SetTexture("_BaseMap", feltTex);
        bgMat.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(bgMat, "Assets/Materials/FeltScreen.mat");
        bgGO.GetComponent<MeshRenderer>().sharedMaterial = bgMat;

        var creamDim = new Color(0.796f, 0.749f, 0.643f); // #CBBFA4 (mockup --cream-dim)
        var inkDim = new Color(0.604f, 0.573f, 0.494f);   // #9A927E (mockup --ink-dim)
        var goldInk = new Color(0.227f, 0.141f, 0.063f);  // dark ink on the gold Play button

        // Places a new child at a viewport point on the level-start plane,
        // parented under the root (keeping the camera-facing pose).
        Transform Place(GameObject go, Vector2 vp)
        {
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, D));
            go.transform.rotation = camera.transform.rotation;
            go.transform.SetParent(root.transform, true);
            return go.transform;
        }

        TextMeshPro Label(string labelName, Vector2 vp, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(labelName, typeof(TextMeshPro));
            Place(go, vp);
            var t = go.GetComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = true;
            return t;
        }

        // NOTE ON SCALE: at this depth 1 world unit ≈ 540px on-screen for
        // meshes/quads, but a TMP fontSize of 1.0 ≈ only ~50px of cap height -
        // so text needs ~10x larger numeric values than shape scales to look
        // balanced. Sizes below are calibrated to that (bigger text, smaller
        // discs) to fix the earlier "huge icons, tiny text" imbalance.
        Label("Eyebrow", new Vector2(0.5f, 0.605f), "NOW PLAYING", 0.5f, creamDim, FontStyles.Normal);

        // Level badge: dark amber-ring disc (same chrome as the HUD buttons) + "6".
        // Sizes here are deliberately small - at this camera distance (D=7,
        // nearer than the HUD's D=11) world units render large, so a modest
        // badge/chip/button footprint needs small localScale/fontSize values.
        var badgeGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        badgeGO.name = "LevelBadge";
        Object.DestroyImmediate(badgeGO.GetComponent<Collider>());
        Place(badgeGO, new Vector2(0.5f, 0.55f));
        badgeGO.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        badgeGO.GetComponent<MeshRenderer>().sharedMaterial = discFaceMaterial;
        var badgeNum = Label("BadgeNum", new Vector2(0.5f, 0.55f), "6", 1.6f, CreamHudText, FontStyles.Bold);
        badgeNum.transform.localPosition += new Vector3(0f, 0f, -0.05f); // toward camera, in front of the disc face

        Label("Title", new Vector2(0.5f, 0.478f), "Level 6", 1.15f, CreamHudText, FontStyles.Bold);

        // Two filled gold stars + one muted (unearned) star - real generated
        // star sprites, NOT ★/☆ glyphs (LiberationSans, the only font in the
        // project, has no star glyph so those render as tofu boxes).
        BuildStars(camera, root.transform, D, new Vector2(0.5f, 0.43f));

        var goal = Label("Goal", new Vector2(0.5f, 0.385f), "Clear every pair on the board before you run out of moves.", 0.55f, inkDim, FontStyles.Normal);
        goal.enableWordWrapping = true;
        goal.rectTransform.sizeDelta = new Vector2(1.7f, 1f); // ~2 wrapped lines like the mockup (was 4.5, wider than the screen so it never wrapped)

        BuildCarryoverChip(camera, root.transform, D, discFaceMaterial, hintIcon,    "icon_hint",    new Vector2(0.37f, 0.29f), "0", GoldIconTint, creamDim);
        BuildCarryoverChip(camera, root.transform, D, discFaceMaterial, undoIcon,    "icon_undo",    new Vector2(0.50f, 0.29f), "3", CreamHudText, creamDim);
        BuildCarryoverChip(camera, root.transform, D, discFaceMaterial, shuffleIcon, "icon_shuffle", new Vector2(0.63f, 0.29f), "3", CreamHudText, creamDim);

        // Play button: gold rounded pill + "PLAY". Narrower than before (was
        // 2.3, which spanned edge-to-edge) so it sits with side margins like
        // the mockup, and raised slightly for a bottom margin.
        var playMesh = SaveRoundedTrayMesh("Assets/Meshes/PlayButton.asset", 1.75f, 0.3f, 0.1f, 0.13f);
        var playGO = new GameObject("PlayButton", typeof(MeshFilter), typeof(MeshRenderer));
        Place(playGO, new Vector2(0.5f, 0.145f));
        playGO.GetComponent<MeshFilter>().sharedMesh = playMesh;
        // Dedicated non-emissive gold (Gold.mat glows via _EMISSION, which blew
        // the button out to a flat bright yellow) so it reads as the mockup's
        // gradient gold pill.
        var playGold = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        playGold.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Gold.png"));
        playGold.SetColor("_BaseColor", Color.white);
        playGold.SetFloat("_Smoothness", 0.35f);
        playGold.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(playGold, "Assets/Materials/PlayGold.mat");
        playGO.GetComponent<MeshRenderer>().sharedMaterial = playGold;
        var playCollider = playGO.AddComponent<BoxCollider>();
        playCollider.size = new Vector3(1.75f, 0.3f, 0.3f);
        var playButton = playGO.AddComponent<PressScaleButton3D>();
        SetField(playButton, "_targetCamera", camera);

        var playText = Label("PlayText", new Vector2(0.5f, 0.145f), "PLAY", 0.95f, goldInk, FontStyles.Bold);
        playText.transform.localPosition += new Vector3(0f, 0f, -0.12f); // in front of the slab

        var levelStart = root.AddComponent<LevelStartScreen3D>();
        SetField(levelStart, "_playButton", playButton);
        SetField(levelStart, "_gameController", gameController);
        SetFieldArray(levelStart, "_gameHudObjects", hudObjects);
    }

    // One carryover chip on the level-start screen: a small dark disc with a
    // hint/undo/shuffle icon and a count label below (mockup's .carryover-item).
    private static void BuildCarryoverChip(
        Camera camera, Transform parent, float distance, Material discFaceMaterial,
        Sprite icon, string iconKey, Vector2 vp, string count, Color iconTint, Color countColor)
    {
        // The chip is built in "unit" space (face = 1 unit) then scaled down
        // by chipRoot so face + icon + count shrink together with one factor.
        var chipRoot = new GameObject("Chip_" + iconKey);
        chipRoot.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, distance));
        chipRoot.transform.rotation = camera.transform.rotation;
        chipRoot.transform.SetParent(parent, true);
        chipRoot.transform.localScale = Vector3.one * 0.17f; // ~92px disc (was 0.22 - too big vs the text)

        var faceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceGO.name = "Face";
        Object.DestroyImmediate(faceGO.GetComponent<Collider>());
        faceGO.transform.SetParent(chipRoot.transform, false);
        faceGO.transform.localScale = Vector3.one;
        faceGO.GetComponent<MeshRenderer>().sharedMaterial = discFaceMaterial;

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        Object.DestroyImmediate(iconGO.GetComponent<Collider>());
        iconGO.transform.SetParent(chipRoot.transform, false);
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        iconGO.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // ~50% of the disc, with padding (was 0.62 - too big)
        var iconMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        URPMaterialUtil.SetTransparent(iconMat);
        URPMaterialUtil.SetAlwaysOnTop(iconMat);
        iconMat.SetTexture("_BaseMap", icon.texture);
        iconMat.SetColor("_BaseColor", iconTint);
        AssetDatabase.CreateAsset(iconMat, "Assets/Materials/LevelStartChip_" + iconKey + ".mat");
        iconGO.GetComponent<MeshRenderer>().material = iconMat;

        var countGO = new GameObject("Count", typeof(TextMeshPro));
        countGO.transform.SetParent(chipRoot.transform, false);
        countGO.transform.localPosition = new Vector3(0f, -0.95f, -0.05f);
        var t = countGO.GetComponent<TextMeshPro>();
        t.text = count;
        t.fontSize = 3.5f; // large in chip space; chipRoot's 0.17 scale brings it to ~24px on-screen
        t.color = countColor;
        t.alignment = TextAlignmentOptions.Center;
    }

    // Three rating stars on the level-start screen (mockup's .stars): two
    // filled gold, one muted (unearned). Uses a generated 5-point star sprite
    // because LiberationSans (the project's only font) has no star glyph.
    private static void BuildStars(Camera camera, Transform parent, float distance, Vector2 vp)
    {
        var starSprite = GetStarSprite();
        var gold = new Color(0.96f, 0.78f, 0.36f);  // #F5C75C
        var muted = new Color(0.44f, 0.32f, 0.18f);  // dim unearned star

        var starsRoot = new GameObject("Stars");
        starsRoot.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, distance));
        starsRoot.transform.rotation = camera.transform.rotation;
        starsRoot.transform.SetParent(parent, true);

        var tints = new[] { gold, gold, muted };
        float[] xs = { -0.16f, 0f, 0.16f }; // ~86px spacing (was 0.42 - stars too far apart and too big)
        for (int i = 0; i < 3; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Star" + i;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(starsRoot.transform, false);
            q.transform.localPosition = new Vector3(xs[i], 0f, -0.02f);
            q.transform.localScale = new Vector3(0.13f, 0.13f, 1f); // ~70px star (was 0.34 - far too big)
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            URPMaterialUtil.SetTransparent(m);
            URPMaterialUtil.SetAlwaysOnTop(m);
            m.SetTexture("_BaseMap", starSprite.texture);
            m.SetColor("_BaseColor", tints[i]);
            AssetDatabase.CreateAsset(m, "Assets/Materials/LevelStartStar" + i + ".mat");
            q.GetComponent<MeshRenderer>().material = m;
        }
    }

    // Bakes a filled 5-point white star (alpha-shaped) once, as a Sprite so
    // the alpha survives Android compression (same settings the icon glyphs use).
    private static Sprite GetStarSprite()
    {
        const string path = "Assets/Textures/HudIcons/star.png";
        const int S = 128;
        var pts = new Vector2[10];
        float c = S / 2f, ro = S * 0.47f, ri = S * 0.20f;
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI / 2f + i * Mathf.PI / 5f; // first point straight up
            float rad = (i % 2 == 0) ? ro : ri;
            pts[i] = new Vector2(c + Mathf.Cos(ang) * rad, c + Mathf.Sin(ang) * rad);
        }
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "star" };
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            bool inside = PointInPolygon(x + 0.5f, y + 0.5f, pts);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
        }
        tex.Apply();
        Directory.CreateDirectory("Assets/Textures/HudIcons");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.sRGBTexture = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Standard ray-casting point-in-polygon test (used to rasterize the star).
    private static bool PointInPolygon(float px, float py, Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if (((v[i].y > py) != (v[j].y > py)) &&
                (px < (v[j].x - v[i].x) * (py - v[i].y) / (v[j].y - v[i].y) + v[i].x))
                inside = !inside;
        }
        return inside;
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

    private static void SetFieldColor(Object target, string fieldName, Color value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.colorValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFieldFloat(Object target, string fieldName, float value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.floatValue = value;
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
