using UnityEngine;
using UnityEngine.InputSystem;

// Controlador simples de personagem em 1ª/3ª pessoa.
// Usa CharacterController (colisão) e Input System (Move/Look/Sprint/Jump).
// Tecla T alterna entre primeira e terceira pessoa.

public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;           // Velocidade básica de movimento no chão
    public float sprintMultiplier = 1.6f;  // Multiplicador de velocidade ao correr
    public float jumpHeight = 1.2f;        // Altura do pulo (em metros aproximados)
    public float gravity = -9.81f;         // Gravidade aplicada (valor negativo)

    [Header("Look")]
    public Transform cameraTransform;      // Transform da câmera filha do player
    public float lookSensitivity = 2f;     // Sensibilidade de rotação (quanto gira ao olhar)
    public bool invertY = false;           // Inverter eixo Y ao olhar (para cima/baixo)
    public float minPitch = -70f;          // Limite mínimo de inclinação da câmera (olhar para baixo)
    public float maxPitch = 80f;           // Limite máximo de inclinação da câmera (olhar para cima)

    [Header("View Toggle")]
    public bool firstPerson = false;                               // Começa em 1ª pessoa?
    public Vector3 firstPersonCamLocalPos = new Vector3(0f, 1.6f, 0f);   // Posição da câmera em 1ª pessoa
    public Vector3 thirdPersonCamLocalPos = new Vector3(0f, 1.6f, -3f);  // Posição da câmera em 3ª pessoa
    public float firstPersonDefaultPitch = 0f;                     // Pitch padrão ao entrar na 1ª pessoa
    public float thirdPersonDefaultPitch = 10f;                    // Pitch padrão ao entrar na 3ª pessoa

    [Header("Visual Roots")]
    public Transform bodyRoot; // Raiz do tronco/corpo (usado para ocultar em 1ª pessoa)
    public Transform legsRoot; // Raiz das pernas (mantidas visíveis em 1ª pessoa)
    public Transform armsRoot; // Raiz dos braços (mantidos visíveis em 1ª e 3ª pessoa)

    [Header("Leg Animation")]
    public Transform leftLeg;   // Referência da perna esquerda (opcional, pode ser encontrada pelo nome)
    public Transform rightLeg;  // Referência da perna direita (opcional)
    public float legSwingMaxAngle = 25f; // Ângulo máximo de balanço das pernas
    public float stepFrequency = 4f;     // Frequência base dos passos por unidade de velocidade

    [Header("Arm Animation")]
    public Transform leftArm;   // Referência do braço esquerdo (opcional)
    public Transform rightArm;  // Referência do braço direito (opcional)
    public float armSwingMaxAngle = 15f; // Ângulo máximo de balanço dos braços

    CharacterController controller; // Componente de colisão/movimento
    PlayerInput playerInput;        // Acesso às Actions do Input System
    InputAction moveAction;         // Ação de movimento (Vector2)
    InputAction lookAction;         // Ação de olhar (Vector2)
    InputAction sprintAction;       // Ação de correr (botão)
    InputAction jumpAction;         // Ação de pular (botão)

    float verticalVelocity; // Velocidade vertical (para gravidade/pulo)
    float yaw;              // Rotação do corpo no eixo Y (esquerda/direita)
    float pitch;            // Inclinação da câmera (cima/baixo) no espaço local
    Renderer[] bodyRenderers; // Renderizadores do tronco
    Renderer[] legsRenderers; // Renderizadores das pernas
    Renderer[] armsRenderers; // Renderizadores dos braços
    Quaternion leftLegDefaultRot;  // Rotação local padrão da perna esquerda
    Quaternion rightLegDefaultRot; // Rotação local padrão da perna direita
    Quaternion leftArmDefaultRot;  // Rotação local padrão do braço esquerdo
    Quaternion rightArmDefaultRot; // Rotação local padrão do braço direito
    float stepPhase;               // Fase do ciclo de passos

    void Awake()
    {
        // Garante que temos um CharacterController (para colisões)
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        // Garante que temos um PlayerInput para ler as Actions
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = gameObject.AddComponent<PlayerInput>();
        }

        // Guarda referências das actions (se o asset estiver atribuído no PlayerInput)
        if (playerInput.actions != null)
        {
            moveAction = playerInput.actions["Move"];
            lookAction = playerInput.actions["Look"];
            sprintAction = playerInput.actions["Sprint"];
            jumpAction = playerInput.actions["Jump"];
        }

        // Tenta encontrar a câmera filha caso não esteja atribuída no inspetor
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        // Inicializa yaw/pitch a partir da rotação atual
        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = cameraTransform.localEulerAngles.x;
            // Converte de 0..360 para -180..180 para facilitar clamp
            if (pitch > 180f) pitch -= 360f;
        }

        // Captura os grupos de render para alternar visibilidade (1ª/3ª pessoa)
        if (bodyRoot != null)
            bodyRenderers = bodyRoot.GetComponentsInChildren<Renderer>(true);
        else
            bodyRenderers = new Renderer[0];

        if (legsRoot != null)
            legsRenderers = legsRoot.GetComponentsInChildren<Renderer>(true);
        else
            legsRenderers = new Renderer[0];

        if (armsRoot != null)
            armsRenderers = armsRoot.GetComponentsInChildren<Renderer>(true);
        else
            armsRenderers = new Renderer[0];

        // Tenta localizar as pernas por nome se não estiverem atribuídas
        if (legsRoot != null)
        {
            if (leftLeg == null)
            {
                var t = legsRoot.Find("Leg_L");
                if (t != null) leftLeg = t;
            }
            if (rightLeg == null)
            {
                var t = legsRoot.Find("Leg_R");
                if (t != null) rightLeg = t;
            }
        }

        // Tenta localizar os braços por nome se não estiverem atribuídos
        if (armsRoot != null)
        {
            if (leftArm == null)
            {
                var t = armsRoot.Find("Arm_L");
                if (t != null) leftArm = t;
            }
            if (rightArm == null)
            {
                var t = armsRoot.Find("Arm_R");
                if (t != null) rightArm = t;
            }
        }

        // Guarda rotações iniciais das pernas para aplicar offsets de animação
        if (leftLeg != null) leftLegDefaultRot = leftLeg.localRotation;
        if (rightLeg != null) rightLegDefaultRot = rightLeg.localRotation;
        if (leftArm != null) leftArmDefaultRot = leftArm.localRotation;
        if (rightArm != null) rightArmDefaultRot = rightArm.localRotation;

        // Aplica o estado inicial de visão (1ª ou 3ª pessoa)
        ApplyView(firstPerson, snapPitchToDefault: true);
    }

    void OnEnable()
    {
        if (jumpAction != null)
        {
            // Assina o evento de pulo (quando o botão é acionado)
            jumpAction.performed += OnJumpPerformed;
        }
    }

    void OnDisable()
    {
        if (jumpAction != null)
        {
            // Remove a assinatura do evento de pulo
            jumpAction.performed -= OnJumpPerformed;
        }
    }

    void Update()
    {
        // Lê entradas do jogador
        Vector2 moveInput = Vector2.zero; // eixo WASD/analógico
        Vector2 lookInput = Vector2.zero; // mouse/right stick
        bool sprintPressed = false;       // correr

        if (moveAction != null) moveInput = moveAction.ReadValue<Vector2>();
        if (lookAction != null) lookInput = lookAction.ReadValue<Vector2>();
        if (sprintAction != null) sprintPressed = sprintAction.IsPressed();

        // Olhar: yaw no corpo, pitch na câmera
        float lookX = lookInput.x;
        float lookY = lookInput.y * (invertY ? 1f : -1f);
        yaw += lookX * lookSensitivity * Time.deltaTime;
        pitch += lookY * lookSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Aplica rotações calculadas
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Movimento relativo ao player (frente/direita)
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 moveDir = Vector3.zero;
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            // Usa os vetores locais do player (horizontal)
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            moveDir = (right * inputDir.x + fwd * inputDir.z).normalized;
        }

        float targetSpeed = moveSpeed * (sprintPressed ? sprintMultiplier : 1f); // corre ou anda

        // Gravidade e contato com o chão
        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // pequeno empurrão para manter grudado no chão
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * targetSpeed;
        velocity.y = verticalVelocity;

        // Move respeitando colisões do CharacterController
        controller.Move(velocity * Time.deltaTime);

        // Animação simples de locomoção (pernas e braços) baseada na velocidade horizontal
        UpdateLegsAndArmsAnimation();

        // Alternar visão com a tecla T
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            firstPerson = !firstPerson;
            ApplyView(firstPerson, snapPitchToDefault: false);
        }
    }

    void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return; // ignora se a ação não foi realmente executada
        if (controller != null && controller.isGrounded)
        {
            // Fórmula do pulo: v = sqrt(2 * |g| * h)
            verticalVelocity = Mathf.Sqrt(Mathf.Max(0.01f, -2f * gravity * jumpHeight));
        }
    }

    // Atualiza o balanço das pernas e braços com base na velocidade atual
    void UpdateLegsAndArmsAnimation()
    {
        bool hasLegs = (leftLeg != null || rightLeg != null);
        bool hasArms = (leftArm != null || rightArm != null);
        if (!hasLegs && !hasArms) return;

        // Velocidade horizontal do controller (ignora Y)
        Vector3 horizVel = controller != null ? controller.velocity : Vector3.zero;
        horizVel.y = 0f;
        float speed = horizVel.magnitude;

        if (speed > 0.05f)
        {
            // A fase avança proporcionalmente à velocidade e à frequência de passos
            stepPhase += speed * stepFrequency * Time.deltaTime;
            float swing = Mathf.Sin(stepPhase * Mathf.PI * 2f) * legSwingMaxAngle;

            if (leftLeg != null)
            {
                Quaternion target = Quaternion.Euler(swing, 0f, 0f) * leftLegDefaultRot;
                leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, target, 0.3f);
            }
            if (rightLeg != null)
            {
                Quaternion target = Quaternion.Euler(-swing, 0f, 0f) * rightLegDefaultRot;
                rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, target, 0.3f);
            }

            // Braços balançam em oposição às pernas do mesmo lado
            float armSwing = Mathf.Sin(stepPhase * Mathf.PI * 2f) * armSwingMaxAngle;
            if (leftArm != null)
            {
                Quaternion target = Quaternion.Euler(-armSwing, 0f, 0f) * leftArmDefaultRot;
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, target, 0.3f);
            }
            if (rightArm != null)
            {
                Quaternion target = Quaternion.Euler(armSwing, 0f, 0f) * rightArmDefaultRot;
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, target, 0.3f);
            }
        }
        else
        {
            // Volta suavemente para a rotação padrão quando parado
            if (leftLeg != null)
                leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, leftLegDefaultRot, 8f * Time.deltaTime);
            if (rightLeg != null)
                rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, rightLegDefaultRot, 8f * Time.deltaTime);

            if (leftArm != null)
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftArmDefaultRot, 8f * Time.deltaTime);
            if (rightArm != null)
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightArmDefaultRot, 8f * Time.deltaTime);
        }
    }

    void ApplyView(bool fp, bool snapPitchToDefault)
    {
        if (cameraTransform != null)
        {
            // Define a posição local da câmera de acordo com o modo
            cameraTransform.localPosition = fp ? firstPersonCamLocalPos : thirdPersonCamLocalPos;
            if (snapPitchToDefault)
            {
                // Ajusta o pitch para um ângulo padrão ao trocar de modo
                pitch = fp ? firstPersonDefaultPitch : thirdPersonDefaultPitch;
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        // Em 1ª pessoa: esconde o tronco (evita ver o próprio corpo), mantém pernas e braços visíveis
        // Em 3ª pessoa: mostra tudo
        if (bodyRenderers != null)
        {
            foreach (var r in bodyRenderers)
                if (r != null) r.enabled = !fp;
        }
        if (legsRenderers != null)
        {
            foreach (var r in legsRenderers)
                if (r != null) r.enabled = true;
        }
        if (armsRenderers != null)
        {
            foreach (var r in armsRenderers)
                if (r != null) r.enabled = true;
        }
    }
}
