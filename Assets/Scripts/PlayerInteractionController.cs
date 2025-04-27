using EPOOutline;
using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerInteractionController : MonoBehaviour
{
    [Required] [SerializeField] [SceneObjectsOnly]
    private TMP_Text InteractionText;

    [Required] [SerializeField] private LayerMask InteractionMask;

    [Required] [SerializeField] private float MaxDistance = 10.0f;


    private PlayerInteractableComponent LastComponent;

    public void DoUpdate()
    {
        var component = GetInteractableAtCenter();
        if (component)
        {
            if (LastComponent != component)
            {
                if (LastComponent)
                {
                    Destroy(LastComponent.GetComponent<Outlinable>());
                }

                LastComponent = component;

                var outlinable = component.AddComponent<Outlinable>();
                outlinable.RenderStyle = RenderStyle.FrontBack;
                outlinable.DrawingMode = OutlinableDrawingMode.Normal;
                outlinable.OutlineParameters.Enabled = true;
                outlinable.AddRenderer(component.GetComponent<MeshRenderer>());
            }

            InteractionText.gameObject.SetActive(true);
            InteractionText.text = component.Label;
        }
        else
        {
            if (LastComponent)
            {
                Destroy(LastComponent.GetComponent<Outlinable>());
                LastComponent = null;
            }

            InteractionText.gameObject.SetActive(false);
        }

        if (component && Input.GetKeyDown(KeyCode.Mouse0))
        {
            Debug.Log("Triggered interaction '" + component.Label + "'");
            component.OnTrigger.Invoke();
        }
    }


    private PlayerInteractableComponent GetInteractableAtCenter()
    {
        var ray = Camera.main.ScreenPointToRay(new Vector2(Screen.width * .5f, Screen.height * .5f));
        return Physics.Raycast(ray, out var hit, MaxDistance, InteractionMask)
               && hit.collider.TryGetComponent(out PlayerInteractableComponent comp)
            ? comp
            : null;
    }
}
