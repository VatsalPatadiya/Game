using System.IO;
using UnityEditor;
using UnityEngine;

// Wood + bronze materials for the HUD chrome (score pill, tray, popup = wood;
// control-button discs = bronze), matching the reference's warm wooden UI.
public static class WoodUiGenerator
{
    private static readonly Color WoodBottom = new Color(0.102f, 0.137f, 0.176f); // #1A232D Obsidian Slate
    private static readonly Color WoodTop    = new Color(0.149f, 0.200f, 0.251f); // #263340
    private static readonly Color Bronze     = new Color(0.855f, 0.710f, 0.380f); // #DAC561 Champagne Gold
    // Champagne Gold accent for HUD borders and button rims (#DAC561)
    private static readonly Color AmberChrome = new Color(0.855f, 0.710f, 0.380f); // #DAC561 gold

    [MenuItem("Tools/Mahjong/Generate Wood UI")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        // --- wood grain texture ---
        const int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = "Wood", wrapMode = TextureWrapMode.Clamp };
        var rng = new System.Random(4242);
        var streak = new float[w];
        for (int x = 0; x < w; x++) streak[x] = ((float)rng.NextDouble() - 0.5f);
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float t = v * v * (3f - 2f * v);
            Color baseCol = Color.Lerp(WoodBottom, WoodTop, t);
            for (int x = 0; x < w; x++)
            {
                float grain = Mathf.Sin(x * 0.20f + streak[x] * 3f) * 0.025f + streak[x] * 0.02f;
                var c = new Color(
                    Mathf.Clamp01(baseCol.r + grain),
                    Mathf.Clamp01(baseCol.g + grain * 0.9f),
                    Mathf.Clamp01(baseCol.b + grain * 0.8f), 1f);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(true);
        File.WriteAllBytes("Assets/Textures/Wood.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Textures/Wood.png");
        var imp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Wood.png");
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.SaveAndReimport();
        var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Wood.png");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        var wood = LoadOrCreate("Assets/Materials/Wood.mat", shader);
        wood.SetTexture("_BaseMap", woodTex);
        wood.SetColor("_BaseColor", Color.white);
        wood.SetFloat("_Smoothness", 0.22f);
        wood.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(wood);

        // --- bronze disc texture: dark chocolate face with a bold warm-tan rim
        // band near the edge (like a bezel), matching the reference HUD button
        // art. Rendered with an UNLIT material (below) so these baked colors
        // show flatly on-device instead of being washed into a soft specular
        // blotch by the scene's tile-board lighting - a Lit material here reads
        // dull/blurry instead of crisp and "premium". A first attempt added a
        // large soft "highlight" blob to fake a glossy bevel, but with no real
        // lighting to soften it, unlit rendering showed it raw as a blown-out
        // white smear instead of a bevel - a flat two-tone ring with only a
        // faint linear gradient reads far closer to the reference.
        Color BronzeColorAt(float u, float vv)
        {
            var faceColor = new Color(0.20f, 0.135f, 0.085f);  // #332216
            var rimColor  = new Color(0.87f, 0.70f, 0.44f);    // #DDB370
            float d = Mathf.Clamp01(Mathf.Sqrt(u * u + vv * vv));
            float rim = Mathf.Clamp01((d - 0.66f) / 0.20f); // stays face colour until d=0.66, full rim by d=0.86 - a noticeably thicker bezel
            rim = rim * rim * (3f - 2f * rim);
            var c = Color.Lerp(faceColor, rimColor, rim);
            float shade = 1f + vv * 0.06f; // faint top-lighter/bottom-darker gradient, not strong enough to blow out either color
            return new Color(Mathf.Clamp01(c.r * shade), Mathf.Clamp01(c.g * shade), Mathf.Clamp01(c.b * shade), 1f);
        }

        // HUD control-button disc: approved "Style A / Ember" button chrome
        // from the HTML mockup, ported literally rather than re-derived - a
        // crisp solid amber ring border (CSS `border:3px solid var(--amber)`)
        // around a radial-gradient dark disc (CSS
        // `radial-gradient(circle at 32% 28%, #4a3218 0%, #1f150d 46%, #0b0704 100%)`).
        // This replaces the earlier hemisphere-fake-light bevel, which was a
        // different (soft-blended) look than what the mockup actually shows -
        // a flat gradient disc with a hard-edged ring, not a lit sphere.
        const float RingInner = 0.86f; // outer ~14% of radius is the solid amber ring - approximates the mockup's 3px border on a ~26px-radius button
        Color RadialDiscColorAt(float u, float vv, Color ringColor, Color highlight, Color face, Color shadow)
        {
            float d = Mathf.Sqrt(u * u + vv * vv);
            if (d >= RingInner) return ringColor;
            // Gradient centre at CSS "32% 28% from top-left" -> UV space
            // (u=-1 left..1 right, vv=-1 bottom..1 top, since vv is 1 at the
            // top-left one 28% down from the top a fraction of 0.72).
            const float cu = -0.36f, cv = 0.44f;
            float dg = Mathf.Sqrt((u - cu) * (u - cu) + (vv - cv) * (vv - cv));
            const float maxDg = 1.9f; // approximates radial-gradient's default farthest-corner sizing from this off-centre point
            float t = Mathf.Clamp01(dg / maxDg);
            if (t < 0.46f)
                return Color.Lerp(highlight, face, t / 0.46f);
            return Color.Lerp(face, shadow, (t - 0.46f) / (1f - 0.46f));
        }

        // Obsidian Slate + Champagne Gold button face
        var StyleARing = AmberChrome;                         // #DAC561 Champagne gold
        var StyleAHighlight = new Color(0.180f, 0.240f, 0.300f); // subtle top-left highlight
        var StyleAFace = new Color(0.110f, 0.150f, 0.190f);      // obsidian slate face
        var StyleAShadow = new Color(0.060f, 0.080f, 0.110f);    // deep obsidian shadow
        Color HudButtonFaceColorAt(float u, float vv) =>
            RadialDiscColorAt(u, vv, StyleARing, StyleAHighlight, StyleAFace, StyleAShadow);

        // Locked/disabled look - muted steel slate
        var LockedRing = new Color(0.350f, 0.390f, 0.440f);      // muted steel slate
        var LockedHighlight = new Color(0.160f, 0.200f, 0.240f); 
        var LockedFace = new Color(0.110f, 0.140f, 0.170f);      
        var LockedShadow = new Color(0.070f, 0.090f, 0.110f);    
        Color HudButtonFaceLockedColorAt(float u, float vv) =>
            RadialDiscColorAt(u, vv, LockedRing, LockedHighlight, LockedFace, LockedShadow);

        // Bakes a vertical light-top/dark-bottom gradient (same direction as
        // Wood.mat's own grain gradient, for a consistent "lit from above"
        // feel across the HUD) into `mat`'s _BaseMap, replacing a flat single
        // _BaseColor. Flat single-color panels (tried for the tray/progress
        // bar backgrounds) read as cheap/paper-cutout regardless of which
        // color is picked - a subtle gradient is what makes a flat shape read
        // as a physical panel with depth.
        // recessed=true flips the gradient (dark at TOP, lighter at bottom) so an
        // inset panel reads as sunken - an inner shadow along the top edge + a
        // faint inner highlight at the bottom, the opposite lighting of a raised
        // panel (guidelines s6: recessed vs raised must read as physically distinct).
        void ApplyVerticalGradientPanel(Material mat, string texName, Color baseColor, float alpha = 1f, bool recessed = false)
        {
            var darker = new Color(baseColor.r * 0.72f, baseColor.g * 0.72f, baseColor.b * 0.72f, 1f);
            var lighter = new Color(Mathf.Clamp01(baseColor.r * 1.35f), Mathf.Clamp01(baseColor.g * 1.35f), Mathf.Clamp01(baseColor.b * 1.35f), 1f);
            var bottom = recessed ? lighter : darker;
            var top = recessed ? darker : lighter;
            const int gw = 8, gh = 128;
            var gtex2 = new Texture2D(gw, gh, TextureFormat.RGBA32, mipChain: false) { name = texName, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < gh; y++)
            {
                float t = y / (float)(gh - 1);
                t = t * t * (3f - 2f * t);
                var c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < gw; x++) gtex2.SetPixel(x, y, c);
            }
            gtex2.Apply(true);
            string path = "Assets/Textures/" + texName + ".png";
            File.WriteAllBytes(path, gtex2.EncodeToPNG());
            Object.DestroyImmediate(gtex2);
            AssetDatabase.ImportAsset(path);
            var gimp2 = (TextureImporter)AssetImporter.GetAtPath(path);
            gimp2.textureType = TextureImporterType.Default;
            gimp2.sRGBTexture = true;
            gimp2.wrapMode = TextureWrapMode.Clamp;
            gimp2.SaveAndReimport();
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
        }

