// =============================================================================
// SceneBuilder.cs (Editor-only)
// -----------------------------------------------------------------------------
// GoodNight My Angel sahnesini hızlıca kurmak için editor menüsü.
//
// Unity menüsü:
//   GoodNight > Build Demo Scene
//
// Tek tıkla: kamera, ışık, yer, yatak, oyuncu, spawn noktaları, basit
// ev outline'ı oluşturulur. Sonra elle mesh/model ekleyebilirsin.
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using Unity.AI.Navigation;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Player;
using GoodNightMyAngel.CameraSys;
using GoodNightMyAngel.World;
using GoodNightMyAngel.Enemies;
using GoodNightMyAngel.Build;
using GoodNightMyAngel.Skills;
using GoodNightMyAngel.UI;
using GoodNightMyAngel.DebugTools;

namespace GoodNightMyAngel.EditorTools
{
    public static class SceneBuilder
    {
        [MenuItem("GoodNight/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            Debug.Log("[SceneBuilder] Demo sahne kurulumu başlıyor...");

            // -----------------------------------------------------------------
            // 1) Bootstrap (her şeyi oluşturur)
            // -----------------------------------------------------------------
            var boot = new GameObject("__Bootstrap");
            boot.AddComponent<GameBootstrap>();

            // -----------------------------------------------------------------
            // 2) Zemin
            // -----------------------------------------------------------------
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8, 1, 8); // 80x80
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.25f, 0.35f, 0.18f); // koyu çim
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // -----------------------------------------------------------------
            // 3) Yatak (merkez, rüya gören beden)
            // -----------------------------------------------------------------
            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Bed";
            bed.transform.position = new Vector3(0, 0.4f, 0);
            bed.transform.localScale = new Vector3(1.2f, 0.5f, 2.0f);
            var bedMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bedMat.color = new Color(0.6f, 0.4f, 0.3f);
            bed.GetComponent<Renderer>().sharedMaterial = bedMat;
            var bedComp = bed.AddComponent<Bed>();
            // Box collider zaten var
            var bedCollider = bed.GetComponent<BoxCollider>();
            bedCollider.isTrigger = false;

            // -----------------------------------------------------------------
            // 4) Oyuncu (CharacterController'lı küp)
            // -----------------------------------------------------------------
            var player = new GameObject("Player");
            player.transform.position = new Vector3(2, 0.9f, 0);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);

            // Görsel mesh
            var playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerVisual.transform.SetParent(player.transform, false);
            playerVisual.transform.localPosition = new Vector3(0, 0.9f, 0);
            playerVisual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(playerVisual.GetComponent<Collider>());
            var pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pMat.color = new Color(0.3f, 0.6f, 0.9f);
            playerVisual.GetComponent<Renderer>().sharedMaterial = pMat;

            player.AddComponent<PlayerHealth>();
            var pc = player.AddComponent<PlayerController>();
            player.tag = "Player";

            // -----------------------------------------------------------------
            // 5) Kamera (top-down)
            // -----------------------------------------------------------------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            cam.transform.position = new Vector3(0, 12, -8);
            cam.transform.rotation = Quaternion.Euler(55, 0, 0);
            camGo.AddComponent<AudioListener>();
            var tdc = camGo.AddComponent<TopDownCamera>();
            tdc.target = player.transform;

            // -----------------------------------------------------------------
            // 6) Yönlendirme ışığı
            // -----------------------------------------------------------------
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(50, 30, 0);

            // -----------------------------------------------------------------
            // 7) Spawn noktaları (orman kenarı — kuzey)
            // -----------------------------------------------------------------
            var spawnRoot = new GameObject("EnemySpawnPoints");
            Vector3[] spawnPositions = new Vector3[]
            {
                new Vector3(-10, 0, 18),
                new Vector3(  0, 0, 20),
                new Vector3( 10, 0, 18),
                new Vector3(-18, 0, 12),
                new Vector3( 18, 0, 12),
            };
            foreach (var p in spawnPositions)
            {
                var sp = new GameObject("Spawn_" + p);
                sp.transform.SetParent(spawnRoot.transform);
                sp.transform.position = p;
                sp.tag = "EnemySpawn";
            }

            // -----------------------------------------------------------------
            // 8) NavMesh (opsiyonel)
            // -----------------------------------------------------------------
            var groundForNav = ground;
            var nav = groundForNav.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.All;
            nav.BuildNavMesh();

            // -----------------------------------------------------------------
            // 9) BuildManager
            // -----------------------------------------------------------------
            var bmGo = new GameObject("BuildManager");
            var bm = bmGo.AddComponent<BuildManager>();
            bm.gridCenter = bed.transform;
            // Not: BuildManager doğrudan spawn noktalarını yönetmez,
            // sadece yatak etrafında grid çizer. Yaratık yol çizgisi
            // GameManager.enemySpawnPoints üzerinden çalışır (aşağıda atanacak).

            // Katalog için örnek BuildItemData
            var barricade = ScriptableObject.CreateInstance<BuildItemData>();
            barricade.displayName = "Barikat";
            barricade.maxHealth = 80;
            barricade.damage = 0;
            barricade.cost = 15;
            barricade.category = BuildItemCategory.Barricade;
            AssetDatabase.CreateAsset(barricade, "Assets/_Game/Data/Barricade.asset");

            var trap = ScriptableObject.CreateInstance<BuildItemData>();
            trap.displayName = "Tuzak";
            trap.maxHealth = 30;
            trap.damage = 25;
            trap.attackRange = 1.5f;
            trap.attackInterval = 0.8f;
            trap.cost = 30;
            trap.category = BuildItemCategory.Trap;
            AssetDatabase.CreateAsset(trap, "Assets/_Game/Data/Trap.asset");

