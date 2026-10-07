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
        GameObject player = GameObject.Find("Player");
        Vector3 position = player != null
            ? player.transform.position + player.transform.forward * 10f
            : new Vector3(0f, 1f, 10f);

        GameObject enemy = CreateEnemyAt("TestEnemy", position);

        Selection.activeGameObject = enemy;
        Debug.Log("Wongwirok: 테스트 더미 적 생성 완료");
    }

    [MenuItem("Wongwirok/Spawn Random Enemies (x5)")]
    public static void SpawnRandomEnemies()
    {
        GameObject player = GameObject.Find("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;

        const int count = 5;
        const float minDistance = 6f;
        const float maxDistance = 16f;

        GameObject[] spawned = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minDistance, maxDistance);
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
            Vector3 position = new Vector3(center.x + offset.x, center.y, center.z + offset.z);

            spawned[i] = CreateEnemyAt($"TestEnemy_Random_{i + 1}", position);
        }

        Selection.objects = spawned;
        Debug.Log($"Wongwirok: 랜덤 위치에 테스트 적 {count}마리 생성 완료");
    }

    [MenuItem("Wongwirok/Spawn Stage1 Mob Set (물손+익사궁인+도깨비불)")]
    public static void SpawnStage1MobSet()
    {
        GameObject player = GameObject.Find("Player");
        Vector3 center = player != null ? player.transform.position : Vector3.zero;

        GameObject waterHand = CreateWaterHand(center + new Vector3(4f, 0f, 6f));
        GameObject drownedLady = CreateDrownedCourtLady(center + new Vector3(-5f, 0f, 7f));
        GameObject willOWisp = CreateWillOWisp(center + new Vector3(0f, 0f, 10f));

        Selection.objects = new GameObject[] { waterHand, drownedLady, willOWisp };
        Debug.Log("Wongwirok: 1스테이지 잡몹 3종(물손, 익사 궁인, 연못 도깨비불) 생성 완료");
    }

    private static GameObject CreateWaterHand(Vector3 position)
    {
        GameObject enemy = GetOrCreateCapsule("Mob_WaterHand", position);
        SetRendererColor(enemy, new Color(0.2f, 0.5f, 0.9f));

        EnemyHealth health = EnsureComponent<EnemyHealth>(enemy);
        SetField(health, "maxHealth", 10f);
        SetupEnemyHealthBar(enemy, health);

        EnsureComponent<EnemyHitReaction>(enemy);

        EnemyAttack attack = EnsureComponent<EnemyAttack>(enemy);
        SetField(attack, "attackDamage", 0f);
        SetField(attack, "attackRange", 1.3f);
        SetField(attack, "telegraphDuration", 0.4f);
        SetField(attack, "activeDuration", 0.15f);
        SetField(attack, "recoveryDuration", 0.6f);
        SetField(attack, "slowsPlayerInsteadOfDamage", true);
        SetField(attack, "slowMultiplier", 0.4f);
        SetField(attack, "slowDuration", 2.5f);

        EnemyChase chase = EnsureComponent<EnemyChase>(enemy);
        SetField(chase, "moveSpeed", 3.5f);
        SetField(chase, "detectionRange", 7f);

        return enemy;
    }

    private static GameObject CreateDrownedCourtLady(Vector3 position)
    {
        GameObject enemy = GetOrCreateCapsule("Mob_DrownedCourtLady", position);
        SetRendererColor(enemy, new Color(0.75f, 0.78f, 0.75f));

        EnemyHealth health = EnsureComponent<EnemyHealth>(enemy);
        SetField(health, "maxHealth", 60f);
        SetupEnemyHealthBar(enemy, health);

        EnsureComponent<EnemyHitReaction>(enemy);

        EnemyAttack attack = EnsureComponent<EnemyAttack>(enemy);
        SetField(attack, "attackDamage", 15f);
        SetField(attack, "attackRange", 1.8f);
        SetField(attack, "telegraphDuration", 0.9f);
        SetField(attack, "activeDuration", 0.25f);
        SetField(attack, "recoveryDuration", 1.3f);

        EnemyChase chase = EnsureComponent<EnemyChase>(enemy);
        SetField(chase, "moveSpeed", 1.5f);
        SetField(chase, "detectionRange", 8f);

        return enemy;
    }

    private static GameObject CreateWillOWisp(Vector3 position)
    {
        position.y = 1.5f;
        GameObject enemy = GetOrCreateCapsule("Mob_WillOWisp", position);
        SetRendererColor(enemy, new Color(1f, 0.45f, 0.1f));

        EnemyHealth health = EnsureComponent<EnemyHealth>(enemy);
        SetField(health, "maxHealth", 10f);
        SetupEnemyHealthBar(enemy, health);

        EnsureComponent<EnemyHitReaction>(enemy);

        EnemyRangedAttack attack = EnsureComponent<EnemyRangedAttack>(enemy);
        SetField(attack, "attackRange", 8f);
        SetField(attack, "telegraphDuration", 0.5f);
        SetField(attack, "cooldownDuration", 1.5f);
        SetField(attack, "projectileSpeed", 8f);
        SetField(attack, "projectileDamage", 8f);

        EnemyChase chase = EnsureComponent<EnemyChase>(enemy);
        SetField(chase, "moveSpeed", 2f);
        SetField(chase, "detectionRange", 10f);

        return enemy;
    }

    [MenuItem("Wongwirok/Spawn Boss Arena + Pond Ghost")]
    public static void SpawnBossArena()
    {
        GameObject player = GameObject.Find("Player");
        Vector3 arenaCenter = player != null
            ? player.transform.position + player.transform.forward * 25f
            : new Vector3(0f, 0f, 25f);

        BossArenaPlatforms arenaPlatforms = CreateBossArena(arenaCenter);
        GameObject boss = CreatePondGhostBoss(arenaCenter, arenaPlatforms);

        Selection.activeGameObject = boss;
        Debug.Log("Wongwirok: 보스 아레나(물+발판 6개) + 연못 귀신 생성 완료");
    }

    private static BossArenaPlatforms CreateBossArena(Vector3 center)
    {
        GameObject arenaRoot = GameObject.Find("BossArena");
        if (arenaRoot == null)
            arenaRoot = new GameObject("BossArena");
        arenaRoot.transform.position = center;

        Transform waterTransform = arenaRoot.transform.Find("Water");
        GameObject water = waterTransform != null ? waterTransform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        if (waterTransform == null)
            water.transform.SetParent(arenaRoot.transform, false);
        water.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        water.transform.localScale = new Vector3(3f, 1f, 3f);
        if (water.GetComponent<Collider>() != null)
            Object.DestroyImmediate(water.GetComponent<Collider>());
        SetRendererColor(water, new Color(0.1f, 0.3f, 0.5f));

        BossArenaPlatforms arenaPlatforms = EnsureComponent<BossArenaPlatforms>(arenaRoot);

        const int platformCount = 6;
        const float ringRadius = 5f;
        GameObject[] platforms = new GameObject[platformCount];

        for (int i = 0; i < platformCount; i++)
        {
            string platformName = $"Platform_{i + 1}";
            Transform existingPlatform = arenaRoot.transform.Find(platformName);
            GameObject platform = existingPlatform != null ? existingPlatform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = platformName;
            if (existingPlatform == null)
                platform.transform.SetParent(arenaRoot.transform, false);

            float angle = i * Mathf.PI * 2f / platformCount;
            platform.transform.localPosition = new Vector3(Mathf.Sin(angle) * ringRadius, 0f, Mathf.Cos(angle) * ringRadius);
            platform.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);
            SetRendererColor(platform, new Color(0.5f, 0.45f, 0.4f));

            platforms[i] = platform;
        }

        SerializedObject arenaSerialized = new SerializedObject(arenaPlatforms);
        SerializedProperty platformsProp = arenaSerialized.FindProperty("platforms");
        platformsProp.arraySize = platforms.Length;
        for (int i = 0; i < platforms.Length; i++)
            platformsProp.GetArrayElementAtIndex(i).objectReferenceValue = platforms[i];
        arenaSerialized.ApplyModifiedProperties();

        return arenaPlatforms;
    }

    private static GameObject CreatePondGhostBoss(Vector3 position, BossArenaPlatforms arenaPlatforms)
    {
        GameObject boss = GetOrCreateCapsule("Boss_PondGhost", position);
        boss.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        SetRendererColor(boss, new Color(0.3f, 0.45f, 0.55f));

        EnemyHealth health = EnsureComponent<EnemyHealth>(boss);
        SetField(health, "maxHealth", 150f);
        SetupEnemyHealthBar(boss, health);

        EnsureComponent<EnemyHitReaction>(boss);

        PondGhostBoss bossScript = EnsureComponent<PondGhostBoss>(boss);
        SerializedObject bossSerialized = new SerializedObject(bossScript);
        bossSerialized.FindProperty("arenaPlatforms").objectReferenceValue = arenaPlatforms;
        bossSerialized.ApplyModifiedProperties();

        return boss;
    }

    private static GameObject GetOrCreateCapsule(string name, Vector3 position)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            obj.name = name;
        }
        obj.transform.position = position;
        return obj;
    }

    private static void SetRendererColor(GameObject obj, Color color)
    {
        Renderer targetRenderer = obj.GetComponent<Renderer>();
        if (targetRenderer != null)
            targetRenderer.material.color = color;
    }

    private static T EnsureComponent<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        if (component == null)
            component = obj.AddComponent<T>();
        return component;
    }

    private static void SetField(Object target, string fieldName, float value)
    {
        SerializedProperty property = FindField(target, fieldName);
        if (property == null)
            return;
        property.floatValue = value;
        property.serializedObject.ApplyModifiedProperties();
    }

    private static void SetField(Object target, string fieldName, bool value)
    {
        SerializedProperty property = FindField(target, fieldName);
        if (property == null)
            return;
        property.boolValue = value;
        property.serializedObject.ApplyModifiedProperties();
    }

    private static SerializedProperty FindField(Object target, string fieldName)
    {
        SerializedObject serializedTarget = new SerializedObject(target);
        SerializedProperty property = serializedTarget.FindProperty(fieldName);
        if (property == null)
            Debug.LogWarning($"Wongwirok: {target.GetType().Name}에 필드 '{fieldName}'를 찾을 수 없음");
        return property;
    }

    private static GameObject CreateEnemyAt(string name, Vector3 position)
    {
        GameObject enemy = GameObject.Find(name);
        if (enemy == null)
        {
            enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = name;
        }

        enemy.transform.position = position;

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

        return enemy;
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
            canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject barRoot = CreateBarVisual(canvasObject.transform, "PlayerHealthBar", new Vector2(220f, 24f), Color.green, out Image fillImage);
        RectTransform barRect = barRoot.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 0f);
        barRect.pivot = new Vector2(0f, 0f);
        barRect.anchoredPosition = new Vector2(24f, 24f);

        HealthBarUI barUI = barRoot.GetComponent<HealthBarUI>();
        if (barUI == null)
            barUI = barRoot.AddComponent<HealthBarUI>();

        SerializedObject barSerialized = new SerializedObject(barUI);
        barSerialized.FindProperty("fillImage").objectReferenceValue = fillImage;
        barSerialized.FindProperty("healthSource").objectReferenceValue = playerHealth;
        barSerialized.ApplyModifiedProperties();

        barUI.Initialize(playerHealth);
    }

    private static void SetupEnemyHealthBar(GameObject enemy, EnemyHealth enemyHealth)
    {
        Transform existingCanvas = enemy.transform.Find("HealthBarCanvas");
        GameObject canvasObject = existingCanvas != null ? existingCanvas.gameObject : new GameObject("HealthBarCanvas", typeof(Canvas));
        if (existingCanvas == null)
            canvasObject.transform.SetParent(enemy.transform, false);

        canvasObject.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 24f);

        GameObject barRoot = CreateBarVisual(canvasObject.transform, "Bar", new Vector2(200f, 24f), Color.green, out Image fillImage);
        barRoot.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        if (canvasObject.GetComponent<BillboardToCamera>() == null)
            canvasObject.AddComponent<BillboardToCamera>();

        HealthBarUI barUI = canvasObject.GetComponent<HealthBarUI>();
        if (barUI == null)
            barUI = canvasObject.AddComponent<HealthBarUI>();

        SerializedObject barSerialized = new SerializedObject(barUI);
        barSerialized.FindProperty("fillImage").objectReferenceValue = fillImage;
        barSerialized.FindProperty("healthSource").objectReferenceValue = enemyHealth;
        barSerialized.ApplyModifiedProperties();

        barUI.Initialize(enemyHealth);
    }

    private static GameObject CreateBarVisual(Transform parent, string name, Vector2 size, Color fillColor, out Image fillImage)
    {
        Transform existingRoot = parent.Find(name);
        GameObject root = existingRoot != null ? existingRoot.gameObject : new GameObject(name, typeof(RectTransform));
        if (existingRoot == null)
            root.transform.SetParent(parent, false);
        root.GetComponent<RectTransform>().sizeDelta = size;

        Transform existingBackground = root.transform.Find("Background");
        GameObject background = existingBackground != null ? existingBackground.gameObject : new GameObject("Background", typeof(Image));
        if (existingBackground == null)
            background.transform.SetParent(root.transform, false);
        background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Transform existingFill = root.transform.Find("Fill");
        GameObject fill = existingFill != null ? existingFill.gameObject : new GameObject("Fill", typeof(Image));
        if (existingFill == null)
            fill.transform.SetParent(root.transform, false);
        fillImage = fill.GetComponent<Image>();
        fillImage.color = fillColor;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        return root;
    }
}
