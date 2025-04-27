using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
public static class RectUtils
{
    /// <summary>
    /// Fast approximate equal for rect position and size in range [0,1]
    /// </summary>
    internal static bool ApproximateEqual01(Rect a, Rect b)
    {
        return QuickApproximate01(a.x,      b.x)
            && QuickApproximate01(a.y,      b.y)
            && QuickApproximate01(a.width,  b.width)
            && QuickApproximate01(a.height, b.height);
    }


    private static bool QuickApproximate01(float a, float b)
    {
        const float epsilon01 = 5.9604644e-8f; // different between 1 and largest float < 1
        return Mathf.Abs(b - a) < epsilon01;
    }

    public static Rect Intersect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        if (xMin < xMax && yMin < yMax)
        {
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        return Rect.zero;
    }

    public static Rect Crop(Rect src, Rect cropRegion)
    {
        var rect = src;
        rect.x      += cropRegion.x * rect.width;
        rect.y      += cropRegion.y * rect.height;
        rect.width  *= cropRegion.width;
        rect.height *= cropRegion.height;

        return rect;
    }

    public static Vector4 ToMinMaxVector(Rect rect)
    {
        return new Vector4(rect.xMin,
                           rect.yMin,
                           rect.xMax,
                           rect.yMax);
    }

    public static Vector4 ToVector4(Rect rect)
    {
        return new Vector4(rect.xMin,
                           rect.yMin,
                           rect.width,
                           rect.height);
    }

    public static Rect Expand(Rect rect, Vector2 padding)
    {
        return new Rect(rect.x - padding.x,
                        rect.y - padding.y,
                        rect.width + 2 * padding.x,
                        rect.height + 2 * padding.y);
    }
}
}
