using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if ENABLE_VR
using UnityEngine.XR;
#endif

namespace LeTai.Asset.TranslucentImage
{
/// <summary>
/// Common source of blur for Translucent Images.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Tai Le Assets/Translucent Image Source")]
[HelpURL("https://leloctai.com/asset/translucentimage/docs/articles/customize.html#translucent-image-source")]
public partial class TranslucentImageSource : MonoBehaviour
{
#region Private Field
    [SerializeField]
    BlurConfig blurConfig;

    [SerializeField] [Range(0, 3)]
    [Tooltip("Reduce the size of the screen before processing. Increase will improve performance but create more artifact")]
    int downsample;

    [SerializeField]
    [Tooltip("Choose which part of the screen to blur. Smaller region is faster")]
    Rect blurRegion = new Rect(0, 0, 1, 1);

    [SerializeField]
    [Tooltip("How many time to blur per second. Reduce to increase performance and save battery for slow moving background")]
    float maxUpdateRate = float.PositiveInfinity;

    [SerializeField]
    [Tooltip("Expand the blurred area to avoid gap around moving UIs when using a low Max Update Rate." +
             "\nUse higher value for lower Max Update Rate or faster UI movement. Use 0 for infinite Update Rate or static UIs." +
             "\nFor the best culling effectiveness, set this at runtime while UIs are moving, and reset to 0 while they're static." +
             "\nUnit: fraction of the screen's shorter side")]
    [Range(0, 1)]
    float cullPadding = 0;

    [SerializeField]
    [Tooltip("Preview the effect fullscreen. Not recommended for runtime use")]
    bool preview;

    [SerializeField]
    [Tooltip("Fill the background where the frame buffer alpha is 0. Useful for VR Underlay and Passthrough, where these areas would otherwise be black")]
    BackgroundFill backgroundFill = new BackgroundFill();

    [SerializeField]
    [Tooltip("Always blur the entire blur region to reduce cpu usage")]
    bool skipCulling;

    int        lastDownsample;
    Rect       lastBlurRegion   = new Rect(0, 0, 1, 1);
    Rect       lastCamPixelRect = new Rect(0, 0, 1, 1);
    Vector2Int lastCamPixelSize = Vector2Int.zero;
    float      lastUpdate;

    IBlurAlgorithm blurAlgorithm;

#pragma warning disable 0108
    Camera camera;
#pragma warning restore 0108
    Material      previewMaterial;
    RenderTexture blurredScreen;
    CommandBuffer cmd;
#pragma warning disable CS0169
    bool isForOverlayCanvas;
#pragma warning restore CS0169

    bool needRegisterCanvasPreRenderCallback;

    readonly List<IActiveRegionProvider> activeRegionProviders = new List<IActiveRegionProvider>();
    VPMatrixCache                        vpMatrixCache;
    NativeList<ActiveRegion>             activeRegions;
    NativeArray<Rect>                    activeRegionJobResult;
    JobHandle                            findBoundsJobHandle;

    static readonly Rect FULLSCREEN_REGION = new Rect(0, 0, 1, 1);

    static ProfilerMarker profilerMarkerCull = new ProfilerMarker(nameof(TranslucentImageSource) + ".Culling");
#endregion


#region Properties
    public BlurConfig BlurConfig
    {
        get { return blurConfig; }
        set
        {
            blurConfig = value;
            InitializeBlurAlgorithm();
        }
    }

    /// <summary>
    /// The rendered image will be shrinked by a factor of 2^{{Downsample}} before bluring to reduce processing time
    /// </summary>
    /// <value>
    /// Must be non-negative. Default to 0
    /// </value>
    public int Downsample
    {
        get { return downsample; }
        set { downsample = Mathf.Max(0, value); }
    }

    /// <summary>
    /// Define the rectangular area on screen that will be blurred.
    /// </summary>
    /// <value>
    /// Between 0 and 1
    /// </value>
    public Rect BlurRegion
    {
        get { return blurRegion; }
        set
        {
            Vector2 min = new Vector2(1 / (float)Cam.pixelWidth, 1 / (float)Cam.pixelHeight);
            blurRegion        = value;
            blurRegion.xMin   = Mathf.Clamp(blurRegion.x,      0,     1 - min.x);
            blurRegion.yMin   = Mathf.Clamp(blurRegion.y,      0,     1 - min.y);
            blurRegion.width  = Mathf.Clamp(blurRegion.width,  min.x, 1 - blurRegion.x);
            blurRegion.height = Mathf.Clamp(blurRegion.height, min.y, 1 - blurRegion.y);

            OnBlurRegionChanged();
        }
    }

