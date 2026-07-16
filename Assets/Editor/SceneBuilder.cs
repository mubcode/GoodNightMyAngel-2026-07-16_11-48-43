// =============================================================================
// SceneBuilder.cs (Editor-only)
// -----------------------------------------------------------------------------
// GoodNight My Angel sahnesini hızlıca kurmak için editor menüsü.
//
// Unity menüsü:
//   GoodNight > 1) Add Required Tags   (mutlaka önce bunu çalıştır)
//   GoodNight > 2) Build Demo Scene
//
// Build Demo Scene tüm sahneyi kurmadan önce:
//   1) Tag'lerin tanımlı olup olmadığını kontrol eder; değilse uyarı verir.
//   2) Assets/_Game/Prefabs/ klasörünü oluşturur.
//   3) Sahneyi kurar.
// =============================================================================

#if UNITY_EDITOR
using System.IO;
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
        // Sahne kurulumu için gereken tag'ler
        private static readonly string[] REQUIRED_TAGS = new string[]
        {
            "Player", "Enemy", "EnemySpawn"
        };

        // --------------------------------------------------------------------
        // MENU: Add Required Tags
        // --------------------------------------------------------------------
        [MenuItem("GoodNight/1) Add Required Tags")]
        public static void AddRequiredTags()
        {
            Debug.Log("[SceneBuilder] Tag'ler ekleniyor...");

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");

            // Zaten var olan tag'leri topla
            var existing = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var v = tagsProp.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(v)) existing.Add(v);
            }

            int added = 0;
            foreach (var t in REQUIRED_TAGS)
            {
                if (existing.Contains(t)) continue;
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = t;
                added++;
                Debug.Log($"[SceneBuilder] Tag eklendi: {t}");
            }

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log($"[SceneBuilder] Tag işlemi tamamlandı. Eklenen: {added}, " +
                      $"toplam: {tagsProp.arraySize}");
        }

        // --------------------------------------------------------------------
        // MENU: Build Demo Scene
        // --------------------------------------------------------------------
        [MenuItem("GoodNight/2) Build Demo Scene")]
        public static void BuildDemoScene()
        {
            // Tag'ler hazır mı kontrol et
            if (!AreAllTagsPresent())
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Tag'ler Eksik",
                    "Sahne kurulumu için 'Player', 'Enemy', 'EnemySpawn' tag'leri gerekli.\n\n" +
                    "Şimdi 'GoodNight > 1) Add Required Tags' çalıştırılsın mı?",
                    "Evet, ekle", "İptal");
                if (!ok) return;
                AddRequiredTags();
                // Asset veritabanını tazele
                AssetDatabase.Refresh();
            }

            Debug.Log("[SceneBuilder] Demo sahne kurulumu başlıyor...");

            // Prefabs klasörünü garanti et
            EnsureFolder("Assets/_Game/Prefabs");
            EnsureFolder("Assets/_Game/Data");

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
            player.tag = "Player";  // tag'i en başta set et
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

            // YÖN GÖSTERGESİ: ön tarafa bakan bir silindir (ok)
            // Capsule'in hemen önünde, yatay, karakter döndüğünde döner
            var aimIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aimIndicator.name = "AimIndicator";
            // Collider kaldır
            var aimCol = aimIndicator.GetComponent<Collider>();
            if (aimCol != null) Object.DestroyImmediate(aimCol);
            // Capsule'in child'ı olarak ekle, ön tarafa konumlandır
            aimIndicator.transform.SetParent(player.transform, false);
            // Cylinder default Y ekseninde duruyor; onu yatırıp Z yönüne çevir
            aimIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
            // Ön tarafa, yerden biraz yukarıda
            aimIndicator.transform.localPosition = new Vector3(0, 0.5f, 0.7f);
            // İnce ve uzun bir ok
            aimIndicator.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
            var aimMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            aimMat.color = new Color(1f, 0.95f, 0.3f, 1f);   // parlak sarı
            aimIndicator.GetComponent<Renderer>().sharedMaterial = aimMat;

            // Ok ucu (küçük küre)
            var aimTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aimTip.name = "AimTip";
            var tipCol = aimTip.GetComponent<Collider>();
            if (tipCol != null) Object.DestroyImmediate(tipCol);
            aimTip.transform.SetParent(player.transform, false);
            aimTip.transform.localPosition = new Vector3(0, 0.5f, 1.3f);
            aimTip.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            var tipMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            tipMat.color = new Color(1f, 0.5f, 0.2f, 1f);     // turuncu uç
            aimTip.GetComponent<Renderer>().sharedMaterial = tipMat;

            player.AddComponent<PlayerHealth>();
            var pc = player.AddComponent<PlayerController>();

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
            var spawnList = new System.Collections.Generic.List<Transform>();
            foreach (var p in spawnPositions)
            {
                var sp = new GameObject("Spawn_" + p);
                sp.transform.SetParent(spawnRoot.transform);
                sp.transform.position = p;
                sp.tag = "EnemySpawn";  // tag artık tanımlı olmalı
                spawnList.Add(sp.transform);
            }

            // -----------------------------------------------------------------
            // 8) NavMesh (opsiyonel)
            // -----------------------------------------------------------------
            var nav = ground.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.All;
            nav.BuildNavMesh();

            // -----------------------------------------------------------------
            // 9) BuildManager
            // -----------------------------------------------------------------
            var bmGo = new GameObject("BuildManager");
            var bm = bmGo.AddComponent<BuildManager>();
            bm.gridCenter = bed.transform;

            // Katalog için örnek BuildItemData
            var barricade = CreateOrLoadBuildItemData("Assets/_Game/Data/Barricade.asset",
                "Barikat", 80, 0, 15, BuildItemCategory.Barricade, 0f, 1f);

            var trap = CreateOrLoadBuildItemData("Assets/_Game/Data/Trap.asset",
                "Tuzak", 30, 25, 30, BuildItemCategory.Trap, 1.5f, 0.8f);

            var turret = CreateOrLoadBuildItemData("Assets/_Game/Data/Turret.asset",
                "Kule", 60, 8, 50, BuildItemCategory.Turret, 6f, 0.6f);

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
            string enemyPath = "Assets/_Game/Prefabs/Enemy.prefab";
            GameObject enemyPrefab = CreateOrLoadEnemyPrefab(enemyPath, "EnemyPrefab",
                new Vector3(0.6f, 0.8f, 0.6f), new Color(0.4f, 0.1f, 0.1f),
                0.3f, 1.6f, 2.5f, 25, 5, 1f, false);
            es.normalEnemyPrefab = enemyPrefab;

            // Boss prefabı
            string bossPath = "Assets/_Game/Prefabs/Boss.prefab";
            GameObject bossPrefab = CreateOrLoadEnemyPrefab(bossPath, "BossPrefab",
                new Vector3(1.5f, 1.8f, 1.5f), new Color(0.7f, 0.1f, 0.3f),
                0.6f, 2.5f, 1.8f, 250, 12, 1.5f, true);
            es.bossPrefab = bossPrefab;

            // GameManager'a spawner ve boss ata
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                var go = new GameObject("GameManager");
                gm = go.AddComponent<GameManager>();
            }
            gm.bed = bedComp;
            gm.bossPrefab = bossPrefab;
            gm.enemySpawnPoints = spawnList.ToArray();
            gm.bossSpawnPoints = spawnList.ToArray();

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
            // 14) PathManager (Fields Runner 2 tarzı yol sistemi)
            // -----------------------------------------------------------------
            var pathMgr = new GameObject("PathManager");
            var pm = pathMgr.AddComponent<PathManager>();
            pm.bed = bedComp;
            // Başlangıç waypoint'leri oluştur (her spawn noktası için)
            pm.startPoints = new System.Collections.Generic.List<PathWaypoint>();
            for (int i = 0; i < spawnList.Count; i++)
            {
                var go = new GameObject($"PathStart_{i}");
                go.transform.position = spawnList[i].position;
                go.AddComponent<PathWaypoint>();
                var wp = go.GetComponent<PathWaypoint>();
                // Orta waypoint (yolun ortasında küçük bir kıvrım)
                var midGo = new GameObject($"PathMid_{i}");
                Vector3 midPos = Vector3.Lerp(spawnList[i].position, bed.transform.position, 0.55f);
                midPos.x += Random.Range(-3f, 3f);
                midPos.z += Random.Range(-1f, 1f);
                midGo.transform.position = midPos;
                var midWp = midGo.AddComponent<PathWaypoint>();
                wp.next = midWp;
                // midWp.next = null (otomatik olarak bed'e gider)
                pm.startPoints.Add(wp);
            }

            // -----------------------------------------------------------------
            // 15) Minimap
            // -----------------------------------------------------------------
            var mapGo = new GameObject("Minimap");
            mapGo.AddComponent<Minimap>();

            // -----------------------------------------------------------------
            // 16) ControlsPanel (sol tarafta kontroller + debug komutları)
            // -----------------------------------------------------------------
            var cpGo = new GameObject("ControlsPanel");
            cpGo.AddComponent<ControlsPanel>();

            // -----------------------------------------------------------------
            // 17) PausePanel (ESC ile duraklatma paneli)
            // -----------------------------------------------------------------
            var ppGo = new GameObject("PausePanel");
            ppGo.AddComponent<PausePanel>();

            // -----------------------------------------------------------------
            // 17) BuildItem prefab'ları oluştur (basit küpler)
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

        // --------------------------------------------------------------------
        // YARDIMCI METODLAR
        // --------------------------------------------------------------------

        private static bool AreAllTagsPresent()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");
            var existing = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var v = tagsProp.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(v)) existing.Add(v);
            }
            foreach (var t in REQUIRED_TAGS)
                if (!existing.Contains(t)) return false;
            return true;
        }

        private static void EnsureFolder(string path)
        {
            // "Assets/_Game/Prefabs" gibi yolu parçalara ayır
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string cur = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static BuildItemData CreateOrLoadBuildItemData(string path, string name,
            float maxHp, float dmg, int cost, BuildItemCategory cat, float range, float interval)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BuildItemData>(path);
            if (existing != null) return existing;

            var data = ScriptableObject.CreateInstance<BuildItemData>();
            data.displayName = name;
            data.maxHealth = maxHp;
            data.damage = dmg;
            data.cost = cost;
            data.category = cat;
            data.attackRange = range;
            data.attackInterval = interval;
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        private static GameObject CreateOrLoadEnemyPrefab(string path, string name,
            Vector3 scale, Color color, float agentRadius, float agentHeight,
            float moveSpeed, float maxHp, float dmg, float atkCooldown, bool isBoss)
        {
            // Önceden var mı?
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Yoksa oluştur
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.localScale = scale;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            // Capsule'in collider'ı var; düşmanlar için CharacterController yerine
            // NavMeshAgent kullanacağız, collider kalsın (fizik için).
            var nma = go.AddComponent<NavMeshAgent>();
            nma.radius = agentRadius;
            nma.height = agentHeight;
            var enemy = go.AddComponent<EnemyBase>();
            enemy.moveSpeed = moveSpeed;
            enemy.maxHealth = maxHp;
            enemy.damage = dmg;
            enemy.attackCooldown = atkCooldown;
            enemy.isBoss = isBoss;
            go.tag = "Enemy";

            // Prefab olarak kaydet
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            // Geçici sahne objesini yok et
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void CreateBuildPrefab(BuildItemData data, PrimitiveType prim,
            Vector3 scale, Color color)
        {
            string path = $"Assets/_Game/Prefabs/{data.displayName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                data.prefab = existing;
                return;
            }

            var go = GameObject.CreatePrimitive(prim);
            go.name = data.displayName + "Prefab";
            go.transform.localScale = scale;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<BuildItem>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            data.prefab = prefab;
            Object.DestroyImmediate(go);
        }
    }
}
#endif
