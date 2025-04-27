using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
[BurstCompile(FloatMode = FloatMode.Fast)]
unsafe struct ActiveRegionMergeJob : IJob
{
    [ReadOnly]
    public NativeList<ActiveRegion> activeRegions;
    [ReadOnly]
    public NativeList<Matrix4x4> vpMatrices;
    public Vector2 screenSize;
    public Rect    viewport;

    public NativeArray<Rect> merged;

    public void Execute()
    {
        var xMin = 1f;
        var yMin = 1f;
        var xMax = 0f;
        var yMax = 0f;

        var screenSizeInv = new Vector2(1f / screenSize.x, 1f / screenSize.y);

        for (int i = 0; i < activeRegions.Length; i++)
        {
            var quad = activeRegions[i];
            var rect = quad.rect;
            var corners = stackalloc[] {
                new Vector2(rect.x,    rect.y),
                new Vector2(rect.x,    rect.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMax, rect.y),
            };
            for (int j = 0; j < 4; j++)
            {
                Vector3 pointWorld = quad.localToWorld.MultiplyPoint3x4(corners[j]);
                Vector2 pointScreen;
                if (quad.IsWorldSpace)
                {
                    Vector4 pointWorldHomo = pointWorld;
                    pointWorldHomo.w = 1;
                    Vector4 pointClip = vpMatrices[quad.vpMatrixCacheIndex.index] * pointWorldHomo;
                    pointScreen = new Vector2(pointClip.x, pointClip.y) / pointClip.w * .5f + new Vector2(.5f, .5f);
                }
                else
                {
                    pointScreen = pointWorld * screenSizeInv;
                    pointScreen = (pointScreen - viewport.min) / viewport.size;
                }

                xMin = Mathf.Min(xMin, pointScreen.x);
                yMin = Mathf.Min(yMin, pointScreen.y);
                xMax = Mathf.Max(xMax, pointScreen.x);
                yMax = Mathf.Max(yMax, pointScreen.y);
            }
        }

        merged[0] = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }
}
}
