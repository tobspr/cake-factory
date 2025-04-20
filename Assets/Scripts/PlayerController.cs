using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Title("References")] [Required] [SerializeField] [ChildGameObjectsOnly]
    private Transform CameraTransform;

    [Required] [SerializeField] [ChildGameObjectsOnly]
    private Transform PlayerTransform;


    [Required] [SerializeField] [ChildGameObjectsOnly]
    private Rigidbody Rigidbody;


    [Title("Movement Parameters")] [Space(20)] [SerializeField]
    private float MovementSpeed = 2.0f;

    [SerializeField] private float MovementSpeedSprintBoost = 2.0f;


    [Title("Jump Parameters")]
    [Space(20)] //
    [Required]
    [SerializeField]
    private float JumpBufferTime = 0.2f;

    [Required] [SerializeField] private float JumpPower = 16f;
    [Required] [SerializeField] private float JumpCoyoteTime = 0.15f;
    [Required] [SerializeField] private float JumpDuration = 0.4f;
    [Required] [SerializeField] private float JumpGroundCheckDist = 0.1f;
    [Required] [SerializeField] private LayerMask JumpFloorMask;

    [Title("Camera Rotation Parameters")] [Space(20)] [Required] [SerializeField]
    private float MouseSensitivityBaseX = 0.5f;

    [Required] [SerializeField] private float MouseSensitivityBaseY = 0.5f;

    [Required] [SerializeField] private float CameraYMinRotation = -20f;

    [Required] [SerializeField] private float CameraYMaxRotation = 40f;

    private float CameraXRotation;
    private float CameraYRotation;

    private float JumpBufferCounter;
    private float JumpCoyoteTimeCounter;
    private float JumpLastTime = -1e10f;
    private bool IsJumping => Time.fixedTime - JumpLastTime < JumpDuration;

    private Vector3 MovementVelocity;


    private void Update()
    {
        var mouseDeltaX = Input.GetAxis("Mouse X");
        var mouseDeltaY = Input.GetAxis("Mouse Y");

        if (Application.isFocused)
        {
            CameraYRotation = math.clamp(CameraYRotation - mouseDeltaY * MouseSensitivityBaseY, CameraYMinRotation,
                CameraYMaxRotation);
            CameraXRotation += mouseDeltaX * MouseSensitivityBaseX;
        }

        CameraTransform.localRotation = Quaternion.Euler(CameraYRotation, 0, 0);
        PlayerTransform.localRotation = Quaternion.Euler(0, CameraXRotation, 0);

        var moveHorizontal = Input.GetAxisRaw("Horizontal");
        var moveVertical = Input.GetAxisRaw("Vertical");


        MovementVelocity = Quaternion.AngleAxis(CameraXRotation, Vector3.up) *
                           new Vector3(moveHorizontal, 0, moveVertical).normalized;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            MovementVelocity *= MovementSpeedSprintBoost;
        }

        if (IsGrounded())
        {
            JumpCoyoteTimeCounter = JumpCoyoteTime;
        }
        else
        {
            JumpCoyoteTimeCounter -= Time.deltaTime;
        }


        if (Input.GetButtonDown("Jump"))
        {
            JumpBufferCounter = JumpBufferTime;
        }
        else
        {
            JumpBufferCounter -= Time.deltaTime;
        }


        var vel = Rigidbody.linearVelocity;
        if (JumpCoyoteTimeCounter > 0f && JumpBufferCounter > 0f && !IsJumping)
        {
            Rigidbody.linearVelocity = vel = new Vector3(vel.x, JumpPower, vel.z);
            JumpBufferCounter = 0f;
            JumpLastTime = Time.fixedTime;
        }

        if (Input.GetButtonUp("Jump") && vel.y > 0f)
        {
            Rigidbody.linearVelocity = vel = new Vector3(vel.x, vel.y * 0.5f, vel.z);
            JumpCoyoteTimeCounter = 0f;
        }
    }

    private void FixedUpdate()
    {
        Rigidbody.MovePosition(Rigidbody.position + MovementVelocity * (Time.fixedDeltaTime * MovementSpeed));
    }

    private void OnEnable()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Rigidbody.freezeRotation = true;
    }

    // optional gizmo for ground check
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * JumpGroundCheckDist);
    }


    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + 0.1f * Vector3.up, Vector3.down, JumpGroundCheckDist + 0.1f,
            JumpFloorMask);
    }
}
