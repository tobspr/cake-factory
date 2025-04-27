#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
[ExecuteAlways]
[AddComponentMenu("UI/Translucent Image", 2)]
public partial class TranslucentImage
{
    protected override void Reset()
    {
        base.Reset();
        color = Color.white;

        material = DefaultResources.Instance.material;

        source = source ? source : Shims.FindObjectOfType<TranslucentImageSource>();
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        SetVerticesDirty();
    }
}
}
#endif
