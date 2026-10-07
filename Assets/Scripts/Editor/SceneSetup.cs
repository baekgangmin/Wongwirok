using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        position.y = GetGroundHeight(position) + 1f;
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
        position.y = GetGroundHeight(position) + 1f;
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
        position.y = GetGroundHeight(position) + 1.5f;
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
    private const float TerrainOuterRadius = ShoreRadius + 20f;
    // 3f/0.8f로 했더니 호수가 깊은 구덩이처럼 보여서, 물 위에서 전투하는 것처럼 보이도록
    // 사용자가 그려준 그림(땅은 거의 평평하고 물만 살짝 낮은 웅덩이)에 맞춰 다시 낮춤.
    private const float LakeFloorDrop = 0.5f;
    private const float ShoreRise = 0.25f;
    private const float WaterAnkleDepth = 0.18f;
    private const float TerrainHeightRange = 8f;
    private const float TerrainBaseNormalized = 0.5f;

    // 터레인이 없는(기존 평지 메뉴) 환경에서는 0을 반환해서 그대로 평평한 바닥을 쓰고,
    // 호수 맵에서는 실제 조각된 터레인 높이를 읽어와서 그 위에 정확히 배치한다.
    private static float GetGroundHeight(Vector3 worldPosition)
    {
        Terrain terrain = Terrain.activeTerrain;
        // Terrain.SampleHeight()는 터레인 오브젝트의 Y 위치를 더하지 않고 반환하는 함정이 있어서
        // 월드 좌표로 쓰려면 transform.position.y를 직접 더해줘야 한다.
        return terrain != null ? terrain.SampleHeight(worldPosition) + terrain.transform.position.y : 0f;
    }

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

        // 이전 버전(Cylinder 단차 지형)의 잔재가 남아있을 수 있어 정리
        Transform oldShore = arenaRoot.transform.Find("Shore");
        if (oldShore != null)
            Object.DestroyImmediate(oldShore.gameObject);
        Transform oldGround = arenaRoot.transform.Find("LakeGround");
        if (oldGround != null)
            Object.DestroyImmediate(oldGround.gameObject);
        Transform oldWater = arenaRoot.transform.Find("Water");
        if (oldWater != null)
            Object.DestroyImmediate(oldWater.gameObject);

        CreateLakeTerrain(center);
        CreateStylizedWaterVolume(arenaRoot.transform, center);
    }

    // Unity Terrain으로 실제 높낮이가 있는 지형을 깎는다: 호수 안쪽은 완만하게 낮아지고,
    // 호수 테두리 밖은 완만하게 높아졌다가 다시 평지로 이어진다 (경계 단차 없이 부드러운 경사).
    private static void CreateLakeTerrain(Vector3 center)
    {
        GameObject existingTerrain = GameObject.Find("LakeTerrain");
        if (existingTerrain != null)
            Object.DestroyImmediate(existingTerrain);

        const int heightmapResolution = 129;
        float terrainSize = TerrainOuterRadius * 2f;

        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = heightmapResolution;
        terrainData.size = new Vector3(terrainSize, TerrainHeightRange, terrainSize);

        float lakeDropNorm = LakeFloorDrop / TerrainHeightRange;
        float shoreRiseNorm = ShoreRise / TerrainHeightRange;

        float[,] heights = new float[heightmapResolution, heightmapResolution];
        for (int zi = 0; zi < heightmapResolution; zi++)
        {
            for (int xi = 0; xi < heightmapResolution; xi++)
            {
                float worldX = (xi / (float)(heightmapResolution - 1)) * terrainSize - terrainSize * 0.5f;
                float worldZ = (zi / (float)(heightmapResolution - 1)) * terrainSize - terrainSize * 0.5f;
                float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);

                float h;
                if (dist <= LakeRadius)
                {
                    h = TerrainBaseNormalized - lakeDropNorm;
                }
                else if (dist <= ShoreRadius)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (dist - LakeRadius) / (ShoreRadius - LakeRadius));
                    h = Mathf.Lerp(TerrainBaseNormalized - lakeDropNorm, TerrainBaseNormalized + shoreRiseNorm, t);
                }
                else if (dist <= TerrainOuterRadius)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (dist - ShoreRadius) / (TerrainOuterRadius - ShoreRadius));
                    h = Mathf.Lerp(TerrainBaseNormalized + shoreRiseNorm, TerrainBaseNormalized, t);
                }
                else
                {
                    h = TerrainBaseNormalized;
                }

                heights[zi, xi] = h;
            }
        }
        terrainData.SetHeights(0, 0, heights);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = "LakeTerrain";
        terrainObject.transform.position = new Vector3(
            center.x - terrainSize * 0.5f,
            -TerrainBaseNormalized * TerrainHeightRange,
            center.z - terrainSize * 0.5f);

        ApplyTerrainTextures(terrainData, terrainSize);
    }

    // 호수 바닥(ground03)과 그 바깥 땅(ground02)을 거리 기반으로 부드럽게 블렌딩해서 칠한다.
    private static void ApplyTerrainTextures(TerrainData terrainData, float terrainSize)
    {
        TerrainLayer lakebedLayer = CreateTerrainLayer("LakebedLayer", "Assets/NatureStarterKit2/Textures/ground03.tga", 8f);
        TerrainLayer shoreLayer = CreateTerrainLayer("ShoreLayer", "Assets/NatureStarterKit2/Textures/ground02.tga", 12f);

        if (lakebedLayer == null || shoreLayer == null)
        {
            Debug.LogWarning("Wongwirok: 터레인 텍스처 레이어를 만들지 못함 (NatureStarterKit2 텍스처 확인 필요)");
            return;
        }

        terrainData.terrainLayers = new[] { lakebedLayer, shoreLayer };

        int alphaRes = terrainData.alphamapResolution;
        float[,,] alphamap = new float[alphaRes, alphaRes, 2];

        const float blendWidth = 6f;
        for (int zi = 0; zi < alphaRes; zi++)
        {
            for (int xi = 0; xi < alphaRes; xi++)
            {
                float worldX = (xi / (float)(alphaRes - 1)) * terrainSize - terrainSize * 0.5f;
                float worldZ = (zi / (float)(alphaRes - 1)) * terrainSize - terrainSize * 0.5f;
                float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);

                float shoreWeight = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, (dist - LakeRadius) / blendWidth));

                alphamap[zi, xi, 0] = 1f - shoreWeight;
                alphamap[zi, xi, 1] = shoreWeight;
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamap);
    }

    private static TerrainLayer CreateTerrainLayer(string name, string texturePath, float tileSize)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            Debug.LogWarning($"Wongwirok: 텍스처를 찾을 수 없음 - {texturePath}");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "GeneratedMaterials");

        string path = $"{GeneratedMaterialsFolder}/{name}.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(tileSize, tileSize);
        EditorUtility.SetDirty(layer);

        return layer;
    }

    // Bitgem과 NVJOB 둘 다 포기 - Bitgem은 전용 메쉬가 필요한 스타일라이즈드 셰이더라 색을 바꿔도
    // 만화풍 파도가 "자연스럽지" 않았고, NVJOB은 Built-in RP 전용이라 URP에서 핑크로 깨졌다.
    // Simple Water Shader URP(Houidisoft technology)는 일반 UV 기반 + 카메라 깊이 텍스처로 얕은/
    // 깊은 색을 블렌딩하는 URP 전용 셰이더라 평범한 Cylinder에도 바로 써서 원형 호수를 유지한다.
    private static void CreateStylizedWaterVolume(Transform parent, Vector3 center)
    {
        Transform existing = parent.Find("Water");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        GameObject waterObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterObject.name = "Water";
        waterObject.transform.SetParent(parent, false);
        Object.DestroyImmediate(waterObject.GetComponent<Collider>());

        float lakeFloorHeight = GetGroundHeight(center);
        const float waterHalfThickness = 0.05f;
        float waterTopWorldY = lakeFloorHeight + WaterAnkleDepth;
        waterObject.transform.position = new Vector3(center.x, waterTopWorldY - waterHalfThickness, center.z);
        waterObject.transform.localScale = new Vector3(LakeRadius * 2f, waterHalfThickness, LakeRadius * 2f);

        Material waterMaterial = GetOrCreateLakeWaterMaterial();
        MeshRenderer meshRenderer = waterObject.GetComponent<MeshRenderer>();
        if (waterMaterial != null)
            meshRenderer.sharedMaterial = waterMaterial;
    }

    // 에셋 원본 샘플 머티리얼(water material sample.mat)은 예전 Pro 셰이더용 값이 섞여 있어 신뢰할 수
    // 없어서, 실제 셰이더 소스(SimpleWaterURP.shader)에 선언된 속성만 가지고 직접 머티리얼을 만든다.
    private static Material GetOrCreateLakeWaterMaterial()
    {
        string generatedPath = $"{GeneratedMaterialsFolder}/LakeWater.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(generatedPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Custom/SimpleWaterURP");
        if (shader == null)
        {
            Debug.LogWarning("Wongwirok: Custom/SimpleWaterURP 셰이더를 찾을 수 없음 - Simple Water Shader URP가 Import됐는지 확인");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "GeneratedMaterials");

        material = new Material(shader) { name = "LakeWater" };

        // 고요하고 음산한 느낌이 되도록 파도는 약하게, 반사는 그레이징 각도가 아니면 거의 안 보이게.
        material.SetFloat("_WaveSpeed", 0.35f);
        material.SetFloat("_WaveStrength", 0.06f);
        material.SetFloat("_WaveScale", 1.5f);

        // 거의 검은색에 가까운 탁하고 썩은 듯한 녹회색 - 귀신 연못 분위기.
        material.SetColor("_ShallowColor", new Color(0.1f, 0.12f, 0.09f, 0.6f));
        material.SetColor("_DeepColor", new Color(0.01f, 0.02f, 0.015f, 0.97f));
        material.SetFloat("_WaterDepth", LakeFloorDrop + WaterAnkleDepth);

        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Houidisoft technology/Simple water/Resources/Normals 1.png");
        if (normalMap != null)
            material.SetTexture("_NormalMap", normalMap);
        material.SetFloat("_NormalTiling", 4f);
        material.SetFloat("_NormalStrength", 0.3f);
        material.SetFloat("_NormalSpeed", 0.05f);

        // 하늘이 밝을 때 그레이징 각도에서 물이 허옇게 반사되는 걸 줄이기 위해 더 낮춤.
        material.SetFloat("_FresnelPower", 7f);
        material.SetFloat("_ReflectionStrength", 0.15f);

        material.SetColor("_FoamColor", new Color(0.75f, 0.75f, 0.68f, 1f));
        material.SetFloat("_FoamDistance", 0.25f);
        material.SetFloat("_FoamTiling", 2f);
        material.SetFloat("_FoamSpeed", 0.08f);

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        AssetDatabase.CreateAsset(material, generatedPath);
        return material;
    }

    private const string GeneratedMaterialsFolder = "Assets/Scripts/GeneratedMaterials";

    [MenuItem("Wongwirok/Apply Sunset Skybox")]
    public static void ApplySunsetSkybox()
    {
        // 일몰(Sunset) 텍스처는 밝은 해/구름이 그대로 박혀있어서 아무리 노출을 낮춰도 "해가 있는
        // 노을"처럼 보임. 사용자가 원하는 건 해가 거의 없는 깜깜한 하늘에 붉은 기운만 도는 느낌이라
        // 달 없는 밤(Night_Moonless) 텍스처를 베이스로 쓰고 빨간 틴트를 강하게 준다.
        Material skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fantasy Skybox FREE/Panoramics/FS002/FS002_Night_Moonless.mat");
        if (skyboxMaterial == null)
        {
            Debug.LogWarning("Wongwirok: FS002_Night_Moonless 스카이박스 머티리얼을 찾을 수 없음 (Fantasy Skybox FREE가 Import됐는지 확인)");
            return;
        }

        // 스카이박스 쉐이더의 _Tint는 0.5가 중립값. R만 높이고 G/B는 낮춰서 "까만 하늘 + 붉은 기운"으로.
        skyboxMaterial.SetColor("_Tint", new Color(0.95f, 0.28f, 0.2f, 0.5f));
        skyboxMaterial.SetFloat("_Exposure", 0.65f);
        EditorUtility.SetDirty(skyboxMaterial);

        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();

        // Height Fog 에셋은 씬의 모든 오브젝트 머티리얼을 전용 쉐이더로 바꿔야 해서,
        // 대신 Unity 내장 안개를 바로 켠다 (설정 하나로 비슷한 분위기를 낼 수 있음)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        // 하늘을 어둡게 바꾼 것에 맞춰 안개도 주황이 아니라 짙은 핏빛 빨강으로.
        RenderSettings.fogColor = new Color(0.35f, 0.08f, 0.06f);
        // 0.022는 너무 옅고, 0.07은 근접 전투 카메라(10~20유닛) 거리에서 화면이 하얗게 덮여버림.
        // 가까운 전투 거리에서는 살짝만, 먼 거리(50유닛+)에서는 짙게 끼도록 절충한 값.
        RenderSettings.fogDensity = 0.028f;

        // 메인(디렉셔널) 라이트도 같은 톤으로 맞춰서 씬 전체 분위기를 사진에 가깝게 - 어둡고 붉게.
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
            sunLight.color = new Color(0.9f, 0.3f, 0.2f);
            sunLight.intensity = 0.4f;
        }

        Debug.Log("Wongwirok: 까만 밤하늘 + 붉은 틴트 + 짙은 안개 + 라이트 톤 적용 완료");
    }

    // Unity 내장 안개는 거리 기반이라 "하늘은 맑고 수면 위에만 낮게 깔리는" 느낌을 낼 수 없다.
    // AERO - Volumetric Fog and Mist(무료)의 높이 기반 안개 셰이더를 URP FullScreenPassRendererFeature로
    // 붙여서 실제 높이에 따라 옅어지는 안개를 만든다.
    private static void SetupHeightFog()
    {
        Material fogMaterial = GetOrCreateHeightFogMaterial();
        if (fogMaterial == null)
            return;

        EnsureHeightFogRendererFeature(fogMaterial);
    }

    private static Material GetOrCreateHeightFogMaterial()
    {
        string generatedPath = $"{GeneratedMaterialsFolder}/HeightFog.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(generatedPath);
        if (material != null)
            return material;

        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Mirza/AERO - Volumetric Fog and Mist/Materials/Volumetric Fog PC.mat");
        if (source == null)
        {
            Debug.LogWarning("Wongwirok: AERO 'Volumetric Fog PC.mat'를 찾을 수 없음 - AERO - Volumetric Fog and Mist가 Import됐는지 확인");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(GeneratedMaterialsFolder))
            AssetDatabase.CreateFolder("Assets/Scripts", "GeneratedMaterials");

        material = new Material(source) { name = "HeightFog" };

        // 핏빛 빨강 분위기에 맞춘 안개색. 호수 수면(대략 Y=-0.3) 바로 위로 낮게 깔리고
        // 그 위로는 빠르게 옅어지도록(Height_Falloff를 높게) 설정.
        material.SetColor("_Colour", new Color(0.5f, 0.06f, 0.05f, 1f));
        // _Height/_Height_Falloff는 실제로는 셰이더에 노출/사용되지 않는 죽은 프로퍼티였음
        // (Inspector에 안 뜸) - 진짜 높이 조절은 Height Mask 쪽 프로퍼티들.
        material.SetFloat("_Height_Mask_Offset", -0.8f);
        material.SetFloat("_Height_Mask_Falloff", 4f);
        material.SetFloat("_Height_Mask_Length", 1f);
        // 1.2는 화면 전체가 새까맣게 덮일 정도로 너무 진했음 - 지형을 따라 붉은 띠로만 보이도록 낮춤.
        material.SetFloat("_Density", 0.3f);
        material.SetFloat("_Max_Distance", 90f);
        // 기본 4는 레이마치 샘플이 부족해서 디더링 노이즈가 자글자글하게 보였음 - 16으로 올려서
        // 부드러운 구름 질감으로. (연산 비용은 약 4배 늘지만 PC 타겟이라 허용 범위)
        material.SetFloat("_Steps", 16f);

        AssetDatabase.CreateAsset(material, generatedPath);
        return material;
    }

    private static void EnsureHeightFogRendererFeature(Material fogMaterial)
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        if (rendererData == null)
        {
            Debug.LogWarning("Wongwirok: PC_Renderer.asset(UniversalRendererData)를 찾을 수 없음");
            return;
        }

        FullScreenPassRendererFeature feature = null;
        foreach (var existingFeature in rendererData.rendererFeatures)
        {
            if (existingFeature is FullScreenPassRendererFeature fullScreenFeature && fullScreenFeature.name == "Height Fog (AERO)")
            {
                feature = fullScreenFeature;
                break;
            }
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "Height Fog (AERO)";
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth;
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty featuresProp = serializedRenderer.FindProperty("m_RendererFeatures");
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            serializedRenderer.ApplyModifiedProperties();
        }

        feature.passMaterial = fogMaterial;
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
    }

    private static GameObject CreatePondGhostBoss(Vector3 position)
    {
        // 보스는 항상 터레인으로 깎인 호수 바닥 위에 선다
        position.y = GetGroundHeight(position) + 1.3f;
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
        SetupHeightFog();
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
            // 나무는 호수 테두리 바깥, 터레인으로 높아진 땅 위에 선다
            float groundHeight = GetGroundHeight(position);
            tree.transform.position = new Vector3(position.x, groundHeight + height * 0.5f, position.z);
            tree.transform.localScale = new Vector3(0.3f, height * 0.5f, 0.3f);
            SetRendererColor(tree, new Color(0.15f, 0.12f, 0.1f));

            trees[i] = tree;
        }

        return trees;
    }

    private static EnemyHealth[] CreateMobsAroundLakeEdge(Vector3 center, float radius)
    {
        // 잡몹은 터레인으로 낮아진 호수 안쪽 바닥 위에 선다 (높이는 각 Create 함수가 직접 샘플링)
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
