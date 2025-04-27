#if UNITY_2020_3_OR_NEWER
#define HAS_CONFIGINPUT
#endif

#if UNITY_2022_3_OR_NEWER
#define HAS_DOUBLEBUFFER_BOTH
#endif

#if UNITY_2023_3_OR_NEWER
#define HAS_RENDERGRAPH
#endif

// ReSharper disable once RedundantUsingDirective
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;

namespace LeTai.Asset.TranslucentImage.UniversalRP
{
enum RendererType
{
    Universal,
    Renderer2D
}

[MovedFrom("LeTai.Asset.TranslucentImage.LWRP")]
public partial class TranslucentImageBlurRenderPass : ScriptableRenderPass
{
    internal struct PassData
    {
        public TranslucentImageSource blurSource;
        public IBlurAlgorithm         blurAlgorithm;
        public Rect                   camPixelRect;
        public bool                   shouldUpdateBlur;
        public bool                   isPreviewing;
        public Material               previewMaterial;
    }

    internal struct SRPassData
    {
#if !HAS_DOUBLEBUFFER_BOTH
        public RendererType                           rendererType;
        public RenderTargetIdentifier                 cameraColorTarget;
        public TranslucentImageBlurSource.RenderOrder renderOrder;
#endif
        public bool canvasDisappearWorkaround;
    }

    public readonly struct PreviewExecutionData
    {
        public readonly TranslucentImageSource blurSource;
        public readonly RenderTargetIdentifier previewTarget;
        public readonly Material               previewMaterial;

        public PreviewExecutionData(
            TranslucentImageSource blurSource,
            RenderTargetIdentifier previewTarget,
            Material               previewMaterial
        )
        {
            this.blurSource      = blurSource;
            this.previewTarget   = previewTarget;
            this.previewMaterial = previewMaterial;
        }
    }

    private const string PROFILER_TAG = "Translucent Image Source";

    readonly URPRendererInternal urpRendererInternal;

    PassData   currentPassData;
    SRPassData currentSRPassData;

    internal TranslucentImageBlurRenderPass(URPRendererInternal urpRendererInternal)
    {
        this.urpRendererInternal = urpRendererInternal;

        RenderGraphInit();
    }

#if !HAS_DOUBLEBUFFER_BOTH
    RenderTargetIdentifier GetAfterPostColor()
    {
        return urpRendererInternal.GetAfterPostColor();
    }
#endif

    internal void SetupSRP(SRPassData srPassData)
    {
        currentSRPassData = srPassData;
    }

    public void Dispose()
    {
        RenderGraphDispose();
    }

    internal void Setup(PassData passData)
    {
        currentPassData = passData;
#if HAS_CONFIGINPUT
        ConfigureInput(ScriptableRenderPassInput.Color);
#if UNITY_6000_0_OR_NEWER
        requiresIntermediateTexture = true;
#endif
#endif
    }

#if HAS_RENDERGRAPH
    [Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
#endif
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var                    cmd = CommandBufferPool.Get(PROFILER_TAG);
        RenderTargetIdentifier sourceTex;

#if !HAS_DOUBLEBUFFER_BOTH
        var isPostProcessEnabled = renderingData.cameraData.postProcessEnabled;
        void SetSource2DRenderer()
        {
            bool useAfterPostTex = isPostProcessEnabled;
            useAfterPostTex &= currentSRPassData.renderOrder == TranslucentImageBlurSource.RenderOrder.AfterPostProcessing;
            sourceTex = useAfterPostTex
                            ? GetAfterPostColor()
                            : currentSRPassData.cameraColorTarget;
        }
#endif

#if HAS_DOUBLEBUFFER_BOTH
        sourceTex = urpRendererInternal.GetBackBuffer();
#else
        if (currentSRPassData.rendererType == RendererType.Universal)
        {
            sourceTex = urpRendererInternal.GetBackBuffer();
        }
        else
        {
            SetSource2DRenderer();
        }
#endif

        var  blurSource        = currentPassData.blurSource;
        bool shouldResetTarget = currentSRPassData.canvasDisappearWorkaround && renderingData.cameraData.resolveFinalTarget;

        if (currentPassData.shouldUpdateBlur)
        {
            if (blurSource.CompleteCull())
            {
                blurSource.ReallocateBlurTexIfNeeded(currentPassData.camPixelRect);
                var blurExecData = new BlurExecutor.BlurExecutionData(sourceTex,
                                                                      blurSource,
                                                                      currentPassData.blurAlgorithm);
                BlurExecutor.ExecuteBlurWithTempTextures(cmd, ref blurExecData);

                if (shouldResetTarget)
                    CoreUtils.SetRenderTarget(cmd, BuiltinRenderTextureType.CameraTarget);
            }
        }


        if (currentPassData.isPreviewing)
        {
            var previewTarget = shouldResetTarget ? BuiltinRenderTextureType.CameraTarget : sourceTex;
            var previewExecData = new PreviewExecutionData(blurSource,
                                                           previewTarget,
                                                           currentPassData.previewMaterial);
            ExecutePreview(cmd, ref previewExecData);
        }


        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public static void ExecutePreview(CommandBuffer cmd, ref PreviewExecutionData data)
    {
        var blurSource = data.blurSource;

        data.previewMaterial.SetVector(ShaderID.CROP_REGION, RectUtils.ToMinMaxVector(blurSource.BlurRegion));
        Blitter.Blit(cmd, blurSource.BlurredScreen, data.previewTarget, data.previewMaterial, 0);
    }
}
}
