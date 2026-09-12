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

    // Premium display font (Cinzel OFL, Pass E) for headings/numbers - LEVEL,
    // score, PLAY. Body text keeps LiberationSans (TMP default).
    private static TMPro.TMP_FontAsset _displayFont;
    private static TMPro.TMP_FontAsset DisplayFont =>
        _displayFont != null ? _displayFont
        : (_displayFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/Cinzel SDF.asset"));
    private static readonly Color MutedIconTint = new Color(0.541f, 0.502f, 0.447f, 1f); // #8A8072 - mockup's .chrome-btn.is-locked svg stroke
    private static readonly Color GoldIconTint = new Color(0.95f, 0.78f, 0.30f, 1f); // hint's lightbulb is colored gold, unlike the other buttons' white/cream glyphs

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
        // Between the backdrop (13) and the farthest board tile (~10.1, plus
        // margin for the untilted-camera approximation that estimate uses) /
        // HUD plane (9), so leaves sit behind all HUD/board content, matching
        // the mockup's DOM order (leaves painted before .hud).
        BuildLeafDecoration(camera, camera.transform, 11.5f);

        var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CardBody.mat");
        RequireNotNull(cardMaterial, "Assets/Materials/CardBody.mat as Material");
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
        var backButtonGO = CreateVisualIconButton3D(camera, hudButtonFaceMaterial, new Vector2(0.09f, TopbarY), "BackButton", backIcon);
        // Make the back button tappable (routed through BackNavigator, same as hardware back).
        var backButton = backButtonGO.AddComponent<PressScaleButton3D>();
        SetField(backButton, "_targetCamera", camera);
        var backButtonCol = backButtonGO.GetComponent<BoxCollider>();
        if (backButtonCol != null) backButtonCol.size = new Vector3(0.42f, 0.42f, 0.1f);
        var menuButtonGO = CreateVisualIconButton3D(camera, hudButtonFaceMaterial, new Vector2(0.91f, TopbarY), "MenuButton", menuIcon);
        // Make the menu (hamburger) button tappable so it can open the pause menu.
        var menuButton = menuButtonGO.AddComponent<PressScaleButton3D>();
        SetField(menuButton, "_targetCamera", camera);
        var menuButtonCol = menuButtonGO.GetComponent<BoxCollider>();
        if (menuButtonCol != null) menuButtonCol.size = new Vector3(0.42f, 0.42f, 0.1f);

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
        const float BottomButtonRowY = 0.08f; // low at the bottom (above the gesture bar) so the taller 5-layer pyramid clears the buttons
        var shuffleButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.17f, BottomButtonRowY), gameController, typeof(ShuffleButton3D), shuffleIcon,
            locked: true, lockedFaceMaterial: hudButtonFaceLockedMaterial, lockedLabel: "Lv. 6");
        var hintButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.5f, BottomButtonRowY), gameController, typeof(HintButton3D), hintIcon,
            iconColorOverride: GoldIconTint);
        var undoButtonGO = CreateHudButton3D(camera, hudButtonFaceMaterial, badgeMaterial, new Vector2(0.83f, BottomButtonRowY), gameController, typeof(UndoButton3D), undoIcon);
        // The control buttons were built at full size (~2.2x) and ran off the
        // screen edges (undo's badge was clipped). Scale the whole button root
        // (face + icon + badge + caption together) down to a mockup-sized disc
        // so all three sit fully on-screen with even spacing.
        foreach (var b in new[] { shuffleButtonGO, hintButtonGO, undoButtonGO })
            b.transform.localScale = Vector3.one * 0.62f;

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
        const float ButtonFaceWorldDiameter = 0.99f * 0.62f;
        float buttonHalfHeight = ScreenHalfHeightFrac(camera, ButtonFaceWorldDiameter, HudDistance);
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

        BuildModalPanelBackground(popupGO.transform, "GameOverPanel", 3.0f, 2.7f, trayBorderMaterial, trayBodyMaterial);

        var titleGO = new GameObject("Title", typeof(TextMeshPro));
        titleGO.transform.SetParent(popupGO.transform, false);
        titleGO.transform.localPosition = new Vector3(0f, 0.95f, -0.15f); // generous gap, see PausedTitle's depth-test comment
        var titleText = titleGO.GetComponent<TextMeshPro>();
        titleText.fontSize = 1.1f;
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
        var gameOverDivider = new GameObject("TitleDivider", typeof(MeshFilter), typeof(MeshRenderer));
        gameOverDivider.transform.SetParent(popupGO.transform, false);
        gameOverDivider.transform.localPosition = new Vector3(0f, 0.78f, -0.15f);
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
        starsRootGO.transform.localPosition = new Vector3(0f, 0.55f, -0.15f);
        starsRootGO.transform.localScale = Vector3.one * 1.6f; // bigger than the level-start sample (closer camera distance here)
        var starRenderers = BuildStarRow(starsRootGO.transform, "GameOverStar", filledCount: 0); // ShowWin sets the real count at runtime

        var messageGO = new GameObject("Message", typeof(TextMeshPro));
        messageGO.transform.SetParent(popupGO.transform, false);
        messageGO.transform.localPosition = new Vector3(0f, 0.15f, -0.15f);
        var messageText = messageGO.GetComponent<TextMeshPro>();
        messageText.fontSize = 0.66f;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = CreamHudText;

        // Primary button: a rounded-pill MESH (real width/height baked into the
        // mesh itself), not a scaled Cube - the old Cube's thin Z-scale (0.15)
        // silently shrank its child text's -0.1 local offset to -0.015, landing
        // the text BEHIND the cube's own front face (-0.075) so it was fully
        // occluded. The label here is a sibling of the button (both children of
        // popupGO, which has no scale), so no parent-scale can distort it again.
        var primaryBtnGO = new GameObject("PrimaryButton", typeof(MeshFilter), typeof(MeshRenderer));
        primaryBtnGO.transform.SetParent(popupGO.transform, false);
        primaryBtnGO.transform.localPosition = new Vector3(0f, -0.75f, -0.1f);
        primaryBtnGO.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/GameOverPrimaryBtn.asset", 2.0f, 0.5f, 0.12f, 0.2f);
        primaryBtnGO.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        var primaryBtnCollider = primaryBtnGO.AddComponent<BoxCollider>();
        primaryBtnCollider.size = new Vector3(2.0f, 0.5f, 0.12f);
        var restartButton = primaryBtnGO.AddComponent<PressScaleButton3D>();
        SetField(restartButton, "_targetCamera", camera);

        var primaryBtnTextGO = new GameObject("PrimaryButtonText", typeof(TextMeshPro));
        primaryBtnTextGO.transform.SetParent(popupGO.transform, false); // sibling of the button, see comment above
        primaryBtnTextGO.transform.localPosition = new Vector3(0f, -0.75f, -0.16f);
        var restartText = primaryBtnTextGO.GetComponent<TextMeshPro>();
        restartText.fontSize = 0.5f;
        restartText.alignment = TextAlignmentOptions.Center;
        restartText.fontStyle = FontStyles.Bold; // bold sans, not Cinzel - matches PauseMenu3D's button labels (see MakeButton)
        restartText.color = GoldInkText; // dark ink on the gold pill, matching every other gold button

        var gameOverPopup = popupGO.GetComponent<GameOverPopup3D>();
        SetField(gameOverPopup, "restartButton", restartButton);
        SetField(gameOverPopup, "titleText", titleText);
        SetField(gameOverPopup, "messageText", messageText);
        SetField(gameOverPopup, "primaryButtonText", restartText);
        SetFieldArray(gameOverPopup, "starRenderers", starRenderers);
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
        var levelStartRoot = BuildLevelStartScreen(camera, gameController, hudObjects, hintIcon, undoIcon, shuffleIcon, hudButtonFaceMaterial);
        var levelSelectRoot = BuildLevelSelectScreen(camera, gameController, hudObjects, levelStartRoot, hudButtonFaceMaterial, hudButtonFaceLockedMaterial);
        levelStartRoot.SetActive(false); // the level-select screen shows first

        var pauseMenu = BuildPauseMenu(camera, gameController, hudObjects, menuButton, hudButtonFaceMaterial,
            gameOverPopup, trayBorderMaterial, trayBodyMaterial);
        SetField(gameOverPopup, "pauseMenu", pauseMenu);
        SetField(pauseMenu, "_boardRoot", boardGO);

        // Shared back navigation (hardware/gesture back + on-screen back button).
        var backNavGO = new GameObject("BackNavigator");
        var backNav = backNavGO.AddComponent<BackNavigator>();
        SetField(backNav, "_levelSelectScreen", levelSelectRoot);
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
        // Felt.png gradient colors (approx):
        radial.innerColor = new Color(0.18f, 0.40f, 0.28f, 1f); // Lighter Jade
        radial.outerColor = new Color(0.04f, 0.12f, 0.08f, 1f); // Darker Jade
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

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(buttonGO.transform, false);
        // Position slightly in front of the disc face (-0.05 on Z) to prevent Z-fighting
        // since we are no longer using SetAlwaysOnTop.
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.05f); 
        iconGO.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        
        var spriteRenderer = iconGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = iconSprite;
        var iconTintColor = locked ? MutedIconTint : (iconColorOverride ?? CreamHudText);
        spriteRenderer.color = iconTintColor;
        spriteRenderer.sortingOrder = 10; // ensure it sorts above the button face

        TextMeshPro badgeText = null;
        if (!locked)
        {
            var badgeBgGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            badgeBgGO.name = "BadgeBackground";
            badgeBgGO.transform.SetParent(buttonGO.transform, false);
            // Position 0.40->0.35, scale 0.37->0.32: measured against the
            // mockup's own numbers (badge 22px / button ~69px = 32% diameter
            // ratio; badge centre sits ~71% of the button's radius from
            // centre, from its top:-1.5%/right:-2% offsets) - ours was both
            // a bit bigger (37%) and sitting further out toward the corner
            // (81%) than the mockup, reading as slightly oversized/detached.
            badgeBgGO.transform.localPosition = new Vector3(0.35f, 0.35f, -0.72f);
            badgeBgGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            badgeBgGO.transform.localScale = new Vector3(0.32f, 0.095f, 0.32f);
            Object.DestroyImmediate(badgeBgGO.GetComponent<Collider>());
            badgeBgGO.GetComponent<MeshRenderer>().sharedMaterial = badgeMaterial;

            var badgeGO = new GameObject("BadgeText", typeof(TextMeshPro));
            badgeGO.transform.SetParent(buttonGO.transform, false);
            // -0.87 clears BadgeBackground's own front face (-0.72-0.095=
            // -0.815) with a small margin, same reasoning as before just
            // re-checked against the new, slightly thinner 0.095 scale.
            badgeGO.transform.localPosition = new Vector3(0.35f, 0.35f, -0.87f);
            badgeText = badgeGO.GetComponent<TextMeshPro>();
            badgeText.text = "3";
            badgeText.fontSize = 0.95f; // scaled down with the badge (was 1.1 at the old 0.37 badge size)
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

        AddButtonDropShadow(buttonGO.transform, faceScale: 0.42f);

        var faceGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceGO.name = "Face";
        faceGO.transform.SetParent(buttonGO.transform, false);
        faceGO.transform.localScale = new Vector3(0.42f, 0.42f, 1f); // smaller than the 0.99 control-button discs - secondary chrome (was 0.55, oversized at the current zoom)
        Object.DestroyImmediate(faceGO.GetComponent<MeshCollider>());
        faceGO.GetComponent<MeshRenderer>().sharedMaterial = faceMaterial;

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        iconGO.transform.SetParent(buttonGO.transform, false);
        Object.DestroyImmediate(iconGO.GetComponent<MeshCollider>());
        iconGO.transform.localPosition = Vector3.zero; // same Z as Face - avoids the parallax bug documented on CreateHudButton3D's Icon
        iconGO.transform.localScale = new Vector3(0.44f, 0.44f, 1f); // glyph ~70% of the disc, scaled with the smaller 0.42 face (was 0.58)
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
    private static GameObject BuildLevelStartScreen(
        Camera camera, GameController gameController, GameObject[] hudObjects,
        Sprite hintIcon, Sprite undoIcon, Sprite shuffleIcon, Material discFaceMaterial)
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
        BuildLeafDecoration(camera, root.transform, 7.7f * 0.8175f); // between the content plane and the backdrop

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

        TextMeshPro Label(string labelName, Vector2 vp, string text, float size, Color color, FontStyles style, bool display = true)
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
            if (display && DisplayFont != null) t.font = DisplayFont; // Cinzel for headings/numbers
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

        var titleLabel = Label("Title", new Vector2(0.5f, 0.478f), "Level 6", 1.15f, CreamHudText, FontStyles.Bold);

        // Two filled gold stars + one muted (unearned) star - real generated
        // star sprites, NOT ★/☆ glyphs (LiberationSans, the only font in the
        // project, has no star glyph so those render as tofu boxes).
        BuildStars(camera, root.transform, D, new Vector2(0.5f, 0.43f));

        var goal = Label("Goal", new Vector2(0.5f, 0.385f), "Collect tiles into the tray and match pairs to clear the board.", 0.55f, inkDim, FontStyles.Normal, display: false);
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
        // Dedicated non-emissive gold (see GetOrCreateNonEmissiveGoldMaterial -
        // Gold.mat's _EmissionColor is set but its _EMISSION keyword is never
        // enabled, a classic Unity gotcha: setting the color property alone
        // does not turn emission rendering on. Depending on when that was
        // last true, Gold.mat has either always silently rendered as its
        // plain, unlit-looking base texture, or the keyword got dropped on a
        // later regeneration - either way, this dedicated material is the
        // one confirmed to actually render the gold gradient) so it reads as
        // the mockup's gradient gold pill.
        var playGold = GetOrCreateNonEmissiveGoldMaterial();
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
        SetField(levelStart, "_titleText", titleLabel);
        SetField(levelStart, "_badgeText", badgeNum);
        return root;
    }

    // Level-select screen (sub-project #4B): a centered row of level tokens
    // (jade+gold disc + number + star pips), shown before the level-start screen.
    private static GameObject BuildLevelSelectScreen(
        Camera camera, GameController gameController, GameObject[] hudObjects,
        GameObject levelStartRoot, Material discFaceMaterial, Material discLockedMaterial)
    {
        const float D = 7f * 0.8175f;
        var root = new GameObject("LevelSelectScreen");
        PositionInFrontOfCamera(root.transform, camera, new Vector2(0.5f, 0.5f), D);

        const float BgD = 8f * 0.8175f;
        BuildScreenFillingBackdrop(camera, root.transform, BgD, GetOrCreateFeltScreenMaterial(), "Backdrop");
        BuildLeafDecoration(camera, root.transform, 7.7f * 0.8175f);

        Transform Place(GameObject go, Vector2 vp)
        {
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, D));
            go.transform.rotation = camera.transform.rotation;
            go.transform.SetParent(root.transform, true);
            return go.transform;
        }
        TextMeshPro Label(string name, Vector2 vp, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            Place(go, vp);
            var t = go.GetComponent<TextMeshPro>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            if (DisplayFont != null) t.font = DisplayFont;
            return t;
        }

        Label("SelectTitle", new Vector2(0.5f, 0.72f), "SELECT LEVEL", 0.72f, CreamHudText);

        var levels = GameDomain.Progression.LevelCatalog.Levels;
        int n = levels.Count;
        var buttons = new PressScaleButton3D[n];
        var ids = new int[n];
        var starTexts = new TextMeshPro[n];
        var numberTexts = new TextMeshPro[n];

        const float spacing = 0.17f;
        float x0 = 0.5f - (n - 1) * 0.5f * spacing;
        for (int i = 0; i < n; i++)
        {
            float vx = x0 + i * spacing;
            ids[i] = levels[i].LevelId;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "LevelToken_" + levels[i].LevelId;
            Place(disc, new Vector2(vx, 0.5f));
            disc.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            disc.GetComponent<MeshRenderer>().sharedMaterial = discFaceMaterial;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.AddComponent<BoxCollider>();
            var btn = disc.AddComponent<PressScaleButton3D>();
            SetField(btn, "_targetCamera", camera);
            buttons[i] = btn;

            numberTexts[i] = Label("LevelNum_" + levels[i].LevelId, new Vector2(vx, 0.5f),
                levels[i].LevelId.ToString(), 0.85f, CreamHudText);
            numberTexts[i].transform.localPosition += new Vector3(0f, 0f, -0.05f); // in front of the disc

            starTexts[i] = Label("LevelStars_" + levels[i].LevelId, new Vector2(vx, 0.42f),
                "...", 0.4f, new Color(0.96f, 0.82f, 0.42f));
        }

        // Daily challenge button: a gold pill below the level row.
        var dailyPill = new GameObject("DailyButton", typeof(MeshFilter), typeof(MeshRenderer));
        Place(dailyPill, new Vector2(0.5f, 0.30f));
        dailyPill.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/DailyBtn.asset", 1.7f, 0.34f, 0.1f, 0.16f);
        dailyPill.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        var dailyCol = dailyPill.AddComponent<BoxCollider>();
        dailyCol.size = new Vector3(1.7f, 0.34f, 0.1f);
        var dailyBtn = dailyPill.AddComponent<PressScaleButton3D>();
        SetField(dailyBtn, "_targetCamera", camera);
        var dailyLabel = Label("DailyText", new Vector2(0.5f, 0.30f), "DAILY CHALLENGE", 0.42f, new Color(0.227f, 0.141f, 0.063f));
        dailyLabel.transform.localPosition += new Vector3(0f, 0f, -0.06f);

        var select = root.AddComponent<LevelSelectScreen3D>();
        SetFieldArray(select, "_levelButtons", buttons);
        SetFieldIntArray(select, "_levelIds", ids);
        SetFieldArray(select, "_starTexts", starTexts);
        SetFieldArray(select, "_numberTexts", numberTexts);
        SetField(select, "_levelStartScreen", levelStartRoot);
        SetFieldArray(select, "_gameHudObjects", hudObjects);
        SetField(select, "_gameController", gameController);
        SetField(select, "_dailyButton", dailyBtn);
        SetField(select, "_dailyLabel", dailyLabel);
        return root;
    }

    // Pause menu overlay (sub-project #4D): a full jade screen with PAUSED +
    // Resume/Restart and Sound/Music toggles. Built on an always-active root that
    // toggles a child overlay; opened by the top menu button.
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

        Transform Place(GameObject go, Vector2 vp)
        {
            go.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, D));
            go.transform.rotation = camera.transform.rotation;
            go.transform.SetParent(overlay.transform, true);
            return go.transform;
        }
        // display=false + Bold: button labels use a bold sans, not the Cinzel
        // display face - Cinzel's thin serif strokes read poorly at small
        // button-label sizes (mockup used the same sans/bold split: Cinzel only
        // for the large title, bold body font for buttons).
        TextMeshPro Label(string name, Vector2 vp, string text, float size, Color color, bool display = true, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            Place(go, vp);
            var t = go.GetComponent<TextMeshPro>();
            t.text = text; t.fontSize = size; t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            if (display && DisplayFont != null)
            {
                t.font = DisplayFont;
                // Cinzel's shared material bakes an underlay (drop-shadow) with
                // _UnderlayOffsetY: -0.5 - at this popup's font size that reads
                // as a disconnected dark duplicate of the letters floating below
                // them, not a subtle shadow (it was always there, just went
                // unnoticed against the plain panel until the divider added
                // nearby contrast). .fontMaterial clones per-instance, so this
                // doesn't touch the shared asset used by level-select/start.
                t.fontMaterial.SetFloat("_UnderlayOffsetY", -0.08f);
            }
            return t;
        }
        (PressScaleButton3D btn, TextMeshPro lbl) MakeButton(string name, Vector2 vp, string text, float width, float height, float fontSize)
        {
            var pill = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Place(pill, vp);
            pill.GetComponent<MeshFilter>().sharedMesh =
                SaveRoundedTrayMesh("Assets/Meshes/PauseBtn_" + name + ".asset", width, height, 0.1f, height * 0.47f);
            pill.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
            var col = pill.AddComponent<BoxCollider>();
            col.size = new Vector3(width, height, 0.1f);
            var b = pill.AddComponent<PressScaleButton3D>();
            SetField(b, "_targetCamera", camera);
            var l = Label(name + "Text", vp, text, fontSize, GoldInkText, display: false, style: FontStyles.Bold);
            l.transform.localPosition += new Vector3(0f, 0f, -0.06f);
            return (b, l);
        }

        // Shared jade+gold-framed panel behind the title+buttons (was missing
        // entirely - buttons floated directly on the felt with no container),
        // matching the same panel the game-over popup now uses. Height/anchor
        // tightened from an earlier 3.1/0.48 that left uneven dead space above
        // the title and below the toggle row - this fits the content snugly.
        var panelAnchor = new GameObject("PausePanelAnchor");
        Place(panelAnchor, new Vector2(0.5f, 0.495f));
        BuildModalPanelBackground(panelAnchor.transform, "PausePanel", 2.3f, 2.75f, trayBorderMaterial, trayBodyMaterial);

        // Explicit, generous forward offset (not the tiny 0.03-0.05 gap the panel
        // itself uses) - small Z gaps are unreliable at HUD camera distance (see
        // the ProgressBar3D "coplanar transparent elements render unreliably"
        // gotcha); this only became visible once the title's viewport position
        // started spatially overlapping the panel's on-screen footprint.
        var pausedTitle = Label("PausedTitle", new Vector2(0.5f, 0.62f), "PAUSED", 0.9f, CreamHudText);
        pausedTitle.transform.localPosition += new Vector3(0f, 0f, -0.15f);

        // A bare thin gold accent under the title - the "just text floating on
        // a panel" look was the missing-polish complaint; this one line gives
        // the header actual structure, same gold as the buttons for cohesion.
        var divider = new GameObject("PausedDivider", typeof(MeshFilter), typeof(MeshRenderer));
        Place(divider, new Vector2(0.5f, 0.585f));
        divider.transform.localPosition += new Vector3(0f, 0f, -0.15f);
        divider.GetComponent<MeshFilter>().sharedMesh =
            SaveRoundedTrayMesh("Assets/Meshes/PausedDivider.asset", 0.9f, 0.022f, 0.05f, 0.011f);
        var dividerRenderer = divider.GetComponent<MeshRenderer>();
        dividerRenderer.sharedMaterial = GetOrCreateNonEmissiveGoldMaterial();
        // ROOT CAUSE of the "ghost text" bug reported after this divider was
        // added: it was casting a real-time shadow onto the panel body a
        // fraction of a unit behind it, rendering as a faint patterned smudge
        // that looked like duplicate text - not a font/underlay/board issue,
        // confirmed by diffing screenshots from before/after this divider
        // existed (clean before, smudge after, at the exact same position).
        dividerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dividerRenderer.receiveShadows = false;

        // Resume is the primary action (bigger, most prominent); Restart is
        // secondary at the same size as the toggles below it, not competing
        // with Resume for attention.
        var resume = MakeButton("Resume", new Vector2(0.5f, 0.53f), "RESUME", 1.9f, 0.42f, 0.46f);
        var restart = MakeButton("Restart", new Vector2(0.5f, 0.435f), "RESTART", 1.6f, 0.32f, 0.38f);
        // Sound/Music are settings toggles, not primary actions - side by side
        // and narrower so they read as a distinct, lower-priority row.
        var sound = MakeButton("SoundToggle", new Vector2(0.30f, 0.345f), "Sound: ON", 0.95f, 0.30f, 0.32f);
        var music = MakeButton("MusicToggle", new Vector2(0.70f, 0.345f), "Music: ON", 0.95f, 0.30f, 0.32f);

        overlay.SetActive(false); // hidden until the menu button is tapped

        var pause = root.AddComponent<PauseMenu3D>();
        SetField(pause, "_overlay", overlay);
        SetField(pause, "_menuButton", menuButton);
        SetField(pause, "_resumeButton", resume.btn);
        SetField(pause, "_restartButton", restart.btn);
        SetField(pause, "_soundToggle", sound.btn);
        SetField(pause, "_musicToggle", music.btn);
        SetField(pause, "_soundLabel", sound.lbl);
        SetField(pause, "_musicLabel", music.lbl);
        SetField(pause, "_gameController", gameController);
        SetFieldArray(pause, "_gameHudObjects", hudObjects);
        SetField(pause, "_gameOverPopup", gameOverPopup);
        return pause;
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
        var starsRoot = new GameObject("Stars");
        starsRoot.transform.position = camera.ViewportToWorldPoint(new Vector3(vp.x, vp.y, distance));
        starsRoot.transform.rotation = camera.transform.rotation;
        starsRoot.transform.SetParent(parent, true);
        BuildStarRow(starsRoot.transform, "LevelStartStar", filledCount: 2);
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
        Material borderMaterial, Material bodyMaterial)
    {
        const float cornerRadius = 0.22f;
        const float strokeWidth = 0.05f;
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

        var slotView = root.AddComponent<TraySlotView3D>();
        SetField(slotView, "_content", content.transform);
        SetField(slotView, "_bodyRenderer", body.GetComponent<MeshRenderer>());
        SetField(slotView, "_foodAnchor", foodAnchorGO.transform);
        SetField(slotView, "_emptyMaterial", cardMaterial);
        SetField(slotView, "_filledMaterial", tileFaceMaterial);
        SetField(slotView, "_flightTrail", trail);

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
