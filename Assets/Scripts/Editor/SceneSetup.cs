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

    // 플레이어가 현재 어디를 보고 있든 매번 똑같은 자리에 맵이 생기도록, 플레이어 기준이 아니라
    // 고정된 월드 좌표(원점에서 +Z 방향)를 기준으로 배치한다.
    private static readonly Vector3 StageOrigin = Vector3.zero;
    private static readonly Vector3 StageDirection = Vector3.forward;
    private const float MobZoneDistance = 12f;
    private const float LakeDistance = 40f;

    [MenuItem("Wongwirok/Spawn Stage1 Mob Set (물손+익사궁인+도깨비불)")]
    public static void SpawnStage1MobSet()
    {
        Vector3 center = StageOrigin + StageDirection * MobZoneDistance;
        GameObject[] mobs = SpawnMobsAround(center);

        Selection.objects = mobs;
        Debug.Log("Wongwirok: 1스테이지 잡몹 3종(물손, 익사 궁인, 연못 도깨비불)을 랜덤 위치에 생성 완료");
    }

    [MenuItem("Wongwirok/Spawn Stage 1 (입구 잡몹 + 호수 보스)")]
    public static void SpawnStage1Level()
    {
        Vector3 mobZoneCenter = StageOrigin + StageDirection * MobZoneDistance;
        SpawnMobsAround(mobZoneCenter);

        Vector3 lakeCenter = StageOrigin + StageDirection * LakeDistance;
        CreateBossArena(lakeCenter);
        GameObject boss = CreatePondGhostBoss(lakeCenter);

        Selection.activeGameObject = boss;
        Debug.Log($"Wongwirok: 1스테이지 구성 완료 - 입구(Z+{MobZoneDistance})에 잡몹 3종, 호수(Z+{LakeDistance})에 보스. 플레이어 위치/방향과 무관하게 항상 같은 자리에 생성됨");
    }

    private static GameObject[] SpawnMobsAround(Vector3 center)
    {
        GameObject waterHand = CreateWaterHand("Mob_WaterHand", RandomPointAround(center, 3f, 7f));
        GameObject drownedLady = CreateDrownedCourtLady("Mob_DrownedCourtLady", RandomPointAround(center, 3f, 7f));
        GameObject willOWisp = CreateWillOWisp("Mob_WillOWisp", RandomPointAround(center, 3f, 7f));

        return new[] { waterHand, drownedLady, willOWisp };
    }

    private static Vector3 RandomPointAround(Vector3 center, float minDistance, float maxDistance)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minDistance, maxDistance);
        return center + new Vector3(Mathf.Sin(angle) * distance, 0f, Mathf.Cos(angle) * distance);
    }

    private static GameObject CreateWaterHand(string name, Vector3 position)
    {
        position.y = 1f;
        GameObject enemy = GetOrCreateCapsule(name, position);
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

    private static GameObject CreateDrownedCourtLady(string name, Vector3 position)
    {
        position.y = 1f;
        GameObject enemy = GetOrCreateCapsule(name, position);
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

    private static GameObject CreateWillOWisp(string name, Vector3 position)
    {
        position.y = 1.5f;
        GameObject enemy = GetOrCreateCapsule(name, position);
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
        Vector3 arenaCenter = StageOrigin + StageDirection * LakeDistance;

        CreateBossArena(arenaCenter);
        GameObject boss = CreatePondGhostBoss(arenaCenter);

        Selection.activeGameObject = boss;
        Debug.Log("Wongwirok: 보스 아레나(물 바닥) + 연못 귀신 생성 완료");
    }

    private const float LakeRadius = 30f;
    private const float ShoreRadius = LakeRadius + 20f;

    private static void CreateBossArena(Vector3 center)
    {
        GameObject arenaRoot = GameObject.Find("BossArena");
        if (arenaRoot == null)
            arenaRoot = new GameObject("BossArena");
        arenaRoot.transform.position = center;

        for (int i = 1; i <= 6; i++)
        {
            Transform oldPlatform = arenaRoot.transform.Find($"Platform_{i}");
            if (oldPlatform != null)
                Object.DestroyImmediate(oldPlatform.gameObject);
        }

        // Plane은 사각형이라 둥근 호수를 만들 수 없어서, 납작하게 누른 Cylinder(원형 단면)로 교체.
        // 기존 오브젝트가 이전 버전(Plane 등)일 수도 있어 매번 지우고 새로 만든다.
        Transform oldShore = arenaRoot.transform.Find("Shore");
        if (oldShore != null)
            Object.DestroyImmediate(oldShore.gameObject);
        Transform oldGround = arenaRoot.transform.Find("LakeGround");
        if (oldGround != null)
            Object.DestroyImmediate(oldGround.gameObject);
        Transform oldWater = arenaRoot.transform.Find("Water");
        if (oldWater != null)
            Object.DestroyImmediate(oldWater.gameObject);

        // 나무가 서 있는 곳까지 "땅"으로 보이도록, 호수보다 넓은 원형 땅을 호수 밑에 깔아둔다
        // (호수 바닥보다 살짝 낮게 둬서 호수 안쪽에서는 가려지고, 바깥쪽만 땅으로 드러남).
        float shoreHalfThickness = 0.1f;
        GameObject shore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shore.name = "Shore";
        shore.transform.SetParent(arenaRoot.transform, false);
        Object.DestroyImmediate(shore.GetComponent<Collider>());
        shore.transform.localPosition = new Vector3(0f, -0.02f - shoreHalfThickness, 0f);
        shore.transform.localScale = new Vector3(ShoreRadius * 2f, shoreHalfThickness, ShoreRadius * 2f);
        shore.AddComponent<MeshCollider>();
        ApplyGroundLook(shore, "Assets/NatureStarterKit2/Textures/ground02.tga", new Color(0.32f, 0.26f, 0.17f), 16f);

        float groundHalfThickness = 0.1f;
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "LakeGround";
        ground.transform.SetParent(arenaRoot.transform, false);
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.transform.localPosition = new Vector3(0f, -groundHalfThickness, 0f);
        ground.transform.localScale = new Vector3(LakeRadius * 2f, groundHalfThickness, LakeRadius * 2f);
        ground.AddComponent<MeshCollider>();
        ApplyGroundLook(ground, "Assets/NatureStarterKit2/Textures/ground03.tga", new Color(0.35f, 0.3f, 0.25f), 10f);

        CreateStylizedWaterVolume(arenaRoot.transform);
    }

    // Bitgem StylisedWater의 WaterVolumeBox는 네모난 타일 블록 방식이라 원형을 못 그려서,
    // LakeGround와 똑같은 둥근 실린더에 Bitgem 물 머티리얼만 입히는 방식으로 변경.
    // (파도 가장자리 거품 효과는 WaterVolumeBox 전용 버텍스 컬러가 있어야 해서 빠짐)
    private static void CreateStylizedWaterVolume(Transform parent)
    {
        Transform existing = parent.Find("Water");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject waterObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterObject.name = "Water";
        waterObject.transform.SetParent(parent, false);
        Object.DestroyImmediate(waterObject.GetComponent<Collider>());

        float waterHalfThickness = 0.05f;
        waterObject.transform.localPosition = new Vector3(0f, 0.18f - waterHalfThickness, 0f);
        waterObject.transform.localScale = new Vector3(LakeRadius * 2f, waterHalfThickness, LakeRadius * 2f);

        Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bitgem/StylisedWater/URP/Materials/example-water-01.mat");
        if (sourceMaterial == null)
        {
            Debug.LogWarning("Wongwirok: Bitgem 물 머티리얼(example-water-01.mat)을 찾을 수 없음 - URP Stylized Water Shader가 Import됐는지 확인");
            return;
        }

        Material waterMaterial = CreateMurkyWaterMaterial(sourceMaterial);
        waterObject.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
    }

    // 실제 쉐이더(Shader Graphs/WaterVolume-URP)의 진짜 프로퍼티 이름은
    // 인스펙터 기준 _ShallowColor / _DeepColor / _Glossiness / _Metallic.
    // (.mat 파일 안에 남아있던 Color_7D9A58EC 같은 이름은 예전/미사용 값이었음)
    private static Material CreateMurkyWaterMaterial(Material sourceMaterial)
    {
        if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "GeneratedMaterials");

        string path = $"{GeneratedMaterialsFolder}/MurkyWater.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(sourceMaterial);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = sourceMaterial.shader;
            material.CopyPropertiesFromMaterial(sourceMaterial);
        }

        material.SetColor("_ShallowColor", new Color(0.1f, 0.1f, 0.07f, 1f));
        material.SetColor("_DeepColor", new Color(0.03f, 0.03f, 0.02f, 1f));
        material.SetFloat("_Glossiness", 0.85f);
        material.SetFloat("_Metallic", 0.05f);
        EditorUtility.SetDirty(material);

        return material;
    }

    private const string GeneratedMaterialsFolder = "Assets/Scripts/GeneratedMaterials";

    // NatureStarterKit2 텍스처가 있으면 그걸로 URP 머티리얼을 만들어 입히고,
    // 에셋이 없는 환경에서도 깨지지 않도록 없으면 단색으로 대체한다.
    private static void ApplyGroundLook(GameObject target, string texturePath, Color fallbackColor, float tiling)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            SetRendererColor(target, fallbackColor);
            return;
        }

        string materialName = System.IO.Path.GetFileNameWithoutExtension(texturePath) + "_GroundMat";
        string materialPath = $"{GeneratedMaterialsFolder}/{materialName}.mat";

        if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "GeneratedMaterials");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(urpLit);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.mainTexture = texture;
        material.mainTextureScale = new Vector2(tiling, tiling);
        EditorUtility.SetDirty(material);

        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
            targetRenderer.sharedMaterial = material;
    }

    [MenuItem("Wongwirok/Apply Sunset Skybox")]
    public static void ApplySunsetSkybox()
    {
        Material skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fantasy Skybox FREE/Panoramics/FS002/FS002_Sunset.mat");
        if (skyboxMaterial == null)
        {
            Debug.LogWarning("Wongwirok: FS002_Sunset 스카이박스 머티리얼을 찾을 수 없음 (Fantasy Skybox FREE가 Import됐는지 확인)");
            return;
        }

        // 참고 사진(짙은 주황-빨강, 어둡고 탁한 안개)에 맞춰 톤을 잡되,
        // 지난번 틴트(1.0)가 너무 세서 빨간 채널이 거의 2배로 뜨고 블룸까지 겹쳐
        // 화면 전체가 허옇게 날아갔었음 -> 틴트와 노출을 크게 낮춤.
        // 스카이박스 쉐이더의 _Tint는 0.5가 중립값.
        skyboxMaterial.SetColor("_Tint", new Color(0.62f, 0.4f, 0.32f, 0.5f));
        skyboxMaterial.SetFloat("_Exposure", 0.6f);
        EditorUtility.SetDirty(skyboxMaterial);

        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();

        // Height Fog 에셋은 씬의 모든 오브젝트 머티리얼을 전용 쉐이더로 바꿔야 해서,
        // 대신 Unity 내장 안개를 바로 켠다 (설정 하나로 비슷한 분위기를 낼 수 있음)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.45f, 0.16f, 0.07f);
        RenderSettings.fogDensity = 0.022f;

        // 메인(디렉셔널) 라이트도 같은 톤으로 맞춰서 씬 전체 분위기를 사진에 가깝게
        Light sunLight = RenderSettings.sun;
        if (sunLight == null)
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sunLight = light;
                    break;
                }
            }
        }
        if (sunLight != null)
        {
            sunLight.color = new Color(1f, 0.55f, 0.35f);
            sunLight.intensity = 0.5f;
        }

        Debug.Log("Wongwirok: 노을 스카이박스(빨간 틴트) + 안개 + 라이트 톤 적용 완료");
    }

    private static GameObject CreatePondGhostBoss(Vector3 position)
    {
        position.y = 1.3f;
        GameObject boss = GetOrCreateCapsule("Boss_PondGhost", position);
        boss.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        SetRendererColor(boss, new Color(0.3f, 0.45f, 0.55f));

        EnemyHealth health = EnsureComponent<EnemyHealth>(boss);
        SetField(health, "maxHealth", 150f);
        SetupEnemyHealthBar(boss, health);

        EnsureComponent<EnemyHitReaction>(boss);
        EnsureComponent<PondGhostBoss>(boss);

        return boss;
    }

    [MenuItem("Wongwirok/Spawn Stage 1 Lake Map (호수 전체 맵)")]
    public static void SpawnStage1LakeMap()
    {
        // 플레이어(원점)가 호수 테두리 바깥에서 출발해 걸어 들어오도록, 호수 반경(LakeRadius)보다
        // 조금 더 먼 거리에 중심을 둔다.
        Vector3 lakeCenter = StageOrigin + StageDirection * (LakeRadius + 15f);

        ApplySunsetSkybox();
        CreateBossArena(lakeCenter);
        CreateTreesAround(lakeCenter, 24, LakeRadius + 3f, LakeRadius + 14f);

        EnemyHealth[] mobHealths = CreateMobsAroundLakeEdge(lakeCenter, LakeRadius - 12f);

        // 처음엔 숨어있다가 잡몹을 다 잡으면 떠오르도록, Stage1Director가 활성화 전까지
        // PondGhostBoss 스크립트와 콜라이더를 꺼둔다.
        GameObject boss = CreatePondGhostBoss(lakeCenter);
        SetupStage1Director(mobHealths, boss);

        Selection.activeGameObject = boss;
        Debug.Log("Wongwirok: 호수 전체 맵 생성 완료 - 잡몹을 모두 처치하면 보스가 호수 중앙에서 떠오름");
    }

    private static GameObject[] CreateTreesAround(Vector3 center, int count, float minRadius, float maxRadius)
    {
        GameObject[] trees = new GameObject[count];
        float angleStepDeg = 360f / count;

        for (int i = 0; i < count; i++)
        {
            string treeName = $"TreeDummy_{i + 1}";

            float angleDeg = i * angleStepDeg + Random.Range(-angleStepDeg * 0.35f, angleStepDeg * 0.35f);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 position = center + new Vector3(Mathf.Sin(angleRad) * radius, 0f, Mathf.Cos(angleRad) * radius);

            GameObject tree = GameObject.Find(treeName);
            if (tree == null)
            {
                tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tree.name = treeName;
                Object.DestroyImmediate(tree.GetComponent<Collider>());
            }

            float height = Random.Range(3f, 6f);
            tree.transform.position = new Vector3(position.x, height * 0.5f, position.z);
            tree.transform.localScale = new Vector3(0.3f, height * 0.5f, 0.3f);
            SetRendererColor(tree, new Color(0.15f, 0.12f, 0.1f));

            trees[i] = tree;
        }

        return trees;
    }

    private static EnemyHealth[] CreateMobsAroundLakeEdge(Vector3 center, float radius)
    {
        GameObject[] mobs = new GameObject[6];
        mobs[0] = CreateWaterHand("Mob_WaterHand_1", RandomPointAround(center, radius, radius + 4f));
        mobs[1] = CreateWaterHand("Mob_WaterHand_2", RandomPointAround(center, radius, radius + 4f));
        mobs[2] = CreateDrownedCourtLady("Mob_DrownedCourtLady_1", RandomPointAround(center, radius, radius + 4f));
        mobs[3] = CreateDrownedCourtLady("Mob_DrownedCourtLady_2", RandomPointAround(center, radius, radius + 4f));
        mobs[4] = CreateWillOWisp("Mob_WillOWisp_1", RandomPointAround(center, radius, radius + 4f));
        mobs[5] = CreateWillOWisp("Mob_WillOWisp_2", RandomPointAround(center, radius, radius + 4f));

        EnemyHealth[] healths = new EnemyHealth[mobs.Length];
        for (int i = 0; i < mobs.Length; i++)
            healths[i] = mobs[i].GetComponent<EnemyHealth>();

        return healths;
    }

    private static void SetupStage1Director(EnemyHealth[] mobHealths, GameObject boss)
    {
        GameObject directorObject = GameObject.Find("Stage1Director");
        if (directorObject == null)
            directorObject = new GameObject("Stage1Director");

        Stage1Director director = EnsureComponent<Stage1Director>(directorObject);

        SerializedObject directorSerialized = new SerializedObject(director);
        SerializedProperty mobsProp = directorSerialized.FindProperty("mobHealths");
        mobsProp.arraySize = mobHealths.Length;
        for (int i = 0; i < mobHealths.Length; i++)
            mobsProp.GetArrayElementAtIndex(i).objectReferenceValue = mobHealths[i];

        directorSerialized.FindProperty("boss").objectReferenceValue = boss;
        directorSerialized.ApplyModifiedProperties();
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

    private const string RuntimeMaterialName = "WongwirokRuntimeMaterial";

    // renderer.material은 에디터 모드에서 호출할 때마다 머티리얼을 새로 복제해서
    // 씬에 계속 쌓이는(leak) 경고를 일으킨다. sharedMaterial로 직접 관리해서
    // 오브젝트당 하나의 인스턴스만 만들고 재사용한다.
    private static void SetRendererColor(GameObject obj, Color color)
    {
        Renderer targetRenderer = obj.GetComponent<Renderer>();
        if (targetRenderer == null)
            return;

        Material material = targetRenderer.sharedMaterial;
        if (material == null || material.name != RuntimeMaterialName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = RuntimeMaterialName };
            targetRenderer.sharedMaterial = material;
        }

        material.color = color;
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

        SetRendererColor(enemy, Color.gray);

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

            SetRendererColor(branchObject, new Color(0.45f, 0.3f, 0.15f));

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
