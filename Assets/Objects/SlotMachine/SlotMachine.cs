using Sirenix.OdinInspector;
using UnityEngine;

public class SlotMachine : MonoBehaviour
{



    [Required] [SerializeField] [ChildGameObjectsOnly]
    private Transform[] SlotTransforms;


    void Start()
    {

    }

    void FixedUpdate()
    {
        for (var i = 0; i < SlotTransforms.Length; i++)
        {
            var t = SlotTransforms[i];
            t.localRotation = Quaternion.Euler(-Time.fixedTime * 360.0f, 0, 0);
        }
    }
}
