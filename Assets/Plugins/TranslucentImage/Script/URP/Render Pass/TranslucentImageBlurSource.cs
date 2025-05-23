#if !UNITY_2023_3_OR_NEWER
#define BUGGY_OVERLAY_CAM_PIXEL_RECT
#endif

#if UNITY_2022_3_OR_NEWER
#define HAS_DOUBLEBUFFER_BOTH
#endif

#if UNITY_2022_1_OR_NEWER
#define HAS_RTHANDLE
#define HAS_SETUP_OVERRIDE
#endif


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;
using Debug = UnityEngine.Debug;


#if HAS_RTHANDLE
using RTH = UnityEngine.Rendering.RTHandle;
#else
using RTH = UnityEngine.Rendering.Universal.RenderTargetHandle;
#endif


[assembly: InternalsVisibleTo("LeTai.TranslucentImage.UniversalRP.Editor")]

namespace LeTai.Asset.TranslucentImage.UniversalRP
{
class URPRendererInternal
{
    ScriptableRenderer renderer;
    Func<RTH>          getBackBufferDelegate;
    Func<RTH>          getAfterPostColorDelegate;


    public void CacheRenderer(ScriptableRenderer renderer)
    {
        if (this.renderer == renderer) return;

        this.renderer = renderer;

        void CacheBackBufferGetter(object rd)
        {
#if UNITY_2022_1_OR_NEWER
            const string backBufferMethodName = "PeekBackBuffer";
#else
            const string backBufferMethodName = "GetBackBuffer";
#endif

            // ReSharper disable once PossibleNullReferenceException
            var cbs = rd.GetType()
                        .GetField("m_ColorBufferSystem", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(rd);
            var gbb = cbs.GetType()
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .First(m => m.Name == backBufferMethodName && m.GetParameters().Length == 0);

            getBackBufferDelegate = (Func<RTH>)gbb.CreateDelegate(typeof(Func<RTH>), cbs);
        }

#if HAS_DOUBLEBUFFER_BOTH
        CacheBackBufferGetter(renderer);
#else
        if (renderer is UniversalRenderer ur)
        {
            CacheBackBufferGetter(ur);
        }
        else
        {
            // ReSharper disable once PossibleNullReferenceException
            getAfterPostColorDelegate =
                (Func<RTH>)renderer.GetType()
                                   .GetProperty("afterPostProcessColorHandle", BindingFlags.NonPublic | BindingFlags.Instance)
                                   .GetGetMethod(true)
                                   .CreateDelegate(typeof(Func<RTH>), renderer);
        }
#endif
    }

    public RenderTargetIdentifier GetBackBuffer()
    {
        Debug.Assert(getBackBufferDelegate != null);

        var r = getBackBufferDelegate.Invoke();
#if HAS_RTHANDLE
        return r.nameID;
#else
        return r.Identifier();
#endif
    }

    public RenderTargetIdentifier GetAfterPostColor()
    {
        Debug.Assert(getAfterPostColorDelegate != null);

        var r = getAfterPostColorDelegate.Invoke();
#if HAS_RTHANDLE
        return r.nameID;
#else
        return r.Identifier();
#endif
    }
}

[MovedFrom("LeTai.Asset.TranslucentImage.LWRP")]
public class TranslucentImageBlurSource : ScriptableRendererFeature
{
    public enum RenderOrder
    {
        AfterPostProcessing,
        BeforePostProcessing,
    }

    public RenderOrder renderOrder = RenderOrder.AfterPostProcessing;

    public bool canvasDisappearWorkaround = false;

    internal RendererType rendererType;

    readonly Dictionary<Camera, TranslucentImageSource> blurSourceCache = new Dictionary<Camera, TranslucentImageSource>();
    readonly Dictionary<Camera, Camera>                 baseCameraCache = new Dictionary<Camera, Camera>();

    URPRendererInternal            urpRendererInternal;
    TranslucentImageBlurRenderPass pass;
    IBlurAlgorithm                 blurAlgorithm;
    Material                       previewMaterial;

    Material PreviewMaterial
    {
        get
        {
            if (!previewMaterial)
                previewMaterial = CoreUtils.CreateEngineMaterial("Hidden/FillCrop_UniversalRP");

            return previewMaterial;
        }
    }

    /// <summary>
    /// When adding new Translucent Image Source to existing Camera at run time, the new Source must be registered here
    /// </summary>
    /// <param name="source"></param>
    public void RegisterSource(TranslucentImageSource source)
    {
        blurSourceCache[source.GetComponent<Camera>()] = source;
    }

    public override void Create()
    {
        blurAlgorithm       = new ScalableBlur();
        urpRendererInternal = new URPRendererInternal();

        var renderPassEvent = renderOrder == RenderOrder.BeforePostProcessing
                                  ? RenderPassEvent.BeforeRenderingPostProcessing
                                  : RenderPassEvent.AfterRenderingPostProcessing;
        pass = new TranslucentImageBlurRenderPass(urpRendererInternal) {
            renderPassEvent = renderPassEvent
        };

        blurSourceCache.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(previewMaterial);
    }

