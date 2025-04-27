using UnityEngine;

namespace LeTai.Asset.TranslucentImage.Demo
{
public class WorldSpaceSetup : MonoBehaviour
{
    public Camera sceneCamera;
    public Camera uiCamera;

    public void SetUIAlwaysOnTop(bool isAlwaysOnTop)
    {
        //In always on top mode, main camera shouldn't render the UI
        //Equivalent to toggling the layer "UI" in the inspector
        if (isAlwaysOnTop)
            sceneCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
        else
            sceneCamera.cullingMask |= 1 << LayerMask.NameToLayer("UI");

        //Instead, another camera that have higher depth should do that.
        uiCamera.gameObject.SetActive(isAlwaysOnTop);
    }
}
}
