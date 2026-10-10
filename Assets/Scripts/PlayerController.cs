using UnityEngine;

/// <summary>
/// Jugador en primera persona: movimiento con CharacterController, cámara con mouse,
/// correr con resistencia (stamina), linterna y ruido (que el guardia puede escuchar).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float gravity = -20f;

    [Header("Cámara")]
    public float mouseSensitivity = 2f;
    public Transform cameraPivot;

    [Header("Resistencia")]
    public float maxStamina = 100f;
    public float staminaDrain = 28f;
    public float staminaRegen = 16f;

    [Header("Linterna")]
    public Light flashlight;

    public float Stamina { get; private set; }
    public float StaminaNormalized { get { return Stamina / maxStamina; } }
    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }

    /// <summary>Radio dentro del cual el guardia "escucha" al jugador.</summary>
    public float NoiseRadius { get { return IsSprinting ? 10f : (IsMoving ? 4f : 1.5f); } }

    CharacterController controller;
    float pitch;
    float verticalVelocity;
    float stepTimer;
    bool exhausted;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Stamina = maxStamina;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        Look();
        Move();

        if (Input.GetKeyDown(KeyCode.F) && flashlight != null)
            flashlight.enabled = !flashlight.enabled;
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -80f, 80f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float dt = Time.deltaTime;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 dir = transform.right * h + transform.forward * v;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        IsMoving = dir.sqrMagnitude > 0.01f;

        // Resistencia: al llegar a 0 queda "agotado" hasta recuperar 25
        if (exhausted && Stamina > 25f) exhausted = false;
        bool wantsSprint = Input.GetKey(KeyCode.LeftShift) && IsMoving;
        IsSprinting = wantsSprint && !exhausted && Stamina > 0f;

        if (IsSprinting)
        {
            Stamina -= staminaDrain * dt;
            if (Stamina <= 0f)
            {
                Stamina = 0f;
                exhausted = true;
                IsSprinting = false;
            }
        }
        else
        {
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * dt);
        }

        float speed = IsSprinting ? sprintSpeed : walkSpeed;

        // Gravedad
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        controller.Move((dir * speed + Vector3.up * verticalVelocity) * dt);

        // Pasos
        if (IsMoving && controller.isGrounded)
        {
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayFootstep();
                stepTimer = IsSprinting ? 0.3f : 0.5f;
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }
}
