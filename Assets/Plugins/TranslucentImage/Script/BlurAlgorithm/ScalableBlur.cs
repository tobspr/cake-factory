using UnityEngine;
using UnityEngine.Rendering;

namespace LeTai.Asset.TranslucentImage
{
public class ScalableBlur : IBlurAlgorithm
{
    readonly RenderTargetIdentifier[] scratches = new RenderTargetIdentifier[14];

    bool                  isBirp;
    Material              material;
    ScalableBlurConfig    config;
    MaterialPropertyBlock propertyBlock;

    Material Material
    {
        get
        {
            if (material == null)
                Material = new Material(Shader.Find(isBirp
                                                        ? "Hidden/EfficientBlur"
                                                        : "Hidden/EfficientBlur_UniversalRP"));

            return material;
        }
        set => material = value;
    }

    public void Init(BlurConfig config, bool isBirp)
    {
        this.isBirp   = isBirp;
        this.config   = (ScalableBlurConfig)config;
        propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
    }

    public void Blur(
        CommandBuffer          cmd,
        RenderTargetIdentifier src,
        Rect                   srcCropRegion,
        Rect                   activeRegion,
        BackgroundFill         backgroundFill,
        RenderTexture          target
    )
    {
        float radius = ScaleWithResolution(config.Radius,
                                           target.width * srcCropRegion.width,
                                           target.height * srcCropRegion.height);
        ConfigMaterial(radius, backgroundFill);

        int   stepCount = Mathf.Clamp(config.Iteration * 2 - 1, 1, scratches.Length * 2 - 1);
        float extent    = config.Strength;

        var activeRegionRelative = RectUtils.Intersect(activeRegion, srcCropRegion);
        activeRegionRelative.x      = (activeRegionRelative.x - srcCropRegion.x) / srcCropRegion.width;
        activeRegionRelative.y      = (activeRegionRelative.y - srcCropRegion.y) / srcCropRegion.height;
        activeRegionRelative.width  = activeRegionRelative.width / srcCropRegion.width;
        activeRegionRelative.height = activeRegionRelative.height / srcCropRegion.height;

        if (activeRegionRelative.width == 0 || activeRegionRelative.height == 0)
            return;

        if (stepCount > 1)
        {
            CropViewport(target.width >> 1, target.height >> 1, extent, out var viewportFirst, out var activeRegionFirst);
            propertyBlock.SetVector(ShaderID.CROP_REGION, RectUtils.ToMinMaxVector(RectUtils.Crop(srcCropRegion, activeRegionFirst)));
            Blitter.Blit(cmd, src, scratches[0], Material, 0, propertyBlock, viewportFirst);
        }

        var maxDepth = Mathf.Min(config.Iteration - 1, scratches.Length - 1);
        for (var i = 1; i < stepCount; i++)
        {
            var fromIdx = SimplePingPong(i - 1, maxDepth);
            var toIdx   = SimplePingPong(i,     maxDepth);

            var targetDepth = toIdx + 1;
            CropViewport(target.width >> targetDepth, target.height >> targetDepth, extent, out var viewportStep, out var activeRegionStep);
            propertyBlock.SetVector(ShaderID.CROP_REGION, RectUtils.ToMinMaxVector(activeRegionStep));
            Blitter.Blit(cmd, scratches[fromIdx], scratches[toIdx], Material, 0, propertyBlock, viewportStep);
        }

        CropViewport(target.width, target.height, 0, out var viewportLast, out var activeRegionLast);
        activeRegionLast = stepCount > 1 ? activeRegionLast : RectUtils.Crop(srcCropRegion, activeRegionLast);
        propertyBlock.SetVector(ShaderID.CROP_REGION, RectUtils.ToMinMaxVector(activeRegionLast));
        Blitter.Blit(cmd,
                     stepCount > 1 ? scratches[0] : src,
                     target,
                     Material,
                     0,
                     propertyBlock,
                     viewportLast);
        return;

        void CropViewport(int targetWidth, int targetHeight, float padding, out Rect viewport, out Rect activeRegionSnapped)
        {
            var x  = activeRegionRelative.x * targetWidth;
            var y  = activeRegionRelative.y * targetHeight;
            var xf = Mathf.Floor(x - padding);
            var yf = Mathf.Floor(y - padding);
            viewport = new Rect(xf,
                                yf,
                                Mathf.Ceil(x + activeRegionRelative.width * targetWidth + padding) - xf,
                                Mathf.Ceil(y + activeRegionRelative.height * targetHeight + padding) - yf);

            viewport.x      = Mathf.Max(viewport.x, 0);
            viewport.y      = Mathf.Max(viewport.y, 0);
            viewport.width  = Mathf.Min(viewport.width,  targetWidth);
            viewport.height = Mathf.Min(viewport.height, targetHeight);

            activeRegionSnapped = new Rect(
                viewport.x / targetWidth,
                viewport.y / targetHeight,
                viewport.width / targetWidth,
                viewport.height / targetHeight
            );
        }
    }

    public int GetScratchesCount()
    {
        return Mathf.Min(config.Iteration, scratches.Length);
    }

    public void GetScratchDescriptor(int index, ref RenderTextureDescriptor descriptor)
    {
        if (index == 0)
        {
            int firstDownsampleFactor = config.Iteration > 0 ? 1 : 0;
            descriptor.width  >>= firstDownsampleFactor;
            descriptor.height >>= firstDownsampleFactor;
        }
        else
        {
            descriptor.width  >>= 1;
            descriptor.height >>= 1;
        }
        if (descriptor.width <= 0) descriptor.width   = 1;
        if (descriptor.height <= 0) descriptor.height = 1;
    }

    public void SetScratch(int index, RenderTargetIdentifier value)
    {
        scratches[index] = value;
    }

    protected void ConfigMaterial(float radius, BackgroundFill backgroundFill)
    {
        switch (backgroundFill.mode)
        {
        case BackgroundFillMode.None:
            Material.EnableKeyword("BACKGROUND_FILL_NONE");
            Material.DisableKeyword("BACKGROUND_FILL_COLOR");
            break;
        case BackgroundFillMode.Color:
            Material.EnableKeyword("BACKGROUND_FILL_COLOR");
            Material.DisableKeyword("BACKGROUND_FILL_NONE");
            Material.SetColor(ShaderID.BACKGROUND_COLOR, backgroundFill.color);
            break;
        }
        Material.SetFloat(ShaderID.RADIUS, radius);
    }

    ///<summary>
    /// Relative blur size to maintain same look across multiple resolution
    /// </summary>
    float ScaleWithResolution(float baseRadius, float width, float height)
    {
        float scaleFactor = Mathf.Min(width, height) / 1080f;
        scaleFactor = Mathf.Clamp(scaleFactor, .5f, 2f); //too much variation cause artifact
        return baseRadius * scaleFactor;
    }

    public static int SimplePingPong(int t, int max)
    {
        if (t > max)
            return 2 * max - t;
        return t;
    }
}
}
