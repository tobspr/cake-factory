using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [ChildGameObjectsOnly] [Required] [SerializeField]
    private PlayerMovementController MovementController;

    [ChildGameObjectsOnly] [Required] [SerializeField]
    private PlayerInteractionController InteractionController;

    private void Update()
    {
        MovementController.DoUpdate();
        InteractionController.DoUpdate();
    }

    private void OnEnable()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