    public Rect ActiveRegion { get; private set; }

    /// <summary>
    /// Maximum number of times to update the blurred image each second
    /// </summary>
    public float MaxUpdateRate
    {
        get => maxUpdateRate;
        set => maxUpdateRate = Mathf.Max(0, value);
    }

    /// <summary>
    /// Expand the blurred area to avoid gap around moving UIs when using a low Max Update Rate.
    /// Use higher value for lower Max Update Rate or faster UI movement. Use 0 for infinite Update Rate or static UIs.
    /// For the best culling effectiveness, set this at runtime while UIs are moving, and reset to 0 while they're static.
    /// Unit: fraction of the screen's shorter side.
    /// </summary>
    public float CullPadding
    {
        get => cullPadding;
        set => cullPadding = Mathf.Max(0, value);
    }

    /// <summary>
    /// Fill the background where the frame buffer alpha is 0. Useful for VR Underlay and Passthrough, where these areas would otherwise be black
    /// </summary>
    public BackgroundFill BackgroundFill
    {
        get => backgroundFill;
        set => backgroundFill = value;
    }

    /// <summary>
    /// Render the blurred result to the render target
    /// </summary>
    public bool Preview
    {
        get => preview;
        set => preview = value;
    }

    /// <summary>
    /// Always blur the entire blur region to reduce cpu usage
    /// </summary>
    public bool SkipCulling
    {
        get => skipCulling;
        set => skipCulling = value;
    }

    /// <summary>
    /// Result of the image effect. Translucent Image use this as their content (read-only)
    /// </summary>
    public RenderTexture BlurredScreen
    {
        get { return blurredScreen; }
        set { blurredScreen = value; }
    }

    /// <summary>
    /// Set in SRP to provide Cam.rect for overlay cameras
    /// </summary>
    public Rect CamRectOverride { get; set; } = Rect.zero;

    /// <summary>
    /// Blur Region rect is relative to Cam.rect . This is relative to the full screen
    /// </summary>
    public Rect BlurRegionNormalizedScreenSpace
    {
        get => ViewportToScreen01Space(BlurRegion);
        set => BlurRegion = Screen01ToViewportSpace(value);
    }

    /// <summary>
    /// The Camera attached to the same GameObject. Cached in field 'camera'
    /// </summary>
    internal Camera Cam
    {
        get { return camera ? camera : camera = GetComponent<Camera>(); }
    }

    /// <summary>
    /// Minimum time in second to wait before refresh the blurred image.
    /// If maxUpdateRate non-positive then just stop updating
    /// </summary>
    float MinUpdateCycle
    {
        get { return (MaxUpdateRate > 0) ? (1f / MaxUpdateRate) : float.PositiveInfinity; }
    }

    bool ShouldCull
    {
        get
        {
            return !SkipCulling
#if ENABLE_VR
                && !XRSettings.enabled
#endif
                ;
        }
    }
#endregion

    public event Action blurredScreenChanged;
    public event Action blurRegionChanged;

    public void OnBlurRegionChanged()
    {
        blurRegionChanged?.Invoke();
    }

    public void RegisterActiveRegionProvider(IActiveRegionProvider provider)
    {
        activeRegionProviders.Add(provider);
    }

    public void UnRegisterActiveRegionProvider(IActiveRegionProvider provider)
    {
        activeRegionProviders.Remove(provider);
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Init();
        }
#endif

        vpMatrixCache         = new VPMatrixCache();
        activeRegions         = new NativeList<ActiveRegion>(4, Allocator.Persistent);
        activeRegionJobResult = new NativeArray<Rect>(1, Allocator.Persistent);

        needRegisterCanvasPreRenderCallback = true;
    }

    void OnDisable()
    {
        needRegisterCanvasPreRenderCallback = false;
        // Must unregister callback before collections disposal!
        Canvas.willRenderCanvases -= OnWillRenderCanvases;

        CompleteCull();

        vpMatrixCache.Dispose();
        activeRegions.Dispose();
        activeRegionJobResult.Dispose();
    }

