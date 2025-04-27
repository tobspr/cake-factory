float2 UnCropUV(float2 uvRelativeToCropped, float4 cropRegion)
{
    return lerp(cropRegion.xy, cropRegion.zw, uvRelativeToCropped);
}

float2 CropUV(float2 uvRelativeToUnCropped, float4 cropRegion)
{
    return (uvRelativeToUnCropped - cropRegion.xy) / (cropRegion.zw - cropRegion.xy);
}

inline float noise(uint2 n)
{
    static const float g = 1.32471795724474602596;
    static const float a1 = 1.0 / g;
    static const float a2 = 1.0 / (g * g);
    return frac(a1 * n.x + a2 * n.y);
}
