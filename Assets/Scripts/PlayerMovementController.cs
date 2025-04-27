using Sirenix.OdinInspector;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    [Title("References")] [Required] [SerializeField] [SceneObjectsOnly]
    private Transform CameraTransform;

    [Required] [SerializeField] [SceneObjectsOnly]
    private Transform PlayerTransform;

    [Required] [SerializeField] [SceneObjectsOnly]
    private Rigidbody Rigidbody;

    [Title("Movement Parameters")] [Space(20)] [Required] [SerializeField]
    private float MovementSpeed = 3.0f;

    [Required] [SerializeField] private float MovementSpeedSprintBoost = 2.0f;

    [Required] [SerializeField] private float StepClimbLowerHeight = 0.1f;
    [Required] [SerializeField] private float StepClimbUpperHeight = 0.3f;

    [Required] [SerializeField] private float StepClimbCheckDistance = 0.2f;

    [Required] [SerializeField] private float StepClimbLift = 0.1f;


    [Title("Jump Parameters")]
    [Space(20)] //
    [Required]
    [SerializeField]
    [Tooltip(
        "How long a jump is 'buffered', i.e. assume you press Jump but you are still jumping, if this value is 0.2 (seconds) and you press it 0.1 seconds before arriving on the floor, you will jump again.")]
    private float JumpBufferTime = 0.15f;

    [Required] [SerializeField] private float JumpPower = 4f;

    [Tooltip("Allows to jump for X seconds after being on ground, i.e. if you miss the jump by a bit for example.")]
    [Required]
    [SerializeField]
    private float JumpCoyoteTime = 0.15f;

    [Tooltip("")] [Required] [SerializeField]
    private float JumpDuration = 0.4f;

    [Required] [SerializeField] private float JumpGroundCheckDist = 0.1f;
    [Required] [SerializeField] private LayerMask JumpFloorMask;

    [Title("Bobbing Parameters")]
    [Space(20)] //
    [Required]
    [SerializeField]
    private float BobbingFrequency = 3f;

    [Required] [SerializeField] private float BobbingHorizontal = 0.03f;
    [Required] [SerializeField] private float BobbingVertical = 0.015f;
    [Required] [SerializeField] private float BobbingLerpSpeed = 5f;


    [Title("Camera Rotation Parameters")] [Space(20)] [Required] [SerializeField]
    private float MouseSensitivityBaseX = 5f;

    [Required] [SerializeField] private float MouseSensitivityBaseY = 5f;

    [Required] [SerializeField] private float CameraYMinRotation = -35f;

    [Required] [SerializeField] private float CameraYMaxRotation = 55f;

    [Required] [SerializeField] private Vector3 CameraLocalBasePosition = new(0, 1.7f, 0);


    private float CameraXRotation;
    private float CameraYRotation;

    private float JumpBufferCounter;
    private float JumpCoyoteTimeCounter;
    private float JumpLastTime = -1e10f;
    private bool IsJumping => Time.fixedTime - JumpLastTime < JumpDuration;

    private float BobbingTimer;

    private Vector3 MovementVelocity;


    public void DoUpdate()
    {
        UpdateMovement();
        UpdateBobbing();
        UpdateJump();
        UpdateRotation();
    }


    private void UpdateMovement()
    {
        var moveHorizontal = Input.GetAxisRaw("Horizontal");
        var moveVertical = Input.GetAxisRaw("Vertical");
        MovementVelocity = Quaternion.AngleAxis(CameraXRotation, Vector3.up) *
                           new Vector3(moveHorizontal, 0, moveVertical).normalized * MovementSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            MovementVelocity *= MovementSpeedSprintBoost;
        }
    }


    private void UpdateBobbing()
    {
        if (IsGrounded() && MovementVelocity.magnitude > 0.1f)
        {
            BobbingTimer += Time.deltaTime * MovementVelocity.magnitude * BobbingFrequency;

            var offset = new Vector3(
                Mathf.Sin(BobbingTimer) * BobbingHorizontal,
                Mathf.Cos(BobbingTimer * 2f) * BobbingVertical, // 2× for nice up‑down
                0f);
            CameraTransform.localPosition = Vector3.Lerp(CameraTransform.localPosition,
                CameraLocalBasePosition + offset,
                Time.deltaTime * BobbingLerpSpeed);
        }
        else
        {
            BobbingTimer = 0.0f;
            CameraTransform.localPosition = Vector3.Lerp(CameraTransform.localPosition,
                CameraLocalBasePosition,
                Time.deltaTime * BobbingLerpSpeed);
        }
    }

    private void UpdateJump()
    {
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
            // actually jump
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

    private void UpdateRotation()
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
    }

    private void UpdateStepClimbFixed()
    {
        if (!IsGrounded())
        {
            return;
        }

        var moveDir = PlayerTransform.TransformDirection(Vector3.forward);

        var originLower = Rigidbody.position + Vector3.up * StepClimbLowerHeight;
        if (!Physics.Raycast(originLower, moveDir, out var hitLower,
                StepClimbCheckDistance, JumpFloorMask))
        {
            return; // nothing to climb
        }

        if (hitLower.normal.y > 0.1f)
        {
            // already a slope
            return;
        }

        // --- upper probe to see if space above is clear --------------
        var originUpper = Rigidbody.position + Vector3.up * StepClimbUpperHeight;
        var blockedAbove = Physics.Raycast(originUpper, moveDir, StepClimbCheckDistance, JumpFloorMask);
        if (blockedAbove)
        {
            return;
        }

        // --- perform step (small vertical snap) ----------------------
        var climb = StepClimbLift * Time.fixedDeltaTime;
        Rigidbody.MovePosition(Rigidbody.position + Vector3.up * climb);
    }

    private void FixedUpdate()
    {
        Rigidbody.MovePosition(Rigidbody.position + MovementVelocity * Time.fixedDeltaTime);
        Rigidbody.linearVelocity = new Vector3(0, Rigidbody.linearVelocity.y, 0);

        UpdateStepClimbFixed();
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(PlayerTransform.position + 0.1f * Vector3.up, Vector3.down, JumpGroundCheckDist + 0.1f,
            JumpFloorMask);
    }
}
