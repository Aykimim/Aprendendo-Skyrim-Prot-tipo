using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// Utilitário de Editor para criar rapidamente um Player configurado:
// - Hierarquia: Player (raiz sem render), Body (tronco), Legs (pernas)
// - CharacterController com dimensões humanoides
// - Camera em 3ª pessoa (com opção de alternar para 1ª pessoa no runtime)
// - PlayerInput com Actions do arquivo Assets/InputSystem_Actions.inputactions
// - Material URP básico aplicado ao modelo

public static class CreateSimplePlayer
{
    // Menu no Unity: GameObject > Create > Player (Simple Controller)
    [MenuItem("GameObject/Create/Player (Simple Controller)", false, 10)]
    public static void Create()
    {
        // Cria o objeto raiz do Player (não vai renderizar)
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.localScale = Vector3.one;

        // Remove render/mesh do root (apenas os filhos irão aparecer)
        var rootRenderer = player.GetComponent<MeshRenderer>();
        var rootFilter = player.GetComponent<MeshFilter>();
        if (rootRenderer != null) Object.DestroyImmediate(rootRenderer);
        if (rootFilter != null) Object.DestroyImmediate(rootFilter);

        // Remove o CapsuleCollider (vamos usar CharacterController)
        var capCol = player.GetComponent<CapsuleCollider>();
        if (capCol != null) Object.DestroyImmediate(capCol);

        // Adiciona componentes: CharacterController + nosso controlador
        var cc = player.AddComponent<CharacterController>();
        // Dimensões aproximadas de um humano
        cc.radius = 0.4f;
        cc.height = 1.8f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.3f;
        cc.slopeLimit = 45f;
        cc.skinWidth = 0.08f;
        var controller = player.AddComponent<SimplePlayerController>();
        controller.moveSpeed = 5f;
        controller.sprintMultiplier = 1.6f;
        controller.jumpHeight = 1.2f;

        // Hierarquia visual: tronco (Body) e pernas (Legs) como filhos
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(player.transform, false);
        Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
        // Posição aproximada do tronco
        body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        body.transform.localScale = new Vector3(1f, 1f, 1f);

        var legsRoot = new GameObject("Legs");
        legsRoot.transform.SetParent(player.transform, false);
        legsRoot.transform.localPosition = Vector3.zero;

        // Cria duas cápsulas para as pernas
        CreateLeg(legsRoot.transform, "Leg_L", new Vector3(-0.25f, 0.5f, 0f));
        CreateLeg(legsRoot.transform, "Leg_R", new Vector3(0.25f, 0.5f, 0f));

        // Atribui raízes ao controlador para controle de visibilidade
        controller.bodyRoot = body.transform;
        controller.legsRoot = legsRoot.transform;
        // Atribui referências diretas às pernas para animação procedural
        var legL = legsRoot.transform.Find("Leg_L");
        var legR = legsRoot.transform.Find("Leg_R");
        controller.leftLeg = legL;
        controller.rightLeg = legR;

        // Hierarquia de braços
        var armsRoot = new GameObject("Arms");
        armsRoot.transform.SetParent(player.transform, false);
        armsRoot.transform.localPosition = Vector3.zero;
        CreateArm(armsRoot.transform, "Arm_L", new Vector3(-0.5f, 1.2f, 0.1f));
        CreateArm(armsRoot.transform, "Arm_R", new Vector3(0.5f, 1.2f, 0.1f));
        controller.armsRoot = armsRoot.transform;
        var armL = armsRoot.transform.Find("Arm_L");
        var armR = armsRoot.transform.Find("Arm_R");
        controller.leftArm = armL;
        controller.rightArm = armR;

        // Cabeça (com olhos e boca simples)
        var skinMat = EnsurePlayerMaterial();
        var darkMat = EnsureFaceDarkMaterial();
        var head = CreateHead(body.transform, skinMat, darkMat);

        // Cria câmera filha (3ª pessoa por padrão)
        GameObject cam = new GameObject("PlayerCamera");
        cam.transform.SetParent(player.transform, false);
        var camera = cam.AddComponent<Camera>();
        cam.transform.localPosition = new Vector3(0f, 1.6f, -3f);
        cam.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        // Assign to controller
        controller.cameraTransform = cam.transform;

        // Substitui a MainCamera existente para evitar câmeras duplicadas
        var existingMainCam = Camera.main;
        if (existingMainCam != null && existingMainCam.gameObject != camera.gameObject)
        {
            existingMainCam.gameObject.SetActive(false);
        }
        camera.tag = "MainCamera";
        // Garante um AudioListener na câmera ativa
        if (Object.FindFirstObjectByType<AudioListener>() == null)
        {
            cam.AddComponent<AudioListener>();
        }

        // Position near the selected object (e.g., your plane) or at origin
        Vector3 spawnPos = Vector3.zero;
        if (Selection.activeTransform != null)
        {
            // Try to place slightly above the selected object
            Bounds bounds = GetWorldBounds(Selection.activeTransform.gameObject);
            spawnPos = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);
        }
        else
        {
            // Raise slightly to avoid intersecting ground
            spawnPos = new Vector3(0f, 1f, 0f);
        }
        player.transform.position = spawnPos;

        // Adiciona PlayerInput e carrega o asset de ações, se existir
        var input = player.AddComponent<PlayerInput>();
        input.defaultActionMap = "Player";
        // Tenta carregar o InputActionAsset do projeto
        #if UNITY_EDITOR
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (actions != null)
        {
            input.actions = actions;
        }
        #endif
        input.camera = camera;

        // Aplica um material simples (URP Lit) ao modelo do player
        var mat = skinMat;
        ApplyMaterialIfAny(body, mat);
        ApplyMaterialIfAny(legsRoot, mat);
        ApplyMaterialIfAny(armsRoot, mat);