            var turret = ScriptableObject.CreateInstance<BuildItemData>();
            turret.displayName = "Kule";
            turret.maxHealth = 60;
            turret.damage = 8;
            turret.attackRange = 6f;
            turret.attackInterval = 0.6f;
            turret.cost = 50;
            turret.category = BuildItemCategory.Turret;
            AssetDatabase.CreateAsset(turret, "Assets/_Game/Data/Turret.asset");

            bm.catalog.Add(barricade);
            bm.catalog.Add(trap);
            bm.catalog.Add(turret);

            // -----------------------------------------------------------------
            // 10) EnemySpawner
            // -----------------------------------------------------------------
            var spawner = new GameObject("EnemySpawner");
            var es = spawner.AddComponent<EnemySpawner>();
            es.spawnPointTag = "EnemySpawn";

            // Basit düşman prefab'ı
            var enemyPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyPrefab.name = "EnemyPrefab";
            enemyPrefab.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            var eMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            eMat.color = new Color(0.4f, 0.1f, 0.1f);
            enemyPrefab.GetComponent<Renderer>().sharedMaterial = eMat;
            // CharacterController yerine NavMeshAgent
            var nma = enemyPrefab.AddComponent<NavMeshAgent>();
            nma.radius = 0.3f; nma.height = 1.6f;
            var enemyComp = enemyPrefab.AddComponent<EnemyBase>();
            enemyComp.moveSpeed = 2.5f;
            enemyComp.maxHealth = 25;
            enemyComp.damage = 5;
            enemyComp.attackCooldown = 1f;
            enemyPrefab.tag = "Enemy";

            // Prefab olarak kaydet
            string prefabPath = "Assets/_Game/Prefabs/Enemy.prefab";
            PrefabUtility.SaveAsPrefabAsset(enemyPrefab, prefabPath);
            es.normalEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            // Boss prefabı (daha büyük)
            var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boss.name = "BossPrefab";
            boss.transform.localScale = new Vector3(1.5f, 1.8f, 1.5f);
            var bMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bMat.color = new Color(0.7f, 0.1f, 0.3f);
            boss.GetComponent<Renderer>().sharedMaterial = bMat;
            var bnma = boss.AddComponent<NavMeshAgent>();
            bnma.radius = 0.6f; bnma.height = 2.5f;
            var bossComp = boss.AddComponent<EnemyBase>();
            bossComp.moveSpeed = 1.8f;
            bossComp.maxHealth = 250;
            bossComp.damage = 12;
            bossComp.attackCooldown = 1.5f;
            bossComp.isBoss = true;
            boss.tag = "Enemy";

            string bossPath = "Assets/_Game/Prefabs/Boss.prefab";
            PrefabUtility.SaveAsPrefabAsset(boss, bossPath);
            es.bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bossPath);

            // GameManager'a spawner ve boss ata
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                // bootstrap henüz Start çalışmamış olabilir, oluştur
                var go = new GameObject("GameManager");
                gm = go.AddComponent<GameManager>();
            }
            gm.bed = bedComp;
            gm.bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bossPath);
            // spawn noktalarını ata
            var sps = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in spawnRoot.transform) sps.Add(child);
            gm.enemySpawnPoints = sps.ToArray();
            gm.bossSpawnPoints = sps.ToArray();

            // -----------------------------------------------------------------
            // 11) CallMom skill (player üzerine)
            // -----------------------------------------------------------------
            player.AddComponent<CallMomSkill>();

            // -----------------------------------------------------------------
            // 12) HUD
            // -----------------------------------------------------------------
            var hudGo = new GameObject("GameHud");
            hudGo.AddComponent<GameHud>();

            // -----------------------------------------------------------------
            // 13) Debug Console
            // -----------------------------------------------------------------
            var dcGo = new GameObject("DebugConsole");
            dcGo.AddComponent<DebugConsole>();

            // -----------------------------------------------------------------
            // 14) BuildItem prefab'ları oluştur (basit küpler)
            // -----------------------------------------------------------------
            CreateBuildPrefab(barricade, PrimitiveType.Cube, new Vector3(0.8f, 0.6f, 0.8f),
                new Color(0.5f, 0.35f, 0.2f));
            CreateBuildPrefab(trap, PrimitiveType.Cube, new Vector3(0.5f, 0.2f, 0.5f),
                new Color(0.6f, 0.2f, 0.2f));
            CreateBuildPrefab(turret, PrimitiveType.Cylinder, new Vector3(0.3f, 0.6f, 0.3f),
                new Color(0.4f, 0.4f, 0.5f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] Demo sahne kurulumu TAMAMLANDI. " +
                      "Play'e bas ve test et. ` tuşu ile debug konsolunu aç.");
        }

        private static void CreateBuildPrefab(BuildItemData data, PrimitiveType prim,
            Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = data.displayName + "Prefab";
            go.transform.localScale = scale;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<BuildItem>();
            string path = $"Assets/_Game/Prefabs/{data.displayName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Object.DestroyImmediate(go);
        }

        [MenuItem("GoodNight/Add Layers and Tags")]
        public static void AddLayersAndTags()
        {
            // Tag'lerin eklenmesi (Layer'lar serialization gerektirir, atlanıyor)
            // Open TagManager
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            bool Has(string s)
            {
                for (int i = 0; i < tagsProp.arraySize; i++)
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == s) return true;
                return false;
            }
            void Add(string s)
            {
                if (Has(s)) return;
                tagsProp.InsertArrayElementAtIndex(0);
                tagsProp.GetArrayElementAtIndex(0).stringValue = s;
            }
            Add("Player");
            Add("Enemy");
            Add("EnemySpawn");
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Player / Enemy / EnemySpawn tag'leri eklendi.");
        }
    }
}
#endif
