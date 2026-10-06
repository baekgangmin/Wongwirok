using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SceneSetup
{
    [MenuItem("Wongwirok/Setup Player and Camera")]
    public static void SetupPlayerAndCamera()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        }

        if (player.GetComponent<CharacterController>() == null)
        {
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = Vector3.zero;
            controller.height = 2f;
            controller.radius = 0.4f;
        }

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
            movement = player.AddComponent<PlayerMovement>();

        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        if (attack == null)
            attack = player.AddComponent<PlayerAttack>();

        PlayerParry parry = player.GetComponent<PlayerParry>();
        if (parry == null)
            parry = player.AddComponent<PlayerParry>();

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = player.AddComponent<PlayerHealth>();

        SetupPlayerHUD(playerHealth);

        WeaponSwing weaponSwing = CreateBranchWeapon(player);

        SerializedObject attackSerialized = new SerializedObject(attack);
        attackSerialized.FindProperty("weaponSwing").objectReferenceValue = weaponSwing;
        attackSerialized.ApplyModifiedProperties();

        SerializedObject parrySerialized = new SerializedObject(parry);
        parrySerialized.FindProperty("weaponSwing").objectReferenceValue = weaponSwing;
        parrySerialized.ApplyModifiedProperties();

        Transform cameraPivot = player.transform.Find("CameraPivot");
        if (cameraPivot == null)
        {
            GameObject pivotObject = new GameObject("CameraPivot");
            pivotObject.transform.SetParent(player.transform);
            pivotObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot = pivotObject.transform;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        mainCamera.transform.SetParent(null);

        ThirdPersonCamera cameraScript = mainCamera.GetComponent<ThirdPersonCamera>();
        if (cameraScript == null)
            cameraScript = mainCamera.gameObject.AddComponent<ThirdPersonCamera>();

        SerializedObject cameraSerialized = new SerializedObject(cameraScript);
        cameraSerialized.FindProperty("target").objectReferenceValue = cameraPivot;
        cameraSerialized.ApplyModifiedProperties();

        SerializedObject movementSerialized = new SerializedObject(movement);
        movementSerialized.FindProperty("cameraTransform").objectReferenceValue = mainCamera.transform;
        movementSerialized.ApplyModifiedProperties();

        Selection.activeGameObject = player;
        Debug.Log("Wongwirok: Player + ThirdPersonCamera 세팅 완료");
    }

    [MenuItem("Wongwirok/Spawn Test Enemy")]
    public static void SpawnTestEnemy()
    {
        GameObject enemy = GameObject.Find("TestEnemy");
        if (enemy == null)
        {
            enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "TestEnemy";
        }

        GameObject player = GameObject.Find("Player");
        enemy.transform.position = player != null
            ? player.transform.position + player.transform.forward * 10f
            : new Vector3(0f, 1f, 10f);

        Renderer enemyRenderer = enemy.GetComponent<Renderer>();
        if (enemyRenderer != null)
            enemyRenderer.material.color = Color.gray;

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
            enemyHealth = enemy.AddComponent<EnemyHealth>();

        SetupEnemyHealthBar(enemy, enemyHealth);

        if (enemy.GetComponent<EnemyHitReaction>() == null)
            enemy.AddComponent<EnemyHitReaction>();

        if (enemy.GetComponent<EnemyAttack>() == null)
            enemy.AddComponent<EnemyAttack>();

        if (enemy.GetComponent<EnemyChase>() == null)
            enemy.AddComponent<EnemyChase>();

        Selection.activeGameObject = enemy;
        Debug.Log("Wongwirok: 테스트 더미 적 생성 완료");
    }

    private static WeaponSwing CreateBranchWeapon(GameObject player)
    {
        Transform pivot = player.transform.Find("WeaponPivot");
        if (pivot == null)
        {
            GameObject pivotObject = new GameObject("WeaponPivot");
            pivotObject.transform.SetParent(player.transform);
            pivotObject.transform.localPosition = new Vector3(0.4f, 0.4f, 0.25f);
            pivotObject.transform.localRotation = Quaternion.identity;
            pivot = pivotObject.transform;
        }

        WeaponSwing weaponSwing = pivot.GetComponent<WeaponSwing>();
        if (weaponSwing == null)
            weaponSwing = pivot.gameObject.AddComponent<WeaponSwing>();

        Transform branch = pivot.Find("Branch");
        if (branch == null)
        {
            GameObject branchObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            branchObject.name = "Branch";
            Object.DestroyImmediate(branchObject.GetComponent<Collider>());
            branchObject.transform.SetParent(pivot);
            branchObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            branchObject.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            branchObject.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);

            Renderer branchRenderer = branchObject.GetComponent<Renderer>();
            if (branchRenderer != null)
                branchRenderer.material.color = new Color(0.45f, 0.3f, 0.15f);

            branch = branchObject.transform;
        }

        return weaponSwing;
    }

    private static void SetupPlayerHUD(PlayerHealth playerHealth)
    {
        GameObject canvasObject = GameObject.Find("HUD Canvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        Transform existingBar = canvasObject.transform.Find("PlayerHealthBar");
        GameObject barRoot;
        Image fillImage;

        if (existingBar == null)
        {
            barRoot = CreateBarVisual(canvasObject.transform, "PlayerHealthBar", new Vector2(220f, 24f), Color.red, out fillImage);
            RectTransform barRect = barRoot.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 0f);
            barRect.pivot = new Vector2(0f, 0f);
            barRect.anchoredPosition = new Vector2(24f, 24f);
        }
        else
        {
            barRoot = existingBar.gameObject;
            fillImage = barRoot.transform.Find("Fill").GetComponent<Image>();
        }

        HealthBarUI barUI = barRoot.GetComponent<HealthBarUI>();
        if (barUI == null)
            barUI = barRoot.AddComponent<HealthBarUI>();

        SerializedObject barSerialized = new SerializedObject(barUI);
        barSerialized.FindProperty("fillImage").objectReferenceValue = fillImage;
        barSerialized.ApplyModifiedProperties();

        barUI.Initialize(playerHealth);
    }

    private static void SetupEnemyHealthBar(GameObject enemy, EnemyHealth enemyHealth)
    {
        Transform existingCanvas = enemy.transform.Find("HealthBarCanvas");
        GameObject canvasObject;
        Image fillImage;

        if (existingCanvas == null)
        {
            canvasObject = new GameObject("HealthBarCanvas", typeof(Canvas));
            canvasObject.transform.SetParent(enemy.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.01f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 24f);

            GameObject barRoot = CreateBarVisual(canvasObject.transform, "Bar", new Vector2(200f, 24f), Color.red, out fillImage);
            barRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            canvasObject.AddComponent<BillboardToCamera>();
        }
        else
        {
            canvasObject = existingCanvas.gameObject;
            fillImage = canvasObject.transform.Find("Bar/Fill").GetComponent<Image>();
        }

        HealthBarUI barUI = canvasObject.GetComponent<HealthBarUI>();
        if (barUI == null)
            barUI = canvasObject.AddComponent<HealthBarUI>();

        SerializedObject barSerialized = new SerializedObject(barUI);
        barSerialized.FindProperty("fillImage").objectReferenceValue = fillImage;
        barSerialized.ApplyModifiedProperties();

        barUI.Initialize(enemyHealth);
    }

    private static GameObject CreateBarVisual(Transform parent, string name, Vector2 size, Color fillColor, out Image fillImage)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        root.GetComponent<RectTransform>().sizeDelta = size;

        GameObject background = new GameObject("Background", typeof(Image));
        background.transform.SetParent(root.transform, false);
        background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(root.transform, false);
        fillImage = fill.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        return root;
    }
}
