using UnityEngine;

// URP's Lit shader normally has its blend state (SrcBlend/DstBlend/ZWrite/
// render queue/keywords) set by its custom ShaderGUI when you toggle
// "Surface Type: Transparent" in the Inspector - setting just the _Surface
// float from a script (as one might reasonably assume mirrors that toggle)
// silently leaves the material still rendering opaque. This reproduces
// exactly what that ShaderGUI does, so icon quads (which need to show the
// card body behind their transparent corners) actually render correctly.
public static class URPMaterialUtil
{
    public static void SetTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f); // 1 = Transparent
        material.SetFloat("_Blend", 0f);   // 0 = Alpha
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // Alpha-blended Transparent materials have ZWrite off and sort back-to-
    // front by distance, which is unreliable between several transparent
    // quads stacked a fraction of a unit apart (e.g. a disc "face" and an
    // icon glyph sitting just in front of it) - the disc can win the sort and
    // paint over the icon's center, leaving only whatever pokes past its edge
    // visible. A shape that's fundamentally binary (opaque or fully invisible,
    // no soft translucency) should use alpha-clip instead: it renders in the
    // Opaque queue with normal Z-buffer testing, so closer geometry reliably
    // wins regardless of draw/sort order.
    public static void SetAlphaCutout(Material material, float cutoff = 0.5f)
    {
        material.SetFloat("_Surface", 0f); // 0 = Opaque
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", cutoff);
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        // This material asset is reused across regenerations (LoadOrCreate),
        // so a material that was previously Transparent still has
        // _SURFACE_TYPE_TRANSPARENT baked into its m_ValidKeywords - SetFloat
        // on _Surface alone doesn't clear it (same trap SetTransparent's own
        // comment describes for the opposite direction). Left enabled
        // alongside _ALPHATEST_ON, the shader variant stayed in a contradictory
        // half-transparent state that kept sorting into the transparent queue
        // instead of actually rendering Opaque with a real Z-buffer test.
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
    }

    // For a glyph placed at the exact same local Z as the disc behind it
    // (done deliberately - see GameSceneBuilder3D's icon comment on
    // perspective parallax at off-center viewport positions). Equal depth
    // still normally passes a default LEqual ZTest and draws in the right
    // order via render queue, but this removes any floating-point-precision
    // doubt at truly equal Z.
    public static void SetAlwaysOnTop(Material material)
    {
        material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }
}
