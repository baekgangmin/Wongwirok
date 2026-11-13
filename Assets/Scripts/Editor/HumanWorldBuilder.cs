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

    [MenuItem("Tools/원귀록/진행 기록 초기화 (보스 깬 기록 지우기)")]
    public static void ResetProgress()
    {
        GameProgress.ResetAll();
        Debug.Log("[원귀록] 진행 기록을 지웠습니다. 다시 연못에 들어갈 수 있습니다.");
    }

    public static Material RopeMaterial()
    {
        string path = "Assets/VarcoAssets/HumanWorld/Rope.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.22f));
            m.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    // 인간 세계 연못만 바닥을 깊게 판다 (물가선은 그대로라 보스맵과 모양은 같음)
    static void DeepenLake(Terrain t, float waterY, float extraDepth)
    {
        var td = t.terrainData;
        int res = td.heightmapResolution;
        var h = td.GetHeights(0, 0, res, res);
        float baseY = t.transform.position.y, sy = td.size.y;
        float minY = float.MaxValue;
        for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) minY = Mathf.Min(minY, baseY + h[y, x] * sy);
        if (minY < waterY - 1f) return; // 이미 팠음
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wy = baseY + h[y, x] * sy;
                float sub = waterY - wy;
                if (sub <= 0) continue;
                float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0f, 0.16f, sub));
                h[y, x] = Mathf.Max(0f, (wy - k * extraDepth - baseY) / sy);
            }
        td.SetHeights(0, 0, h);
    }

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
        // 실사 바닥 (Poly Haven CC0): 풀 / 마당 모래 / 박석 / 흙 / 성긴 풀
        var grass = PHLayer("grass_ground", 6f, new Color(0.78f, 0.95f, 0.62f, 1f)); // 누런 풀 → 조금 푸르게
        var dirt = PHLayer("dirt", 4f);
        var groundLayers = new[] { grass, PHLayer("gravelly_sand", 3f, new Color(0.93f, 0.9f, 0.88f, 1f)), PHLayer("rock_tile_floor", 3f), dirt }; // URP 지형은 한 번에 4종까지만 그려서 4종으로 제한
        var lakebed = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GenDir + "LakebedLayer.terrainlayer");
        DeepenLake(lake, -1.32f, 2.4f);
        RepaintLakeTerrain(lake, grass, dirt, lakebed);

        var village = CreateTerrain("HW_Terrain_Village", new Vector3(-160, BaseY, -275), new Vector3(320, TerrainHeight, 240), 257, lake, groundLayers, VillageCore);
        var west = CreateTerrain("HW_Terrain_West", new Vector3(-160, BaseY, -35), new Vector3(80, TerrainHeight, 160), 129, lake, groundLayers, r => false);
        var east = CreateTerrain("HW_Terrain_East", new Vector3(80, BaseY, -35), new Vector3(80, TerrainHeight, 160), 129, lake, groundLayers, r => false);
        var north = CreateTerrain("HW_Terrain_North", new Vector3(-160, BaseY, 125), new Vector3(320, TerrainHeight, 80), 129, lake, groundLayers, r => false);
        terrains.AddRange(new[] { village, west, east, north });
        lake.SetNeighbors(west, north, east, village);
        village.SetNeighbors(null, lake, null, null);

        // 금천 (정문 안쪽, 금천교 자리)
        CarveStream(village, StreamZ, 4f, 1.6f);

        // 4) 연못가: 보스맵과 같은 자리에 인간 세계 짝
        PlaceLakeside();

        // 5) 궁궐
        PlacePalace();

        // 6) 연못 물을 맑은 낮 물로
        SetupWater();

        // 7) 플레이어 시작 위치: 정문 바로 안쪽, 북쪽(정전 방향)을 바라봄
        var player = GameObject.Find("Player");
        player.transform.position = new Vector3(0, Ground(0, -182) + 1.2f, -182);
        player.transform.rotation = Quaternion.identity;
        // 손에 든 나뭇가지·검도 같은 그림체로 (glb 마다 재질 따로)
        foreach (var t in player.GetComponentsInChildren<Transform>(true))
            if (t.parent != null && t.parent.name == "WeaponMount" && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                Toonify(t.gameObject, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject), 0.004f);
        var cam = GameObject.Find("Main Camera");
        cam.transform.position = player.transform.position + new Vector3(0, 3, -6);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HumanWorldBuilder] 완료");
    }

    // ---------------- 분위기 ----------------
    static void SetupDaylight()
    {
        RenderSettings.skybox = InkSkyMat();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        DynamicGI.UpdateEnvironment();
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.86f);
        RenderSettings.fogDensity = 0.0025f;
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
        so.FindProperty("density").floatValue = 0.008f; // 낮이라 옅게
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 손그림 물 재질 (연못·금천 공용)
    static Material InkWaterMat()
    {
        string path = GenDir + "HW_InkWater.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Wongwirok/InkWater")); AssetDatabase.CreateAsset(mat, path); }
        return mat;
    }

    static void SetupWater()
    {
        var water = GameObject.Find("BossArena/Water");
        if (water == null) return;
        water.GetComponent<MeshRenderer>().sharedMaterial = InkWaterMat();
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

    // 궁궐 평지 영역 (가로 ±CoreX, 남쪽 끝 CoreSouth) / 금천 위치
    const float CoreX = 78f, CoreSouth = -198f, StreamZ = -172f;
    static bool VillageCore(Vector2 p) => p.x > -CoreX && p.x < CoreX && p.y > CoreSouth && p.y < -35;

    static Terrain CreateTerrain(string name, Vector3 pos, Vector3 size, int res, Terrain lake, TerrainLayer[] layers, System.Func<Vector2, bool> isCore)
    {
        var td = new TerrainData { heightmapResolution = res, size = size };
        td.alphamapResolution = res - 1;
        string path = "Assets/Scenes/HumanWorld_01/" + name + ".asset";
        System.IO.Directory.CreateDirectory("Assets/Scenes/HumanWorld_01");
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(td, path);
        td.terrainLayers = layers;

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
                float gentle = 0.8f * (Mathf.PerlinNoise(wx * 0.015f, wz * 0.015f) - 0.5f) * Smooth(4f, 30f, dOut) * Smooth(0f, 10f, dCore); // 궁궐 안은 평평하게
                h += hill + gentle;
                heights[y, x] = Mathf.Clamp01((h - BaseY) / size.y);
            }
        td.SetHeights(0, 0, heights);

        int ar = td.alphamapResolution;
        var a = new float[ar, ar, layers.Length];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = pos.x + (x + 0.5f) / ar * size.x;
                float wz = pos.z + (y + 0.5f) / ar * size.z;
                var w = GroundWeights(wx, wz);
                for (int k = 0; k < w.Length; k++) a[y, x, k] = w[k];
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

    // 궁궐 담 안쪽 전체를 모래 바닥으로 (담 밖 4m 까지 부드럽게)
    static float RoadMask(float x, float z)
    {
        float dx = Mathf.Max(-CoreX - x, 0, x - CoreX), dz = Mathf.Max(CoreSouth - z, 0, z - (-35));
        return 1 - Smooth(0f, 4f, Mathf.Sqrt(dx * dx + dz * dz));
    }

    // ---------------- 실사 바닥·하늘 (Poly Haven CC0, Assets/Environment/PolyHaven) ----------------
    const string PH = "Assets/Environment/PolyHaven/";

    static Texture2D PHTex(string file, bool normal)
    {
        string p = PH + file;
        var imp = (TextureImporter)AssetImporter.GetAtPath(p);
        if (normal && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static TerrainLayer PHLayer(string n, float tile) => PHLayer(n, tile, Color.white);
    static TerrainLayer PHLayer(string n, float tile, Color tint)
    {
        string path = GenDir + "PH_" + n + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = PHTex(n + "_diff_2k.jpg", false);
        layer.normalMapTexture = PHTex(n + "_nor_gl_2k.jpg", true);
        layer.normalScale = 1f;
        layer.tileSize = new Vector2(tile, tile);
        layer.smoothness = 0f;
        layer.diffuseRemapMax = tint; // 색감 보정 (곱하기)
        EditorUtility.SetDirty(layer);
        return layer;
    }

    // 바닥 레이어 비율: [풀, 마당 모래, 박석, 흙]
    const float WallX = 70f, WallS = -195f, WallN = -35f;
    static float[] GroundWeights(float x, float z)
    {
        float big = Mathf.PerlinNoise(x * 0.025f + 10, z * 0.025f + 5);
        float fine = Mathf.PerlinNoise(x * 0.11f + 3, z * 0.11f + 9);
        // 담 안쪽 깊이 (담에서 멀어질수록 +)
        float inside = Mathf.Min(WallX - Mathf.Abs(x), Mathf.Min(z - WallS, WallN - z));
        float inMask = Smooth(-1.5f, 1.5f, inside);
        // 담 안: 마사토 마당 + 군데군데 다져진 흙 + 담 밑 풀 + 중심축 박석 길
        float dirtIn = Mathf.Clamp01((big - 0.5f) * 2.2f) * 0.7f + Mathf.Clamp01((fine - 0.6f) * 2f) * 0.3f;
        float edgeGrass = (1 - Smooth(1.5f, 5f, inside)) * (0.6f + 0.4f * fine);
        float stone = (1 - Smooth(2.6f, 3.3f, Mathf.Abs(x))) * Smooth(0f, 1f, inside);
        float sand = Mathf.Max(0, 1 - dirtIn - edgeGrass);
        // 담 밖: 풀밭 + 흙 얼룩
        float outDirt = Mathf.Clamp01((fine - 0.68f) * 3f) * 0.45f;
        float[] w = {
            (1 - inMask) * (1 - outDirt) + inMask * edgeGrass * (1 - stone),
            inMask * sand * (1 - stone),
            inMask * stone,
            inMask * dirtIn * (1 - stone) + (1 - inMask) * outDirt,
        };
        float sum = 0; foreach (var v in w) sum += v;
        for (int i = 0; i < w.Length; i++) w[i] /= Mathf.Max(sum, 1e-4f);
        return w;
    }

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
                float d = Mathf.Abs(wz - zCenter); // 궁궐 금천은 곧은 물길
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
        water.transform.localScale = new Vector3(CoreX * 2f / 10f - 0.4f, 1, 0.5f);
        water.GetComponent<MeshRenderer>().sharedMaterial = InkWaterMat();
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
        Toonify(vis, AssetDatabase.GetAssetPath(src), Mathf.Clamp(scale * 0.003f, 0.008f, 0.06f));
        GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return holder;
    }

    // ---------------- 손그림 그림체 (셀 셰이딩 + 붓 외곽선) ----------------
    const string ToonDir = "Assets/Environment/Painted/Toon/";

    // glb 원래 재질의 색 텍스처로 ToonInk 재질을 만들어(에셋별 1개) 바꿔 끼운다. 외곽선 굵기는 처음 쓰인 크기 기준
    static void Toonify(GameObject go, string assetPath, float outline)
    {
        var shader = Shader.Find("Wongwirok/ToonInk");
        System.IO.Directory.CreateDirectory(ToonDir);
        string key = System.IO.Path.GetFileNameWithoutExtension(assetPath).Replace(" ", "_");
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null || ms[i].shader == shader) continue;
                string path = ToonDir + key + "_" + ms[i].name.Replace(" ", "_").Replace("/", "_") + ".mat";
                var toon = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (toon == null)
                {
                    toon = new Material(shader);
                    toon.SetFloat("_OutlineWidth", outline);
                    AssetDatabase.CreateAsset(toon, path);
                }
                // 에셋을 새로 뽑아 덮어쓰면 텍스처도 바뀌므로 매번 다시 연결
                Texture tex = null;
                foreach (var prop in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                    if (ms[i].HasProperty(prop) && ms[i].GetTexture(prop) != null) { tex = ms[i].GetTexture(prop); break; }
                toon.SetTexture("_BaseMap", tex);
                if (ms[i].HasProperty("baseColorFactor")) toon.SetColor("_BaseColor", ms[i].GetColor("baseColorFactor"));
                EditorUtility.SetDirty(toon);
                ms[i] = toon;
            }
            r.sharedMaterials = ms;
        }
    }

    static Material InkSkyMat()
    {
        string path = GenDir + "HW_InkSky.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Wongwirok/InkSky")); AssetDatabase.CreateAsset(mat, path); }
        return mat;
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
        if (pier) pier.transform.position = new Vector3(6f, -1.95f, shoreZ + 2.5f); // 갑판이 물 위로 살짝 (새 그림체 선착장은 높이 0.9m)

        // 선착장 옆 버드나무: 밧줄을 묶고 몸에 감은 뒤 연못 깊이 들어가면 귀의 세계로
        Vector3 treePos = new Vector3(-3f, 0, shoreZ - 5f);
        var ropeTree = Place("Assets/VarcoAssets/Tree/Green Weeping Willow.glb", "RopeTree_Willow", treePos, 200f, 9f, lakeRoot, false, 0.02f);
        if (ropeTree)
        {
            TreeCollider(ropeTree, 1.0f, 5f);
            var tie = new GameObject("TiePoint").transform;
            tie.SetParent(ropeTree.transform, false);
            // 줄기 쪽 연못을 향한 면, 허리 높이
            Vector3 toLake = (new Vector3(6f, 0, shoreZ + 3f) - treePos); toLake.y = 0; toLake.Normalize();
            tie.position = ropeTree.transform.position + Vector3.up * 1.1f + toLake * 0.35f;
            var rt = ropeTree.AddComponent<RopeTieTree>();
            var so = new SerializedObject(rt);
            so.FindProperty("tiePoint").objectReferenceValue = tie;
            so.FindProperty("ropeMaterial").objectReferenceValue = RopeMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[HumanWorldBuilder] 밧줄 나무 " + ropeTree.transform.position + " 묶는 자리 " + tie.position);
        }
        Debug.Log("[HumanWorldBuilder] 남쪽 물가 z=" + shoreZ);

        // 보스맵에서 복사된 정자 → 인간 세계용 궁궐 정자로 바꿈 (계단 위 난간을 뚫은 버전)
        var oldPav = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Structure_Pavilion");
        if (oldPav)
        {
            oldPav.SetActive(false);
            Place(PL + "Pavilion.glb", "Pavilion", oldPav.transform.position, 180, 14f, lakeRoot, true, 0f);
        }
        // 보스맵에서 복사된 장승도 같은 그림체로
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name.StartsWith("Structure_Totem")) Toonify(g, "Totem", 0.02f);
    }

    static float FindShore(float x)
    {
        for (float z = 45; z > -35; z -= 0.5f)
            if (Ground(x, z) > -1.3f) return z;
        return 10;
    }

    // ---------------- 궁궐 ----------------
    // 에셋 앞면 = +Z(transform.forward). 남쪽을 보게 하려면 yaw 180
    const string PL = "Assets/VarcoAssets/Palace/";
    const float WallLen = 6f;

    static void PlacePalace()
    {
        var v = new GameObject("HW_Palace").transform;
        v.SetParent(root);

        // 중심축: 정문 → 금천교 → 중문 → 품계석 → 월대·정전 → 편전 → 침전 → 후원 문 → 연못
        Place(PL + "PalaceGate.glb", "Jeongmun", new Vector3(0, 0, -195), 180, 24f, v, true, 0f);
        var br = Place("StoneBridge", "Geumcheongyo", new Vector3(0, 0, StreamZ), 90, 11f, v, true, 0f);
        if (br) br.transform.position = new Vector3(0, Ground(0, StreamZ + 8) - 0.6f, StreamZ);
        Place(PL + "MiddleGate.glb", "Jungmun", new Vector3(0, 0, -150), 180, 14f, v, true, 0f);
        for (int i = 0; i < 4; i++)
            foreach (int side in new[] { -1, 1 })
                Place(PL + "RankStone.glb", "Pumgyeseok", new Vector3(side * 4f, 0, -140 + i * 4f), 180, 1f, v, true, 0f);

        // 월대(납작하게 늘림) 위에 정전
        var woldae = Place(PL + "Woldae.glb", "Woldae", new Vector3(0, 0, -112), 180, 1f, v, true, 0f);
        if (woldae) woldae.transform.GetChild(0).localScale = new Vector3(30f, 10f, 22f);
        var hall = Place(PL + "ThroneHall.glb", "Jeongjeon", new Vector3(0, 0, -110), 180, 22f, v, true, 0f);
        if (hall) hall.transform.position += Vector3.up * 2.0f;
        Place(PL + "Deumu.glb", "Deumu_W", new Vector3(-13, 0, -126), 0, 1.6f, v, true, 0f);
        Place(PL + "Deumu.glb", "Deumu_E", new Vector3(13, 0, -126), 0, 1.6f, v, true, 0f);
        // 정전 마당 양옆 행각 (안쪽을 바라봄)
        foreach (float z in new[] { -137f, -119f, -101f })
        {
            Place(PL + "Corridor.glb", "Haenggak_W", new Vector3(-28, 0, z), 90, 18f, v, true, 0f);
            Place(PL + "Corridor.glb", "Haenggak_E", new Vector3(28, 0, z), -90, 18f, v, true, 0f);
        }
        Place(PL + "Hall.glb", "Pyeonjeon", new Vector3(0, 0, -82), 180, 18f, v, true, 0f);
        Place(PL + "Hall.glb", "Chimjeon", new Vector3(0, 0, -55), 180, 18f, v, true, 0f);

        // 서쪽: 수라간·궐내각사·장원서(나뭇가지 상점) / 동쪽: 대비전·동궁 (나중 스테이지)
        Place(PL + "Hall.glb", "Suragan", new Vector3(-50, 0, -60), 90, 14f, v, true, 0f);
        Place(PL + "Hall.glb", "Gwollaegaksa", new Vector3(-50, 0, -88), 90, 14f, v, true, 0f);
        Place(PL + "Hall.glb", "Jangwonseo", new Vector3(-50, 0, -120), 90, 14f, v, true, 0f);
        Place("Jige", "Jangwonseo_Jige", new Vector3(-40, 0, -126), 60, 1.6f, v, false, 0f);
        Place(PL + "Hall.glb", "Daebijeon", new Vector3(50, 0, -60), -90, 14f, v, true, 0f);
        Place(PL + "Hall.glb", "Donggung", new Vector3(50, 0, -110), -90, 16f, v, true, 0f);

        // 궁궐 담장: 남쪽(정문 자리 비움), 북쪽(후원 문 자리 비움), 동·서
        WallLine(v, -70, 70, -195, true, 12.5f);
        WallLine(v, -70, 70, -35, true, 3f);
        WallLine(v, -195, -35, -70, false, 0f);
        WallLine(v, -195, -35, 70, false, 0f);
        Place(PL + "GardenGate.glb", "HuwonGate", new Vector3(0, 0, -35), 180, 6f, v, true, 0f);

        // 담 밖 숲, 연못 지형 양옆/북쪽 언덕 나무
        for (int i = 0; i < 90; i++)
        {
            float x = R(-158, 158), zz = R(-272, -40);
            if (Mathf.Abs(x) < CoreX + 4 && zz > CoreSouth - 4) continue;
            string kind = (i % 5 == 0) ? "Pine" : (i % 2 == 0 ? "Zelkova" : "Assets/VarcoAssets/Tree/Green Weeping Willow.glb");
            var t = Place(kind, "Tree", new Vector3(x, 0, zz), R(0, 360), R(10, 14), v, false, 0.02f);
            TreeCollider(t, 0.6f, 6f);
        }
        for (int i = 0; i < 50; i++)
        {
            float x = R(-158, 158), zz = R(-34, 200);
            if (Mathf.Abs(x) < 84 && zz < 128) continue;
            var t = Place(i % 3 == 0 ? "Pine" : "Zelkova", "HillTree", new Vector3(x, 0, zz), R(0, 360), R(11, 16), v, false, 0.02f);
            TreeCollider(t, 0.6f, 6f);
        }
    }

    // a~b 구간에 담장을 잇는다. alongX 면 z=fixedC 인 동서 담, 아니면 x=fixedC 인 남북 담. 0 을 중심으로 ±gapHalf 는 문 자리로 비움
    static void WallLine(Transform parent, float a, float b, float fixedC, bool alongX, float gapHalf)
    {
        if (gapHalf > 0) { WallRun(parent, a, -gapHalf, fixedC, alongX); WallRun(parent, gapHalf, b, fixedC, alongX); }
        else WallRun(parent, a, b, fixedC, alongX);
    }

    static void WallRun(Transform parent, float s, float e, float fixedC, bool alongX)
    {
        int n = Mathf.CeilToInt((e - s) / WallLen);
        float step = (e - s) / n; // 조각끼리 살짝 겹치게 해서 틈이 없게
        for (int k = 0; k < n; k++)
        {
            float c = s + (k + 0.5f) * step;
            var pos = alongX ? new Vector3(c, 0, fixedC) : new Vector3(fixedC, 0, c);
            Place(PL + "PalaceWall.glb", "Wall", pos, alongX ? 0 : 90, WallLen, parent, true, 0.02f);
        }
    }
}
