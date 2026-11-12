using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 인간 세계 씬(HumanWorld_01)을 만든다.
// - 연못가: 보스맵(귀의 세계)과 같은 지형·같은 좌표에 인간 세계 버전 물건을 둔다 (평행 세계)
// - 마을: 연못 남쪽으로 넓게 붙인다
// 다시 실행하면 이전에 배치한 것을 지우고 처음부터 다시 만든다.
public static class HumanWorldBuilder
{
    const string ScenePath = "Assets/Scenes/HumanWorld_01.unity";
    const string HW = "Assets/VarcoAssets/HumanWorld/";
    const string GenDir = "Assets/Scripts/GeneratedMaterials/";
    const float BaseY = -5f;
    const float TerrainHeight = 40f;

    static readonly List<Terrain> terrains = new List<Terrain>();
    static Transform root;
    static System.Random rng;

    [MenuItem("Tools/원귀록/인간 세계 다시 배치")]
    public static void Build()
    {
        rng = new System.Random(1234);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 1) 귀의 세계 전용 오브젝트 정리 + 이전 배치 삭제
        string[] removePrefixes = { "Mob_", "Boss_", "Stage1Director", "TreeDummy", "Structure_Lantern", "Prop_StumpCluster", "HW_" };
        foreach (var g in scene.GetRootGameObjects())
            if (removePrefixes.Any(p => g.name.StartsWith(p)))
                Object.DestroyImmediate(g);

