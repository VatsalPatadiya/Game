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
}
