using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.6f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    [Header("Look")]
    public Transform cameraTransform;
    public float lookSensitivity = 10f; // degrees per second for full-scale input
    public bool invertY = false;
    public float minPitch = -70f;
    public float maxPitch = 80f;

    [Header("View Toggle")]
    public bool firstPerson = false;
    public Vector3 firstPersonCamLocalPos = new Vector3(0f, 1.6f, 0f);
    public Vector3 thirdPersonCamLocalPos = new Vector3(0f, 1.6f, -3f);
    public float firstPersonDefaultPitch = 0f;
    public float thirdPersonDefaultPitch = 10f;

    CharacterController controller;
    PlayerInput playerInput;
    InputAction moveAction;
    InputAction lookAction;
    InputAction sprintAction;
    InputAction jumpAction;

    float verticalVelocity; // Y-axis velocity for gravity/jump
    float yaw;   // player yaw (rotates body)
    float pitch; // camera pitch (local)
    Renderer[] bodyRenderers;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = gameObject.AddComponent<PlayerInput>();
        }

        // Cache actions if available
        if (playerInput.actions != null)
        {
            moveAction = playerInput.actions["Move"];
            lookAction = playerInput.actions["Look"];
            sprintAction = playerInput.actions["Sprint"];
            jumpAction = playerInput.actions["Jump"];
        }

        // Find a camera child if not assigned
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        // Initialize yaw/pitch from current transforms
        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = cameraTransform.localEulerAngles.x;
            // Convert from 0..360 to -180..180
            if (pitch > 180f) pitch -= 360f;
        }

        // Cache renderers of the body so we can hide in first-person
        bodyRenderers = GetComponentsInChildren<Renderer>();

        // Ensure initial view state is applied
        ApplyView(firstPerson, snapPitchToDefault: true);
    }

    void OnEnable()
    {
        if (jumpAction != null)
        {
            jumpAction.performed += OnJumpPerformed;
        }
    }

    void OnDisable()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
        }
    }

    void Update()
    {
        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;
        bool sprintPressed = false;

        if (moveAction != null) moveInput = moveAction.ReadValue<Vector2>();
        if (lookAction != null) lookInput = lookAction.ReadValue<Vector2>();
        if (sprintAction != null) sprintPressed = sprintAction.IsPressed();

        // Looking: yaw on body, pitch on camera
        float lookX = lookInput.x;
        float lookY = lookInput.y * (invertY ? 1f : -1f);
        yaw += lookX * lookSensitivity * Time.deltaTime;
        pitch += lookY * lookSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movement relative to camera/player forward
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 moveDir = Vector3.zero;
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            // Use camera forward on horizontal plane if available
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            moveDir = (right * inputDir.x + fwd * inputDir.z).normalized;
        }

        float targetSpeed = moveSpeed * (sprintPressed ? sprintMultiplier : 1f);

        // Gravity and jumping
        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // small stick-to-ground
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * targetSpeed;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);

        // Toggle view with T key
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            firstPerson = !firstPerson;
            ApplyView(firstPerson, snapPitchToDefault: false);
        }
    }

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (controller != null && controller.isGrounded)
        {
            // v = sqrt(2 * g * h), g is negative gravity
            verticalVelocity = Mathf.Sqrt(Mathf.Max(0.01f, -2f * gravity * jumpHeight));
        }
    }

    void ApplyView(bool fp, bool snapPitchToDefault)
    {
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = fp ? firstPersonCamLocalPos : thirdPersonCamLocalPos;
            if (snapPitchToDefault)
            {
                pitch = fp ? firstPersonDefaultPitch : thirdPersonDefaultPitch;
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        if (bodyRenderers != null)
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (cameraTransform != null && bodyRenderers[i].transform.IsChildOf(cameraTransform)) continue;
                bodyRenderers[i].enabled = !fp;
            }
        }
    }
}
