using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BawalUmihiMaze.Editor
{
    [InitializeOnLoad]
    public static class BawalUmihiMazeBuilder
    {
        private const string ExtObjPath = "Assets/concrete_wall_extension_3d/concrete_wall_extension_3d.obj";
        private const string ExtDiffusePath = "Assets/concrete_wall_extension_3d/textures/material_0_diffuse.png";
        private const string ExtNormalPath = "Assets/concrete_wall_extension_3d/textures/material_0_normal.png";

        private const string BawalObjPath = "Assets/concrete_wall_bawal_umihi_3d/concrete_wall_bawal_umihi_3d.obj";
        private const string BawalDiffusePath = "Assets/concrete_wall_bawal_umihi_3d/textures/material_0_diffuse.png";
        private const string BawalNormalPath = "Assets/concrete_wall_bawal_umihi_3d/textures/material_0_normal.png";

        private const string ExtLodAtlasPath = "Assets/Maze/Textures/Extension_LOD1_Atlas.png";
        private const string BawalLodAtlasPath = "Assets/Maze/Textures/BawalUmihi_LOD1_Atlas.png";
        private const string FloorAlbedoPath = "Assets/Maze/Textures/AlleyFloor_Albedo.png";
        private const string FloorNormalPath = "Assets/Maze/Textures/AlleyFloor_Normal.png";
        private const string BgMusicPath = "Assets/Maze/Audio/MazeBackgroundMusic.wav";

        private const string MaterialsFolder = "Assets/Maze/Materials";
        private const string GeneratedFolder = "Assets/Maze/Generated";
        private const string PrefabsFolder = "Assets/Maze/Prefabs";

        private const string AutoBuildSessionKey = "BawalUmihiMaze_AutoBuilt_v1";
        private const string AutoPlayerSetupSessionKey = "BawalUmihiMaze_PlayerTimer_v1";
        private const string AutoMusicSetupSessionKey = "BawalUmihiMaze_BgMusic_v1";
        private static int s_retryTicks = 0;

        // Directions: 0 = North (+Z, r+1), 1 = East (+X, c+1), 2 = South (-Z, r-1), 3 = West (-X, c-1)
        private static readonly int[] DR = { 1, 0, -1, 0 };
        private static readonly int[] DC = { 0, 1, 0, -1 };
        private static readonly int[] OPP = { 2, 3, 0, 1 };
        private static readonly Vector3[] DIR_VEC =
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(-1f, 0f, 0f)
        };

        static BawalUmihiMazeBuilder()
        {
            EditorApplication.delayCall += TryAutoBuildOnLoad;
        }

        private static void TryAutoBuildOnLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (s_retryTicks++ < 40)
                    EditorApplication.delayCall += TryAutoBuildOnLoad;
                return;
            }

            // On a fresh git clone (no UserSettings/), Unity opens an unsaved empty "Untitled" scene.
            // Automatically open Assets/Scenes/SampleScene.unity if it exists!
            Scene currentScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.path) && !currentScene.isDirty && File.Exists("Assets/Scenes/SampleScene.unity"))
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            }

            MazeMetadata existingMeta = UnityEngine.Object.FindFirstObjectByType<MazeMetadata>();
            if (existingMeta != null || GameObject.Find("MazeRoot") != null)
            {
                SessionState.SetBool(AutoBuildSessionKey, true);
                RepairPrefabMeshesIfNeeded();

                bool missingPlayer = UnityEngine.Object.FindFirstObjectByType<PlayerCubeController>() == null;
                bool missingTimer = UnityEngine.Object.FindFirstObjectByType<MazeTimerUI>() == null;
                if (existingMeta != null && (missingPlayer || missingTimer) && !SessionState.GetBool(AutoPlayerSetupSessionKey, false))
                {
                    SessionState.SetBool(AutoPlayerSetupSessionKey, true);
                    SetupPlayerCubeAndTimer(existingMeta, saveScene: true);
                }

                bool missingMusic = UnityEngine.Object.FindFirstObjectByType<MazeBackgroundMusic>() == null;
                if (missingMusic && !SessionState.GetBool(AutoMusicSetupSessionKey, false))
                {
                    if (AssetDatabase.LoadAssetAtPath<AudioClip>(BgMusicPath) == null && s_retryTicks++ < 40)
                    {
                        EditorApplication.delayCall += TryAutoBuildOnLoad;
                        return;
                    }
                    SessionState.SetBool(AutoMusicSetupSessionKey, true);
                    SetupBackgroundMusic(saveScene: true);
                }
                return;
            }

            if (SessionState.GetBool(AutoBuildSessionKey, false))
                return;

            // Verify source wall models are ready in AssetDatabase before building
            if (LoadMeshFromModel(ExtObjPath) == null || LoadMeshFromModel(BawalObjPath) == null)
            {
                if (s_retryTicks++ < 40)
                    EditorApplication.delayCall += TryAutoBuildOnLoad;
                return;
            }

            SessionState.SetBool(AutoBuildSessionKey, true);
            SessionState.SetBool(AutoPlayerSetupSessionKey, true);
            SessionState.SetBool(AutoMusicSetupSessionKey, true);
            BuildMaze(
                width: 24,
                height: 24,
                cellSize: 3.6f,
                wallHeight: 2.75f,
                seed: 574u,
                straightBias: 0.48f,
                frameSceneView: true,
                saveScene: true);
        }

        [MenuItem("Tools/Bawal Umihi Maze/Generate or Rebuild Long Maze", priority = 10)]
        public static void MenuRebuildDefault()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<MazeMetadata>();
            int w = existing != null ? existing.gridWidth : 24;
            int h = existing != null ? existing.gridHeight : 24;
            float cs = existing != null ? existing.cellSize : 3.6f;
            float wh = existing != null ? existing.wallHeight : 2.75f;
            uint seed = existing != null ? existing.seed : 574u;
            float bias = existing != null ? existing.straightCorridorBias : 0.48f;

            BuildMaze(w, h, cs, wh, seed, bias, frameSceneView: false, saveScene: true);
        }

        [MenuItem("Tools/Bawal Umihi Maze/Randomize New Long Maze Seed & Rebuild", priority = 11)]
        public static void MenuRandomizeAndRebuild()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<MazeMetadata>();
            int w = existing != null ? existing.gridWidth : 24;
            int h = existing != null ? existing.gridHeight : 24;
            float cs = existing != null ? existing.cellSize : 3.6f;
            float wh = existing != null ? existing.wallHeight : 2.75f;
            float bias = existing != null ? existing.straightCorridorBias : 0.48f;

            uint bestSeed = FindHighPathSeed(w, h, bias, (uint)UnityEngine.Random.Range(1, 50000));
            BuildMaze(w, h, cs, wh, bestSeed, bias, frameSceneView: false, saveScene: true);
        }

        [MenuItem("Tools/Bawal Umihi Maze/Setup or Reset Player Cube & Timer", priority = 12)]
        public static void MenuSetupPlayerAndTimer()
        {
            var meta = UnityEngine.Object.FindFirstObjectByType<MazeMetadata>();
            if (meta != null)
                SetupPlayerCubeAndTimer(meta, saveScene: true);
        }

        [MenuItem("Tools/Bawal Umihi Maze/Setup or Reset Background Music", priority = 13)]
        public static void MenuSetupBackgroundMusic()
        {
            SetupBackgroundMusic(saveScene: true);
        }

        [MenuItem("Tools/Bawal Umihi Maze/Frame Full Maze Overview in Scene View", priority = 30)]
        public static void MenuFrameOverview()
        {
            var meta = UnityEngine.Object.FindFirstObjectByType<MazeMetadata>();
            if (meta != null)
                FrameMazeInSceneView(meta.footprintMeters.x, meta.footprintMeters.y);
        }

        [MenuItem("Tools/Bawal Umihi Maze/Frame Maze Entrance in Scene View", priority = 31)]
        public static void MenuFrameEntrance()
        {
            var meta = UnityEngine.Object.FindFirstObjectByType<MazeMetadata>();
            if (meta == null || SceneView.lastActiveSceneView == null) return;
            SceneView sv = SceneView.lastActiveSceneView;
            sv.LookAt(meta.entranceWorldPos + new Vector3(0f, 1.6f, 2.5f), Quaternion.Euler(14f, 0f, 0f), 7f);
            sv.Repaint();
        }

        public static uint FindHighPathSeed(int width, int height, float straightBias, uint baseSeed)
        {
            uint bestSeed = baseSeed;
            float bestScore = -1f;
            for (uint offset = 0; offset < 120u; offset++)
            {
                uint candidate = baseSeed + offset + 1u;
                bool[,,] walls = GenerateMazeWalls(width, height, candidate, straightBias);
                AnalyzeMaze(walls, width, height, out List<Vector2Int> sol, out List<(int r, int c, int openDir)> deadEnds);
                float score = sol.Count + deadEnds.Count * 2.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSeed = candidate;
                }
            }
            return bestSeed;
        }

        public static void BuildMaze(
            int width,
            int height,
            float cellSize,
            float wallHeight,
            uint seed,
            float straightBias,
            bool frameSceneView,
            bool saveScene)
        {
            EnsureFolders();
            ConfigureTexturesIfNeeded();

            Mesh extMesh = LoadMeshFromModel(ExtObjPath);
            Mesh bawalMesh = LoadMeshFromModel(BawalObjPath);
            if (extMesh == null || bawalMesh == null)
            {
                Debug.LogError("[BawalUmihiMaze] Could not load wall meshes from Assets/concrete_wall_extension_3d or Assets/concrete_wall_bawal_umihi_3d.");
                return;
            }

            // Build URP PBR Materials
            Material extMat = CreateOrUpdateWallMaterial(
                $"{MaterialsFolder}/ConcreteWall_Extension_URP.mat", ExtDiffusePath, ExtNormalPath, 0.22f);
            Material bawalMat = CreateOrUpdateWallMaterial(
                $"{MaterialsFolder}/ConcreteWall_BawalUmihi_URP.mat", BawalDiffusePath, BawalNormalPath, 0.22f);
            Material extLodMat = CreateOrUpdateSimpleLitMaterial(
                $"{MaterialsFolder}/ConcreteWall_Extension_LOD1_URP.mat", ExtLodAtlasPath, 0.18f);
            Material bawalLodMat = CreateOrUpdateSimpleLitMaterial(
                $"{MaterialsFolder}/ConcreteWall_BawalUmihi_LOD1_URP.mat", BawalLodAtlasPath, 0.18f);
            Material shadowProxyMat = CreateOrUpdateShadowMaterial(
                $"{MaterialsFolder}/Wall_ShadowProxy_URP.mat");

            float mazeWorldW = width * cellSize;
            float mazeWorldH = height * cellSize;
            float floorMargin = 10f;
            float floorSizeX = mazeWorldW + floorMargin * 2f;
            float floorSizeZ = mazeWorldH + floorMargin * 2f;
            Material floorMat = CreateOrUpdateFloorMaterial(
                $"{MaterialsFolder}/AlleyFloor_URP.mat",
                new Vector2(floorSizeX / 2.8f, floorSizeZ / 2.8f));

            // Build LOD1 & Shadow proxy meshes + Wall Prefabs
            Bounds extBounds = extMesh.bounds;
            Bounds bawalBounds = bawalMesh.bounds;

            Mesh extLodMesh = CreateOrUpdateLodBoxMesh($"{GeneratedFolder}/Extension_LOD1_Mesh.asset", extBounds);
            Mesh bawalLodMesh = CreateOrUpdateLodBoxMesh($"{GeneratedFolder}/BawalUmihi_LOD1_Mesh.asset", bawalBounds);

            Bounds extShadowBounds = new Bounds(
                extBounds.center,
                new Vector3(extBounds.size.x * 0.985f, extBounds.size.y * 0.985f, extBounds.size.z * 0.60f));
            Mesh extShadowMesh = CreateOrUpdateLodBoxMesh($"{GeneratedFolder}/Extension_ShadowMesh.asset", extShadowBounds);

            // Concrete body of Bawal Umihi wall is lower 96.1% of Y bounds (excluding thin pipe arch at very top)
            float bawalConcreteSpanY = bawalBounds.size.y * 0.9611f;
            Bounds bawalShadowBounds = new Bounds(
                new Vector3(bawalBounds.center.x, bawalBounds.min.y + bawalConcreteSpanY * 0.5f, bawalBounds.center.z),
                new Vector3(bawalBounds.size.x * 0.98f, bawalConcreteSpanY * 0.98f, bawalBounds.size.z * 0.80f));
            Mesh bawalShadowMesh = CreateOrUpdateLodBoxMesh($"{GeneratedFolder}/BawalUmihi_ShadowMesh.asset", bawalShadowBounds);

            GameObject extPrefab = CreateOrUpdateWallPrefab(
                $"{PrefabsFolder}/MazeWall_Extension.prefab",
                "MazeWall_Extension",
                extMesh, extMat,
                extLodMesh, extLodMat,
                extShadowMesh, shadowProxyMat,
                extBounds,
                lod0ScreenHeight: 0.055f);

            GameObject bawalPrefab = CreateOrUpdateWallPrefab(
                $"{PrefabsFolder}/MazeWall_BawalUmihi_DeadEnd.prefab",
                "MazeWall_BawalUmihi_DeadEnd",
                bawalMesh, bawalMat,
                bawalLodMesh, bawalLodMat,
                bawalShadowMesh, shadowProxyMat,
                bawalBounds,
                lod0ScreenHeight: 0.045f);

            AssetDatabase.SaveAssets();

            // Remove any previous MazeRoot in the scene
            var oldMeta = UnityEngine.Object.FindObjectsByType<MazeMetadata>(FindObjectsSortMode.None);
            foreach (var m in oldMeta)
            {
                if (m != null && m.gameObject != null)
                    UnityEngine.Object.DestroyImmediate(m.gameObject);
            }
            GameObject strayRoot = GameObject.Find("MazeRoot");
            if (strayRoot != null)
                UnityEngine.Object.DestroyImmediate(strayRoot);

            // Generate deterministic long-corridor maze topology
            bool[,,] walls = GenerateMazeWalls(width, height, seed, straightBias);
            AnalyzeMaze(walls, width, height, out List<Vector2Int> solutionCells, out List<(int r, int c, int openDir)> deadEnds);

            Vector3 origin = new Vector3(-mazeWorldW * 0.5f, 0f, -mazeWorldH * 0.5f);

            GameObject mazeRoot = new GameObject("MazeRoot");
            MazeMetadata meta = mazeRoot.AddComponent<MazeMetadata>();
            meta.gridWidth = width;
            meta.gridHeight = height;
            meta.cellSize = cellSize;
            meta.wallHeight = wallHeight;
            meta.seed = seed;
            meta.straightCorridorBias = straightBias;

            // 1. Walkable Alley Ground Slab (top surface at y = 0)
            GameObject floorHolder = new GameObject("Floor");
            floorHolder.transform.SetParent(mazeRoot.transform, false);

            GameObject groundGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundGo.name = "AlleyConcreteGround";
            groundGo.transform.SetParent(floorHolder.transform, false);
            groundGo.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            groundGo.transform.localScale = new Vector3(floorSizeX, 0.5f, floorSizeZ);
            MeshRenderer groundMr = groundGo.GetComponent<MeshRenderer>();
            groundMr.sharedMaterial = floorMat;
            groundMr.shadowCastingMode = ShadowCastingMode.Off;
            groundMr.receiveShadows = true;

            // 2. Compute exact scales & ground-contact offsets directly from imported Mesh bounds
            const float groundEmbed = 0.04f;
            const float targetWallThickness = 0.35f;

            float extSpan = cellSize + targetWallThickness; // Full corner-post overlap -> zero corner seams
            float extScaleX = extSpan / extBounds.size.x;
            float extScaleY = (wallHeight + groundEmbed) / extBounds.size.y;
            float extScaleZ = targetWallThickness / extBounds.size.z;
            Vector3 extScale = new Vector3(extScaleX, extScaleY, extScaleZ);
            float extPosY = -groundEmbed - extBounds.min.y * extScaleY;

            // Vertical (N-S) walls get a 2mm height offset so overlapping top coping faces never Z-fight
            float extScaleY_NS = (wallHeight + 0.002f + groundEmbed) / extBounds.size.y;
            Vector3 extScaleNS = new Vector3(extScaleX, extScaleY_NS, extScaleZ);
            float extPosY_NS = -groundEmbed - extBounds.min.y * extScaleY_NS;

            // Bawal Umihi Dead-End wall scale:
            // Width seals wall-to-wall across the corridor (embedded into side walls without poking outside);
            // Concrete top aligns flush with Extension wall concrete top (blue pipe arch crowns slightly above);
            // Back (-Z) tucks into the Extension back wall so mirrored back text is never visible from adjacent alleys.
            float bawalTargetWidth = cellSize - 0.12f;
            float bawalTargetDepth = Mathf.Min(0.92f, cellSize * 0.30f);
            float bawalScaleX = bawalTargetWidth / bawalBounds.size.x;
            float bawalScaleY = (wallHeight + groundEmbed) / bawalConcreteSpanY;
            float bawalScaleZ = bawalTargetDepth / bawalBounds.size.z;
            Vector3 bawalScale = new Vector3(bawalScaleX, bawalScaleY, bawalScaleZ);
            float bawalPosY = -groundEmbed - bawalBounds.min.y * bawalScaleY;

            float bawalBackHalfDepth = Mathf.Abs(bawalBounds.min.z) * bawalScaleZ;
            float backWallInnerDist = (cellSize * 0.5f) - (targetWallThickness * 0.5f);
            float bawalCenterBackOffset = (backWallInnerDist + 0.05f) - bawalBackHalfDepth;

            // 3. Instantiate all Perimeter & Corridor Extension Walls
            GameObject extHolder = new GameObject("Corridor_And_Perimeter_Walls");
            extHolder.transform.SetParent(mazeRoot.transform, false);

            int extCount = 0;
            Quaternion rotEW = Quaternion.identity;
            Quaternion rotNS = Quaternion.Euler(0f, 90f, 0f);

            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    // South edge of cell (r, c)
                    if (walls[r, c, 2])
                    {
                        Vector3 pos = origin + new Vector3((c + 0.5f) * cellSize, extPosY, r * cellSize);
                        SpawnWallInstance(extPrefab, extHolder.transform, $"Wall_EW_R{r:D2}_C{c:D2}_S", pos, rotEW, extScale, extBounds.center);
                        extCount++;
                    }

                    // West edge of cell (r, c)
                    if (walls[r, c, 3])
                    {
                        Vector3 pos = origin + new Vector3(c * cellSize, extPosY_NS, (r + 0.5f) * cellSize);
                        SpawnWallInstance(extPrefab, extHolder.transform, $"Wall_NS_R{r:D2}_C{c:D2}_W", pos, rotNS, extScaleNS, extBounds.center);
                        extCount++;
                    }

                    // North outer perimeter edge on top row
                    if (r == height - 1 && walls[r, c, 0])
                    {
                        Vector3 pos = origin + new Vector3((c + 0.5f) * cellSize, extPosY, (r + 1) * cellSize);
                        SpawnWallInstance(extPrefab, extHolder.transform, $"Wall_EW_R{r:D2}_C{c:D2}_N", pos, rotEW, extScale, extBounds.center);
                        extCount++;
                    }

                    // East outer perimeter edge on rightmost column
                    if (c == width - 1 && walls[r, c, 1])
                    {
                        Vector3 pos = origin + new Vector3((c + 1) * cellSize, extPosY_NS, (r + 0.5f) * cellSize);
                        SpawnWallInstance(extPrefab, extHolder.transform, $"Wall_NS_R{r:D2}_C{c:D2}_E", pos, rotNS, extScaleNS, extBounds.center);
                        extCount++;
                    }
                }
            }

            // 4. Instantiate Bawal Umihi Walls at Every Dead End
            GameObject deadEndHolder = new GameObject("DeadEnd_BawalUmihi_Walls");
            deadEndHolder.transform.SetParent(mazeRoot.transform, false);

            meta.deadEndPositions.Clear();
            for (int i = 0; i < deadEnds.Count; i++)
            {
                var (r, c, openDir) = deadEnds[i];
                Vector3 openVec = DIR_VEC[openDir];
                Vector3 backVec = -openVec;

                Vector3 cellCenter = origin + new Vector3((c + 0.5f) * cellSize, 0f, (r + 0.5f) * cellSize);
                Vector3 pos = new Vector3(cellCenter.x, bawalPosY, cellCenter.z) + backVec * bawalCenterBackOffset;

                // Local +Z of concrete_wall_bawal_umihi_3d has the readable "BAWAL UMIHI DITO" paint & pipe clamp,
                // so LookRotation(openVec) faces it directly down the open dead-end alley toward the player.
                Quaternion rot = Quaternion.LookRotation(openVec, Vector3.up);

                SpawnWallInstance(
                    bawalPrefab,
                    deadEndHolder.transform,
                    $"DeadEnd_BawalUmihi_{i + 1:D2}_R{r:D2}_C{c:D2}",
                    pos,
                    rot,
                    bawalScale,
                    bawalBounds.center);

                meta.deadEndPositions.Add(cellCenter);
            }

            // 5. Markers for Entrance & Exit + Populate Metadata
            GameObject markersHolder = new GameObject("Markers");
            markersHolder.transform.SetParent(mazeRoot.transform, false);

            Vector3 entrancePos = origin + new Vector3(0.5f * cellSize, 0f, 0f);
            Vector3 exitPos = origin + new Vector3((width - 0.5f) * cellSize, 0f, height * cellSize);

            GameObject startMarker = new GameObject("Entrance_Start");
            startMarker.transform.SetParent(markersHolder.transform, false);
            startMarker.transform.SetPositionAndRotation(entrancePos, Quaternion.identity);

            GameObject exitMarker = new GameObject("Exit_Finish");
            exitMarker.transform.SetParent(markersHolder.transform, false);
            exitMarker.transform.SetPositionAndRotation(exitPos, Quaternion.identity);

            meta.totalCells = width * height;
            meta.footprintMeters = new Vector2(mazeWorldW, mazeWorldH);
            meta.solutionPathCells = solutionCells.Count;
            meta.solutionPathMeters = Mathf.Round(solutionCells.Count * cellSize * 10f) / 10f;
            meta.totalCorridorMeters = Mathf.Round(width * height * cellSize * 10f) / 10f;
            meta.extensionWallCount = extCount;
            meta.bawalUmihiDeadEndCount = deadEnds.Count;
            meta.entranceWorldPos = entrancePos;
            meta.exitWorldPos = exitPos;

            meta.solutionWaypoints.Clear();
            meta.solutionWaypoints.Add(entrancePos + new Vector3(0f, 0f, -1.5f));
            foreach (Vector2Int cell in solutionCells)
            {
                meta.solutionWaypoints.Add(origin + new Vector3((cell.y + 0.5f) * cellSize, 0f, (cell.x + 0.5f) * cellSize));
            }
            meta.solutionWaypoints.Add(exitPos + new Vector3(0f, 0f, 1.5f));

            // 6. Setup Player Cube, Camera Follow, Timer-Only UI & Background Music
            SetupPlayerCubeAndTimer(meta, saveScene: false);
            SetupBackgroundMusic(saveScene: false);

            if (frameSceneView)
            {
                FrameMazeInSceneView(mazeWorldW, mazeWorldH);
            }

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (saveScene && !string.IsNullOrEmpty(activeScene.path))
            {
                EditorSceneManager.SaveScene(activeScene);
            }

            Selection.activeGameObject = mazeRoot;
            Debug.Log(
                $"[BawalUmihiMaze] Built {width}x{height} maze ({mazeWorldW:F1}m x {mazeWorldH:F1}m) | " +
                $"Solution path: {solutionCells.Count} cells ({meta.solutionPathMeters:F1}m) | " +
                $"Total corridors: {meta.totalCorridorMeters:F1}m | " +
                $"Extension walls: {extCount} | Bawal Umihi dead ends: {deadEnds.Count} | Seed: {seed}");
        }

        public static void SetupPlayerCubeAndTimer(MazeMetadata meta, bool saveScene)
        {
            EnsureFolders();

            // 1. Ensure Exit_Finish has a trigger collider so escaping stops the timer cleanly
            Transform exitTransform = meta.transform.Find("Markers/Exit_Finish");
            if (exitTransform != null)
            {
                BoxCollider exitBox = exitTransform.GetComponent<BoxCollider>();
                if (exitBox == null) exitBox = exitTransform.gameObject.AddComponent<BoxCollider>();
                exitBox.isTrigger = true;
                exitBox.center = new Vector3(0f, 1.4f, 0.5f);
                exitBox.size = new Vector3(meta.cellSize, 2.8f, 2.2f);

                if (exitTransform.GetComponent<MazeExitTrigger>() == null)
                    exitTransform.gameObject.AddComponent<MazeExitTrigger>();
            }

            // 2. Create or update URP Material for the Player Cube
            string playerMatPath = $"{MaterialsFolder}/PlayerCube_URP.mat";
            Material playerMat = AssetDatabase.LoadAssetAtPath<Material>(playerMatPath);
            Shader litShader = GetUrpLitShader();
            if (playerMat == null)
            {
                playerMat = new Material(litShader);
                AssetDatabase.CreateAsset(playerMat, playerMatPath);
            }
            playerMat.SetColor("_BaseColor", new Color(0.18f, 0.68f, 0.95f, 1f));
            playerMat.SetColor("_Color", new Color(0.18f, 0.68f, 0.95f, 1f));
            playerMat.SetFloat("_Smoothness", 0.45f);
            playerMat.enableInstancing = true;
            EditorUtility.SetDirty(playerMat);
            AssetDatabase.SaveAssets();

            // 3. Create or update Player Cube right at the Maze Entrance
            PlayerCubeController existingPlayer = UnityEngine.Object.FindFirstObjectByType<PlayerCubeController>();
            GameObject playerGo = existingPlayer != null ? existingPlayer.gameObject : GameObject.Find("PlayerCube");
            if (playerGo == null)
            {
                playerGo = new GameObject("PlayerCube");
            }
            playerGo.tag = "Player";

            Vector3 spawnPos = meta.entranceWorldPos + new Vector3(0f, 0f, -1.2f);
            playerGo.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);

            CharacterController cc = playerGo.GetComponent<CharacterController>();
            if (cc == null) cc = playerGo.AddComponent<CharacterController>();
            cc.radius = 0.42f;
            cc.height = 0.90f;
            cc.center = new Vector3(0f, 0.45f, 0f);
            cc.stepOffset = 0.25f;
            cc.skinWidth = 0.03f;

            Transform visualTr = playerGo.transform.Find("CubeVisual");
            if (visualTr == null)
            {
                GameObject cubePrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubePrim.name = "CubeVisual";
                Collider primCol = cubePrim.GetComponent<Collider>();
                if (primCol != null) UnityEngine.Object.DestroyImmediate(primCol);
                cubePrim.transform.SetParent(playerGo.transform, false);
                visualTr = cubePrim.transform;
            }
            visualTr.localPosition = new Vector3(0f, 0.45f, 0f);
            visualTr.localRotation = Quaternion.identity;
            visualTr.localScale = new Vector3(0.90f, 0.90f, 0.90f);

            MeshRenderer cubeMr = visualTr.GetComponent<MeshRenderer>();
            if (cubeMr != null)
            {
                cubeMr.sharedMaterial = playerMat;
                cubeMr.shadowCastingMode = ShadowCastingMode.On;
                cubeMr.receiveShadows = true;
            }

            PlayerCubeController controller = playerGo.GetComponent<PlayerCubeController>();
            if (controller == null) controller = playerGo.AddComponent<PlayerCubeController>();
            Camera mainCam = Camera.main;
            controller.followCamera = mainCam;
            controller.cubeVisual = visualTr;

            // Place Main Camera right behind Player Cube in Edit Mode so Game View matches Play Mode immediately
            if (mainCam != null)
            {
                Vector3 pivot = spawnPos + Vector3.up * controller.cameraPivotHeight;
                Quaternion camRot = Quaternion.Euler(14f, 0f, 0f);
                mainCam.transform.SetPositionAndRotation(
                    pivot + (camRot * Vector3.back) * controller.cameraDistance,
                    camRot);
            }

            // 4. Create or update minimal Timer-Only UI Canvas
            MazeTimerUI timerUi = UnityEngine.Object.FindFirstObjectByType<MazeTimerUI>();
            GameObject canvasGo = timerUi != null ? timerUi.gameObject : GameObject.Find("TimerCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("TimerCanvas");
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform textTr = canvasGo.transform.Find("TimerText");
            GameObject textGo;
            if (textTr == null)
            {
                textGo = new GameObject("TimerText");
                textGo.transform.SetParent(canvasGo.transform, false);
            }
            else
            {
                textGo = textTr.gameObject;
            }

            RectTransform rt = textGo.GetComponent<RectTransform>();
            if (rt == null) rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(420f, 80f);

            Text uiText = textGo.GetComponent<Text>();
            if (uiText == null) uiText = textGo.AddComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Consolas", 46);
            uiText.font = font;
            uiText.fontSize = 46;
            uiText.fontStyle = FontStyle.Bold;
            uiText.alignment = TextAnchor.UpperCenter;
            uiText.color = Color.white;
            uiText.raycastTarget = false;
            uiText.text = "00:00.00";

            Outline outline = textGo.GetComponent<Outline>();
            if (outline == null) outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            if (timerUi == null) timerUi = canvasGo.AddComponent<MazeTimerUI>();
            timerUi.SetLabel(uiText);

            if (saveScene)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);
                if (!string.IsNullOrEmpty(activeScene.path))
                {
                    EditorSceneManager.SaveScene(activeScene);
                }
                Debug.Log("[BawalUmihiMaze] Added PlayerCube (WASD + Mouse Look + Sprint/Jump) and Timer UI at Maze Entrance.");
            }
        }

        public static void SetupBackgroundMusic(bool saveScene)
        {
            // Configure AudioImporter for smooth streaming & low memory footprint
            var audioImp = AssetImporter.GetAtPath(BgMusicPath) as AudioImporter;
            if (audioImp != null)
            {
                AudioImporterSampleSettings settings = audioImp.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.Streaming || !audioImp.loadInBackground)
                {
                    audioImp.loadInBackground = true;
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.75f;
                    audioImp.defaultSampleSettings = settings;
                    audioImp.SaveAndReimport();
                }
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgMusicPath);

            MazeBackgroundMusic musicComp = UnityEngine.Object.FindFirstObjectByType<MazeBackgroundMusic>();
            GameObject musicGo = musicComp != null ? musicComp.gameObject : GameObject.Find("BackgroundMusic");
            if (musicGo == null)
            {
                musicGo = new GameObject("BackgroundMusic");
            }

            AudioSource source = musicGo.GetComponent<AudioSource>();
            if (source == null) source = musicGo.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.45f;

            if (musicComp == null) musicComp = musicGo.AddComponent<MazeBackgroundMusic>();
            if (clip != null) musicComp.musicClip = clip;
            musicComp.targetVolume = 0.45f;
            musicComp.fadeInSeconds = 1.5f;

            if (saveScene)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);
                if (!string.IsNullOrEmpty(activeScene.path))
                {
                    EditorSceneManager.SaveScene(activeScene);
                }
                Debug.Log("[BawalUmihiMaze] Added looping atmospheric BackgroundMusic to scene.");
            }
        }

        private static void RepairPrefabMeshesIfNeeded()
        {
            RepairSingleWallPrefab($"{PrefabsFolder}/MazeWall_Extension.prefab", ExtObjPath);
            RepairSingleWallPrefab($"{PrefabsFolder}/MazeWall_BawalUmihi_DeadEnd.prefab", BawalObjPath);
        }

        private static void RepairSingleWallPrefab(string prefabPath, string objPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;
            Transform lod0 = prefab.transform.Find("Mesh_LOD0");
            if (lod0 == null) return;
            MeshFilter mf = lod0.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh == null)
            {
                Mesh loaded = LoadMeshFromModel(objPath);
                if (loaded != null)
                {
                    mf.sharedMesh = loaded;
                    EditorUtility.SetDirty(prefab);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private static void SpawnWallInstance(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 desiredBoundsCenterXZAndPosY,
            Quaternion rotation,
            Vector3 scale,
            Vector3 localMeshCenter)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            Vector3 centerOffsetXZ = rotation * new Vector3(localMeshCenter.x * scale.x, 0f, localMeshCenter.z * scale.z);
            inst.transform.SetPositionAndRotation(desiredBoundsCenterXZAndPosY - centerOffsetXZ, rotation);
            inst.transform.localScale = scale;
        }

        private static void FrameMazeInSceneView(float mazeW, float mazeH)
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            float radius = Mathf.Max(mazeW, mazeH) * 0.68f;
            sv.LookAt(Vector3.zero, Quaternion.Euler(52f, 25f, 0f), radius);
            sv.Repaint();
        }

        // Deterministic XorShift32 PRNG for reproducible cross-platform maze topologies
        private struct XorShift32
        {
            private uint _state;
            public XorShift32(uint seed) { _state = seed == 0u ? 1u : seed; }
            public uint NextUInt()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
            public float NextFloat() => (NextUInt() & 0xFFFFFFu) / (float)0x1000000u;
            public int NextInt(int maxExclusive) => (int)(NextUInt() % (uint)maxExclusive);
        }

        private static bool[,,] GenerateMazeWalls(int width, int height, uint seed, float straightBias)
        {
            var rng = new XorShift32(seed);
            bool[,,] walls = new bool[height, width, 4];
            bool[,] visited = new bool[height, width];

            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                    for (int d = 0; d < 4; d++)
                        walls[r, c, d] = true;

            var stack = new List<(int r, int c, int lastDir)>(width * height);
            stack.Add((0, 0, -1));
            visited[0, 0] = true;

            int[] neighbors = new int[4];

            while (stack.Count > 0)
            {
                var (r, c, lastDir) = stack[stack.Count - 1];
                int nCount = 0;
                bool canGoStraight = false;

                for (int d = 0; d < 4; d++)
                {
                    int nr = r + DR[d];
                    int nc = c + DC[d];
                    if (nr >= 0 && nr < height && nc >= 0 && nc < width && !visited[nr, nc])
                    {
                        neighbors[nCount++] = d;
                        if (d == lastDir) canGoStraight = true;
                    }
                }

                if (nCount == 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                int chosen = (canGoStraight && rng.NextFloat() < straightBias)
                    ? lastDir
                    : neighbors[rng.NextInt(nCount)];

                int nextR = r + DR[chosen];
                int nextC = c + DC[chosen];
                walls[r, c, chosen] = false;
                walls[nextR, nextC, OPP[chosen]] = false;
                visited[nextR, nextC] = true;
                stack.Add((nextR, nextC, chosen));
            }

            // Carve South-West Entrance (0,0 South) and North-East Exit (height-1, width-1 North)
            walls[0, 0, 2] = false;
            walls[height - 1, width - 1, 0] = false;

            return walls;
        }

        private static void AnalyzeMaze(
            bool[,,] walls,
            int width,
            int height,
            out List<Vector2Int> solutionPath,
            out List<(int r, int c, int openDir)> deadEnds)
        {
            int[,] dist = new int[height, width];
            Vector2Int[,] parent = new Vector2Int[height, width];
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                    dist[r, c] = -1;

            var q = new Queue<Vector2Int>();
            dist[0, 0] = 0;
            parent[0, 0] = new Vector2Int(-1, -1);
            q.Enqueue(new Vector2Int(0, 0));

            while (q.Count > 0)
            {
                Vector2Int cur = q.Dequeue();
                int r = cur.x, c = cur.y;
                for (int d = 0; d < 4; d++)
                {
                    if (!walls[r, c, d])
                    {
                        int nr = r + DR[d];
                        int nc = c + DC[d];
                        if (nr >= 0 && nr < height && nc >= 0 && nc < width && dist[nr, nc] == -1)
                        {
                            dist[nr, nc] = dist[r, c] + 1;
                            parent[nr, nc] = cur;
                            q.Enqueue(new Vector2Int(nr, nc));
                        }
                    }
                }
            }

            solutionPath = new List<Vector2Int>();
            Vector2Int step = new Vector2Int(height - 1, width - 1);
            while (step.x >= 0 && step.y >= 0 && dist[step.x, step.y] >= 0)
            {
                solutionPath.Add(step);
                step = parent[step.x, step.y];
            }
            solutionPath.Reverse();

            deadEnds = new List<(int r, int c, int openDir)>();
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    int wallCnt = 0;
                    int openDir = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        if (walls[r, c, d]) wallCnt++;
                        else openDir = d;
                    }
                    if (wallCnt == 3)
                    {
                        deadEnds.Add((r, c, openDir));
                    }
                }
            }
        }

        private static void EnsureFolders()
        {
            string[] folders = { "Assets/Maze", MaterialsFolder, GeneratedFolder, PrefabsFolder, "Assets/Maze/Textures" };
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/') ?? "Assets";
                    string leaf = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }

        private static void ConfigureTexturesIfNeeded()
        {
            SetNormalMapIfNeeded(ExtNormalPath, TextureWrapMode.Repeat);
            SetNormalMapIfNeeded(BawalNormalPath, TextureWrapMode.Repeat);
            SetNormalMapIfNeeded(FloorNormalPath, TextureWrapMode.Repeat);

            SetColorTextureSettingsIfNeeded(FloorAlbedoPath, TextureWrapMode.Repeat, aniso: 8);
            SetColorTextureSettingsIfNeeded(ExtLodAtlasPath, TextureWrapMode.Clamp, aniso: 4);
            SetColorTextureSettingsIfNeeded(BawalLodAtlasPath, TextureWrapMode.Clamp, aniso: 4);
        }

        private static void SetNormalMapIfNeeded(string path, TextureWrapMode wrap)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (imp.textureType != TextureImporterType.NormalMap || imp.wrapMode != wrap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = wrap;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
        }

        private static void SetColorTextureSettingsIfNeeded(string path, TextureWrapMode wrap, int aniso)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (imp.textureType != TextureImporterType.Default || imp.wrapMode != wrap || imp.anisoLevel != aniso)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.wrapMode = wrap;
                imp.anisoLevel = aniso;
                imp.SaveAndReimport();
            }
        }

        private static Mesh LoadMeshFromModel(string assetPath)
        {
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (all != null)
            {
                foreach (UnityEngine.Object obj in all)
                {
                    if (obj is Mesh m && m.vertexCount > 0)
                        return m;
                }
            }
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go != null)
            {
                MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null) return mf.sharedMesh;
            }
            return null;
        }

        private static Shader GetUrpLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private static Material CreateOrUpdateWallMaterial(string matPath, string albedoPath, string normalPath, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = GetUrpLitShader();
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Cull", 0f); // Render Both Faces (matches materials.json doubleSided: true)

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 0.75f);
                mat.EnableKeyword("_NORMALMAP");
            }

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateSimpleLitMaterial(string matPath, string albedoPath, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = GetUrpLitShader();
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateFloorMaterial(string matPath, Vector2 tiling)
        {
            Material mat = CreateOrUpdateWallMaterial(matPath, FloorAlbedoPath, FloorNormalPath, 0.24f);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureScale("_MainTex", tiling);
            mat.SetTextureScale("_BumpMap", tiling);
            mat.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateShadowMaterial(string matPath)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = GetUrpLitShader();
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh CreateOrUpdateLodBoxMesh(string assetPath, Bounds b)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh();
            else
                mesh.Clear();

            mesh.name = Path.GetFileNameWithoutExtension(assetPath);

            float x0 = b.min.x, x1 = b.max.x;
            float y0 = b.min.y, y1 = b.max.y;
            float z0 = b.min.z, z1 = b.max.z;

            // Identity-bijection UVs matching BakeLodAtlases 4-quadrant layout:
            // +Z Front: u=[0.004..0.496] (x0->x1), v=[0.504..0.996] (y0->y1)
            // -Z Back:  u=[0.504..0.996] (x0->x1), v=[0.504..0.996] (y0->y1)
            // +Y Top:   u=[0.004..0.496] (x0->x1), v=[0.004..0.496] (z0->z1)
            // +X Side:  u=[0.504..0.746] (z0->z1), v=[0.004..0.496] (y0->y1)
            // -X Side:  u=[0.754..0.996] (z0->z1), v=[0.004..0.496] (y0->y1)
            const float pad = 0.003f;

            Vector3[] verts = new Vector3[20]
            {
                // 0..3: +Z face (outward normal +Z)
                new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
                // 4..7: -Z face (outward normal -Z)
                new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0),
                // 8..11: +Y top face (outward normal +Y)
                new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                // 12..15: +X side face (outward normal +X)
                new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1),
                // 16..19: -X side face (outward normal -X)
                new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0)
            };

            Vector2[] uvs = new Vector2[20]
            {
                // +Z
                new Vector2(0.0f + pad, 0.5f + pad), new Vector2(0.5f - pad, 0.5f + pad),
                new Vector2(0.5f - pad, 1.0f - pad), new Vector2(0.0f + pad, 1.0f - pad),
                // -Z
                new Vector2(1.0f - pad, 0.5f + pad), new Vector2(0.5f + pad, 0.5f + pad),
                new Vector2(0.5f + pad, 1.0f - pad), new Vector2(1.0f - pad, 1.0f - pad),
                // +Y
                new Vector2(0.0f + pad, 0.0f + pad), new Vector2(0.0f + pad, 0.5f - pad),
                new Vector2(0.5f - pad, 0.5f - pad), new Vector2(0.5f - pad, 0.0f + pad),
                // +X
                new Vector2(0.5f + pad, 0.0f + pad), new Vector2(0.5f + pad, 0.5f - pad),
                new Vector2(0.75f - pad, 0.5f - pad), new Vector2(0.75f - pad, 0.0f + pad),
                // -X
                new Vector2(1.0f - pad, 0.0f + pad), new Vector2(1.0f - pad, 0.5f - pad),
                new Vector2(0.75f + pad, 0.5f - pad), new Vector2(0.75f + pad, 0.0f + pad)
            };

            int[] tris = new int[30];
            for (int q = 0; q < 5; q++)
            {
                int v = q * 4;
                int t = q * 6;
                tris[t + 0] = v + 0; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 0; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.bounds = b;

            if (isNew)
                AssetDatabase.CreateAsset(mesh, assetPath);
            else
                EditorUtility.SetDirty(mesh);

            return mesh;
        }

        private static GameObject CreateOrUpdateWallPrefab(
            string prefabPath,
            string prefabName,
            Mesh lod0Mesh,
            Material lod0Mat,
            Mesh lod1Mesh,
            Material lod1Mat,
            Mesh shadowMesh,
            Material shadowMat,
            Bounds meshBounds,
            float lod0ScreenHeight)
        {
            GameObject tempRoot = new GameObject(prefabName);
            try
            {
                BoxCollider col = tempRoot.AddComponent<BoxCollider>();
                col.center = meshBounds.center;
                col.size = meshBounds.size;

                // LOD0: Full high-detail original OBJ mesh + 4K normal-mapped PBR material
                GameObject lod0Go = new GameObject("Mesh_LOD0");
                lod0Go.transform.SetParent(tempRoot.transform, false);
                MeshFilter mf0 = lod0Go.AddComponent<MeshFilter>();
                mf0.sharedMesh = lod0Mesh;
                MeshRenderer mr0 = lod0Go.AddComponent<MeshRenderer>();
                mr0.sharedMaterial = lod0Mat;
                mr0.shadowCastingMode = ShadowCastingMode.Off;
                mr0.receiveShadows = true;

                // LOD1: Orthographic-baked 10-triangle box for crisp distant / zoomed-out Scene View @ 200+ FPS
                GameObject lod1Go = new GameObject("Mesh_LOD1");
                lod1Go.transform.SetParent(tempRoot.transform, false);
                MeshFilter mf1 = lod1Go.AddComponent<MeshFilter>();
                mf1.sharedMesh = lod1Mesh;
                MeshRenderer mr1 = lod1Go.AddComponent<MeshRenderer>();
                mr1.sharedMaterial = lod1Mat;
                mr1.shadowCastingMode = ShadowCastingMode.Off;
                mr1.receiveShadows = true;

                // Dedicated lightweight ShadowCaster core box (30,000x faster shadow cascades than 365k/524k tris)
                GameObject shadowGo = new GameObject("ShadowProxy");
                shadowGo.transform.SetParent(tempRoot.transform, false);
                MeshFilter mfs = shadowGo.AddComponent<MeshFilter>();
                mfs.sharedMesh = shadowMesh;
                MeshRenderer mrs = shadowGo.AddComponent<MeshRenderer>();
                mrs.sharedMaterial = shadowMat;
                mrs.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                mrs.receiveShadows = false;

                LODGroup lodGroup = tempRoot.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.SetLODs(new LOD[]
                {
                    new LOD(lod0ScreenHeight, new Renderer[] { mr0 }),
                    new LOD(0.0005f, new Renderer[] { mr1 })
                });
                lodGroup.RecalculateBounds();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempRoot);
            }
        }
    }

    [CustomEditor(typeof(MazeMetadata))]
    public class MazeMetadataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MazeMetadata meta = (MazeMetadata)target;
            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Maze (Current Settings)", GUILayout.Height(30)))
                {
                    BawalUmihiMazeBuilder.BuildMaze(
                        meta.gridWidth,
                        meta.gridHeight,
                        meta.cellSize,
                        meta.wallHeight,
                        meta.seed,
                        meta.straightCorridorBias,
                        frameSceneView: false,
                        saveScene: true);
                }

                if (GUILayout.Button("New Random Long Maze", GUILayout.Height(30)))
                {
                    uint nextSeed = BawalUmihiMazeBuilder.FindHighPathSeed(
                        meta.gridWidth,
                        meta.gridHeight,
                        meta.straightCorridorBias,
                        (uint)UnityEngine.Random.Range(1, 50000));

                    BawalUmihiMazeBuilder.BuildMaze(
                        meta.gridWidth,
                        meta.gridHeight,
                        meta.cellSize,
                        meta.wallHeight,
                        nextSeed,
                        meta.straightCorridorBias,
                        frameSceneView: false,
                        saveScene: true);
                }
            }
        }
    }
}