        // Foca e seleciona o Player criado na cena
        Selection.activeGameObject = player;
        SceneView.lastActiveSceneView?.FrameSelected();

        // Note: This simple controller uses Transform.Translate without physics.
        // For collisions, consider switching to CharacterController and using Move().
    }

    // Calcula Bounds em mundo para posicionar o player acima do objeto selecionado
    private static Bounds GetWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        // Fallback: use transform position
        return new Bounds(go.transform.position, Vector3.zero);
    }

    // Cria uma cápsula representando uma perna na posição local indicada
    private static void CreateLeg(Transform parent, string name, Vector3 localPos)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leg.name = name;
        leg.transform.SetParent(parent, false);
        Object.DestroyImmediate(leg.GetComponent<CapsuleCollider>());
        leg.transform.localPosition = localPos; // aproximadamente metade da altura
        leg.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
    }

    // Cria um cilindro/cápsula simples para representar o braço
    private static void CreateArm(Transform parent, string name, Vector3 localPos)
    {
        var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        arm.name = name;
        arm.transform.SetParent(parent, false);
        Object.DestroyImmediate(arm.GetComponent<CapsuleCollider>());
        arm.transform.localPosition = localPos;
        arm.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
    }

    // Cria uma cabeça simples (esfera) com olhos (esferas) e boca (cubinho)
    private static GameObject CreateHead(Transform parentBody, Material skinMat, Material darkMat)
    {
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(parentBody, false);
        Object.DestroyImmediate(head.GetComponent<SphereCollider>());
        head.transform.localPosition = new Vector3(0f, 1.6f - parentBody.localPosition.y, 0.1f);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        // Material da "pele"
        var headRenderer = head.GetComponent<MeshRenderer>();
        if (skinMat != null && headRenderer != null) headRenderer.sharedMaterial = skinMat;

        // Olhos
        var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeL.name = "Eye_L";
        eyeL.transform.SetParent(head.transform, false);
        Object.DestroyImmediate(eyeL.GetComponent<SphereCollider>());
        eyeL.transform.localPosition = new Vector3(-0.12f, 0.05f, 0.22f);
        eyeL.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeR.name = "Eye_R";
        eyeR.transform.SetParent(head.transform, false);
        Object.DestroyImmediate(eyeR.GetComponent<SphereCollider>());
        eyeR.transform.localPosition = new Vector3(0.12f, 0.05f, 0.22f);
        eyeR.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

        // Boca
        var mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mouth.name = "Mouth";
        mouth.transform.SetParent(head.transform, false);
        Object.DestroyImmediate(mouth.GetComponent<BoxCollider>());
        mouth.transform.localPosition = new Vector3(0f, -0.08f, 0.23f);
        mouth.transform.localScale = new Vector3(0.2f, 0.05f, 0.02f);

        // Materiais escuros para olhos e boca
        if (darkMat != null)
        {
            var rL = eyeL.GetComponent<MeshRenderer>();
            var rR = eyeR.GetComponent<MeshRenderer>();
            var rM = mouth.GetComponent<MeshRenderer>();
            if (rL != null) rL.sharedMaterial = darkMat;
            if (rR != null) rR.sharedMaterial = darkMat;
            if (rM != null) rM.sharedMaterial = darkMat;
        }

        return head;
    }

    // Cria/obtém um material escuro para olhos/boca (URP Lit com cor quase preta)
    private static Material EnsureFaceDarkMaterial()
    {
        #if UNITY_EDITOR
        const string matFolder = "Assets/Materials";
        const string matPath = matFolder + "/Player_FaceDark_Mat.mat";
        if (!AssetDatabase.IsValidFolder(matFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            mat = new Material(shader);
            mat.name = "Player_FaceDark_Mat";
            var dark = new Color(0.05f, 0.05f, 0.06f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", dark);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", dark);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }
        return mat;
        #else
        return null;
        #endif
    }

    // Creates/loads a URP Lit material for the Player and returns it
    // Cria/obtém um material URP Lit padrão para o Player
    private static Material EnsurePlayerMaterial()
    {
        #if UNITY_EDITOR
        const string matFolder = "Assets/Materials";
        const string matPath = matFolder + "/Player_Mat.mat";
        if (!AssetDatabase.IsValidFolder(matFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            mat = new Material(shader);
            mat.name = "Player_Mat";
            // Define uma cor e suavidade agradáveis
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.25f, 0.55f, 0.95f));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.25f, 0.55f, 0.95f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.7f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }
        return mat;
        #else
        return null;
        #endif
    }

    // Aplica um material a todos os MeshRenderers dentro de um GameObject
    private static void ApplyMaterialIfAny(GameObject go, Material mat)
    {
        if (go == null || mat == null) return;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
        }
    }

    // Menu para aplicar rapidamente material/ajustes ao objeto selecionado
    [MenuItem("GameObject/Player/Polish Selected Model", false, 11)]
    public static void PolishSelected()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Selecione um GameObject para polir.");
            return;
        }

        var go = Selection.activeGameObject;
        var mat = EnsurePlayerMaterial();
        ApplyMaterialIfAny(go, mat);

        var cam = go.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = 65f;   // FOV confortável
            cam.nearClipPlane = 0.1f; // Evita clipping próximo
        }

        var cc = go.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.stepOffset = 0.3f; // altura de degrau
            cc.slopeLimit = 45f;  // inclinação máxima
            cc.skinWidth = 0.08f; // folga para colisão
        }

        Debug.Log("Modelo polido: material URP aplicado e ajustes básicos feitos.");
    }
}
