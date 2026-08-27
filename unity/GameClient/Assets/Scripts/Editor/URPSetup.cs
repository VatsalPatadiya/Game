using UnityEditor;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class URPSetup
{
    private const string RendererPath = "Assets/Settings/URP-Mobile-Renderer.asset";
    private const string PipelineAssetPath = "Assets/Settings/URP-Mobile-Pipeline.asset";

    public static void Configure()
    {
        System.IO.Directory.CreateDirectory("Assets/Settings");

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }

        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset == null)
        {
            pipelineAsset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
        }

        // Mobile-tier: shadows on (per spec, visuals are prioritized), but
        // capped resolution/distance so it degrades in framerate rather
        // than failing to render on the Galaxy A50 test device.
        pipelineAsset.supportsCameraDepthTexture = false;
        pipelineAsset.supportsCameraOpaqueTexture = false;
        pipelineAsset.shadowDistance = 15f;
        pipelineAsset.mainLightShadowmapResolution = (int)UnityEngine.Rendering.Universal.ShadowResolution._1024;
        pipelineAsset.shadowCascadeCount = 1;
        pipelineAsset.msaaSampleCount = 2;

        GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        QualitySettings.renderPipeline = pipelineAsset;

        AssetDatabase.SaveAssets();
        Debug.Log("URP_SETUP_DONE");
    }
}
