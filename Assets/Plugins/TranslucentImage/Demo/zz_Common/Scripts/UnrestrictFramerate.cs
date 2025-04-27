using UnityEngine;

namespace LeTai.Asset.TranslucentImage.Demo
{
public class UnrestrictFramerate : MonoBehaviour
{
    void Start()
    {
        if (Application.isMobilePlatform)
            Application.targetFrameRate = 120;
    }
}
}
