using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
    public abstract class BlurConfig : ScriptableObject
    {
        public abstract float Strength { get; set; }
    }
}