    protected virtual void Start()
    {
        Init();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Attempt to filter out scene view calls. Not sure if this is robust enough
    /// </summary>
    bool isFirstOnWillRenderCanvas = false;
#endif

    void Update()
    {
        // Register this earlier would cause it to be called before the builtin Layout Groups
        if (needRegisterCanvasPreRenderCallback)
        {
            Canvas.willRenderCanvases += OnWillRenderCanvases;

            needRegisterCanvasPreRenderCallback = false;
        }
#if UNITY_EDITOR
        isFirstOnWillRenderCanvas = true;
#endif
    }

    void OnDestroy()
    {
        if (BlurredScreen)
            BlurredScreen.Release();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (cmd == null)
        {
            cmd      = new CommandBuffer();
            cmd.name = "Translucent Image Source";
        }

        if (blurAlgorithm != null && BlurConfig != null)
        {
            if (ShouldUpdateBlur())
            {
                cmd.Clear();

                if (CompleteCull())
                {
                    ReallocateBlurTexIfNeeded(Cam.pixelRect);
                    blurAlgorithm.Init(BlurConfig, true);
                    var blurExecData = new BlurExecutor.BlurExecutionData(source,
                                                                          this,
                                                                          blurAlgorithm);
                    BlurExecutor.ExecuteBlurWithTempTextures(cmd, ref blurExecData);

                    Graphics.ExecuteCommandBuffer(cmd);
                }
            }

            // Using custom Blit for this lead to warning: OnRenderImage() possibly didn't write anything to the destination texture
            if (Preview)
            {
                previewMaterial.SetVector(ShaderID.CROP_REGION, RectUtils.ToMinMaxVector(BlurRegion));
                Graphics.Blit(BlurredScreen, destination, previewMaterial);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }


    void Init()
    {
        previewMaterial = new Material(Shader.Find("Hidden/FillCrop"));

        InitializeBlurAlgorithm();
        ReallocateBlurTexIfNeeded(Cam.pixelRect);

        lastDownsample = Downsample;
    }

    void InitializeBlurAlgorithm()
    {
        switch (blurConfig)
        {
        case ScalableBlurConfig _:
            blurAlgorithm = new ScalableBlur();
            break;
        default:
            blurAlgorithm = new ScalableBlur();
            break;
        }
    }

    void OnWillRenderCanvases()
    {
#if UNITY_EDITOR
        if (!isFirstOnWillRenderCanvas)
            return;
        isFirstOnWillRenderCanvas = false;
#endif
        StartCull();
    }

    void StartCull()
    {
        profilerMarkerCull.Begin();

        findBoundsJobHandle.Complete(); // No way to cancel job
        vpMatrixCache.Clear();
        activeRegions.Clear();

        bool haveAnyActiveRegion = false;

        for (var i = 0; i < activeRegionProviders.Count; i++)
        {
            if (!activeRegionProviders[i].HaveActiveRegion())
                continue;

            haveAnyActiveRegion = true;
            if (!ShouldCull)
                break;

            activeRegionProviders[i].GetActiveRegion(vpMatrixCache, out var activeRegion);
            activeRegions.Add(activeRegion);
        }

        if (haveAnyActiveRegion)
        {
            if (ShouldCull)
            {
                findBoundsJobHandle = new ActiveRegionMergeJob {
                    vpMatrices    = vpMatrixCache.VpMatrices,
                    activeRegions = activeRegions,
                    screenSize    = new Vector2(Screen.width, Screen.height),
                    viewport      = GetActiveCameraRect(),
                    merged        = activeRegionJobResult
                }.Schedule();
            }
            else
            {
                ActiveRegion = FULLSCREEN_REGION;
            }
        }
        else
        {
            ActiveRegion = Rect.zero;
        }

        profilerMarkerCull.End();
    }

    /// <summary>
    /// Merge active regions into one
    /// </summary>
    /// <returns>False if the merged region is empty</returns>
    public bool CompleteCull()
    {
        if (!ShouldCull)
            return true;

        profilerMarkerCull.Begin();

        findBoundsJobHandle.Complete();
        ActiveRegion = activeRegionJobResult[0];

        if (MaxUpdateRate < float.PositiveInfinity)
        {
            var padding = new Vector2(CullPadding, CullPadding);
            var aspect  = Screen.width / (float)Screen.height;
            if (aspect > 1)
            {
                padding.x /= aspect;
            }
            else
            {
                padding.y *= aspect;
            }
            ActiveRegion = RectUtils.Expand(ActiveRegion, padding);
        }

        profilerMarkerCull.End();

        return ActiveRegion.width > 0 && ActiveRegion.height > 0;
    }

    void CreateNewBlurredScreen(Vector2Int camPixelSize)
    {
        if (BlurredScreen)
            BlurredScreen.Release();

#if ENABLE_VR
        if (XRSettings.enabled)
        {
            BlurredScreen        = new RenderTexture(XRSettings.eyeTextureDesc);
            BlurredScreen.width  = Mathf.RoundToInt(BlurredScreen.width * BlurRegion.width) >> Downsample;
            BlurredScreen.height = Mathf.RoundToInt(BlurredScreen.height * BlurRegion.height) >> Downsample;
            BlurredScreen.depth  = 0;
        }
        else
#endif
        {
            BlurredScreen = new RenderTexture(Mathf.RoundToInt(camPixelSize.x * BlurRegion.width) >> Downsample,
                                              Mathf.RoundToInt(camPixelSize.y * BlurRegion.height) >> Downsample, 0);
        }

        BlurredScreen.antiAliasing = 1;
        BlurredScreen.useMipMap    = false;

        BlurredScreen.name       = $"{gameObject.name} Translucent Image Source";
        BlurredScreen.filterMode = FilterMode.Bilinear;


#if UNITY_EDITOR
        // Avoid error logging when dragging related fields in the inspector
        if (BlurredScreen.width > 0 && BlurredScreen.height > 0)
#endif
            BlurredScreen.Create();

        blurredScreenChanged?.Invoke();
    }

    TextureDimension lastEyeTexDim;

    public void ReallocateBlurTexIfNeeded(Rect camPixelRect)
    {
        if (camPixelRect != lastCamPixelRect)
        {
            blurRegionChanged?.Invoke();
            lastCamPixelRect = camPixelRect;
        }

        var camPixelSize = Vector2Int.RoundToInt(camPixelRect.size);
        if (
            BlurredScreen == null
         || !BlurredScreen.IsCreated()
         || Downsample != lastDownsample
         || !RectUtils.ApproximateEqual01(BlurRegion, lastBlurRegion)
         || camPixelSize != lastCamPixelSize
#if ENABLE_VR
         || XRSettings.deviceEyeTextureDimension != lastEyeTexDim
#endif
        )
        {
            CreateNewBlurredScreen(camPixelSize);
            lastDownsample   = Downsample;
            lastBlurRegion   = BlurRegion;
            lastCamPixelSize = camPixelSize;
#if ENABLE_VR
            lastEyeTexDim = XRSettings.deviceEyeTextureDimension;
#endif
        }


        lastUpdate = GetTrueCurrentTime();
    }

    public bool ShouldUpdateBlur()
    {
        if (!enabled)
            return false;

        if (Preview)
            return true;

        float now    = GetTrueCurrentTime();
        bool  should = now - lastUpdate >= MinUpdateCycle;

        return should;
    }

    private static float GetTrueCurrentTime()
    {
#if UNITY_EDITOR
        return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.unscaledTime;
#endif
    }

    Rect GetActiveCameraRect()
    {
        var camRect = CamRectOverride.width == 0 ? Cam.rect : CamRectOverride;
        camRect.min = Vector2.Max(Vector2.zero, camRect.min);
        camRect.max = Vector2.Min(Vector2.one, camRect.max);
        return camRect;
    }

    Rect ViewportToScreen01Space(Rect rect)
    {
        var camRect = GetActiveCameraRect();

        return new Rect(camRect.position + rect.position * camRect.size,
                        rect.size * camRect.size);
    }

    Rect Screen01ToViewportSpace(Rect rect)
    {
        var camRect = GetActiveCameraRect();

        return new Rect((rect.position - camRect.position) / camRect.size,
                        rect.size / camRect.size);
    }

#if UNITY_EDITOR

    protected virtual void OnGUI()
    {
        if (!Preview) return;

        var curBlurRegionNSS = BlurRegionNormalizedScreenSpace;
        var newBlurRegionNSS = ResizableScreenRect.Draw(curBlurRegionNSS, true);

        GUI.color = Color.green;
        ResizableScreenRect.Draw(RectUtils.Intersect(curBlurRegionNSS, ViewportToScreen01Space(ActiveRegion)));

        if (newBlurRegionNSS != curBlurRegionNSS)
        {
            UnityEditor.Undo.RecordObject(this, "Change Blur Region");
            BlurRegionNormalizedScreenSpace = newBlurRegionNSS;
        }

        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif
}
}
