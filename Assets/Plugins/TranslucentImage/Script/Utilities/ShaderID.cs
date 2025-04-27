using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
public static class ShaderID
{
    public static readonly int VIBRANCY   = Shader.PropertyToID("_Vibrancy");
    public static readonly int BRIGHTNESS = Shader.PropertyToID("_Brightness");
    public static readonly int FLATTEN    = Shader.PropertyToID("_Flatten");
    public static readonly int BLUR_TEX   = Shader.PropertyToID("_BlurTex");

    public static readonly int MAIN_TEX         = Shader.PropertyToID("_MainTex");
    public static readonly int RADIUS           = Shader.PropertyToID("_Radius");
    public static readonly int BACKGROUND_COLOR = Shader.PropertyToID("_BackgroundColor");
    public static readonly int CROP_REGION      = Shader.PropertyToID("_CropRegion");
    // public static readonly int ENV_TEX      = Shader.PropertyToID("_EnvTex");
}
}
