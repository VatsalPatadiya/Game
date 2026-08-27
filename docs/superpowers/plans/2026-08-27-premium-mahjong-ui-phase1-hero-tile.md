# Premium Mahjong UI — Phase 1: Hero Tile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat white cube tile with a rounded, ivory, jade-beveled 3D tile that has real thickness, an enlarged food icon, and a soft cast shadow — matching the approved hero-tile mock.

**Architecture:** Two pure, unit-testable builders live in runtime code — `RoundedTileMesh` (procedural rounded-rectangle extruded mesh) and `TileFaceTexture` (procedural ivory + jade-frame albedo texture). Editor generators consume them to bake a `TileBody.mat` material and a regenerated `Tile3D.prefab`. Scene lighting is tuned so stacked tiles cast/receive soft shadows. No gameplay changes in this phase.

**Tech Stack:** Unity (URP), C#, Unity Test Framework (EditMode / NUnit).

**Spec:** `docs/superpowers/specs/2026-08-27-mahjong-premium-ui-redesign-design.md`

## Global Constraints

- **Render pipeline:** URP. Tile material uses shader `Universal Render Pipeline/Lit`.
- **Do NOT mutate the shared `Assets/Materials/CardBody.mat`** — it is used by the score pill, HUD buttons, tray slots, and popup. Create a new `Assets/Materials/TileBody.mat` for board/tray tiles only.
- **Per-instance color at runtime goes through `MeshRendererTint` (MaterialPropertyBlock)** — never mutate a shared material asset at runtime. Existing tile tint keys: `_BaseColor`, `_EmissionColor`.
- **Keep a `BoxCollider` on the tile root** as the raycast tap target (rounded silhouette vs box collider difference is imperceptible for tapping).
- **Food icons** are instantiated via `TileVisual.FoodModelFor(tileSet, value)` onto the tile's `FoodAnchor` (unchanged).
- **Regenerate assets** by running the editor generators. Batchmode form:
  `Unity -batchmode -quit -projectPath unity/GameClient -executeMethod <Class>.<Method>`.
  (Or the equivalent editor menu item in the Unity UI.)
- **Approved visual targets (from the mock):** ivory face gradient `#F7F2E6` (top) → `#EAE1C9` (bottom); jade bevel `#2F8A54`, **medium** weight; corner radius ≈ 16% of tile width; real thickness (keep ≈ current `0.18`); blocked-tile veil multiplies toward `#262E22`.

---

## File Structure

- Create `unity/GameClient/Assets/Scripts/Presentation/Board3D/RoundedTileMesh.cs` — pure static mesh builder (runtime, so both the editor generator and EditMode tests can call it).
- Create `unity/GameClient/Assets/Scripts/Presentation/Board3D/TileFaceTexture.cs` — pure static albedo-texture builder (runtime).
- Create `unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs` — bakes the face texture asset + `TileBody.mat`.
- Modify `unity/GameClient/Assets/Scripts/Editor/TileMeshGenerator.cs` — use `RoundedTileMesh` + `TileBody.mat`; enlarge `FoodAnchor`.
- Modify `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder3D.cs` — key-light angle + soft fill + URP shadow distance so stacked tiles read with depth.
- Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Rendering/RoundedTileMeshTests.cs`.
- Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Rendering/TileFaceTextureTests.cs`.

> **Note on verification:** mesh/texture *logic* is unit-tested (pure functions). The *rendered* result (prefab in scene, lighting, shadows) is verified visually against the mock — Unity graphics have no meaningful headless assertion. Visual-check steps say exactly what to look for.

---

### Task 1: Rounded tile mesh builder

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board3D/RoundedTileMesh.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Rendering/RoundedTileMeshTests.cs`

**Interfaces:**
- Produces: `public static Mesh RoundedTileMesh.Build(float width, float height, float thickness, float cornerRadius, int cornerSegments)` — a rounded-rectangle slab centered at the origin on the XY plane, extruded along Z (front face toward `-Z`, matching how `BoardView3D` pulls tiles toward the camera).

- [ ] **Step 1: Write the failing test**

```csharp
using GameClient.Presentation.Board3D;
using NUnit.Framework;
using UnityEngine;