    void SetupSRP(ScriptableRenderer renderer)
    {
        urpRendererInternal.CacheRenderer(renderer);

#if UNITY_2021_3_OR_NEWER
        if (renderer is UniversalRenderer)
#else
        if (renderer is ForwardRenderer)
#endif
        {
            rendererType = RendererType.Universal;
        }
        else
        {
            rendererType = RendererType.Renderer2D;
        }

        pass.SetupSRP(new TranslucentImageBlurRenderPass.SRPassData {
#if !HAS_DOUBLEBUFFER_BOTH
            rendererType = rendererType,
#if HAS_RTHANDLE
            cameraColorTarget = renderer.cameraColorTargetHandle,
#else
            cameraColorTarget = renderer.cameraColorTarget,
#endif
            renderOrder = renderOrder,
#endif // !HAS_DOUBLEBUFFER_BOTH
            canvasDisappearWorkaround = canvasDisappearWorkaround,
        });
    }

#if HAS_SETUP_OVERRIDE
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        var blurSource = GetBlurSource(cameraData.camera);

        if (blurSource == null)
            return;

        SetupSRP(renderer);
    }
#endif

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData  renderingData
    )
    {
        var cameraData = renderingData.cameraData;

        if (cameraData.cameraType != CameraType.Game)
            return;

        var camera     = renderingData.cameraData.camera;
        var blurSource = GetBlurSource(camera);

        if (blurSource == null)
            return;
        if (blurSource.BlurConfig == null)
            return;

        blurSource.CamRectOverride = Rect.zero;
#if BUGGY_OVERLAY_CAM_PIXEL_RECT
        if (cameraData.renderType == CameraRenderType.Overlay)
        {
            var baseCam = GetBaseCamera(camera);
            if (baseCam)
                blurSource.CamRectOverride = baseCam.rect;
        }
#endif

        blurAlgorithm.Init(blurSource.BlurConfig, false);


#if UNITY_BUGGED_HAS_PASSES_AFTER_POSTPROCESS
        bool applyFinalPostProcessing = renderingData.postProcessingEnabled
                                     && cameraData.resolveFinalTarget
                                     && cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;
        pass.renderPassEvent = applyFinalPostProcessing ? RenderPassEvent.AfterRenderingPostProcessing : RenderPassEvent.AfterRendering;
#endif

        pass.Setup(new TranslucentImageBlurRenderPass.PassData {
            blurAlgorithm    = blurAlgorithm,
            blurSource       = blurSource,
            camPixelRect     = GetPixelRect(cameraData),
            shouldUpdateBlur = blurSource.ShouldUpdateBlur(),
            isPreviewing     = blurSource.Preview,
            previewMaterial  = PreviewMaterial
        });

#if !HAS_SETUP_OVERRIDE
        SetupSRP(renderer);
#endif

        renderer.EnqueuePass(pass);
    }

    TranslucentImageSource GetBlurSource(Camera camera)
    {
        if (!blurSourceCache.ContainsKey(camera))
        {
            blurSourceCache.Add(camera, camera.GetComponent<TranslucentImageSource>());
        }

        return blurSourceCache[camera];
    }

#if BUGGY_OVERLAY_CAM_PIXEL_RECT
    Camera GetBaseCamera(Camera camera)
    {
        if (!baseCameraCache.ContainsKey(camera))
        {
            Camera baseCamera = null;

            foreach (var uacd in Shims.FindObjectsOfType<UniversalAdditionalCameraData>())
            {
                if (uacd.renderType != CameraRenderType.Base) continue;
                if (uacd.cameraStack == null) continue;
                if (!uacd.cameraStack.Contains(camera)) continue;

                baseCamera = uacd.GetComponent<Camera>();
            }

            baseCameraCache.Add(camera, baseCamera);
        }

        return baseCameraCache[camera];
    }

    readonly FieldInfo cameraDataPixelRectField = typeof(CameraData).GetField("pixelRect", BindingFlags.Instance | BindingFlags.NonPublic);
#endif

    public Rect GetPixelRect(CameraData cameraData)
    {
#if !BUGGY_OVERLAY_CAM_PIXEL_RECT
        return cameraData.camera.pixelRect;
#else
        if (cameraData.renderType == CameraRenderType.Base)
            return cameraData.camera.pixelRect;

        if (cameraDataPixelRectField == null)
            Debug.LogError("CameraData.pixelRect does not exists in this version of URP. Please report a bug.");

        // ReSharper disable once PossibleNullReferenceException
        return (Rect)cameraDataPixelRectField.GetValue(cameraData);
#endif
    }
}
}
