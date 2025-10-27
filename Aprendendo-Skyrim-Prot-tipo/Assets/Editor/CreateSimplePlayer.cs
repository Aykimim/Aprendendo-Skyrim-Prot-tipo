using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class CreateSimplePlayer
{
    [MenuItem("GameObject/Create/Player (Simple Controller)", false, 10)]
    public static void Create()
    {
        // Create a capsule as the player body
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";

        // Ensure the capsule has a reasonable scale
        player.transform.localScale = Vector3.one;

        // Remove the CapsuleCollider (we'll use CharacterController)
        var capCol = player.GetComponent<CapsuleCollider>();
        if (capCol != null) Object.DestroyImmediate(capCol);

        // Add components: CharacterController, SimplePlayerController
        player.AddComponent<CharacterController>();
        var controller = player.AddComponent<SimplePlayerController>();
        controller.moveSpeed = 5f;
        controller.sprintMultiplier = 1.6f;
        controller.jumpHeight = 1.2f;

        // Create a child camera for a simple 3rd-person view
        GameObject cam = new GameObject("PlayerCamera");
        cam.transform.SetParent(player.transform, false);
        var camera = cam.AddComponent<Camera>();
        cam.transform.localPosition = new Vector3(0f, 1.6f, -3f);
        cam.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        // Assign to controller
        controller.cameraTransform = cam.transform;

        // Replace existing MainCamera to avoid duplicates
        var existingMainCam = Camera.main;
        if (existingMainCam != null && existingMainCam.gameObject != camera.gameObject)
        {
            existingMainCam.gameObject.SetActive(false);
        }
        camera.tag = "MainCamera";
        // Ensure there is an AudioListener on the active camera
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

        // Add PlayerInput and assign actions if found
        var input = player.AddComponent<PlayerInput>();
        input.defaultActionMap = "Player";
        // Try to load the InputActionAsset from project
        #if UNITY_EDITOR
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (actions != null)
        {
            input.actions = actions;
        }
        #endif
        input.camera = camera;

        // Apply a simple polished material to the player body (URP Lit)
        var bodyRenderer = player.GetComponent<MeshRenderer>();
        if (bodyRenderer != null)
        {
            var mat = EnsurePlayerMaterial();
            if (mat != null) bodyRenderer.sharedMaterial = mat;
        }

        // Focus and select
        Selection.activeGameObject = player;
        SceneView.lastActiveSceneView?.FrameSelected();

        // Note: This simple controller uses Transform.Translate without physics.
        // For collisions, consider switching to CharacterController and using Move().
    }

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

    // Creates/loads a URP Lit material for the Player and returns it
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
            // Set a nice color and smoothness
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

    [MenuItem("GameObject/Player/Polish Selected Model", false, 11)]
    public static void PolishSelected()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Selecione um GameObject para polir.");
            return;
        }

        var go = Selection.activeGameObject;
        var renderer = go.GetComponentInChildren<MeshRenderer>();
        var mat = EnsurePlayerMaterial();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }

        var cam = go.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.1f;
        }

        var cc = go.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.08f;
        }

        Debug.Log("Modelo polido: material URP aplicado e ajustes básicos feitos.");
    }
}
