using System.IO;
using GameClient.Data;
using GameClient.Presentation;
using GameClient.Presentation.Board3D;
using GameClient.Presentation.HUD;
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
    private static readonly Color GoldChrome = new Color(0.85f, 0.65f, 0.25f, 1f); // amber/gold border
    private static readonly Color DarkWood = new Color(0.18f, 0.10f, 0.05f, 1f); // dark wood interior
    private static readonly Color BadgeRed = new Color(0.80f, 0.20f, 0.20f, 1f); // notification badge
    private static readonly Color GoldInkText = new Color(0.141f, 0.082f, 0.020f); // #241505, matches the approved mockup's button ink
    private static readonly Color StarGold = new Color(0.96f, 0.78f, 0.36f); // #F5C75C, earned star
    private static readonly Color StarMuted = new Color(0.44f, 0.32f, 0.18f); // dim unearned star
    private static readonly Color SettingsLabelTint = new Color(0.88f, 0.83f, 0.68f); // pause menu's "SETTINGS" eyebrow label - brightened from an earlier (0.73,0.68,0.55) that was too low-contrast to actually read against the card at its small size

    // Premium display font (Cinzel OFL, Pass E) for headings/numbers - LEVEL,
    // score, PLAY. Body text keeps LiberationSans (TMP default).
    private static TMPro.TMP_FontAsset _displayFont;
    private static TMPro.TMP_FontAsset DisplayFont =>
        _displayFont != null ? _displayFont
        : (_displayFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/Cinzel SDF.asset"));
    private static readonly Color MutedIconTint = new Color(0.541f, 0.502f, 0.447f, 1f); // #8A8072 - mockup's .chrome-btn.is-locked svg stroke

    // BoardView3D.FitCameraToBoard's natural fit distance for the current
    // board layout (6-column layer 0, portrait screen, width is the binding
    // axis) is ~10.4 world units at the wider 48deg FOV set below (was ~13.6
    // at the old 40deg FOV/0.3 margin) - HudDistance must stay safely LESS
    // than that so HUD elements (parented to the camera at this fixed local
    // Z) render in FRONT of the board instead of behind it.
    // NOTE: if the board layout ever becomes variable-sized again, a static
    // margin like this is fragile - re-derive HudDistance dynamically from
    // BoardView3D's actual fit distance instead of hardcoding it here.
    private const float HudDistance = 9f;
    private const float PopupDistance = 7.35f; // closer than HudDistance so the modal reads larger, in front of the board (also scaled by the same 0.8175 ratio)
    // Constant orthographic zoom for the board. The camera-parented HUD's on-screen
    // positions are baked (ViewportToWorldPoint) against the camera's orthographicSize
    // at build time, so the board MUST render at this same value at runtime or the HUD
    // scales off-screen. Tuned so tiles render at the ~147px size the user approved on
    // the earlier "after_tilt_board" build (5.0 gave a smaller ~136px face; lower zoom
    // = bigger tiles). Trade-off: at this zoom a 7-wide board overflows the width, so
    // the largest board is 6-wide (~70 tiles), and every OTHER camera-facing screen
    // (level-start / level-select / pause) renders ~8% larger since they share it.
    private const float FixedBoardOrthoSize = 4.0f;

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true;
        // Calibrate the camera at the SAME fixed zoom the board renders at, so every
        // PositionInFrontOfCamera call below bakes HUD positions that stay correct at
        // runtime (BoardView3D also renders at FixedBoardOrthoSize).
        camera.orthographicSize = FixedBoardOrthoSize;
        // 40 -> 48: the board's width (6 columns on a narrow portrait FOV) was
        // the binding fit constraint, forcing the camera much farther back
        // than its height needed - a wider FOV lets the board fit at a closer
        // distance (bigger, less dead space) without reshaping the board.
        // HudDistance/TrayDistance/PopupDistance below are scaled by the same
        // tan(20deg)/tan(24deg) ratio so every HUD element's apparent screen
        // size is unchanged despite the wider lens - only the board grows.
        camera.fieldOfView = 48f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.450f, 0.530f, 0.640f); // steel blue vignette edge
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

        // Felt-table backdrop: was a large FIXED WORLD-SPACE quad (scale 60,
        // Lit) behind the board - at that scale the camera only ever saw the
        // texture's near-uniform centre (the radial spotlight glow never
        // showed), and the Lit shader let scene ambient wash out its dark
        // vignette into a flat green wash. Switched to the same technique the
        // level-start screen already uses successfully: a camera-PARENTED
        // quad sized to exactly fill the viewport (so the bake maps
        // screen-to-texture regardless of where BoardView3D later moves the
        // camera) with an Unlit material (so the baked darks survive scene
        // ambient). GameBackdropDistance (12) sits beyond both the HUD plane
        // (HudDistance=9) and the farthest board tile (~10.1 at this FOV/
        // board size), so it always renders behind everything.
        const float GameBackdropDistance = 13f;
        var feltScreenMat = GetOrCreateFeltScreenMaterial();
        BuildScreenFillingBackdrop(camera, camera.transform, GameBackdropDistance, feltScreenMat, "FeltBackground");
        // BuildLeafDecoration removed as requested: leaves removed from background

        var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CardBody.mat");
        RequireNotNull(cardMaterial, "Assets/Materials/CardBody.mat as Material");
        var hudButtonFaceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HudButtonFace.mat");
        RequireNotNull(hudButtonFaceMaterial, "Assets/Materials/HudButtonFace.mat as Material (run WoodUiGenerator first)");
        var hudTopButtonFaceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HudTopButtonFace.mat");
        RequireNotNull(hudTopButtonFaceMaterial, "Assets/Materials/HudTopButtonFace.mat as Material");
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
        // width exactly. Also used further down to build the tray row itself.
        // Slots are PORTRAIT, matching the real board tile aspect
        // (CardStyle.CardAspectRatio) so a collected tile looks like the same
        // object on the board and in the tray (fix spec section 2).
        const int TraySlotCount = 4;    // pair-match tray: 4 slots (matches BoardState.MaxTraySize)
        const float TraySlotWidth = 0.56f; // was 0.52 - slight bump per user request; height/frame/progress-bar width all follow automatically
        float TraySlotHeight = TraySlotWidth / CardStyle.CardAspectRatio; // portrait, ~0.76
        // Tight, even spacing (fix spec section 5): the inter-slot gap and the
        // left/right frame padding are both small and roughly equal, so the 4
        // slots fill the container as one strip instead of floating with slack.
        const float TraySlotGap = 0.05f;
        const float TraySlotSpacing = TraySlotWidth + TraySlotGap;
        const float TrayEdgePadX = 0.07f; // tight left/right, ~= the inter-slot gap (fix 5)
        const float TrayEdgePadY = 0.045f; // tight top/bottom so slots fill ~90% of the container height (the tightened Z-stack below handles tilt parallax over the border)
        float trayContainerWidth = (TraySlotCount - 1) * TraySlotSpacing + TraySlotWidth + TrayEdgePadX * 2f;
        float trayFrameHeight = TraySlotHeight + TrayEdgePadY * 2f;

        // ------------------
        // Progress bar - simplified to just the bar (border/background/fill),
        // no milestone numbers or avatar - width pinned to the tray's width so
        // the two sit as one aligned unit.
        // ------------------
        float TrackWidth = trayContainerWidth;
        const float TrackHeight = 0.22f; // slimmer/flatter to match the mockup (fix spec section 3)
        const float ProgressMaxScore = 2000f; // matches ProgressBar3D._maxScore default

        // Computed from the topbar's own bottom EDGE (not a guessed centre-Y
        // gap - see ScreenHalfHeightFrac) so this row can never overlap the
        // back/menu discs regardless of either row's height.
        const float HudRowGap = 0.02f; // consistent edge-to-edge gap between every stacked HUD row below
        // Tighter than HudRowGap, used only for the score-bar-to-tray gap and the
        // tray-to-board gap: the default gap left a visibly empty felt strip below
        // the score bar, and tightening both here also raises the board's top
        // anchor (bandTop below), giving the pyramid more headroom above the
        // bottom button row.
        const float TightRowGap = 0.0f; // was 0.008, then 0.003 - tightened to touching so tall boards get maximum headroom above the button row
        const float TopbarFaceDiameter = 0.42f; // CreateVisualIconButton3D's face scale, must match its own call below (was 0.55 - looked oversized at the current zoom)
        // 0.92 -> 0.94: lifts the whole topbar/progress/tray cluster together
        // (everything below is computed FROM this anchor) to free up more
        // clearance before the board starts - the tray's bottom edge was
        // landing directly against the board's top row with no felt gap,
        // reading as the tray sitting on/overlapping the pyramid. (First
        // attempt at 0.96 fixed the gap but pushed the topbar discs to
        // clip against the very top of frame - confirmed via a live-editor
        // capture. 0.94 is a smaller lift, re-verify the gap is still real.)
        const float TopbarY = 0.94f;
        float topbarBottomEdge = TopbarY - ScreenHalfHeightFrac(camera, TopbarFaceDiameter, HudDistance);
        float progressHalfHeight = ScreenHalfHeightFrac(camera, TrackHeight + 0.12f, HudDistance);
        float progressBarY = topbarBottomEdge - HudRowGap - progressHalfHeight;

        var scoreRootGO = new GameObject("ProgressBar");
        PositionInFrontOfCamera(scoreRootGO.transform, camera, new Vector2(0.5f, progressBarY), HudDistance);

        // Rounded wood border with a vertical light/dark gradient bake (not a
        // flat colour - see WoodUiGenerator.ApplyVerticalGradientPanel) and a
        // near-transparent wood-tinted background inset within it (10%
        // opacity, so the felt shows through faintly) - same layered-mesh
        // technique as the tray's amber border/dark body. Gold fill sits in
        // front of both. A soft drop shadow (reusing the board tiles'
        // TileShadow.mat) sits behind everything so the whole bar reads as
        // raised off the felt instead of painted flat onto it.
        // 0.04 = the frame radius (0.16) minus the inner/body radius (0.12), so the
        // border STROKE is uniform width all the way around including the corners
        // (a smaller inset than the radius difference left the corners thinner/
        // broken); also ~2.2x the old hairline, a clean visible line (round-2 fix 1/2).
        const float ProgressBorderThickness = 0.04f;
        var progressBorderMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProgressBorder.mat");
        RequireNotNull(progressBorderMaterial, "Assets/Materials/ProgressBorder.mat (run WoodUiGenerator first)");
        var progressBackgroundMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProgressBackground.mat");
        RequireNotNull(progressBackgroundMaterial, "Assets/Materials/ProgressBackground.mat (run WoodUiGenerator first)");
        // Transparent + ZWrite off (alpha stays 1, so it still reads solid dark
        // jade): it must NOT write depth, so the coplanar gold fill + score text
        // aren't occluded by it (depth precision is too poor for a Z gap at this
        // distance). It still draws over the far board via the transparent queue.
        URPMaterialUtil.SetTransparent(progressBackgroundMaterial);
        progressBackgroundMaterial.renderQueue = 2900;
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
            TrackWidth + 0.12f - ProgressBorderThickness * 2f, TrackHeight + 0.12f - ProgressBorderThickness * 2f, 0.1f, 0.16f - ProgressBorderThickness);
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

        // gold fill (left-anchored, grown by ProgressBar3D). Z pulled out to
        // -0.05 (was 0.04, only a 0.01 gap from Background's 0.05) - at
        // HudDistance=9 that 0.01 gap silently lost the depth test against
        // Background (confirmed live: forcing Fill to z=-1 made it instantly
        // visible; perspective Z-buffers concentrate precision near the
        // camera, so a gap this small this far out can fall below the
        // buffer's effective resolution). Fill and Background overlap in
        // most of their screen pixels (Fill grows to cover the same area),
        // unlike Border vs Background which barely overlap (frame vs inset)
        // and never showed this problem despite an equally small 0.01 gap.
        // 0.10 is a comfortable margin, not a tuned minimum.
        var barFillGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        barFillGO.name = "Fill";
        // The fill is COPLANAR with the background (same Z 0.05) and its material
        // draws after it (render queue) so LEqual depth paints it over - no Z gap,
        // so no tilt-parallax. It can therefore fill the inner area edge-to-edge
        // (full inner height, pinned to the inner-left) with no gap/overflow.
        float progInnerW = TrackWidth + 0.12f - ProgressBorderThickness * 2f;
        float progInnerH = TrackHeight + 0.12f - ProgressBorderThickness * 2f;
        barFillGO.transform.SetParent(scoreRootGO.transform, false);
        // z=0.03: a TINY distinct gap IN FRONT of the background (0.05) - coplanar
        // transparent elements render unreliably here (proven: a coplanar fill/text
        // vanished), but a distinct in-front z renders like the HUD icons do. The
        // background no longer writes depth (transparent), so this small gap can't
        // fail a depth test, and 0.02 is small enough that tilt-parallax is
        // negligible - so the full-height fill stays inside the border (round-2 fix 1).
        float progFillRadius = 0.16f - ProgressBorderThickness;
        // Full inner width (was progInnerW - progFillRadius, deliberately leaving
        // the right rounded end always dark) - now that the fill's max score is
        // calibrated to the level's actual tile count, a full clear really does
        // mean 100%, so the fill should be able to cover the track edge-to-edge,
        // matching rounded end to rounded end, instead of always stopping one
        // corner-radius short of the right edge.
        float progUsableW = progInnerW;
        barFillGO.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        // Height is BAKED INTO the fill mesh (1.0 wide x progInnerH tall, radius =
        // progInnerH/2 = horizontal capsule). ProgressBar3D scales ONLY the X
        // (width), leaving Y=1, so the fill is ALWAYS full inner height with
        // rounded ends - unlike scaling a 1x1 rounded mesh, which produced a
        // floating ellipse that never touched the top/bottom (the reported bug).
        // Fill = a rounded capsule mesh built at the FULL usable width, scaled only
        // DOWN (0..1) by the fill fraction. Scaling DOWN compresses - it can never
        // BALLOON (the reported shape bug was caused by scaling a small mesh UP).
        // At 100% it's a perfect capsule; at lower fills a smaller rounded fill.
        // The rounded mesh faces the camera (unlike a Quad primitive, which faced
        // away and wouldn't render). Definitive fix for the recurring shape bug.
        Object.DestroyImmediate(barFillGO.GetComponent<Collider>());
        barFillGO.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/ProgressFill.asset", progUsableW, progInnerH, 0.05f, progFillRadius);
        barFillGO.transform.localScale = Vector3.one;
        barFillGO.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateNonEmissiveGoldMaterial(alwaysOnTop: true);

        var scoreGO = new GameObject("ScoreText", typeof(TextMeshPro));
        scoreGO.transform.SetParent(scoreRootGO.transform, false);
        scoreGO.transform.localPosition = new Vector3(0f, 0f, -0.15f); // vertically centered in the pill (was y=-0.06, nudged low); z pulls it well in FRONT of the fill/background so centered text has no visible parallax and no transparent-sort ambiguity
        var scoreText = scoreGO.GetComponent<TextMeshPro>();
        scoreText.text = "0";
        scoreText.color = CreamHudText;
        scoreText.fontSize = 1.05f; // was 0.9 - larger so it reads clearly centered on the dark track, like the mockup
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.alignment = TextAlignmentOptions.Center;
        if (DisplayFont != null) scoreText.font = DisplayFont; // Cinzel for the score number
        // TMP's synthetic Bold style is subtle on Cinzel (no true bold weight in
        // the SDF asset) - dilate the glyph edges further on this instance's own
        // material clone so the score number reads as genuinely bold, without
        // touching the shared font material other Cinzel text uses.
        scoreText.fontMaterial.SetFloat("_FaceDilate", 0.35f);

        var progressBar = scoreRootGO.AddComponent<ProgressBar3D>();
        SetField(progressBar, "_fillFilter", barFillGO.GetComponent<MeshFilter>());
        SetField(progressBar, "_label", scoreText);
        SetField(progressBar, "_gameController", gameController);
        // Capsule fill scaled DOWN by fraction. _trackWidth is the full usable
        // width (for the left-pin math); _fillHeight=1 (height baked into the mesh).
        SetFieldFloat(progressBar, "_trackWidth", progUsableW);
        SetFieldFloat(progressBar, "_fillHeight", 1f);
        SetFieldFloat(progressBar, "_maxScore", 400f); // TEMP: low so testing reaches 100% quickly; revert to 2000
        // _maxScore (2000) still uses the component's
        // serialized defaults.

        // Combo meter geometry is deferred: with the tray restored to this band
        // it would overlap. The ComboMeter3D component + GameController.ComboChanged
        // event remain for a later dedicated combo pass; match feedback for now is
        // the MatchCelebrationController burst on each tray pair.

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
        // ------------------
        // Common HUD Button Creation
        // ------------------
        var backButtonGO = CreateHudButton3D(
            camera, hudTopButtonFaceMaterial, null, new Vector2(0.09f, TopbarY), null,
            null, backIcon, faceScale: 0.42f, iconScale: 0.28f);
        var backButton = backButtonGO.GetComponent<PressScaleButton3D>();
        
        var menuButtonGO = CreateHudButton3D(
            camera, hudTopButtonFaceMaterial, null, new Vector2(0.91f, TopbarY), null,
            null, menuIcon, faceScale: 0.42f, iconScale: 0.24f);
        var menuButton = menuButtonGO.GetComponent<PressScaleButton3D>();

        // ------------------
        // Control bar (hint/undo/shuffle)
        // ------------------
        var hintIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_hint.png");
        var undoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_undo.png");
        var shuffleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_shuffle.png");
        RequireNotNull(hintIcon, "Assets/Textures/HudIcons/icon_hint.png as Sprite");
        RequireNotNull(undoIcon, "Assets/Textures/HudIcons/icon_undo.png as Sprite");
        RequireNotNull(shuffleIcon, "Assets/Textures/HudIcons/icon_shuffle.png as Sprite");

        var badgeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HudBadgeRed.mat");
        RequireNotNull(badgeMaterial, "Assets/Materials/HudBadgeRed.mat as Material");

        // x = 0.17 / 0.5 / 0.83 so the outer buttons' edges line up cleanly
        // Y is derived, not hardcoded: BottomButtonRowY = HudRowGap +
        // buttonHalfHeight puts the row's BOTTOM edge exactly HudRowGap (0.02)
        // above the screen's bottom edge - the same tight gap this row already
        // keeps above itself (via bandBottom below), instead of the old fixed
        // 0.105 which left a ~6.7%-tall dead strip beneath the row while the
        // gap to the board above was only ~2%. Confirmed via a headless-Editor
        // measurement that button diameter (faceScale 0.62) barely differs
        // between the "wrong" fieldOfView-based ScreenHalfHeightFrac formula
        // and the camera's true orthographic frustum (0.0383 vs 0.0384) - the
        // two constants (HudDistance/fieldOfView) were apparently tuned to
        // match the orthographic size, so this derivation is safe to reuse.
        const float ButtonFaceWorldDiameter = 0.99f * 0.62f;
        float buttonHalfHeight = ScreenHalfHeightFrac(camera, ButtonFaceWorldDiameter, HudDistance);
        float BottomButtonRowY = HudRowGap + buttonHalfHeight;
        var shuffleButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.17f, BottomButtonRowY), gameController, typeof(ShuffleButton3D), shuffleIcon,
            lockedFaceMaterial: hudButtonFaceLockedMaterial, faceScale: 0.62f, iconScale: 0.24f);
        var hintButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.5f, BottomButtonRowY), gameController, typeof(HintButton3D), hintIcon,
            lockedFaceMaterial: hudButtonFaceLockedMaterial, faceScale: 0.62f, iconScale: 0.24f);
        var undoButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.83f, BottomButtonRowY), gameController, typeof(UndoButton3D), undoIcon,
            lockedFaceMaterial: hudButtonFaceLockedMaterial, faceScale: 0.62f, iconScale: 0.24f);

        // ------------------
        // Tray - row of fixed 3D slots in front of the board (restored: the game
        // uses the tray-collection mechanic per the corrected master plan). Sizing
        // consts (TraySlotCount/Size/Spacing, trayContainerWidth/trayFrameHeight)
        // are declared earlier near the progress bar so the bar matches its width.
        // Materials are jade+gold (Pass C): gold border, dark-jade body/recess.
        // ------------------
        var trayRootGO = new GameObject("TrayRoot", typeof(TrayView3D));
        var trayView = trayRootGO.GetComponent<TrayView3D>();
        const float TrayDistance = 7.35f; // 9 * 0.8175, same FOV-compensation ratio as HudDistance
        float progressBottomEdge = progressBarY - progressHalfHeight;
        float trayHalfHeight = ScreenHalfHeightFrac(camera, trayFrameHeight, TrayDistance);
        float trayY = progressBottomEdge - TightRowGap - trayHalfHeight;
        PositionInFrontOfCamera(trayRootGO.transform, camera, new Vector2(0.5f, trayY), TrayDistance);
        trayRootGO.transform.localScale = Vector3.one * (TrayDistance / HudDistance);

        // Board vertical bias: centre the board in the band between the tray's
        // bottom edge and the button row's top edge (the top cluster eats more
        // screen than the bottom row, so a screen-centred board leaves a bigger
        // gap below - this shifts it to fill the space).
        // (ButtonFaceWorldDiameter/buttonHalfHeight computed above, reused here.)
        float bandTop = trayY - trayHalfHeight - TightRowGap;
        float bandBottom = BottomButtonRowY + buttonHalfHeight + HudRowGap;
        // Pin the board's TOP edge just under the tray (bandTop) so every board size
        // sits there and grows downward - a small board no longer floats with a big
        // gap above it, and the largest (80-tile) board rides just under the tray
        // instead of up into it. The centring bias stays as a fallback.
        SetFieldFloat(boardView, "_boardTopAnchorViewport", bandTop);
        float desiredBoardCenterY = (bandTop + bandBottom) * 0.5f;
        float verticalBiasFrac = 0.5f - desiredBoardCenterY;
        SetFieldFloat(boardView, "_verticalBiasViewportFrac", verticalBiasFrac);

        // Board band = the screen height between the tray's bottom edge and the
        // button row's top edge (~0.60). Cap the board's on-screen height to this
        // band so a tall board can't bleed into the top cluster or the buttons.
        float boardBandHeightFrac = Mathf.Max(0.2f, bandTop - bandBottom);
        SetFieldFloat(boardView, "_boardBandHeightFrac", boardBandHeightFrac);
        // Tile-size floor: an upper clamp on orthographicSize so tiles never shrink
        // below ~124px wide on this 1080px-wide panel (tile width 0.626 world =
        // minWidthFrac * viewport width; maxOrtho = 0.626 / (2*aspect*minWidthFrac),
        // aspect ~0.4615, minWidthFrac ~0.115). A board too big to fit at that size
        // overflows the band instead of shrinking. World-space value, so it holds
        // regardless of the exact device once the aspect is close.
        const float TileSizeFloorMaxOrtho = 5.9f;
        SetFieldFloat(boardView, "_maxOrthographicSize", TileSizeFloorMaxOrtho);
        // Constant zoom (matches the camera's HUD-calibration size above): keeps the
        // HUD stable across levels and gives every level the same tile size.
        SetFieldFloat(boardView, "_fixedOrthographicSize", FixedBoardOrthoSize);

        // Soft drop shadow behind the whole tray.
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

        // Gold border frame behind the slots (trayBorderMaterial uses AmberChrome,
        // now gold after Pass C).
        float containerWidth = trayContainerWidth;
        float frameHeight = trayFrameHeight;
        var frameMesh = SaveRoundedTrayMesh("Assets/Meshes/TrayFrame.asset", containerWidth, frameHeight, 0.12f, 0.14f);
        var trayContainerGO = new GameObject("TrayContainer", typeof(MeshFilter), typeof(MeshRenderer));
        trayContainerGO.transform.SetParent(trayRootGO.transform, false);
        trayContainerGO.transform.localPosition = new Vector3(0f, 0f, 0.03f); // tighter Z stack so slots don't parallax over the border (fix 7)
        trayContainerGO.GetComponent<MeshFilter>().sharedMesh = frameMesh;
        var trayContainerRenderer = trayContainerGO.GetComponent<MeshRenderer>();
        trayContainerRenderer.sharedMaterial = trayBorderMaterial;
        // The frame, jade body, and slot recess/card are stacked only fractions
        // of a unit apart in Z (see the near-Z-fight fix on the slot body's
        // position below) - the board's directional light (tuned with a long
        // shadow distance for the tilted board, see shadowDistance=30 above)
        // casts a real-time shadow between these tightly-stacked panels that
        // contributed to the "half box" tray bug, the same root cause already
        // fixed once for the divider mesh onto the modal panels (see
        // BuildModalPanelBackground).
        trayContainerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trayContainerRenderer.receiveShadows = false;

        // Dark-jade body inset within the gold frame (TrayBody, jade after Pass C+).
        var trayBodyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TrayBody.mat");
        RequireNotNull(trayBodyMaterial, "Assets/Materials/TrayBody.mat (run WoodUiGenerator first)");
        // 0.03 = frame radius (0.14) - body radius (0.11), so the gold stroke is
        // uniform at the corners (no notch) and ~2.3x the old hairline (round-2 fix 2).
        const float TrayBorderThickness = 0.035f;
        var trayBodyMesh = SaveRoundedTrayMesh("Assets/Meshes/TrayBody.asset",
            containerWidth - TrayBorderThickness * 2f, frameHeight - TrayBorderThickness * 2f, 0.12f, 0.14f - TrayBorderThickness);
        var trayBodyGO = new GameObject("TrayBody", typeof(MeshFilter), typeof(MeshRenderer));
        trayBodyGO.transform.SetParent(trayRootGO.transform, false);
        trayBodyGO.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        trayBodyGO.GetComponent<MeshFilter>().sharedMesh = trayBodyMesh;
        var trayBodyRenderer = trayBodyGO.GetComponent<MeshRenderer>();
        trayBodyRenderer.sharedMaterial = trayBodyMaterial;
        trayBodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trayBodyRenderer.receiveShadows = false;

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
        var traySlotPrefab = BuildTraySlotPrefab(recessMaterial, TraySlotWidth, TraySlotHeight);
        SetField(trayView, "traySlotPrefab", traySlotPrefab);
        SetField(trayView, "tileSet", tileSet);
        SetFieldArray(trayView, "slotAnchors", anchors);
        SetField(gameController, "_trayView", trayView);

        // Match celebration: a white particle burst at the tray slot when two
        // tiles match (reference: user-provided screen recording of a similar
        // mahjong game's "white balls" effect). The component already existed
        // but was never instantiated here, so _matchCelebration was always
        // null and PlayMatchCelebration silently no-opped every match.
        var matchGlowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MatchParticleGlow.mat");
        RequireNotNull(matchGlowMaterial, "Assets/Materials/MatchParticleGlow.mat (run MatchParticleGenerator first)");
        var matchSparkleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MatchParticleSparkle.mat");
        RequireNotNull(matchSparkleMaterial, "Assets/Materials/MatchParticleSparkle.mat (run MatchParticleGenerator first)");
        var matchCelebrationGO = new GameObject("MatchCelebration", typeof(MatchCelebrationController));
        var matchCelebration = matchCelebrationGO.GetComponent<MatchCelebrationController>();
        SetField(matchCelebration, "_glowMaterial", matchGlowMaterial);
        SetField(matchCelebration, "_sparkleMaterial", matchSparkleMaterial);
        SetField(gameController, "_matchCelebration", matchCelebration);

        // ------------------
        // Game over popup - shared jade+gold modal panel (was a one-off
        // Wood.mat cube left over from before the jade+gold retheme; also
        // fixes the "invisible button text" bug, see PrimaryButton below).
        // ------------------
        var popupGO = new GameObject("GameOverPopup", typeof(GameOverPopup3D));
        PositionInFrontOfCamera(popupGO.transform, camera, new Vector2(0.5f, 0.5f), PopupDistance);

        // The camera is orthographic (see Build() top), so its frustum size is
        // constant at every distance - used both to size the full-screen dim
        // scrim below and, later, to place the shared-component button.
        float frustumHeight = 2f * camera.orthographicSize;
        float frustumWidth = frustumHeight * camera.aspect;
        float FontSizeForScreenFrac(float frac) => (frac * frustumHeight) / 0.11f;

        // Full-screen dim scrim, behind the card but in front of the board/HUD
        // (PopupDistance=7.35 vs HudDistance=9 - a 1.0 unit push still lands
        // safely closer than the HUD, so it keeps occluding/dimming it).
        // Alpha-blended Transparent materials sort back-to-front by DISTANCE
        // (see URPMaterialUtil.SetAlphaCutout's comment) - this huge scrim
        // quad was intermittently winning that sort against the card's own
        // Transparent-queue TextMeshPro elements a fraction of a unit away
        // and painting over them. A render-queue override alone didn't stick
        // (URP's Lit/Unlit ShaderGUI re-validates and resets a script-set
        // custom queue back to the surface type's default on the next
        // reimport/domain reload - the same "ShaderGUI silently undoes a
        // script property" trap URPMaterialUtil.SetTransparent's own header
        // comment warns about). A full 1.0-unit gap - instead of the
        // originally-tried 0.05 - removes the ambiguity outright: nothing
        // else on the card sits anywhere near that far back, so the sort
        // isn't a near-tie regardless of what the queue value resolves to.
        var scrimGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        scrimGO.name = "Scrim";
        Object.DestroyImmediate(scrimGO.GetComponent<Collider>());
        scrimGO.transform.SetParent(popupGO.transform, false);
        scrimGO.transform.localPosition = new Vector3(0f, 0f, 1.0f);
        scrimGO.transform.localScale = new Vector3(frustumWidth, frustumHeight, 1f);
        // Load-or-create, not a bare CreateAsset: on a re-generation the asset
        // already exists at this path, and AssetDatabase.CreateAsset silently
        // no-ops against an existing path instead of overwriting it.
        const string scrimMaterialPath = "Assets/Materials/GameOverScrim.mat";
        var scrimMaterial = AssetDatabase.LoadAssetAtPath<Material>(scrimMaterialPath);
        bool scrimMaterialIsNew = scrimMaterial == null;
        if (scrimMaterialIsNew) scrimMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        URPMaterialUtil.SetTransparent(scrimMaterial);
        scrimMaterial.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.02f, 0.55f));
        scrimMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1;
        if (scrimMaterialIsNew) AssetDatabase.CreateAsset(scrimMaterial, scrimMaterialPath);
        else EditorUtility.SetDirty(scrimMaterial);
        var scrimRenderer = scrimGO.GetComponent<MeshRenderer>();
        scrimRenderer.sharedMaterial = scrimMaterial;
        scrimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        scrimRenderer.receiveShadows = false;

        BuildModalPanelBackground(popupGO.transform, "GameOverPanel", 3.0f, 2.7f, trayBorderMaterial, trayBodyMaterial);

        var titleGO = new GameObject("Title", typeof(TextMeshPro));
        titleGO.transform.SetParent(popupGO.transform, false);
        titleGO.transform.localPosition = new Vector3(0f, 0.95f, -0.15f); // generous gap, see PausedTitle's depth-test comment
        var titleText = titleGO.GetComponent<TextMeshPro>();
        titleText.fontSize = 1.3f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = CreamHudText;
        if (DisplayFont != null)
        {
            titleText.font = DisplayFont; // Cinzel for the win/lose title
            // Tamed underlay offset - see PauseMenu3D's Label() comment on why
            // Cinzel's default -0.5 reads as a disconnected ghost duplicate at
            // this font size instead of a subtle shadow.
            titleText.fontMaterial.SetFloat("_UnderlayOffsetY", -0.08f);
        }

        // Same thin gold accent as PauseMenu3D's divider, for a consistent
        // "structured header" look across both popups instead of bare text.
        // Y positions below (divider/chips-or-stars/caption-or-eyebrow/
        // message-or-value) are laid out on one even rhythm: a consistent
        // ~0.16 gap between every element's own visual edge, not just its
        // center - computed from each element's measured half-height
        // (fontSize*0.11/2 for text, mesh-height/2 for shapes) so a visually
        // bigger element (the tray chips) still gets the same whitespace
        // around it as a thin text line. Previously the divider sat almost
        // flush against the chip row (~0.02 gap) while other gaps varied
        // widely - this was the fix.
        var gameOverDivider = new GameObject("TitleDivider", typeof(MeshFilter), typeof(MeshRenderer));
        gameOverDivider.transform.SetParent(popupGO.transform, false);
        gameOverDivider.transform.localPosition = new Vector3(0f, 0.70f, -0.15f);
        gameOverDivider.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/GameOverDivider.asset", 1.1f, 0.022f, 0.05f, 0.011f);
        var gameOverDividerRenderer = gameOverDivider.GetComponent<MeshRenderer>();
        gameOverDividerRenderer.sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        // The actual root cause of the "ghost text" bug (see PausedDivider):
        // this thin bright bar was casting a real-time shadow onto the panel
        // body a fraction of a unit behind it, rendering as a faint patterned
        // smudge - not a text/font/board issue at all.
        gameOverDividerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gameOverDividerRenderer.receiveShadows = false;

        var starsRootGO = new GameObject("Stars");
        starsRootGO.transform.SetParent(popupGO.transform, false);
        starsRootGO.transform.localPosition = new Vector3(0f, 0.41f, -0.15f);
        starsRootGO.transform.localScale = Vector3.one * 1.6f; // bigger than the level-start sample (closer camera distance here)
        var starRenderers = BuildStarRow(starsRootGO.transform, "GameOverStar", filledCount: 0); // ShowWin sets the real count at runtime

        // Lose-only: 4 tray-slot chips (same 4 slots as BoardState.MaxTraySize)
        // + a caption, so the popup shows WHY the tray is full instead of just
        // saying so. Shares the stars row's Y slot - the two are mutually
        // exclusive per popup state, toggled at runtime by ShowWin/ShowLose.
        var trayChipRowGO = new GameObject("TrayChipRow");
        trayChipRowGO.transform.SetParent(popupGO.transform, false);
        trayChipRowGO.transform.localPosition = new Vector3(0f, 0.34f, -0.15f);
        const int chipCount = 4; // matches BoardState.MaxTraySize
        const float chipWidth = 0.34f;
        const float chipHeight = 0.36f;
        const float chipGap = 0.10f;
        float chipsTotalWidth = chipCount * chipWidth + (chipCount - 1) * chipGap;
        float chipStartX = -chipsTotalWidth * 0.5f + chipWidth * 0.5f;
        var chipMesh = SaveRoundedTrayMesh("Assets/Meshes/GameOverTrayChip.asset", chipWidth, chipHeight, 0.08f, chipWidth * 0.22f);
        var chipRenderers = new MeshRenderer[chipCount];
        for (int i = 0; i < chipCount; i++)
        {
            var chipGO = new GameObject("Chip" + i, typeof(MeshFilter), typeof(MeshRenderer));
            chipGO.transform.SetParent(trayChipRowGO.transform, false);
            chipGO.transform.localPosition = new Vector3(chipStartX + i * (chipWidth + chipGap), 0f, 0f);
            chipGO.GetComponent<MeshFilter>().sharedMesh = chipMesh;
            var chipMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            chipMat.SetColor("_BaseColor", new Color(0.060f, 0.080f, 0.100f)); // placeholder empty tint; SetTrayChips sets the real state at runtime
            AssetDatabase.CreateAsset(chipMat, "Assets/Materials/GameOverTrayChip" + i + ".mat");
            var chipRenderer = chipGO.GetComponent<MeshRenderer>();
            chipRenderer.material = chipMat;
            chipRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chipRenderer.receiveShadows = false;
            chipRenderers[i] = chipRenderer;
        }

        var trayChipCaptionGO = new GameObject("TrayChipCaption", typeof(TextMeshPro));
        trayChipCaptionGO.transform.SetParent(popupGO.transform, false);
        trayChipCaptionGO.transform.localPosition = new Vector3(0f, -0.03f, -0.15f);
        var trayChipCaptionText = trayChipCaptionGO.GetComponent<TextMeshPro>();
        trayChipCaptionText.fontSize = 0.42f;
        trayChipCaptionText.alignment = TextAlignmentOptions.Center;
        trayChipCaptionText.fontStyle = FontStyles.Bold;
        trayChipCaptionText.color = GoldChrome;

        var messageGO = new GameObject("Message", typeof(TextMeshPro));
        messageGO.transform.SetParent(popupGO.transform, false);
        messageGO.transform.localPosition = new Vector3(0f, -0.32f, -0.15f);
        var messageText = messageGO.GetComponent<TextMeshPro>();
        messageText.fontSize = 0.78f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = CreamHudText;

        // Win-only: score split out of the message sentence into its own
        // eyebrow + big Cinzel numeral (a result, not a caption). Shares the
        // message row's general area - mutually exclusive with it per state.
        var scoreBlockGO = new GameObject("ScoreBlock");
        scoreBlockGO.transform.SetParent(popupGO.transform, false);

        var scoreEyebrowGO = new GameObject("ScoreEyebrow", typeof(TextMeshPro));
        scoreEyebrowGO.transform.SetParent(scoreBlockGO.transform, false);
        scoreEyebrowGO.transform.localPosition = new Vector3(0f, 0.13f, -0.15f);
        var scoreEyebrowText = scoreEyebrowGO.GetComponent<TextMeshPro>();
        scoreEyebrowText.text = "BOARD CLEARED · FINAL SCORE";
        scoreEyebrowText.fontSize = 0.40f;
        scoreEyebrowText.alignment = TextAlignmentOptions.Center;
        scoreEyebrowText.fontStyle = FontStyles.Bold;
        scoreEyebrowText.color = CreamHudText;

        var scoreValueGO = new GameObject("ScoreValue", typeof(TextMeshPro));
        scoreValueGO.transform.SetParent(scoreBlockGO.transform, false);
        scoreValueGO.transform.localPosition = new Vector3(0f, -0.14f, -0.15f);
        var scoreValueText = scoreValueGO.GetComponent<TextMeshPro>();
        scoreValueText.fontSize = 1.5f;
        scoreValueText.alignment = TextAlignmentOptions.Center;
        scoreValueText.color = GoldChrome;
        if (DisplayFont != null)
        {
            scoreValueText.font = DisplayFont;
            scoreValueText.fontMaterial.SetFloat("_UnderlayOffsetY", -0.08f);
        }

        // Primary button: the SAME CreateSolidButton3D helper Resume/Play use
        // (not a hand-rolled mesh) - so a future press-scale or corner-radius
        // tweak on that shared helper propagates here too. Width is 84% of
        // the card's own width (GameOverCardWidth), matching Resume/Play's
        // own contentWidth = 0.84*cardWidth convention on the (narrower)
        // pause/level-start cards - a fixed 2.0 width read proportionally
        // narrower here since this card is wider. frustumHeight/
        // FontSizeForScreenFrac are the same ones computed above for the scrim -
        // the orthographic camera makes them constant at every distance, letting
        // a popup-local UIStage3D reproduce the exact same local-offset layout
        // the old hand-placed button used.
        const float GameOverCardWidth = 3.0f; // matches BuildModalPanelBackground's width, above
        const float GameOverButtonWidth = 0.84f * GameOverCardWidth;
        const float primaryButtonLocalY = -0.70f; // same even 0.16 rhythm as the content above it
        var popupStage = new UIStage3D(camera, popupGO.transform, PopupDistance);
        var primaryButtonVp = new Vector2(0.5f, 0.5f + primaryButtonLocalY / frustumHeight);
        var (restartButton, restartText) = CreateSolidButton3D(popupStage, "PrimaryButton", primaryButtonVp, "TRY AGAIN", GameOverButtonWidth, 0.24f, FontSizeForScreenFrac(0.014f), GoldInkText);

        var gameOverPopup = popupGO.GetComponent<GameOverPopup3D>();
        SetField(gameOverPopup, "restartButton", restartButton);
        SetField(gameOverPopup, "titleText", titleText);
        SetField(gameOverPopup, "messageText", messageText);
        SetField(gameOverPopup, "primaryButtonText", restartText);
        SetFieldArray(gameOverPopup, "starRenderers", starRenderers);
        SetField(gameOverPopup, "trayChipRow", trayChipRowGO);
        SetFieldArray(gameOverPopup, "trayChipRenderers", chipRenderers);
        SetField(gameOverPopup, "trayChipCaption", trayChipCaptionText);
        SetField(gameOverPopup, "scoreBlock", scoreBlockGO);
        SetField(gameOverPopup, "scoreValueText", scoreValueText);
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
        // Level-start is now the app's first/home screen (level-select removed) -
        // stays active by default, no initial SetActive(false).
        var levelStartRoot = BuildLevelStartScreen(camera, gameController, hudObjects, hudButtonFaceMaterial, trayBorderMaterial, trayBodyMaterial);

        var pauseMenu = BuildPauseMenu(camera, gameController, hudObjects, menuButton, hudButtonFaceMaterial,
            gameOverPopup, trayBorderMaterial, trayBodyMaterial);
        SetField(gameOverPopup, "pauseMenu", pauseMenu);
        SetField(pauseMenu, "_boardRoot", boardGO);

        // Shared back navigation (hardware/gesture back + on-screen back button).
        var backNavGO = new GameObject("BackNavigator");
        var backNav = backNavGO.AddComponent<BackNavigator>();
        SetField(backNav, "_levelStartScreen", levelStartRoot);
        SetFieldArray(backNav, "_gameHudObjects", hudObjects);
        SetField(backNav, "_pauseMenu", pauseMenu);
        SetField(backNav, "_gameOverPopup", gameOverPopup);
        SetField(backNav, "_backButton", backButton);

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

    // Screen-space HALF-height (as a viewport fraction) that a `worldHeight`
    // element subtends at `distance` in front of this camera. Used to lay
    // out stacked HUD rows by their actual EDGES instead of guessing a
    // centre-Y gap - guessing silently overlaps two rows whenever they
    // differ enough in height, which is exactly what happened here: the
    // tray (screen height ~14% at its distance) got moved up by the same
    // centre-Y gap that separates the much-shorter progress bar (~6%) from
    // the topbar, driving its top edge straight through the progress bar's
    // bottom edge. Confirmed both mathematically and via a live-editor
    // screenshot before landing this fix.
    private static float ScreenHalfHeightFrac(Camera camera, float worldHeight, float distance)
    {
        float frustumHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return (worldHeight / frustumHeight) * 0.5f;
    }

    // Loads the shared Unlit felt-gradient material used as a full-screen
    // backdrop (both the level-start screen and the game HUD screen), or
    // creates it if this is the first call. Unlit (not Felt.mat's Lit
    // shader) so the baked radial-glow darks aren't washed out by
    // RenderSettings.ambientLight.
    private static Material GetOrCreateFeltScreenMaterial()
    {
        var premMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PremiumBackground.mat");
        if (premMat != null) return premMat;

        var feltTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Felt.png");
        RequireNotNull(feltTex, "Assets/Textures/Felt.png (run FeltBackgroundGenerator first)");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FeltScreen.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, "Assets/Materials/FeltScreen.mat");
        }
        mat.SetTexture("_BaseMap", feltTex);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Shared non-emissive gold material (used by the Play button and the
    // progress bar's fill) - see the callers' comments for why this exists
    // instead of just loading Assets/Materials/Gold.mat directly.
    private static Material GetOrCreateNonEmissiveGoldMaterial(bool alwaysOnTop = false)
    {
        // Separate asset per variant, not one material with alwaysOnTop
        // toggled at each call site - SetAlwaysOnTop mutates the material
        // asset itself, so sharing one instance between the Play button and
        // the fill would silently force ZTest:Always onto whichever caller
        // ran second.
        string path = alwaysOnTop ? "Assets/Materials/ProgressFillGold.mat" : "Assets/Materials/PlayGold.mat";
        // Unlit, not Lit, when alwaysOnTop is requested - removes the
        // ORIGINAL cause of the fill's invisibility: a Lit material's gold
        // only shows via specular response to scene lighting, and this
        // thin, off-centre bar was catching that light too poorly to read
        // as anything but near-black. (Tried pairing this with
        // URPMaterialUtil.SetTransparent+SetAlwaysOnTop to also fix the
        // separate Z-fighting cause below, on both Lit and Unlit - both
        // threw "doesn't have a float or range property '_ZTest'" from
        // Material.GetFloat, meaning URP 17's shaders here don't expose
        // _ZTest as a material property at all, so SetAlwaysOnTop's
        // SetInt("_ZTest",...) has silently been a no-op absolutely
        // everywhere it's called in this codebase, not just here. Left
        // that alone rather than chase a real fix for an unrelated
        // pre-existing helper - the actual Z-fighting fix is the wider
        // gap at the Fill's placement site below, not a material trick.)
        var shader = Shader.Find(alwaysOnTop ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Gold.png"));
        mat.SetColor("_BaseColor", Color.white);
        if (!alwaysOnTop)
        {
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
        }
        else
        {
            // Transparent + ZWrite off so it doesn't depth-test against the (also
            // ZWrite-off) bar background: layering is purely by render queue, so
            // the fill can be COPLANAR with the background (zero Z gap => zero
            // tilt-parallax) and still paint over it, filling the inner area
            // edge-to-edge with no overflow (round-2 fix 1). Alpha stays 1 (solid
            // gold). Depth precision at this camera distance is too poor for a
            // small Z gap to work, so we remove the depth dependency entirely.
            URPMaterialUtil.SetTransparent(mat);
            mat.SetFloat("_Cull", 0f); // double-sided: the plain-quad fill faces +Z (away from camera), so it must render from both sides
            mat.renderQueue = 3000; // after the background (2900), before the text
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Solid cream disc material for a toggle switch's knob - stays the same
    // color regardless of on/off state (only the track recolors), matching
    // the plain white knob of a standard iOS-style switch.
    private static Material GetOrCreateToggleKnobMaterial()
    {
        const string path = "Assets/Materials/ToggleKnob.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", CreamHudText);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // A quad parented to (and facing) the camera, sized to exactly fill the
    // viewport at `distance` - so a baked full-screen texture (the felt
    // radial glow) maps screen-to-texture correctly regardless of the
    // camera's later position/orientation, unlike a fixed-scale world-space
    // quad which only shows whatever portion of the texture its bounds
    // happen to cover from wherever the camera ends up.
    private static GameObject BuildScreenFillingBackdrop(
        Camera camera, Transform parent, float distance, Material material, string name)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        var radial = go.AddComponent<BackgroundRadial3D>();
        radial.targetCamera = camera;
        radial.distance = distance;
        // Light Steel Blue (#B0C4DE) to deeper steel blue vignette:
        radial.innerColor = new Color(0.690f, 0.769f, 0.871f, 1f); // Light Steel Blue
        radial.outerColor = new Color(0.450f, 0.530f, 0.640f, 1f); // Deeper Steel Blue vignette
        radial.backgroundMaterial = material;
        if (material != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        go.transform.SetParent(parent, false);
        return go;
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
        Color? iconColorOverride = null, float faceScale = 0.42f, float iconScale = 0.35f)
    {
        string buttonName = hudComponentType != null ? hudComponentType.Name : (iconSprite != null ? iconSprite.name : "Button");
        var buttonGO = new GameObject(buttonName);
        PositionInFrontOfCamera(buttonGO.transform, camera, viewportPos, HudDistance);

        var faceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceGO.name = "Face";
        faceGO.transform.SetParent(buttonGO.transform, false);
        faceGO.transform.localScale = new Vector3(faceScale, faceScale, 1f);
        Object.DestroyImmediate(faceGO.GetComponent<MeshCollider>());
        faceGO.GetComponent<MeshRenderer>().sharedMaterial = locked ? lockedFaceMaterial : cardMaterial;

        var pressButton = buttonGO.AddComponent<PressScaleButton3D>();
        var buttonCollider = buttonGO.GetComponent<BoxCollider>();
        buttonCollider.size = new Vector3(faceScale, faceScale, 0.33f);
        SetField(pressButton, "_targetCamera", camera);

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(buttonGO.transform, false);
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.05f); 
        iconGO.transform.localScale = new Vector3(iconScale, iconScale, 1f);
        
        var spriteRenderer = iconGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = iconSprite;
        var iconTintColor = locked ? MutedIconTint : (iconColorOverride ?? Color.white);
        spriteRenderer.color = iconTintColor;
        spriteRenderer.sortingOrder = 10;

        TextMeshPro badgeText = null;
        TextMeshPro levelLabelText = null;
        Renderer badgeRenderer = null;
        if (!locked && badgeMaterial != null)
        {
            var badgeBgGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            badgeBgGO.name = "BadgeBackground";
            badgeBgGO.transform.SetParent(buttonGO.transform, false);
            badgeBgGO.transform.localPosition = new Vector3(faceScale * 0.35f, faceScale * 0.35f, -0.1f);
            badgeBgGO.transform.localScale = new Vector3(faceScale * 0.50f, faceScale * 0.50f, 1f);
            Object.DestroyImmediate(badgeBgGO.GetComponent<Collider>());
            badgeRenderer = badgeBgGO.GetComponent<MeshRenderer>();
            badgeRenderer.sharedMaterial = badgeMaterial;

            var badgeGO = new GameObject("BadgeText", typeof(TextMeshPro));
            badgeGO.transform.SetParent(buttonGO.transform, false);
            badgeGO.transform.localPosition = new Vector3(faceScale * 0.35f, faceScale * 0.35f, -0.15f);
            badgeText = badgeGO.GetComponent<TextMeshPro>();
            badgeText.text = "3";
            badgeText.fontSize = 1.15f;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.color = Color.white;
            badgeText.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            if (!string.IsNullOrEmpty(lockedLabel))
            {
                var levelLabelGO = new GameObject("LevelLabel", typeof(TextMeshPro));
                levelLabelGO.transform.SetParent(buttonGO.transform, false);
                levelLabelGO.transform.localPosition = new Vector3(0f, -0.29f, -0.08f); // below the disc
                levelLabelText = levelLabelGO.GetComponent<TextMeshPro>();
                levelLabelText.text = lockedLabel;
                levelLabelText.fontSize = 0.23f; // scaled down
                levelLabelText.color = CreamHudText;
                levelLabelText.alignment = TextAlignmentOptions.Center;
            }
        }

        var usesDisplay = buttonGO.AddComponent<ControlButtonUsesDisplay3D>();
        SetField(usesDisplay, "_button", pressButton);
        SetField(usesDisplay, "_faceRenderer", faceGO.GetComponent<MeshRenderer>());
        SetField(usesDisplay, "_iconSpriteRenderer", spriteRenderer);
        SetField(usesDisplay, "_enabledFaceMaterial", cardMaterial);
        SetField(usesDisplay, "_disabledFaceMaterial", lockedFaceMaterial);
        if (badgeRenderer != null) SetField(usesDisplay, "_badgeRenderer", badgeRenderer);
        SetField(usesDisplay, "_badgeText", badgeText);
        SetFieldColor(usesDisplay, "_enabledIconColor", iconTintColor);
        SetFieldColor(usesDisplay, "_disabledIconColor", new Color(0.50f, 0.55f, 0.62f, 0.5f));

        var hudButton = buttonGO.AddComponent<HudButton3D>();
        hudButton.Button = pressButton;
        hudButton.UsesDisplay = usesDisplay;
        hudButton.IconRenderer = spriteRenderer;
        hudButton.LevelLabel = levelLabelText;

        if (hudComponentType != null)
        {
            var hudComponent = buttonGO.AddComponent(hudComponentType);
            SetField(hudComponent, "_hudButton", hudButton);
            if (gameController != null)
            {
                SetField(hudComponent, "_gameController", gameController);
            }
        }

        return buttonGO;
    }

    // Level-start screen (mockup's left phone): "Now playing / Level 6" badge,
    // stars, goal text, carryover hint/undo/shuffle chips, and a big gold Play
    // button. Placed on a camera-parallel plane NEARER than the felt backdrop
    // (world z=9) so it draws in front of it - the felt itself is the screen's
    // background, matching the mockup's felt screen. LevelStartScreen3D hides
    // the passed-in hudObjects until Play is tapped, then reveals them and
    // calls GameController.BeginLevel.
    private static GameObject BuildLevelStartScreen(
        Camera camera, GameController gameController, GameObject[] hudObjects,
        Material discFaceMaterial, Material trayBorderMaterial, Material trayBodyMaterial)
    {
        // 7 -> 5.72: this screen's own elements (badge/stars/chips/Play button)
        // are all sized in fixed world units at this fixed distance D,
        // independent of the game HUD's HudDistance - so the FOV change in
        // Build() (40->48, for the board spacing fix) would shrink them too
        // unless D is scaled by the same tan(20deg)/tan(24deg)=0.8175 ratio.
        const float D = 7f * 0.8175f;

        var root = new GameObject("LevelStartScreen");
        PositionInFrontOfCamera(root.transform, camera, new Vector2(0.5f, 0.5f), D);

        // Full-screen radial-felt backdrop for this screen - see
        // BuildScreenFillingBackdrop/GetOrCreateFeltScreenMaterial (shared
        // with the game HUD screen's own backdrop, below in Build()).
        const float BgD = 8f * 0.8175f; // behind the content plane, same compensation ratio as D above
        BuildScreenFillingBackdrop(camera, root.transform, BgD, GetOrCreateFeltScreenMaterial(), "Backdrop");
        // BuildLeafDecoration removed as requested

        var creamDim = new Color(0.796f, 0.749f, 0.643f); // #CBBFA4 (mockup --cream-dim)
        var inkDim = new Color(0.604f, 0.573f, 0.494f);   // #9A927E (mockup --ink-dim)

        // Places a new child at a viewport point on the level-start plane,
        // parented under the root (keeping the camera-facing pose). Used for
        // the card root itself, which must stay at the baseline the card's
        // own frame/body offsets (local +0.05/+0.03, from
        // BuildModalPanelBackground) are relative to.
        Transform Place(GameObject go, Vector2 vp)
        {
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, D));
            go.transform.rotation = camera.transform.rotation;
            go.transform.SetParent(root.transform, true);
            return go.transform;
        }

        // Same as Place, but nudged 0.15 toward the camera (same safety
        // margin as the pause menu's card content, e.g. PausedTitle/divider)
        // so screen CONTENT (not the card itself) reliably renders in front
        // of the card's frame/body instead of relying on the bare,
        // Z-fight-prone gap to baseline zero.
        Transform PlaceContent(GameObject go, Vector2 vp)
        {
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, D)) - camera.transform.forward * 0.15f;
            go.transform.rotation = camera.transform.rotation;
            go.transform.SetParent(root.transform, true);
            return go.transform;
        }

        TextMeshPro Label(string labelName, Vector2 vp, string text, float size, Color color, FontStyles style, bool display = true)
        {
            var go = new GameObject(labelName, typeof(TextMeshPro));
            PlaceContent(go, vp);
            var t = go.GetComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = true;
            if (display && DisplayFont != null) t.font = DisplayFont; // Cinzel for headings/numbers
            return t;
        }

        // Card: single bordered panel framing all level-start content, matching
        // the pause menu's redesign (same BuildModalPanelBackground frame+body
        // technique, same card-to-screen size fractions and corner radius) so
        // the two screens read as one consistent design system. See
        // BuildPauseMenu's own comment for why frustum size is derived from
        // camera.orthographicSize (this camera is orthographic, not
        // perspective - fieldOfView has no effect on what actually renders).
        float frustumHeight = 2f * camera.orthographicSize;
        float frustumWidth = frustumHeight * camera.aspect;
        const float CardWidthFrac = 0.84f;
        const float CardHeightFrac = 0.63f;
        float cardWidth = CardWidthFrac * frustumWidth;
        float cardHeight = CardHeightFrac * frustumHeight;
        var cardRoot = new GameObject("LevelStartCardRoot");
        Place(cardRoot, new Vector2(0.5f, 0.5f));
        BuildModalPanelBackground(cardRoot.transform, "LevelStartCard", cardWidth, cardHeight, trayBorderMaterial, trayBodyMaterial, cornerRadius: 0.07f * cardWidth);

        float contentWidth = 0.84f * cardWidth;
        var stage = new UIStage3D(camera, root.transform, D);

        // Converts a target "% of screen height" (as measured in the approved
        // mockup, in cqh units relative to the phone's full height) into a TMP
        // fontSize - same conversion the pause menu uses (renderedHeight runs
        // ~fontSize*0.11).
        float FontSizeForScreenFrac(float frac) => (frac * frustumHeight) / 0.11f;

        // Content vertically distributed within the card's own bounds (vp.y
        // 0.185-0.815). Simplified per an approved mockup: stars row, the
        // Hint/Undo/Shuffle carryover row, and the "Level N" title (redundant
        // with the badge) are all gone - remaining content (eyebrow -> badge
        // -> goal -> PLAY) is spread across the freed-up space instead of
        // leaving a gap where the removed rows used to be.
        Label("Eyebrow", new Vector2(0.5f, 0.752f), "NOW PLAYING", FontSizeForScreenFrac(0.018f), creamDim, FontStyles.Bold);

        // Level badge: dark amber-ring disc (same chrome as the HUD buttons) +
        // level number - the sole level indicator now that the title is gone,
        // so it's sized up (0.4 -> 0.58, matching the mockup's 22%->32% of
        // card width) and given more central room.
        var badgeGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        badgeGO.name = "LevelBadge";
        Object.DestroyImmediate(badgeGO.GetComponent<Collider>());
        PlaceContent(badgeGO, new Vector2(0.5f, 0.601f));
        badgeGO.transform.localScale = new Vector3(0.58f, 0.58f, 1f);
        badgeGO.GetComponent<MeshRenderer>().sharedMaterial = discFaceMaterial;
        var badgeNum = Label("BadgeNum", new Vector2(0.5f, 0.601f), "6", 2.33f, CreamHudText, FontStyles.Bold);
        badgeNum.transform.localPosition += new Vector3(0f, 0f, -0.05f); // toward camera, in front of the disc face

        var goal = Label("Goal", new Vector2(0.5f, 0.450f), "Collect tiles into the tray and match pairs to clear the board.", FontSizeForScreenFrac(0.017f), inkDim, FontStyles.Normal, display: false);
        goal.enableWordWrapping = true;
        goal.rectTransform.sizeDelta = new Vector2(1.7f, 1f); // ~2 wrapped lines

        // Play button: same CreateSolidButton3D helper (same corner radius,
        // same gold gradient) as the pause menu's RESUME button, full-width
        // within the card's content inset instead of the old screen-relative
        // fixed 1.75-unit pill.
        var play = CreateSolidButton3D(stage, "Play", new Vector2(0.5f, 0.235f), "PLAY", contentWidth, 0.09f * cardHeight, (0.014f * frustumHeight) / 0.11f, GoldInkText);

        var levelStart = root.AddComponent<LevelStartScreen3D>();
        SetField(levelStart, "_playButton", play.btn);
        SetField(levelStart, "_gameController", gameController);
        SetFieldArray(levelStart, "_gameHudObjects", hudObjects);
        SetField(levelStart, "_badgeText", badgeNum);
        return root;
    }

    // ================= Shared 3D HUD widgets =================
    // Common components any screen-builder method in this class can use, so a
    // button/label/toggle only needs to be gotten right once. Extracted from
    // what used to be one-off local functions trapped inside BuildPauseMenu.
    //
    // SCALE NOTE (the actual root cause of "text too small" across this file's
    // history): a mesh's width/height and a TextMeshPro's fontSize are NOT the
    // same unit even though both are plain floats in "world units" - a fontSize
    // of 1.0 renders at roughly 1/10th the visual size of a mesh dimension of
    // 1.0 (confirmed empirically: a toggle track of height 0.32 next to a label
    // at fontSize 0.30 measured a rendered text height of only ~0.033 world
    // units - 10% of the track's own height, not comparable at all). Any new
    // label's fontSize should be chosen against OTHER fontSize values already
    // proven legible in this file (BadgeNum=2.33, scoreText=1.05), never
    // against a nearby mesh's width/height number.
    private readonly struct UIStage3D
    {
        public readonly Camera Camera;
        public readonly Transform Parent;
        public readonly float Distance;
        public UIStage3D(Camera camera, Transform parent, float distance)
        {
            Camera = camera; Parent = parent; Distance = distance;
        }
        public Transform Place(GameObject go, Vector2 vp)
        {
            go.transform.position = Camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, Distance));
            go.transform.rotation = Camera.transform.rotation;
            go.transform.SetParent(Parent, true);
            return go.transform;
        }
    }

    // Always Center-aligned - confirmed via a minimal live repro (a single bare
    // Left-aligned TextMeshPro, nothing else in the scene) that Left/Right
    // alignment simply never renders for a 3D (non-UI) TextMeshPro on this
    // Unity/TMP version. Callers that need a left-aligned look use
    // SimulateLeftAlign3D() instead of TextAlignmentOptions.Left.
    private static TextMeshPro CreateLabel3D(UIStage3D stage, string name, Vector2 vp, string text, float fontSize, Color color, bool useDisplayFont = false, FontStyles style = FontStyles.Bold)
    {
        var go = new GameObject(name, typeof(TextMeshPro));
        stage.Place(go, vp);
        var t = go.GetComponent<TextMeshPro>();
        t.text = text; t.fontSize = fontSize; t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        if (useDisplayFont && DisplayFont != null)
        {
            t.font = DisplayFont;
            // Cinzel's shared material bakes an underlay (drop-shadow) with
            // _UnderlayOffsetY: -0.5 - reads as a disconnected dark duplicate
            // of the letters at small sizes. .fontMaterial clones per-instance.
            t.fontMaterial.SetFloat("_UnderlayOffsetY", -0.08f);
        }
        t.ForceMeshUpdate();
        return t;
    }

    // Repositions an already-created (already mesh-finalized) label so its LEFT
    // EDGE lands at `leftX`, using its measured renderedWidth - simulates left
    // alignment since TextAlignmentOptions.Left doesn't render (see above).
    private static void SimulateLeftAlign3D(TextMeshPro t, float leftX, float zOffset)
    {
        t.transform.localPosition += new Vector3(leftX + t.renderedWidth * 0.5f, 0f, zOffset);
    }

    // Solid gold pill button + centered label.
    private static (PressScaleButton3D btn, TextMeshPro lbl) CreateSolidButton3D(UIStage3D stage, string name, Vector2 vp, string text, float width, float height, float fontSize, Color textColor, float cornerRadiusFrac = 0.32f)
    {
        var pill = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        stage.Place(pill, vp);
        pill.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/Btn_" + name + ".asset", width, height, 0.1f, height * cornerRadiusFrac);
        pill.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        var col = pill.AddComponent<BoxCollider>();
        col.size = new Vector3(width, height, 0.1f);
        var b = pill.AddComponent<PressScaleButton3D>();
        SetField(b, "_targetCamera", stage.Camera);
        var l = CreateLabel3D(stage, name + "Text", vp, text, fontSize, textColor);
        l.transform.localPosition += new Vector3(0f, 0f, -0.06f);
        return (b, l);
    }

    // Outline ("ghost") button: gold frame, near-empty body, gold ink label -
    // reads as clearly secondary next to a CreateSolidButton3D. Same layered
    // frame+body technique as BuildModalPanelBackground, sized to one button.
    private static (PressScaleButton3D btn, TextMeshPro lbl) CreateOutlineButton3D(UIStage3D stage, string name, Vector2 vp, string text, float width, float height, float fontSize, Material bodyMaterial, Color textColor)
    {
        const float stroke = 0.035f;
        float radius = height * 0.32f;

        var frameMesh = SaveRoundedTrayMesh("Assets/Meshes/" + name + "Frame.asset", width, height, 0.1f, radius);
        var frameGO = new GameObject(name + "Frame", typeof(MeshFilter), typeof(MeshRenderer));
        stage.Place(frameGO, vp);
        frameGO.transform.localPosition += new Vector3(0f, 0f, -0.06f);
        frameGO.GetComponent<MeshFilter>().sharedMesh = frameMesh;
        var frameR = frameGO.GetComponent<MeshRenderer>();
        frameR.sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        frameR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        frameR.receiveShadows = false;

        var bodyMesh = SaveRoundedTrayMesh("Assets/Meshes/" + name + "Body.asset",
            width - stroke * 2f, height - stroke * 2f, 0.1f, Mathf.Max(radius - stroke, 0.02f));
        var bodyGO = new GameObject(name + "Body", typeof(MeshFilter), typeof(MeshRenderer));
        stage.Place(bodyGO, vp);
        // 0.02 gap from the frame's own Z, matching BuildModalPanelBackground's
        // proven separation - but note SaveRoundedTrayMesh's `thickness` extrudes
        // the mesh symmetrically (+-thickness/2), so at thickness 0.1 the frame's
        // actual FRONT face sits 0.05 further forward than its nominal Z. Any
        // label placed in front of both layers needs to clear the BODY's front
        // face (nominal - 0.05), not just its nominal Z - confirmed live by
        // toggling each layer's MeshRenderer.enabled and re-screenshotting.
        bodyGO.transform.localPosition += new Vector3(0f, 0f, -0.08f);
        bodyGO.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
        var bodyR = bodyGO.GetComponent<MeshRenderer>();
        bodyR.sharedMaterial = bodyMaterial;
        bodyR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyR.receiveShadows = false;

        var col = frameGO.AddComponent<BoxCollider>();
        col.size = new Vector3(width, height, 0.1f);
        var b = frameGO.AddComponent<PressScaleButton3D>();
        SetField(b, "_targetCamera", stage.Camera);

        var l = CreateLabel3D(stage, name + "Text", vp, text, fontSize, textColor);
        l.transform.localPosition += new Vector3(0f, 0f, -0.20f); // clears both front faces above
        return (b, l);
    }

    // Settings-style row: a left-aligned label + an iOS-style switch on the
    // right, both offset from the row's own centre in WORLD units (never a
    // separate viewport-fraction offset - those two scales don't track each
    // other across aspect ratios, so a fixed vp.x offset drifts past a
    // world-unit-sized container's actual edge on a different screen aspect).
    private static (ToggleSwitch3D toggle, TextMeshPro label) CreateToggleRow3D(UIStage3D stage, string name, Vector2 vp, string labelText, float rowHalfWidth, Material trayBodyMaterial, float labelFontSize, float trackW, float trackH, float knobD)
    {
        var label = CreateLabel3D(stage, name + "Label", vp, labelText, labelFontSize, CreamHudText);
        SimulateLeftAlign3D(label, -rowHalfWidth, -0.06f);

        var trackGO = new GameObject(name + "Track", typeof(MeshFilter), typeof(MeshRenderer));
        stage.Place(trackGO, vp);
        trackGO.transform.localPosition += new Vector3(rowHalfWidth - trackW * 0.5f, 0f, -0.06f);
        trackGO.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/Toggle_" + name + "Track.asset", trackW, trackH, 0.08f, trackH * 0.5f);
        trackGO.GetComponent<MeshRenderer>().sharedMaterial = trayBodyMaterial; // starts "off"-colored; SetOn() below/at runtime recolors
        var col = trackGO.AddComponent<BoxCollider>();
        col.size = new Vector3(trackW + 0.3f, trackH + 0.3f, 0.1f); // generous tap target beyond the visual track
        var btn = trackGO.AddComponent<PressScaleButton3D>();
        SetField(btn, "_targetCamera", stage.Camera);

        var knobGO = new GameObject(name + "Knob", typeof(MeshFilter), typeof(MeshRenderer));
        knobGO.transform.SetParent(trackGO.transform, false);
        knobGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        knobGO.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/Toggle_" + name + "Knob.asset", knobD, knobD, 0.09f, knobD * 0.5f);
        knobGO.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateToggleKnobMaterial();

        float halfTravel = (trackW - knobD) * 0.5f - 0.03f; // small inset so the knob never touches the track ends
        var toggle = trackGO.AddComponent<ToggleSwitch3D>();
        SetField(toggle, "_trackRenderer", trackGO.GetComponent<MeshRenderer>());
        SetField(toggle, "_onMaterial", GetOrCreateNonEmissiveGoldMaterial());
        SetField(toggle, "_offMaterial", trayBodyMaterial);
        SetField(toggle, "_knob", knobGO.transform);
        SetFieldFloat(toggle, "_knobOffLocalX", -halfTravel);
        SetFieldFloat(toggle, "_knobOnLocalX", halfTravel);

        return (toggle, label);
    }

    // Pause menu overlay (sub-project #4D, redesign pass 5 - borderless):
    // PAUSED + Resume/Restart/Sound/Vibration all sit directly on the backdrop,
    // no panel outline and no corner brackets - a later follow-up superseded
    // pass 4's bordered Settings card and open-corner-bracket framing. Built on
    // an always-active root that toggles a child overlay; opened by the top
    // menu button.
    //
    // Pass 3 shipped readable-when-zoomed-in-300%-on-a-screenshot text that
    // was actually too small on the real device: every font size had been
    // picked as if it were directly comparable to a nearby mesh's world-unit
    // width/height, which CreateLabel3D's scale note above debunks (fontSize
    // needs ~10x the numeric value of a mesh dimension for similar visual
    // weight). Every size below is rebuilt from that ratio instead - see each
    // value's comment for the specific comparison it's calibrated against.
    private static PauseMenu3D BuildPauseMenu(
        Camera camera, GameController gameController, GameObject[] hudObjects,
        PressScaleButton3D menuButton, Material discFaceMaterial,
        GameOverPopup3D gameOverPopup, Material trayBorderMaterial, Material trayBodyMaterial)
    {
        const float D = 7f * 0.8175f;
        var root = new GameObject("PauseMenu");
        PositionInFrontOfCamera(root.transform, camera, new Vector2(0.5f, 0.5f), D);

        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(root.transform, false);

        const float BgD = 8f * 0.8175f;
        BuildScreenFillingBackdrop(camera, overlay.transform, BgD, GetOrCreateFeltScreenMaterial(), "Backdrop");

        var stage = new UIStage3D(camera, overlay.transform, D);

        // Card: single bordered panel (same frame+body technique as
        // GameOverPanel, via BuildModalPanelBackground) framing all pause-menu
        // content, matching a user-approved HTML mockup built at the phone's
        // exact 1080x2340 proportions. World-unit sizes below are derived from
        // the live camera frustum, so they carry the mockup's screen-relative
        // percentages over exactly rather than via hand-eyeballed constants.
        // The camera is ORTHOGRAPHIC (see camera.orthographic = true / Build()
        // top), so frustum size is 2*orthographicSize and is constant at every
        // distance - fieldOfView has no effect on what actually renders. An
        // earlier version of this used the perspective formula
        // (2*D*tan(fov/2)), which under-sized everything here by ~1.57x
        // (undersized card overflowed by its own content, undersized text/
        // buttons) since it doesn't apply to this camera's projection mode.
        float frustumHeight = 2f * camera.orthographicSize;
        float frustumWidth = frustumHeight * camera.aspect;
        const float CardWidthFrac = 0.84f;   // 8% inset from each screen edge
        const float CardHeightFrac = 0.63f;  // spans screen vp.y 0.185-0.815, vertically centered
        float cardWidth = CardWidthFrac * frustumWidth;
        float cardHeight = CardHeightFrac * frustumHeight;
        var cardRoot = new GameObject("PauseCardRoot");
        stage.Place(cardRoot, new Vector2(0.5f, 0.5f)); // card is vertically centered on screen
        // cornerRadius matches the mockup's border-radius:7% (of card width) -
        // the function's own 0.22 default was tuned for GameOverPanel's wider
        // 3.0-unit panel and reads proportionally too rounded (11%) on this
        // narrower card.
        BuildModalPanelBackground(cardRoot.transform, "PauseCard", cardWidth, cardHeight, trayBorderMaterial, trayBodyMaterial, cornerRadius: 0.07f * cardWidth);

        // Buttons/rows span 84% of the card's own width (mockup's inner content
        // inset), same as the settings rows below.
        float contentWidth = 0.84f * cardWidth;
        // Converts a target "% of screen height" (as measured in the mockup)
        // into a TMP fontSize: renderedHeight (world units) runs ~fontSize*0.11.
        float FontSizeForScreenFrac(float frac) => (frac * frustumHeight) / 0.11f;

        // Title: calibrated against BadgeNum (fontSize 2.33, level-start's own
        // single-digit level number) - a 6-letter screen title should read at
        // least as large as that, not smaller. Unchanged by the card pass -
        // confirmed as the one element already at the right size.
        var pausedTitle = CreateLabel3D(stage, "PausedTitle", new Vector2(0.5f, 0.76f), "PAUSED", 1.7f, CreamHudText, useDisplayFont: true, style: FontStyles.Normal);
        pausedTitle.transform.localPosition += new Vector3(0f, 0f, -0.15f);

        // Ornamented divider: two short bars closing on a small rotated-square
        // "diamond". Same shadowCastingMode fix as elsewhere - a thin gold
        // element this close to text/backdrop reads as a duplicate-text smudge
        // if it's allowed to cast a real-time shadow. Geometry scaled by
        // dividerScale to track the new (narrower) content width - 2.0 is the
        // previous PauseContentWidth this divider was originally tuned against.
        float dividerScale = contentWidth / 2.0f;
        var dividerLineMesh = SaveRoundedTrayMesh("Assets/Meshes/PausedDividerLine.asset", 0.36f * dividerScale, 0.022f * dividerScale, 0.05f, 0.011f * dividerScale);
        var dividerDiamondMesh = SaveRoundedTrayMesh("Assets/Meshes/PausedDividerDiamond.asset", 0.065f * dividerScale, 0.065f * dividerScale, 0.05f, 0.010f * dividerScale);
        void MakeDividerPart(string name, Vector2 vp, float xOffset, Mesh mesh, float rotateZ = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            stage.Place(go, vp);
            go.transform.localPosition += new Vector3(xOffset, 0f, -0.15f);
            if (rotateZ != 0f) go.transform.rotation *= Quaternion.Euler(0f, 0f, rotateZ);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        var dividerVp = new Vector2(0.5f, 0.702f);
        MakeDividerPart("PausedDividerLeft", dividerVp, -0.23f * dividerScale, dividerLineMesh);
        MakeDividerPart("PausedDividerDiamond", dividerVp, 0f, dividerDiamondMesh, rotateZ: 45f);
        MakeDividerPart("PausedDividerRight", dividerVp, 0.23f * dividerScale, dividerLineMesh);

        // Resume is the primary action - solid gold fill. Restart is secondary:
        // same family (same corner radius, same gold), but an outline instead
        // of a fill, so the two stop competing for attention despite Restart
        // being the more destructive of the two. Both span the card's full
        // content width (matching the Settings rows below) rather than a
        // narrower centered pill, and sit vertically centered in the gap
        // between the divider and the section rule above Settings.
        var resume = CreateSolidButton3D(stage, "Resume", new Vector2(0.5f, 0.607f), "RESUME", contentWidth, 0.09f * cardHeight, FontSizeForScreenFrac(0.014f), GoldInkText);
        var restart = CreateOutlineButton3D(stage, "Restart", new Vector2(0.5f, 0.528f), "RESTART", contentWidth, 0.09f * cardHeight, FontSizeForScreenFrac(0.0125f), trayBodyMaterial, GoldChrome);

        // Section rule: thin gold hairline separating the Resume/Restart
        // actions from the Settings group below, inside the card - reuses the
        // divider's line mesh technique (single continuous bar, no diamond).
        var sectionRuleMesh = SaveRoundedTrayMesh("Assets/Meshes/PauseSectionRule.asset", contentWidth, 0.01f, 0.05f, 0.005f);
        var sectionRuleGO = new GameObject("SectionRule", typeof(MeshFilter), typeof(MeshRenderer));
        stage.Place(sectionRuleGO, new Vector2(0.5f, 0.437f));
        sectionRuleGO.transform.localPosition += new Vector3(0f, 0f, -0.15f);
        sectionRuleGO.GetComponent<MeshFilter>().sharedMesh = sectionRuleMesh;
        var sectionRuleRenderer = sectionRuleGO.GetComponent<MeshRenderer>();
        sectionRuleRenderer.sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        sectionRuleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sectionRuleRenderer.receiveShadows = false;

        // Settings rows sit directly on the card, no separate bordered card of
        // their own - the outer card (built above) already provides that
        // frame. Same contentWidth as the buttons above so the row
        // label/toggle group lines up with them instead of sitting narrower.
        float settingsHalfWidth = contentWidth * 0.5f;

        // Settings eyebrow: smaller than the row labels below it (it's a
        // caption, not content) but still legible.
        var settingsLabel = CreateLabel3D(stage, "SettingsLabel", new Vector2(0.5f, 0.391f), "SETTINGS", FontSizeForScreenFrac(0.016f), SettingsLabelTint);
        SimulateLeftAlign3D(settingsLabel, -settingsHalfWidth, -0.09f);

        // ALL-CAPS, not "Sound"/"Vibration": live-tested in isolation (a bare
        // TextMeshPro, nothing else in the scene) and confirmed mixed-case
        // "Vibration" specifically renders as overlapping/garbled glyphs on this
        // font asset - "VIBRATION" (and "SOUND") render clean. Also matches the
        // ALL-CAPS convention every other label on this screen already uses.
        // Track/knob sized as a fraction of contentWidth (matching the
        // mockup's row-relative percentages) - noticeably smaller than pass 5,
        // which read as oversized next to the row label text.
        float toggleTrackW = 0.165f * contentWidth;
        float toggleTrackH = 0.087f * contentWidth;
        float toggleKnobD = 0.76f * toggleTrackH;
        float rowLabelFontSize = FontSizeForScreenFrac(0.018f);
        var sound = CreateToggleRow3D(stage, "SoundToggle", new Vector2(0.5f, 0.332f), "SOUND", settingsHalfWidth, trayBodyMaterial, rowLabelFontSize, toggleTrackW, toggleTrackH, toggleKnobD);
        var vibration = CreateToggleRow3D(stage, "VibrationToggle", new Vector2(0.5f, 0.259f), "VIBRATION", settingsHalfWidth, trayBodyMaterial, rowLabelFontSize, toggleTrackW, toggleTrackH, toggleKnobD);

        // WORKAROUND for a confirmed Editor-time quirk: whichever TextMeshPro is
        // the LAST one created inside this method never commits its post-creation
        // position offset into the saved scene (reproduced repeatedly by swapping
        // Sound/Vibration's creation order - the bug always follows whoever is
        // built last, regardless of word/content; the identical position mutation
        // applied live at runtime, after the scene is already loaded, sticks
        // immediately, so this is specific to edit-time construction/serialization,
        // not a real ToggleSwitch3D/TextMeshPro bug). Rather than fight that
        // serialization path directly, this harmless off-screen placeholder simply
        // absorbs being "last" so VibrationToggleLabel isn't.
        var endOfBuildMarker = CreateLabel3D(stage, "PauseMenuBuildEndMarker", new Vector2(0.5f, 0.259f), " ", 0.01f, Color.clear);
        endOfBuildMarker.transform.localPosition += new Vector3(0f, -50f, 0f); // parked far off-screen either way

        overlay.SetActive(false); // hidden until the menu button is tapped

        var pause = root.AddComponent<PauseMenu3D>();
        SetField(pause, "_overlay", overlay);
        SetField(pause, "_menuButton", menuButton);
        SetField(pause, "_resumeButton", resume.btn);
        SetField(pause, "_restartButton", restart.btn);
        SetField(pause, "_soundToggle", sound.toggle);
        SetField(pause, "_vibrationToggle", vibration.toggle);
        SetField(pause, "_gameController", gameController);
        SetFieldArray(pause, "_gameHudObjects", hudObjects);
        SetField(pause, "_gameOverPopup", gameOverPopup);
        return pause;
    }

    // Shared 3-star row (mockup's .stars): a generated 5-point star sprite
    // tinted per-star (gold = earned, muted = not) because LiberationSans (the
    // project's only font) has no star glyph. Returns the per-star renderers
    // so a caller whose fill count isn't known until runtime (the game-over
    // popup's actual result) can recolor them later via MeshRenderer.material.
    private static MeshRenderer[] BuildStarRow(Transform starsRoot, string assetNamePrefix, int filledCount)
    {
        var starSprite = GetStarSprite();
        var renderers = new MeshRenderer[3];
        float[] xs = { -0.16f, 0f, 0.16f }; // ~86px spacing
        for (int i = 0; i < 3; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Star" + i;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(starsRoot, false);
            q.transform.localPosition = new Vector3(xs[i], 0f, -0.02f);
            q.transform.localScale = new Vector3(0.13f, 0.13f, 1f); // ~70px star
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            URPMaterialUtil.SetTransparent(m);
            URPMaterialUtil.SetAlwaysOnTop(m);
            m.SetTexture("_BaseMap", starSprite.texture);
            m.SetColor("_BaseColor", i < filledCount ? StarGold : StarMuted);
            AssetDatabase.CreateAsset(m, "Assets/Materials/" + assetNamePrefix + i + ".mat");
            var mr = q.GetComponent<MeshRenderer>();
            mr.material = m;
            renderers[i] = mr;
        }
        return renderers;
    }

    // Three faint corner leaf silhouettes (mockup's `.leaf.l1/.l2/.l3`), at
    // 7% white opacity. Viewport fractions are the leaf's CENTER (Unity
    // positions a quad by its centre), converted from the mockup's CSS,
    // which anchors by top-left/right/bottom EDGE, not centre - e.g. l1's
    // `left:-38px` is where its edge sits, so the centre is at
    // left+width/2, not at -38px itself. Also measured against the
    // `.screen` box (362x817, phone 390x845 minus the .phone element's own
    // 14px padding on each side), not the outer phone frame - both of these
    // were wrong in the first pass, which placed the centres far enough
    // off-screen (up to -0.10/1.02) that the whole shape missed the visible
    // viewport entirely (confirmed via an in-editor Game view capture, whose
    // wider aspect showed the letterboxed margins where they were actually
    // landing).
    // WidthFrac bumped ~1.5x over the literal CSS-px conversion in an
    // earlier pass (measuring the user's own annotated mockup screenshot
    // against its phone screen gave ~50%/44%/39% of screen width for
    // l1/l2/l3, vs the ~41%/30%/25% the raw CSS px values converted to), then
    // bumped again here per explicit follow-up feedback that it still read
    // too small - another ~1.3x on top of that first pass.
    private static readonly (Vector2 Viewport, float WidthFrac, float RotationDeg)[] LeafSpecs =
    {
        (new Vector2(0.102f, 0.894f), 0.80f, 18f),   // l1: top-left
        (new Vector2(0.931f, 0.241f), 0.60f, -150f), // l2: right edge, lower third
        (new Vector2(0.086f, 0.065f), 0.48f, 58f),   // l3: bottom-left
    };

    private static void BuildLeafDecoration(Camera camera, Transform parent, float distance)
    {
        var leafSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Decor/leaf.png");
        RequireNotNull(leafSprite, "Assets/Textures/Decor/leaf.png as Sprite (run DecorIconGenerator first)");

        var leafMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LeafDecor.mat");
        if (leafMat == null)
        {
            leafMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(leafMat, "Assets/Materials/LeafDecor.mat");
        }
        URPMaterialUtil.SetTransparent(leafMat);
        URPMaterialUtil.SetAlwaysOnTop(leafMat); // without this the quad depth-tests against nearby opaque geometry (same fix every other camera-facing glyph in this file already relies on) and can be fully occluded
        leafMat.SetTexture("_BaseMap", leafSprite.texture);
        leafMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.07f)); // mockup: opacity:.07, fill:#fff
        EditorUtility.SetDirty(leafMat);

        var leafRoot = new GameObject("LeafDecoration");
        leafRoot.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        leafRoot.transform.rotation = camera.transform.rotation;
        leafRoot.transform.SetParent(parent, true);

        float frustumHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * camera.aspect;

        for (int i = 0; i < LeafSpecs.Length; i++)
        {
            var (vp, widthFrac, rotationDeg) = LeafSpecs[i];
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Leaf" + i;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, distance));
            go.transform.rotation = camera.transform.rotation * Quaternion.Euler(0f, 0f, rotationDeg);
            go.transform.SetParent(leafRoot.transform, true);
            float worldWidth = widthFrac * frustumWidth;
            go.transform.localScale = new Vector3(worldWidth, worldWidth, 1f); // leaf.png's own SDF is already ~1.5:1 tall, no extra stretch needed
            go.GetComponent<MeshRenderer>().sharedMaterial = leafMat;
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
        var mesh = RoundedTileMesh.Build(w, h, thickness, radius, cornerSegments: 24);
        mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
        System.IO.Directory.CreateDirectory("Assets/Meshes");
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    // Shared jade+gold-framed modal panel: a gold rounded-rect frame with a
    // dark inset body on top, same technique (and materials) as the tray's
    // own border - so every popup shares one consistent look instead of each
    // being its own one-off (the old game-over popup was a plain Wood.mat
    // cube left over from before the jade+gold retheme; the pause menu had
    // no panel at all). Border stroke width = frame radius - body radius,
    // the same "uniform corner stroke" rule the tray/score-bar borders use.
    private static void BuildModalPanelBackground(
        Transform parent, string namePrefix, float width, float height,
        Material borderMaterial, Material bodyMaterial,
        float cornerRadius = 0.22f, float strokeWidth = 0.05f)
    {
        const float thickness = 0.12f;

        var frameMesh = SaveRoundedTrayMesh(
            "Assets/Meshes/" + namePrefix + "Frame.asset", width, height, thickness, cornerRadius);
        var frameGO = new GameObject(namePrefix + "Frame", typeof(MeshFilter), typeof(MeshRenderer));
        frameGO.transform.SetParent(parent, false);
        frameGO.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        frameGO.GetComponent<MeshFilter>().sharedMesh = frameMesh;
        var frameRenderer = frameGO.GetComponent<MeshRenderer>();
        frameRenderer.sharedMaterial = borderMaterial;
        frameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // a real-time shadow cast onto the body mesh a fraction of a unit behind it read as a faint, patterned smudge - not caught until a bright divider nearby made it visible
        frameRenderer.receiveShadows = false;

        var bodyMesh = SaveRoundedTrayMesh(
            "Assets/Meshes/" + namePrefix + "Body.asset",
            width - strokeWidth * 2f, height - strokeWidth * 2f, thickness, cornerRadius - strokeWidth);
        var bodyGO = new GameObject(namePrefix + "Body", typeof(MeshFilter), typeof(MeshRenderer));
        bodyGO.transform.SetParent(parent, false);
        bodyGO.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        bodyGO.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
        var bodyRenderer = bodyGO.GetComponent<MeshRenderer>();
        bodyRenderer.sharedMaterial = bodyMaterial;
        bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyRenderer.receiveShadows = false;
    }

    private static GameObject BuildTraySlotPrefab(Material cardMaterial, float width, float height)
    {
        var root = new GameObject("TraySlot3D");
        var content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);

        // Rounded PORTRAIT slot body matching the board tile aspect, with tight
        // padding (0.96 fill) so the tile sits snugly (fix spec section 2); empty =
        // warm recess (cardMaterial), filled swaps to the ivory tile face.
        var slotMesh = SaveRoundedTrayMesh("Assets/Meshes/TraySlot.asset", width * 0.96f, height * 0.96f, 0.05f, width * 0.16f);
        var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(content.transform, false);
        // -0.05 (was -0.015): at -0.015 with this mesh's 0.05 thickness, the
        // card's own near face landed at almost exactly the same depth as the
        // shared TrayBody panel behind it (both ~Z0-0.04) - an effective
        // Z-fight that let TrayBody win the depth test over the lower portion
        // of every filled slot, reading as the card being cut off ("half box").
        // Comfortably ahead of TrayBody's near face removes the tie outright.
        body.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        body.GetComponent<MeshFilter>().sharedMesh = slotMesh;
        var bodyRenderer = body.GetComponent<MeshRenderer>();
        bodyRenderer.sharedMaterial = cardMaterial; // recess material passed in
        // Same shadow-casting fix as the tray frame/body above: the recess/card
        // sits within fractions of a unit of those panels, and the directional
        // light's real-time shadow between these tightly-stacked layers is what
        // reads as the lower portion of the slot being cut off.
        bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bodyRenderer.receiveShadows = false;

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

        // Motion-blur trail (mockup reference: a fast-flying collected tile
        // trails a soft warm blur) - built onto every tray slot instance but
        // disabled by default (TraySlotView3D.SetFlightTrailEnabled), so only
        // the pooled flight-card instances TrayView3D reuses ever show one;
        // the 4 static tray slots never call SetFlightTrailEnabled(true).
        var trailGO = new GameObject("FlightTrail");
        trailGO.transform.SetParent(content.transform, false);
        var trail = trailGO.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = width * 0.55f;
        trail.endWidth = 0.01f;
        trail.minVertexDistance = 0.01f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.textureMode = LineTextureMode.Stretch;
        var trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Unlit");
        var trailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FlightTrail.mat");
        if (trailMat == null)
        {
            trailMat = new Material(trailShader);
            AssetDatabase.CreateAsset(trailMat, "Assets/Materials/FlightTrail.mat");
        }
        else
        {
            trailMat.shader = trailShader;
        }
        URPMaterialUtil.SetTransparent(trailMat);
        trailMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.55f, 0.5f)); // warm gold, matches the ivory/amber tile chrome
        EditorUtility.SetDirty(trailMat);
        trail.sharedMaterial = trailMat;
        var trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = trailGradient;
        trail.emitting = false;
        trail.enabled = false;

        // Landing-impact puff (soft white burst the instant a flown tile lands
        // in this slot - see TraySlotView3D.PlayLandingPuff); reuses the same
        // baked glow sprite the match celebration uses (run MatchParticleGenerator first).
        var landingPuffMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MatchParticleGlow.mat");
        RequireNotNull(landingPuffMaterial, "Assets/Materials/MatchParticleGlow.mat (run MatchParticleGenerator first)");

        var slotView = root.AddComponent<TraySlotView3D>();
        SetField(slotView, "_content", content.transform);
        SetField(slotView, "_bodyRenderer", body.GetComponent<MeshRenderer>());
        SetField(slotView, "_foodAnchor", foodAnchorGO.transform);
        SetField(slotView, "_emptyMaterial", cardMaterial);
        SetField(slotView, "_filledMaterial", tileFaceMaterial);
        SetField(slotView, "_flightTrail", trail);
        SetField(slotView, "_landingPuffMaterial", landingPuffMaterial);

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

    private static void SetFieldIntArray(Object target, string fieldName, int[] values)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).intValue = values[i];
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
