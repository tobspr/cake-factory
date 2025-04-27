using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class PlayerInteractionController : MonoBehaviour
{
    [Required] [SerializeField] [SceneObjectsOnly]
    private TMP_Text InteractionText;

    [Required] [SerializeField] private LayerMask InteractionMask;

    [Required] [SerializeField] private float MaxDistance = 10.0f;

    public void DoUpdate()
    {
        var component = GetInteractableAtCenter();
        if (component)
        {
            InteractionText.gameObject.SetActive(true);
            InteractionText.text = "[E] " + component.Label;
        }
        else
        {
            InteractionText.gameObject.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Triggered");
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