namespace GameClient.Tests.EditMode.Rendering
{
    public class RoundedTileMeshTests
    {
        [Test]
        public void Build_ProducesSlabWithRequestedExtents()
        {
            var mesh = RoundedTileMesh.Build(width: 1.0f, height: 1.3f, thickness: 0.18f,
                                             cornerRadius: 0.16f, cornerSegments: 6);

            Assert.Greater(mesh.triangles.Length, 0, "mesh should have triangles");
            Assert.AreEqual(1.0f, mesh.bounds.size.x, 0.001f);
            Assert.AreEqual(1.3f, mesh.bounds.size.y, 0.001f);
            Assert.AreEqual(0.18f, mesh.bounds.size.z, 0.01f);
        }

        [Test]
        public void Build_ClampsCornerRadiusToHalfShortSide()
        {
            // radius bigger than half the short side must not blow past the extents
            var mesh = RoundedTileMesh.Build(1.0f, 1.3f, 0.18f, cornerRadius: 5f, cornerSegments: 4);
            Assert.LessOrEqual(mesh.bounds.size.x, 1.001f);
            Assert.LessOrEqual(mesh.bounds.size.y, 1.301f);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (Unity UI): Window → General → Test Runner → EditMode → run `RoundedTileMeshTests`.
Batchmode: `Unity -batchmode -quit -projectPath unity/GameClient -runTests -testPlatform EditMode -testFilter RoundedTileMeshTests`
Expected: FAIL — `RoundedTileMesh` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    // Procedural rounded-rectangle slab, centred at the origin on XY, extruded
    // along Z. Front face at -Z (toward the camera); back at +Z. Pure/testable:
    // no editor or asset dependencies, so EditMode tests can call it directly.
    public static class RoundedTileMesh
    {
        public static Mesh Build(float width, float height, float thickness,
                                 float cornerRadius, int cornerSegments)
        {
            cornerSegments = Mathf.Max(1, cornerSegments);
            cornerRadius = Mathf.Min(cornerRadius, Mathf.Min(width, height) * 0.5f);

            var perimeter = BuildPerimeter(width, height, cornerRadius, cornerSegments);
            int n = perimeter.Count;
            float halfT = thickness * 0.5f;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // FRONT (-Z), triangle fan from centre
            int frontCentre = verts.Count;
            verts.Add(new Vector3(0, 0, -halfT)); normals.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            int frontStart = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = perimeter[i];
                verts.Add(new Vector3(p.x, p.y, -halfT));
                normals.Add(Vector3.back);
                uvs.Add(new Vector2(p.x / width + 0.5f, p.y / height + 0.5f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = frontStart + i;
                int b = frontStart + (i + 1) % n;
                tris.Add(frontCentre); tris.Add(b); tris.Add(a);
            }

            // BACK (+Z)
            int backCentre = verts.Count;
            verts.Add(new Vector3(0, 0, halfT)); normals.Add(Vector3.forward); uvs.Add(new Vector2(0.5f, 0.5f));
            int backStart = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = perimeter[i];
                verts.Add(new Vector3(p.x, p.y, halfT));
                normals.Add(Vector3.forward);
                uvs.Add(new Vector2(p.x / width + 0.5f, p.y / height + 0.5f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = backStart + i;
                int b = backStart + (i + 1) % n;
                tris.Add(backCentre); tris.Add(a); tris.Add(b);
            }

            // SIDE wall
            int sideStart = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = perimeter[i];
                var outward = new Vector3(p.x, p.y, 0f).normalized;
                verts.Add(new Vector3(p.x, p.y, -halfT)); normals.Add(outward); uvs.Add(new Vector2((float)i / n, 0f));
                verts.Add(new Vector3(p.x, p.y, halfT));  normals.Add(outward); uvs.Add(new Vector2((float)i / n, 1f));
            }
            for (int i = 0; i < n; i++)
            {
                int i0 = sideStart + i * 2;
                int i1 = sideStart + i * 2 + 1;
                int j0 = sideStart + ((i + 1) % n) * 2;
                int j1 = sideStart + ((i + 1) % n) * 2 + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(j0);
                tris.Add(j0); tris.Add(i1); tris.Add(j1);
            }

            var mesh = new Mesh { name = "RoundedTile" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // CCW rounded-rect outline, starting at the bottom-right corner arc.
        private static List<Vector2> BuildPerimeter(float w, float h, float r, int seg)
        {
            var pts = new List<Vector2>();
            float hw = w * 0.5f, hh = h * 0.5f;
            var centres = new[]
            {
                new Vector2(hw - r, -hh + r), // bottom-right
                new Vector2(hw - r,  hh - r), // top-right
                new Vector2(-hw + r, hh - r), // top-left
                new Vector2(-hw + r,-hh + r), // bottom-left
            };
            float[] startAng = { -90f, 0f, 90f, 180f };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = (startAng[c] + 90f * s / seg) * Mathf.Deg2Rad;
                    pts.Add(centres[c] + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
            return pts;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode `RoundedTileMeshTests`. Expected: PASS (both tests).

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/RoundedTileMesh.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Rendering/RoundedTileMeshTests.cs
git commit -m "feat: add procedural rounded tile mesh builder"
```

---

### Task 2: Ivory + jade face texture builder

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board3D/TileFaceTexture.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Rendering/TileFaceTextureTests.cs`

**Interfaces:**
- Produces: `public static Texture2D TileFaceTexture.Build(int size, Color ivoryTop, Color ivoryBottom, Color jade, float framePadding, float frameThickness, float cornerRadius)` — square albedo: vertical ivory gradient with an inset jade rounded-rect stroke. `framePadding`, `frameThickness`, `cornerRadius` are fractions of the texture size (0..1).

- [ ] **Step 1: Write the failing test**

```csharp
using GameClient.Presentation.Board3D;
using NUnit.Framework;
using UnityEngine;

namespace GameClient.Tests.EditMode.Rendering
{
    public class TileFaceTextureTests
    {
        private static readonly Color Ivory = new Color(0.965f, 0.949f, 0.902f); // ~#F6F2E6
        private static readonly Color Jade  = new Color(0.184f, 0.541f, 0.329f); // ~#2F8A54

        [Test]
        public void Build_CentreIsIvory_NotJade()
        {
            var tex = TileFaceTexture.Build(128, Ivory, Ivory, Jade,
                framePadding: 0.10f, frameThickness: 0.03f, cornerRadius: 0.14f);
            var c = tex.GetPixel(64, 64);
            Assert.Less(Vector4.Distance((Vector4)c, (Vector4)Ivory), 0.08f, "centre should be ivory");
            Assert.Greater(Vector4.Distance((Vector4)c, (Vector4)Jade), 0.2f, "centre must not be jade");
        }

        [Test]
        public void Build_FrameRingContainsJade()
        {
            var tex = TileFaceTexture.Build(128, Ivory, Ivory, Jade,
                framePadding: 0.10f, frameThickness: 0.03f, cornerRadius: 0.14f);
            // walk a vertical line inward from the top edge; the frame band should
            // produce at least one strongly-jade pixel.
            bool foundJade = false;
            for (int y = 0; y < 64; y++)
            {
                var c = tex.GetPixel(64, y);
                if (Vector4.Distance((Vector4)c, (Vector4)Jade) < 0.08f) { foundJade = true; break; }
            }
            Assert.IsTrue(foundJade, "expected a jade frame pixel along the inset ring");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode `TileFaceTextureTests`. Expected: FAIL — `TileFaceTexture` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    // Pure albedo generator for the tile face: vertical ivory gradient + an inset
    // jade rounded-rect frame stroke. Fractions (padding/thickness/radius) are in
    // 0..1 of the texture size so the look is resolution-independent.
    public static class TileFaceTexture
    {
        public static Texture2D Build(int size, Color ivoryTop, Color ivoryBottom, Color jade,
                                      float framePadding, float frameThickness, float cornerRadius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "TileFace",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                Color baseCol = Color.Lerp(ivoryBottom, ivoryTop, v);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float d = Mathf.Abs(RoundedRectSdf(u, v, framePadding, cornerRadius));
                    // 1 inside the stroke band, fading to 0 just outside it
                    float half = frameThickness * 0.5f;
                    float band = 1f - Mathf.SmoothStep(half, half + 1.5f / size, d);
                    tex.SetPixel(x, y, Color.Lerp(baseCol, jade, band));
                }
            }
            tex.Apply(updateMipmaps: true);
            return tex;
        }

        // Signed distance (in uv units) to a centred rounded rectangle whose edges
        // sit `padding` in from each side. Negative = inside, positive = outside.
        private static float RoundedRectSdf(float u, float v, float padding, float radius)
        {
            float px = Mathf.Abs(u - 0.5f);
            float py = Mathf.Abs(v - 0.5f);
            float halfExtent = 0.5f - padding;      // rect half-size
            float inner = halfExtent - radius;      // straight-section half-size
            float qx = px - inner;
            float qy = py - inner;
            float ax = Mathf.Max(qx, 0f);
            float ay = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(ax * ax + ay * ay);
            float insideCorner = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + insideCorner - radius;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode `TileFaceTextureTests`. Expected: PASS (both tests).

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/TileFaceTexture.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Rendering/TileFaceTextureTests.cs
git commit -m "feat: add procedural ivory+jade tile face texture builder"
```

---

### Task 3: Tile material generator (bakes TileBody.mat)

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs`

**Interfaces:**
- Consumes: `TileFaceTexture.Build(...)`.
- Produces: asset `Assets/Textures/TileFace.png` and material `Assets/Materials/TileBody.mat` (shader `Universal Render Pipeline/Lit`, matte, `_BaseMap` = TileFace). Entry point `public static void TileMaterialGenerator.Generate()` under menu `Tools/Mahjong/Generate Tile Material`.

- [ ] **Step 1: Write the generator (no unit test — asset baking; verified in Task 4)**

```csharp
using System.IO;
using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMaterialGenerator
{
    private static readonly Color IvoryTop    = new Color(0.969f, 0.949f, 0.902f); // #F7F2E6
    private static readonly Color IvoryBottom = new Color(0.918f, 0.882f, 0.788f); // #EAE1C9
    private static readonly Color Jade        = new Color(0.184f, 0.541f, 0.329f); // #2F8A54

    [MenuItem("Tools/Mahjong/Generate Tile Material")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        var tex = TileFaceTexture.Build(512, IvoryTop, IvoryBottom, Jade,
            framePadding: 0.085f, frameThickness: 0.028f, cornerRadius: 0.14f);
        File.WriteAllBytes("Assets/Textures/TileFace.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Textures/TileFace.png");

        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/TileFace.png");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var faceTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/TileFace.png");

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", faceTex);
        mat.SetColor("_BaseColor", Color.white);           // tint stays white; MeshRendererTint drives free/blocked
        mat.SetFloat("_Smoothness", 0.15f);                // matte bone
        mat.SetColor("_EmissionColor", Color.black);
        AssetDatabase.CreateAsset(mat, "Assets/Materials/TileBody.mat");

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_MATERIAL_GENERATOR_DONE");
    }
}
```

- [ ] **Step 2: Run the generator**

Unity UI: menu `Tools → Mahjong → Generate Tile Material`.
Batchmode: `Unity -batchmode -quit -projectPath unity/GameClient -executeMethod TileMaterialGenerator.Generate`
Expected: console logs `TILE_MATERIAL_GENERATOR_DONE`; `Assets/Materials/TileBody.mat` and `Assets/Textures/TileFace.png` exist.

- [ ] **Step 3: Visual check the texture**

Open `Assets/Textures/TileFace.png` in the Inspector. Confirm: warm ivory field, a clean jade rounded-rect frame inset from the edge, medium stroke weight (compare to mock variant B). If the frame is too thin/thick, adjust `frameThickness`; if too close to the edge, adjust `framePadding`; regenerate.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs \
        unity/GameClient/Assets/Materials/TileBody.mat* \
        unity/GameClient/Assets/Textures/TileFace.png*
git commit -m "feat: generate ivory+jade TileBody material"
```

---

### Task 4: Rebuild the tile prefab with the rounded mesh + material + bigger icon

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Editor/TileMeshGenerator.cs`

**Interfaces:**
- Consumes: `RoundedTileMesh.Build(...)`, `Assets/Materials/TileBody.mat`.
- Produces: regenerated `Assets/Prefabs/Tile3D.prefab` — a `MeshFilter`+`MeshRenderer` using the rounded mesh and `TileBody.mat`, a `BoxCollider` sized to the tile footprint, and a `FoodAnchor` scaled up ~1.4×.

- [ ] **Step 1: Replace the Cube body with the rounded mesh**

In `TileMeshGenerator.Generate()`, replace the `GameObject.CreatePrimitive(PrimitiveType.Cube)` body block with a mesh built from `RoundedTileMesh`, and load `TileBody.mat` instead of `CardBody.mat`:

```csharp
float w = CardStyle.CardSizeRatio * CardStyle.CardAspectRatio;
float h = CardStyle.CardSizeRatio;
float cornerRadius = w * 0.16f;

var body = new GameObject("CardBody", typeof(MeshFilter), typeof(MeshRenderer));
body.transform.SetParent(root.transform, false);
body.GetComponent<MeshFilter>().sharedMesh =
    RoundedTileMesh.Build(w, h, CardThickness, cornerRadius, cornerSegments: 6);

var bodyRenderer = body.GetComponent<MeshRenderer>();
var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileBody.mat");
if (cardMaterial == null)
    throw new System.Exception("TILE_MESH_GENERATOR_MISSING_MATERIAL: run TileMaterialGenerator first");
bodyRenderer.sharedMaterial = cardMaterial;
bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
bodyRenderer.receiveShadows = true;
```

The generated mesh is not an asset; save it alongside the prefab so it survives:

```csharp
AssetDatabase.AddObjectToAsset(body.GetComponent<MeshFilter>().sharedMesh, "Assets/Prefabs/Tile3D.prefab");
```
(Place this call immediately after `SaveAsPrefabAsset`, then `AssetDatabase.SaveAssets()`.)

- [ ] **Step 2: Enlarge the food icon**

Where `FoodAnchor` is created, scale it up and keep it proud of the face:

```csharp
foodAnchorGO.transform.localPosition = new Vector3(0f, 0f, -(CardThickness / 2f + 0.02f));
foodAnchorGO.transform.localScale = Vector3.one * 1.4f;
```

- [ ] **Step 3: Run the generator**

Unity UI: run `TileMeshGenerator.Generate` (its existing menu item), or
batchmode `-executeMethod TileMeshGenerator.Generate`.
Expected: console logs `TILE_MESH_GENERATOR_DONE`; `Assets/Prefabs/Tile3D.prefab` updated.

- [ ] **Step 4: Visual check the prefab**

Drag `Tile3D.prefab` into an empty scene (or open the prefab). Confirm against the mock: rounded silhouette (no square corners), ivory face with jade frame, visible thickness on the side, food model centred and noticeably larger than before. If the face texture appears inside-out or the tile renders dark, the front-face winding is flipped — swap the two `tris.Add(b); tris.Add(a);` on the FRONT face in `RoundedTileMesh` and regenerate.

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/TileMeshGenerator.cs \
        unity/GameClient/Assets/Prefabs/Tile3D.prefab*
git commit -m "feat: rebuild tile prefab with rounded ivory+jade mesh and larger icon"
```

---

### Task 5: Tune scene lighting & shadows for depth

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder3D.cs`

**Interfaces:**
- Consumes: nothing new. Produces: a regenerated scene whose key light + shadow settings make stacked tiles cast soft shadows onto the tiles below.

- [ ] **Step 1: Angle the key light and add a soft fill**

In `GameSceneBuilder3D` where the `Key Light` is created, set a modelling angle, soft shadows, and add a dim fill light so the ivory doesn't read flat:

```csharp
lightGO.transform.rotation = Quaternion.Euler(55f, -35f, 0f); // top-left key
light.intensity = 1.05f;
light.shadows = LightShadows.Soft;
light.shadowStrength = 0.55f;   // soft, not black

var fillGO = new GameObject("Fill Light", typeof(Light));
var fill = fillGO.GetComponent<Light>();
fill.type = LightType.Directional;
fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
fill.intensity = 0.35f;
fill.shadows = LightShadows.None;
```

- [ ] **Step 2: Ensure shadow distance covers the stack**

After the camera is created in `GameSceneBuilder3D`, set the URP shadow distance so the whole tilted board is within shadow range (tiles are only ~a couple units deep, but the camera sits ~18 units back):

```csharp
var urp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)
          UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
if (urp != null) urp.shadowDistance = 40f;
```
(If the project pins the URP asset elsewhere, set `shadowDistance` on that asset instead. Import `using UnityEngine.Rendering.Universal;` if preferred.)

- [ ] **Step 3: Regenerate the scene**

Run `GameSceneBuilder3D`'s existing build entry point (menu item / `-executeMethod`).
Expected: the game scene is rebuilt without errors.

- [ ] **Step 4: Visual check on device/editor against the mock**

Enter Play mode (or build to device). Confirm: tiles have gentle top-left shading (not flat), and where layers overlap, the raised tiles cast a soft shadow onto the tiles below — the "clean stacked pyramid" read from the mock's depth demo. Jitter is still present at this phase (removed in Phase 2); ignore alignment for now — judge only tile material, lighting, and shadows.

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder3D.cs \
        unity/GameClient/Assets/Scenes/*.unity
git commit -m "feat: tune key/fill light and shadow distance for tile depth"
```

---

## Phase 1 Definition of Done

- EditMode tests pass for `RoundedTileMesh` and `TileFaceTexture`.
- `Tile3D.prefab` renders as a rounded ivory tile with a jade frame, real thickness, and an enlarged food icon.
- In Play mode, stacked tiles cast soft shadows onto lower tiles.
- `CardBody.mat` (HUD) is untouched; only `TileBody.mat` drives tiles.
- **You review the result against the mock and approve before Phase 2 (board rollout + felt background + jitter removal) is planned.**

## Self-Review (completed)

- **Spec coverage (Phase 1 slice):** tile mesh ✓ (T1,T4), ivory+jade material ✓ (T2,T3), enlarged icon ✓ (T4), lighting/shadows for depth ✓ (T5). Board rollout, felt bg, no-tray mechanic, wooden chrome, progress bar, selected-tile glow+lift are **later phases**, intentionally out of this plan.
- **Placeholder scan:** none — all steps carry real C# and exact run/verify commands.
- **Type consistency:** `RoundedTileMesh.Build(width,height,thickness,cornerRadius,cornerSegments)` and `TileFaceTexture.Build(size,ivoryTop,ivoryBottom,jade,framePadding,frameThickness,cornerRadius)` are referenced with matching signatures in Tasks 3–4. `TileBody.mat` path is consistent across Tasks 3–4. `CardBody.mat` is explicitly *not* modified.
