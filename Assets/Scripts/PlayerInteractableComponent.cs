using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(MeshRenderer))]
public class PlayerInteractableComponent : MonoBehaviour
{
    public UnityEvent OnTrigger;

    public string Label;
}