        var btex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = "Bronze", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1) * 2f - 1f;
            float vv = y / (float)(h - 1) * 2f - 1f;
            btex.SetPixel(x, y, BronzeColorAt(u, vv));
        }
        btex.Apply(true);
        File.WriteAllBytes("Assets/Textures/Bronze.png", btex.EncodeToPNG());
        Object.DestroyImmediate(btex);
        AssetDatabase.ImportAsset("Assets/Textures/Bronze.png");
        var bimp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Bronze.png");
        bimp.textureType = TextureImporterType.Default; bimp.sRGBTexture = true; bimp.SaveAndReimport();
        var bronzeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Bronze.png");

        var bronze = LoadOrCreate("Assets/Materials/Bronze.mat", unlitShader);
        bronze.SetTexture("_BaseMap", bronzeTex);
        bronze.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(bronze);

        // --- HUD button face: the same bronze look, but as an alpha-cutout
        // circle on a flat QUAD instead of a Cylinder cap. A Cylinder's
        // built-in primitive cap UVs turned out not to be a clean radial
        // mapping - the dark-center/tan-rim gradient rendered as a lopsided
        // blob (bright top, dark bottom) instead of a symmetric ring, even
        // though the source texture itself (Bronze.png) is a perfect
        // symmetric circle. Icon/badge/lock glyphs in GameSceneBuilder3D
        // already render crisply as flat alpha-cutout quads - this reuses
        // that same, proven approach for the button face itself.
        const int faceSize = 256;
        Material BakeButtonFace(string name, System.Func<float, float, Color> colorAt)
        {
            var facetex = new Texture2D(faceSize, faceSize, TextureFormat.RGBA32, mipChain: true) { name = name, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < faceSize; y++)
            for (int x = 0; x < faceSize; x++)
            {
                float u = x / (float)(faceSize - 1) * 2f - 1f;
                float vv = y / (float)(faceSize - 1) * 2f - 1f;
                float d = Mathf.Sqrt(u * u + vv * vv);
                var c = colorAt(u, vv);
                c.a = 1f - Mathf.Clamp01((d - 0.97f) / 0.03f); // crisp circular edge, antialiased over the last 3% of radius
                facetex.SetPixel(x, y, c);
            }
            facetex.Apply(true);
            string texPath = "Assets/Textures/" + name + ".png";
            File.WriteAllBytes(texPath, facetex.EncodeToPNG());
            Object.DestroyImmediate(facetex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var fimp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            // Sprite type + alphaIsTransparency, not Default: Default-type textures on
            // this project's Android target compress to a format that drops the alpha
            // channel entirely, silently turning the circular cutout into an opaque
            // square on-device (looked fine in the Editor preview, which doesn't use
            // the same compressed format). This exactly mirrors ProceduralSpriteGenerator's
            // settings, already proven to preserve alpha correctly for the icon/badge glyphs.
            fimp.textureType = TextureImporterType.Sprite;
            fimp.spriteImportMode = SpriteImportMode.Single;
            fimp.alphaIsTransparency = true;
            fimp.mipmapEnabled = false;
            fimp.sRGBTexture = true;
            fimp.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);

            var mat = LoadOrCreate("Assets/Materials/" + name + ".mat", unlitShader);
            mat.SetTexture("_BaseMap", sprite.texture);
            mat.SetColor("_BaseColor", Color.white);
            // Alpha-CUTOUT, not Transparent/blend: the disc's content is binary
            // (opaque circle or fully invisible outside it), and blend-mode
            // transparency put this disc in Unity's back-to-front transparent
            // sort alongside the icon glyph sitting just in front of it - an
            // unreliable sort that let the "opaque-looking" disc win and paint
            // over the icon's center. Cutout renders Opaque with a real Z-buffer
            // test, so the icon (at a genuinely closer Z) reliably wins instead.
            URPMaterialUtil.SetAlphaCutout(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        BakeButtonFace("HudButtonFace", HudButtonFaceColorAt);
        BakeButtonFace("HudButtonFaceLocked", HudButtonFaceLockedColorAt);

        // Flat near-black for each individual EMPTY slot cell - reference
        // shows a flat dark tray, not a wood-grain texture, so this has no
        // _BaseMap (a null base map with a flat _BaseColor renders as solid
        // color). Deliberately DARKER than TrayBody below so the slot cutouts
        // still read as distinct recessed pockets against the panel behind
        // them - using the same flat color for both (an earlier attempt)
        // made the four slots invisibly blend into one solid black rectangle.
        // Much darker than TrayBody (0.06,0.16,0.12) across its WHOLE gradient so
        // the slot recess reads as a full-height dark pocket. The previous recess
        // was so close to the body colour that each slot's lighter lower half blended
        // into the body, looking like a HALF-HEIGHT slot (the reported "half box" -
        // it was a colour collision, not geometry; the slot mesh is 80% of the
        // container, centered, confirmed by runtime bounds).
        var recess = LoadOrCreate("Assets/Materials/TrayRecess.mat", shader);
        recess.SetTexture("_BaseMap", null);
        recess.SetTexture("_MainTex", null);
        recess.SetColor("_BaseColor", new Color(0.060f, 0.080f, 0.100f)); // #0F141A deep recessed well
        recess.SetFloat("_Smoothness", 0.1f);
        recess.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(recess);

        // Obsidian Slate for the tray's inner PANEL
        var trayBody = LoadOrCreate("Assets/Materials/TrayBody.mat", shader);
        ApplyVerticalGradientPanel(trayBody, "TrayBodyGradient", new Color(0.120f, 0.160f, 0.205f)); // #1F2934
        trayBody.SetFloat("_Smoothness", 0.1f);
        trayBody.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(trayBody);

        // Champagne Gold border frame behind the tray's body
        var trayBorder = LoadOrCreate("Assets/Materials/TrayBorder.mat", shader);
        ApplyVerticalGradientPanel(trayBorder, "TrayBorderGradient", AmberChrome);
        trayBorder.SetFloat("_Smoothness", 0f); // flat matte gold
        trayBorder.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(trayBorder);

        // Progress bar Champagne Gold border frame
        var progressBorder = LoadOrCreate("Assets/Materials/ProgressBorder.mat", shader);
        ApplyVerticalGradientPanel(progressBorder, "ProgressBorderGradient", AmberChrome);
        progressBorder.SetFloat("_Smoothness", 0f);
        progressBorder.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(progressBorder);

        // Sunken obsidian slate track for the progress bar
        var progressBackground = LoadOrCreate("Assets/Materials/ProgressBackground.mat", unlitShader);
        ApplyVerticalGradientPanel(progressBackground, "ProgressBackgroundGradient", new Color(0.070f, 0.095f, 0.125f), recessed: true); // #121820
        EditorUtility.SetDirty(progressBackground);

        // --- gold fill texture for the progress bar (vertical sheen) ---
        var gLight = new Color(0.96f, 0.78f, 0.36f); // #F5C75C
        var gDeep  = new Color(0.82f, 0.55f, 0.16f); // #D18C29
        var gtex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = "Gold", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float sheen = Mathf.Sin(v * Mathf.PI); // brighter in the middle band
            var c = Color.Lerp(gDeep, gLight, sheen);
            for (int x = 0; x < w; x++) gtex.SetPixel(x, y, c);
        }
        gtex.Apply(true);
        File.WriteAllBytes("Assets/Textures/Gold.png", gtex.EncodeToPNG());
        Object.DestroyImmediate(gtex);
        AssetDatabase.ImportAsset("Assets/Textures/Gold.png");
        var gimp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Gold.png");
        gimp.textureType = TextureImporterType.Default; gimp.sRGBTexture = true; gimp.SaveAndReimport();
        var goldTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Gold.png");

        var gold = LoadOrCreate("Assets/Materials/Gold.mat", shader);
        gold.SetTexture("_BaseMap", goldTex);
        gold.SetColor("_BaseColor", Color.white);
        gold.SetColor("_EmissionColor", new Color(0.35f, 0.24f, 0.05f)); // subtle glow so it reads bright
        gold.EnableKeyword("_EMISSION");
        gold.SetFloat("_Smoothness", 0.4f);
        gold.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(gold);

        AssetDatabase.SaveAssets();
        Debug.Log("WOOD_UI_GENERATOR_DONE");
    }

    private static Material LoadOrCreate(string path, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        return m;
    }
}
