using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
public readonly struct ActiveRegion
{
    public static readonly ActiveRegion INACTIVE = new ActiveRegion(Rect.zero,
                                                                    Matrix4x4.zero,
                                                                    VPMatrixCache.Index.INVALID);

    public readonly Rect                rect;
    public readonly Matrix4x4           localToWorld;
    public readonly VPMatrixCache.Index vpMatrixCacheIndex;
    public          bool                IsWorldSpace => vpMatrixCacheIndex.IsValid();

    /// <param name="vpMatrixCacheIndex">Use VPMatrixCache.Index.INVALID For Screen Space - Overlay Canvas. Otherwise, see <see cref="VPMatrixCache"/></param>
    public ActiveRegion(Rect rect, Matrix4x4 localToWorld, VPMatrixCache.Index vpMatrixCacheIndex)
    {
        this.rect               = rect;
        this.localToWorld       = localToWorld;
        this.vpMatrixCacheIndex = vpMatrixCacheIndex;
    }
}

public interface IActiveRegionProvider
{
    bool HaveActiveRegion();
    // out params to avoid double allocation of the fat struct
    void GetActiveRegion(VPMatrixCache vpMatrixCache, out ActiveRegion activeRegion);
}
}
