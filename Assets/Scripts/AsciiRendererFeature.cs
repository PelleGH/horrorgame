using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class AsciiRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        public Texture2D glyphAtlas;
        [Range(4, 64)] public int cellSize = 8;
        [Range(1, 64)] public int glyphColumns = 16;
        [Range(1, 16)] public int glyphRows = 1;
        [Range(0f, 4f)] public float luminanceBoost = 1f;
        [Range(0f, 4f)] public float contrast = 1.2f;
        [Range(0f, 8f)] public float colorSteps = 0f;
    }

    public Settings settings = new Settings();

    Material material;
    AsciiPass asciiPass;

    public override void Create()
    {
        if (settings.shader == null)
            return;

        material = CoreUtils.CreateEngineMaterial(settings.shader);
        asciiPass = new AsciiPass(material, settings);
        asciiPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (asciiPass == null || settings.glyphAtlas == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(asciiPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (material == null) return;

        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }

    class AsciiPass : ScriptableRenderPass
    {
        readonly Material material;
        readonly Settings settings;

        static readonly int GlyphAtlasId = Shader.PropertyToID("_GlyphAtlas");
        static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
        static readonly int GlyphGridId = Shader.PropertyToID("_GlyphGrid");
        static readonly int LuminanceBoostId = Shader.PropertyToID("_LuminanceBoost");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int ColorStepsId = Shader.PropertyToID("_ColorSteps");

        const string k_PassName = "ASCII Post Process";

        public AsciiPass(Material material, Settings settings)
        {
            this.material = material;
            this.settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle src = resourceData.activeColorTexture;
            if (!src.IsValid())
                return;

            material.SetTexture(GlyphAtlasId, settings.glyphAtlas);
            material.SetFloat(CellSizeId, settings.cellSize);
            material.SetVector(GlyphGridId, new Vector4(settings.glyphColumns, settings.glyphRows, 0, 0));
            material.SetFloat(LuminanceBoostId, settings.luminanceBoost);
            material.SetFloat(ContrastId, settings.contrast);
            material.SetFloat(ColorStepsId, settings.colorSteps);

            RenderGraphUtils.BlitMaterialParameters blitParams =
                new (src, src, material, 0);

            renderGraph.AddBlitPass(blitParams, k_PassName);
        }
    }
}