        var lake = GameObject.Find("LakeTerrain").GetComponent<Terrain>();
        // 보스맵 지형 데이터를 건드리지 않도록 이 씬 전용 복사본으로 바꾼다
        string lakeTdPath = "Assets/Scenes/HumanWorld_01/HW_Terrain_Lake.asset";
        if (AssetDatabase.GetAssetPath(lake.terrainData) != lakeTdPath)
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes/HumanWorld_01");
            var copy = Object.Instantiate(lake.terrainData);
            AssetDatabase.DeleteAsset(lakeTdPath);
            AssetDatabase.CreateAsset(copy, lakeTdPath);
            lake.terrainData = copy;
            lake.GetComponent<TerrainCollider>().terrainData = copy;
        }
        terrains.Clear();
        terrains.Add(lake);

        root = new GameObject("HW_World").transform;

        // 2) 낮 분위기
        SetupDaylight();

        // 3) 지형: 연못 지형(복제본)의 겉흙을 풀밭으로, 주변에 마을/언덕 지형 추가
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/TerrainGrass.terrainlayer");
        var dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/TerrainDirt.terrainlayer");
        var lakebed = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GenDir + "LakebedLayer.terrainlayer");
        RepaintLakeTerrain(lake, grass, dirt, lakebed);

        var village = CreateTerrain("HW_Terrain_Village", new Vector3(-160, BaseY, -275), new Vector3(320, TerrainHeight, 240), 257, lake, grass, dirt, VillageCore);
        var west = CreateTerrain("HW_Terrain_West", new Vector3(-160, BaseY, -35), new Vector3(80, TerrainHeight, 160), 129, lake, grass, dirt, r => false);
        var east = CreateTerrain("HW_Terrain_East", new Vector3(80, BaseY, -35), new Vector3(80, TerrainHeight, 160), 129, lake, grass, dirt, r => false);
        var north = CreateTerrain("HW_Terrain_North", new Vector3(-160, BaseY, 125), new Vector3(320, TerrainHeight, 80), 129, lake, grass, dirt, r => false);
        terrains.AddRange(new[] { village, west, east, north });
        lake.SetNeighbors(west, north, east, village);
        village.SetNeighbors(null, lake, null, null);

        // 마을 개울 (돌다리 자리)
        CarveStream(village, StreamZ, 4f, 1.6f);
        PaintRoads(village, dirt);

        // 4) 연못가: 보스맵과 같은 자리에 인간 세계 짝
        PlaceLakeside();

        // 5) 마을
        PlaceVillage();

        // 6) 연못 물을 맑은 낮 물로
        SetupWater();

        // 7) 플레이어 시작 위치: 마을 광장 남쪽
        var player = GameObject.Find("Player");
        player.transform.position = new Vector3(0, Ground(0, -84) + 1.2f, -84);
        player.transform.rotation = Quaternion.identity;
        var cam = GameObject.Find("Main Camera");
        cam.transform.position = player.transform.position + new Vector3(0, 3, -6);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HumanWorldBuilder] 완료");
    }

    // ---------------- 분위기 ----------------
    static void SetupDaylight()
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Fantasy Skybox FREE/Cubemaps/Classic/FS000_Day_01.mat");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.86f);
        RenderSettings.fogDensity = 0.006f;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type != LightType.Directional) continue;
            l.color = new Color(1f, 0.95f, 0.86f);
            l.intensity = 1.6f;
            l.shadows = LightShadows.Soft;
            l.transform.rotation = Quaternion.Euler(48f, 330f, 0f);
        }
        var fogGo = new GameObject("HW_HeightFogOverride");
        fogGo.transform.SetParent(root);
        var fo = fogGo.AddComponent<SceneHeightFogOverride>();
        var so = new SerializedObject(fo);
        so.FindProperty("heightFogMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "HeightFog.mat");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupWater()
    {
        var water = GameObject.Find("BossArena/Water");
        if (water == null) return;
        string path = GenDir + "LakeWater_Day.mat";
        var src = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "LakeWater.mat");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(src);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.CopyPropertiesFromMaterial(src);
        mat.SetColor("_DeepColor", new Color(0.04f, 0.2f, 0.25f, 0.95f));
        mat.SetColor("_ShallowColor", new Color(0.2f, 0.45f, 0.5f, 0.9f));
        mat.SetFloat("_ReflectionStrength", 0.35f);
        mat.SetFloat("_WaterDepth", 1.6f);
        EditorUtility.SetDirty(mat);
        water.GetComponent<MeshRenderer>().sharedMaterial = mat;
        water.transform.parent.name = "Lake";
    }

    // ---------------- 지형 ----------------
    static void RepaintLakeTerrain(Terrain t, TerrainLayer grass, TerrainLayer dirt, TerrainLayer lakebed)
    {
        var td = t.terrainData;
        td.terrainLayers = new[] { grass, dirt, lakebed };
        int res = td.alphamapResolution;
        var a = new float[res, res, 3];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = t.transform.position.x + (x + 0.5f) / res * td.size.x;
                float wz = t.transform.position.z + (y + 0.5f) / res * td.size.z;
                float h = Ground(wx, wz);
                // 물가 아래는 진흙 바닥, 물가 근처는 흙, 위는 풀
                float wet = Mathf.InverseLerp(-1.35f, -1.7f, h);
                float shore = Mathf.InverseLerp(-1.22f, -1.34f, h) * (1 - wet);
                float road = RoadMask(wx, wz);
                shore = Mathf.Max(shore, road * (1 - wet));
                float g = Mathf.Clamp01(1 - wet - shore);
                a[y, x, 0] = g; a[y, x, 1] = shore; a[y, x, 2] = wet;
            }
        td.SetAlphamaps(0, 0, a);
    }

    // 마을 중심부(평지) 영역
    // 마을 크기 (가로 ±CoreX, 남쪽 끝 CoreSouth) / 광장 / 개울 위치
    const float CoreX = 78f, CoreSouth = -198f, PlazaZ = -105f, StreamZ = -135f;
    static bool VillageCore(Vector2 p) => p.x > -CoreX && p.x < CoreX && p.y > CoreSouth && p.y < -35;

    static Terrain CreateTerrain(string name, Vector3 pos, Vector3 size, int res, Terrain lake, TerrainLayer grass, TerrainLayer dirt, System.Func<Vector2, bool> isCore)
    {
        var td = new TerrainData { heightmapResolution = res, size = size };
        td.alphamapResolution = res - 1;
        string path = "Assets/Scenes/HumanWorld_01/" + name + ".asset";
        System.IO.Directory.CreateDirectory("Assets/Scenes/HumanWorld_01");
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(td, path);
        td.terrainLayers = new[] { grass, dirt };

        var heights = new float[res, res];
        var lakeTd = lake.terrainData;
        Vector3 lp = lake.transform.position;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = pos.x + (float)x / (res - 1) * size.x;
                float wz = pos.z + (float)y / (res - 1) * size.z;
                // 연못 지형 가장자리 높이를 그대로 이어 받아 이음매를 없앤다
                float cx = Mathf.Clamp(wx, lp.x, lp.x + lakeTd.size.x);
                float cz = Mathf.Clamp(wz, lp.z, lp.z + lakeTd.size.z);
                float baseH = lake.SampleHeight(new Vector3(cx, 0, cz)) + lp.y;
                // 연못 지형 밖으로 얼마나 벗어났는지 (가장자리에서 0)
                float dOut = Mathf.Max(Mathf.Abs(wx - cx), Mathf.Abs(wz - cz));
                float h = baseH;
                // 마을 평지 밖은 점점 언덕
                float dCore = DistanceOutsideCore(new Vector2(wx, wz), isCore);
                float hill = 22f * Smooth(0f, 60f, dCore) + 6f * (Mathf.PerlinNoise(wx * 0.02f + 31, wz * 0.02f + 7) - 0.3f) * Smooth(0f, 25f, dCore);
                // 마을 안쪽도 아주 완만한 기복 (이음매 근처는 0)
                float gentle = 0.8f * (Mathf.PerlinNoise(wx * 0.015f, wz * 0.015f) - 0.5f) * Smooth(4f, 30f, dOut);
                h += hill + gentle;
                heights[y, x] = Mathf.Clamp01((h - BaseY) / size.y);
            }
        td.SetHeights(0, 0, heights);

        int ar = td.alphamapResolution;
        var a = new float[ar, ar, 2];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = pos.x + (x + 0.5f) / ar * size.x;
                float wz = pos.z + (y + 0.5f) / ar * size.z;
                float r = RoadMask(wx, wz);
                a[y, x, 0] = 1 - r; a[y, x, 1] = r;
            }
        td.SetAlphamaps(0, 0, a);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = name;
        go.transform.position = pos;
        go.transform.SetParent(root);
        var t = go.GetComponent<Terrain>();
        t.materialTemplate = lake.materialTemplate;
        t.drawInstanced = true;
        t.groupingID = lake.groupingID;
        t.allowAutoConnect = true;
        return t;
    }

    static float DistanceOutsideCore(Vector2 p, System.Func<Vector2, bool> isCore)
    {
        // 마을 평지 사각형 바깥 거리 (평지가 없는 지형은 연못 지형 경계로부터의 거리)
        if (isCore == null) return 0;
        if (isCore(p)) return 0;
        // 사각형 (-125..125, -320..-35) 기준 거리, 연못 지형(-80..80, -35..125)은 0으로 취급
        float dx = Mathf.Max(-CoreX - p.x, 0, p.x - CoreX);
        float dz = Mathf.Max(CoreSouth - p.y, 0, p.y - (-35));
        float dVillage = Mathf.Sqrt(dx * dx + dz * dz);
        float lx = Mathf.Max(-80 - p.x, 0, p.x - 80);
        float lz = Mathf.Max(-35 - p.y, 0, p.y - 125);
        float dLake = Mathf.Sqrt(lx * lx + lz * lz);
        return Mathf.Min(dVillage, dLake);
    }

    static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));

    // 길: 남북 큰길(x≈0), 광장, 동서 골목 두 줄
    static float RoadMask(float x, float z)
    {
        float m = 0;
        // 큰길 (연못 → 남쪽 입구)
        if (z < 8 && z > CoreSouth + 6) m = Mathf.Max(m, 1 - Smooth(2.5f, 4.5f, Mathf.Abs(x + 1.5f * Mathf.Sin(z * 0.03f))));
        // 광장
        m = Mathf.Max(m, 1 - Smooth(12f, 15f, Vector2.Distance(new Vector2(x, z), new Vector2(0, PlazaZ))));
        // 광장 → 기와집 대문
        if (x > 8 && x < 37) m = Mathf.Max(m, 1 - Smooth(1.5f, 2.8f, Mathf.Abs(z - (PlazaZ + 1.5f))));
        // 동서 골목 두 줄
        foreach (float rz in new[] { -75f, -150f })
            if (Mathf.Abs(x) < 62) m = Mathf.Max(m, 1 - Smooth(1.6f, 3f, Mathf.Abs(z - rz - 2f * Mathf.Sin(x * 0.05f))));
        // 세로 골목 두 줄
        if (z > -128 && z < -75) foreach (float rx in new[] { -40f, 66f })
                m = Mathf.Max(m, 1 - Smooth(1.5f, 2.8f, Mathf.Abs(x - rx)));
        return Mathf.Clamp01(m);
    }

    static void PaintRoads(Terrain t, TerrainLayer dirt) { /* CreateTerrain 에서 이미 칠함 */ }

    static void CarveStream(Terrain t, float zCenter, float halfWidth, float depth)
    {
        var td = t.terrainData;
        int res = td.heightmapResolution;
        var h = td.GetHeights(0, 0, res, res);
        Vector3 p = t.transform.position;
        float refY = Ground(0, zCenter);
        float bottom = (refY - depth - BaseY) / td.size.y;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = p.x + (float)x / (res - 1) * td.size.x;
                float wz = p.z + (float)y / (res - 1) * td.size.z;
                float d = Mathf.Abs(wz - zCenter - 4f * Mathf.Sin(wx * 0.03f));
                float k = (1 - Smooth(halfWidth * 0.5f, halfWidth * 1.6f, d)) * (1 - Smooth(CoreX - 6f, CoreX + 6f, Mathf.Abs(wx)));
                h[y, x] = Mathf.Lerp(h[y, x], Mathf.Min(h[y, x], bottom), k);
            }
        td.SetHeights(0, 0, h);
        // 개울 물
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.name = "HW_StreamWater";
        water.transform.SetParent(root);
        water.transform.position = new Vector3(0, refY - depth * 0.45f, zCenter);
        water.transform.localScale = new Vector3(CoreX * 2f / 10f - 0.4f, 1, 1.6f);
        water.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "LakeWater_Day.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(GenDir + "LakeWater.mat");
    }

    // ---------------- 배치 도우미 ----------------
    public static float Ground(float x, float z)
    {
        foreach (var t in terrains)
        {
            var p = t.transform.position; var s = t.terrainData.size;
            if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                return t.SampleHeight(new Vector3(x, 0, z)) + p.y;
        }
        return 0;
    }

    static GameObject Place(string asset, string name, Vector3 pos, float yaw, float scale, Transform parent, bool collider, float sink = 0.05f)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(asset.Contains("/") ? asset : HW + asset + ".glb");
        if (src == null) { Debug.LogWarning("없음: " + asset); return null; }
        var holder = new GameObject(name);
        holder.transform.SetParent(parent);
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(src);
        vis.name = "Visual";
        vis.transform.SetParent(holder.transform, false);
        vis.transform.localScale = Vector3.one * scale;
        holder.transform.position = new Vector3(pos.x, Ground(pos.x, pos.z) - sink * scale, pos.z);
        holder.transform.rotation = Quaternion.Euler(0, yaw, 0);
        if (collider)
            foreach (var mf in vis.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return holder;
    }

    static void TreeCollider(GameObject tree, float radius, float height)
    {
        if (tree == null) return;
        var c = tree.AddComponent<CapsuleCollider>();
        c.radius = radius; c.height = height; c.center = new Vector3(0, height / 2, 0);
    }

    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // ---------------- 연못가 (보스맵과 같은 좌표) ----------------
    // 보스맵의 마른 나무 / 석등 / 그루터기 좌표 (BossMap_01 기준)
    static readonly Vector3[] BossTrees = {
        new Vector3(74.00f,0,18.48f), new Vector3(59.61f,0,25.47f), new Vector3(-62.45f,0,-1.10f), new Vector3(63.88f,0,56.78f), new Vector3(-67.95f,0,82.90f),
        new Vector3(-52.10f,0,-13.33f), new Vector3(-33.06f,0,103.56f), new Vector3(32.27f,0,-18.84f), new Vector3(53.62f,0,100.17f), new Vector3(-53.25f,0,26.09f),
        new Vector3(74.00f,0,34.48f), new Vector3(44.24f,0,119.00f), new Vector3(29.12f,0,107.14f), new Vector3(59.75f,0,115.04f), new Vector3(-30.38f,0,87.68f),
        new Vector3(-22.05f,0,117.67f), new Vector3(61.63f,0,-7.67f), new Vector3(-5.61f,0,119.00f), new Vector3(60.40f,0,77.67f), new Vector3(-51.87f,0,83.59f),
        new Vector3(53.28f,0,-21.32f), new Vector3(-47.50f,0,8.64f), new Vector3(46.25f,0,-3.27f), new Vector3(-10.75f,0,103.85f), new Vector3(-57.48f,0,-28.69f),
        new Vector3(77.66f,0,67.38f), new Vector3(50.91f,0,12.04f), new Vector3(-55.31f,0,44.69f), new Vector3(-70.47f,0,-15.00f), new Vector3(74.00f,0,2.48f),
        new Vector3(-34.97f,0,-1.48f), new Vector3(74.00f,0,86.10f), new Vector3(-29.40f,0,-18.52f), new Vector3(-38.00f,0,119.00f), new Vector3(-68.40f,0,32.33f),
        new Vector3(-64.73f,0,14.80f), new Vector3(6.95f,0,104.64f), new Vector3(59.26f,0,41.46f), new Vector3(-37.48f,0,-26.00f),
    };
    static readonly Vector3[] BossLanterns = {
        new Vector3(-45.28f,0,68.22f), new Vector3(71.38f,0,70.47f), new Vector3(-44.11f,0,-4.33f), new Vector3(22.68f,0,119.00f), new Vector3(-67.64f,0,49.79f), new Vector3(54.62f,0,61.55f),
    };
    static readonly Vector3[] BossStumps = { new Vector3(12.69f, 0, 66.99f), new Vector3(7.47f, 0, 24.47f), new Vector3(-22.49f, 0, 41.03f) };

    static void PlaceLakeside()
    {
        var lakeRoot = new GameObject("HW_Lakeside").transform;
        lakeRoot.SetParent(root);
        const string willow = "Assets/VarcoAssets/Tree/Green Weeping Willow.glb";
        for (int i = 0; i < BossTrees.Length; i++)
        {
            var p = BossTrees[i];
            // 물가 가까운 나무는 버드나무, 바깥쪽은 소나무/느티나무
            float distToLake = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, 45));
            string asset; float s;
            if (distToLake < 62) { asset = willow; s = R(10f, 13f); }
            else if (i % 3 != 0) { asset = "Zelkova"; s = R(11f, 14f); }
            else { asset = "Pine"; s = R(11f, 15f); }
            var tr = Place(asset, "Tree_" + i, p, R(0, 360), s, lakeRoot, false, 0.02f);
            TreeCollider(tr, 0.6f, 6f);
        }
        for (int i = 0; i < BossLanterns.Length; i++)
            Place("Cheongsachorong", "Chorong_" + i, BossLanterns[i], R(0, 360), 2.6f, lakeRoot, true, 0f);
        // 물에 잠긴 그루터기 자리 → 연꽃 무리
        foreach (var p in BossStumps)
            for (int k = 0; k < 4; k++)
            {
                var q = p + new Vector3(R(-3, 3), 0, R(-3, 3));
                var lo = Place("Lotus", "Lotus", q, R(0, 360), R(1.6f, 2.4f), lakeRoot, false, 0f);
                if (lo) lo.transform.position = new Vector3(q.x, -1.45f, q.z);
            }
        // 선착장: 연못 남쪽 물가 (보스맵 플레이어 시작 지점 앞), 물 쪽으로 뻗게
        float shoreZ = FindShore(0f);
        var pier = Place("Pier", "Pier", new Vector3(6f, 0, shoreZ + 2f), 90f, 8f, lakeRoot, true, 0f);
        if (pier) pier.transform.position = new Vector3(6f, -2.85f, shoreZ + 2.5f); // 갑판이 물가 땅보다 살짝 높게 (올라설 수 있게)

        // 선착장 끝: 귀의 세계로 들어가는 자리 (보스맵의 같은 좌표로 도착)
        if (pier)
        {
            var b = new Bounds(pier.transform.position, Vector3.zero);
            foreach (var r in pier.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var portal = new GameObject("HW_LakePortal");
            portal.transform.SetParent(lakeRoot);
            portal.transform.position = new Vector3(b.center.x, b.max.y, b.max.z - 1.2f);
            portal.AddComponent<LakePortal>();
            Debug.Log("[HumanWorldBuilder] 연못 입구 " + portal.transform.position);
        }
        Debug.Log("[HumanWorldBuilder] 남쪽 물가 z=" + shoreZ);
    }

    static float FindShore(float x)
    {
        for (float z = 45; z > -35; z -= 0.5f)
            if (Ground(x, z) > -1.3f) return z;
        return 10;
    }

    // ---------------- 마을 ----------------
    static void PlaceVillage()
    {
        var v = new GameObject("HW_Village").transform;
        v.SetParent(root);

        // 남쪽 입구 (z≈-185): 장승, 솟대, 서낭당
        Place("Assets/VarcoAssets/Structure/Korean Wooden Totem.glb", "Jangseung_L", new Vector3(-5, 0, -184), 180, 4.2f, v, true, 0f);
        Place("Assets/VarcoAssets/Structure/Korean Wooden Totem.glb", "Jangseung_R", new Vector3(5, 0, -184), 180, 4.2f, v, true, 0f);
        Place("Sotdae", "Sotdae_L", new Vector3(-10, 0, -189), 0, 7f, v, false, 0f);
        Place("Sotdae", "Sotdae_R", new Vector3(10, 0, -191), 20, 6.5f, v, false, 0f);
        Place("Seonangdang", "Seonangdang", new Vector3(-17, 0, -180), 30, 5f, v, true, 0f);

        // 광장: 느티나무 정자목 + 평상 + 우물
        var z = Place("Zelkova", "Plaza_Zelkova", new Vector3(-8, 0, PlazaZ - 3), 40, 15f, v, false, 0.02f);
        TreeCollider(z, 1.2f, 8f);
        Place("Pyeongsang", "Plaza_Pyeongsang", new Vector3(-4, 0, PlazaZ + 4), 15, 2.4f, v, true, 0f);
        Place("Well", "Plaza_Well", new Vector3(7, 0, PlazaZ - 4), 0, 2.4f, v, true, 0f);
        Place("Cheongsachorong", "Plaza_Chorong_1", new Vector3(11, 0, PlazaZ + 8), 200, 2.6f, v, true, 0f);
        Place("Cheongsachorong", "Plaza_Chorong_2", new Vector3(-11, 0, PlazaZ - 10), 20, 2.6f, v, true, 0f);

        // 주막: 연못으로 가는 큰길 북쪽 끝
        Place("Jumak", "Jumak", new Vector3(-20, 0, -52), 90, 10f, v, true);
        Place("Pyeongsang", "Jumak_Pyeongsang", new Vector3(-9, 0, -47), 0, 2.4f, v, true, 0f);
        Place("Jangdokdae", "Jumak_Jangdok", new Vector3(-30, 0, -44), 0, 2.2f, v, true, 0f);
        Place("Cheongsachorong", "Jumak_Chorong", new Vector3(-7, 0, -58), 0, 2.6f, v, true, 0f);
        Place("Cart", "Jumak_Cart", new Vector3(-9, 0, -63), 170, 2.6f, v, true, 0f);

        // 나뭇가지 상점: 광장 북동쪽, 큰길을 바라봄
        Place("BranchShop", "BranchShop", new Vector3(17, 0, -88), -90, 6f, v, true);
        Place("Jige", "BranchShop_Jige", new Vector3(12, 0, -93), 60, 1.6f, v, false, 0f);

        // 기와집 + 솟을대문 + 흙담 (광장 동쪽, 대문이 광장 쪽을 바라봄)
        Vector3 giwa = new Vector3(50, 0, PlazaZ);
        Place("GiwaHouse", "GiwaHouse", giwa + new Vector3(2, 0, 1.5f), -90, 11f, v, true);
        Place("SoseulGate", "SoseulGate", giwa + new Vector3(-13.5f, 0, 1.5f), -90, 6.5f, v, true);
        for (int i = -3; i <= 3; i++)
        {
            Place("EarthWall", "GiwaWall_N" + i, giwa + new Vector3(i * 3.6f, 0, 15), 0, 3.8f, v, true);
            Place("EarthWall", "GiwaWall_S" + i, giwa + new Vector3(i * 3.6f, 0, -12), 0, 3.8f, v, true);
        }
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            Place("EarthWall", "GiwaWall_W" + i, giwa + new Vector3(-12.6f, 0, 1.5f + i * 3.6f), 90, 3.8f, v, true);
            Place("EarthWall", "GiwaWall_E" + i, giwa + new Vector3(12.6f, 0, 1.5f + i * 3.6f), 90, 3.8f, v, true);
        }
        Place("Jangdokdae", "Giwa_Jangdok", giwa + new Vector3(8, 0, -8), 0, 2.2f, v, true, 0f);

        // 초가집들 (집 앞이 길을 바라보게)
        var houseSpots = new List<(Vector3 p, float yaw)>
        {
            // 큰길 양옆
            (new Vector3(-21, 0, -80), 90), (new Vector3(-21, 0, -122), 90), (new Vector3(-21, 0, -165), 90),
            (new Vector3(21, 0, -68), -90), (new Vector3(21, 0, -124), -90), (new Vector3(21, 0, -168), -90),
            // 북쪽 골목(z=-75) 위아래
            (new Vector3(-52, 0, -64), 180), (new Vector3(38, 0, -63), 180), (new Vector3(58, 0, -63), 180),
            (new Vector3(-55, 0, -87), 0),
            // 서쪽 세로 골목(x=-40) 양옆
            (new Vector3(-52, 0, -108), 90), (new Vector3(-28, 0, -100), -90),
            // 남쪽 골목(z=-150) 아래
            (new Vector3(-52, 0, -161), 0), (new Vector3(-35, 0, -162), 0), (new Vector3(36, 0, -161), 0), (new Vector3(54, 0, -162), 0),
            // 바깥 줄
            (new Vector3(-68, 0, -128), 90), (new Vector3(70, 0, -132), -90),
        };
        int n = 0;
        foreach (var (p, yaw) in houseSpots)
        {
            string asset = (n % 2 == 0) ? "ChogaHouseA" : "ChogaHouseB";
            var h = Place(asset, "Choga_" + n, p, yaw + R(-6, 6), R(8f, 9.5f), v, true);
            DecorateHouse(h.transform, v, n);
            n++;
        }

        // 큰길 청사초롱
        for (float lz = -178; lz < -40; lz += 26)
        {
            if (Mathf.Abs(lz - PlazaZ) < 16 || Mathf.Abs(lz - StreamZ) < 7) continue;
            Place("Cheongsachorong", "Road_Chorong_L", new Vector3(-5.5f, 0, lz), 90, 2.6f, v, true, 0f);
            Place("Cheongsachorong", "Road_Chorong_R", new Vector3(5.5f, 0, lz + 13), -90, 2.6f, v, true, 0f);
        }

        // 돌다리: 개울을 건너는 큰길
        var br = Place("StoneBridge", "StoneBridge", new Vector3(0, 0, StreamZ), 90, 11f, v, true, 0f);
        if (br) br.transform.position = new Vector3(0, Ground(0, StreamZ + 8) - 0.6f, StreamZ);

        // 남쪽 들판: 돌담과 짚더미 (입구 양옆)
        for (int i = 0; i < 10; i++)
        {
            float x = R(-72, 72), zz = R(-196, -172);
            if (Mathf.Abs(x) < 22 || RoadMask(x, zz) > 0.1f) continue;
            Place("StrawStack", "Field_Straw_" + i, new Vector3(x, 0, zz), R(0, 360), R(2.2f, 3f), v, true, 0f);
        }
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * R(28, 70), zz = R(-194, -174);
            Place("StoneWall", "FieldWall_" + i, new Vector3(x, 0, zz), R(-20, 20), 4.5f, v, true);
        }
        // 마을 안 나무 몇 그루 + 바깥 숲
        for (int i = 0; i < 90; i++)
        {
            float x = R(-158, 158), zz = R(-272, -40);
            bool outer = Mathf.Abs(x) > CoreX + 4 || zz < CoreSouth - 4;
            if (!outer && i % 5 != 0) continue;
            if (RoadMask(x, zz) > 0.05f) continue;
            if (!outer && NearHouse(houseSpots, x, zz, 13)) continue;
            if (!outer && Mathf.Abs(x - giwa.x) < 18 && Mathf.Abs(zz - giwa.z) < 18) continue;
            string kind = (i % 5 == 0) ? "Pine" : (i % 2 == 0 ? "Zelkova" : "Assets/VarcoAssets/Tree/Green Weeping Willow.glb");
            var t = Place(kind, "Tree", new Vector3(x, 0, zz), R(0, 360), R(10, 14), v, false, 0.02f);
            TreeCollider(t, 0.6f, 6f);
        }
        // 연못 지형 양옆/북쪽 언덕 소나무
        for (int i = 0; i < 50; i++)
        {
            float x = R(-158, 158), zz = R(-34, 200);
            if (Mathf.Abs(x) < 84 && zz < 128) continue;
            var t = Place(i % 3 == 0 ? "Pine" : "Zelkova", "HillTree", new Vector3(x, 0, zz), R(0, 360), R(11, 16), v, false, 0.02f);
            TreeCollider(t, 0.6f, 6f);
        }
    }

    static bool NearHouse(List<(Vector3 p, float yaw)> spots, float x, float z, float r)
    {
        foreach (var s in spots) if (Vector2.Distance(new Vector2(s.p.x, s.p.z), new Vector2(x, z)) < r) return true;
        return false;
    }

    static void DecorateHouse(Transform house, Transform parent, int n)
    {
        // 집 앞(앞쪽 = 집의 -forward 쪽이라고 가정) 마당 소품
        Vector3 fwd = house.forward, right = house.right, c = house.position;
        switch (n % 5)
        {
            case 0:
                Place("Jangdokdae", "Jangdok", c + right * 6 - fwd * 2, house.eulerAngles.y, 2f, parent, true, 0f);
                Place("SaripGate", "SaripGate", c - fwd * 8, house.eulerAngles.y, 2.2f, parent, true, 0f);
                break;
            case 1:
                Place("Clothesline", "Clothesline", c - right * 6 - fwd * 4, house.eulerAngles.y + 90, 4f, parent, false, 0f);
                Place("Jige", "Jige", c + right * 4.5f - fwd * 3, house.eulerAngles.y + 30, 1.6f, parent, false, 0f);
                break;
            case 2:
                Place("StrawStack", "Straw", c + right * 7, 0, 2.4f, parent, true, 0f);
                Place("Cart", "Cart", c - right * 5 - fwd * 5, house.eulerAngles.y + 70, 2.6f, parent, true, 0f);
                break;
            case 3:
                Place("Pyeongsang", "Pyeongsang", c - fwd * 5 + right * 3, house.eulerAngles.y, 2.2f, parent, true, 0f);
                Place("Jangdokdae", "Jangdok", c - right * 6, 0, 1.8f, parent, true, 0f);
                break;
            default:
                Place("SaripGate", "SaripGate", c - fwd * 8, house.eulerAngles.y, 2.2f, parent, true, 0f);
                Place("Clothesline", "Clothesline", c + right * 6 - fwd * 3, house.eulerAngles.y + 90, 4f, parent, false, 0f);
                break;
        }
    }
}
