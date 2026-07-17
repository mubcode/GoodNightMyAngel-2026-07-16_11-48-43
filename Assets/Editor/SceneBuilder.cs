// =============================================================================
// SceneBuilder.cs (Editor-only)
// -----------------------------------------------------------------------------
// GoodNight My Angel sahnesini hızlıca kurmak için editor menüsü.
//
// Unity menüsü:
//   GoodNight > 1) Add Required Tags   (mutlaka önce bunu çalıştır)
//   GoodNight > 2) Build Demo Scene
//
// Build Demo Scene artık "akıllı mod" çalışır:
//   - Sahneyi SİLMEZ. Mevcut objelere dokunmaz (pozisyon, materyal, ek objeler
//     hepsi korunur).
//   - Eksik olan manager / HUD / minimap / PathManager / Route objelerini ekler.
//   - BuildItemData ve prefab asset'lerini gerektiğinde oluşturur, varsa kullanır.
//   - Kullanıcının Hierarchy'de yaptığı manuel değişiklikler (ör. EnemySpawnPoints
//     silinmesi, kendi Route_X objeleri, farklı materyaller) KORUNUR.
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
        // MENU: Build Demo Scene (AKILLI MOD)
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

            Debug.Log("[SceneBuilder] Akıllı mod: mevcut objelere dokunulmadan eksikler ekleniyor...");

            // Prefabs klasörünü garanti et
            EnsureFolder("Assets/_Game/Prefabs");
            EnsureFolder("Assets/_Game/Data");

            // -----------------------------------------------------------------
            // 1) Zemin — yoksa oluştur, varsa DOKUNMA
            // -----------------------------------------------------------------
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(8, 1, 8); // 80x80
                var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                groundMat.color = new Color(0.25f, 0.35f, 0.18f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            }

            // -----------------------------------------------------------------
            // 2) Yatak — yoksa oluştur, varsa Bed component'i ekle (dokunma)
            // -----------------------------------------------------------------
            var bed = GameObject.Find("Bed");
            if (bed == null)
            {
                bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bed.name = "Bed";
                bed.transform.position = new Vector3(0, 0.4f, 0);
                bed.transform.localScale = new Vector3(1.2f, 0.5f, 2.0f);
                var bedMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bedMat.color = new Color(0.6f, 0.4f, 0.3f);
                bed.GetComponent<Renderer>().sharedMaterial = bedMat;
            }
            var bedComp = bed.GetComponent<Bed>();
            if (bedComp == null) bedComp = bed.AddComponent<Bed>();

            // -----------------------------------------------------------------
            // 3) Oyuncu — yoksa oluştur, varsa component'leri kontrol et
            // -----------------------------------------------------------------
            var player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = new Vector3(2, 0.9f, 0);
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.35f;
                cc.center = new Vector3(0, 0.9f, 0);

                var playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerVisual.transform.SetParent(player.transform, false);
                playerVisual.transform.localPosition = new Vector3(0, 0.9f, 0);
                playerVisual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                Object.DestroyImmediate(playerVisual.GetComponent<Collider>());
                var pMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pMat.color = new Color(0.3f, 0.6f, 0.9f);
                playerVisual.GetComponent<Renderer>().sharedMaterial = pMat;

                var aimIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                aimIndicator.name = "AimIndicator";
                var aimCol = aimIndicator.GetComponent<Collider>();
                if (aimCol != null) Object.DestroyImmediate(aimCol);
                aimIndicator.transform.SetParent(player.transform, false);
                aimIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
                aimIndicator.transform.localPosition = new Vector3(0, 0.5f, 0.7f);
                aimIndicator.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                var aimMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                aimMat.color = new Color(1f, 0.95f, 0.3f, 1f);
                aimIndicator.GetComponent<Renderer>().sharedMaterial = aimMat;

                var aimTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aimTip.name = "AimTip";
                var tipCol = aimTip.GetComponent<Collider>();
                if (tipCol != null) Object.DestroyImmediate(tipCol);
                aimTip.transform.SetParent(player.transform, false);
                aimTip.transform.localPosition = new Vector3(0, 0.5f, 1.3f);
                aimTip.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                var tipMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                tipMat.color = new Color(1f, 0.5f, 0.2f, 1f);
                aimTip.GetComponent<Renderer>().sharedMaterial = tipMat;
            }
            // Player component'lerini her zaman kontrol et (idempotent)
            if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerController>() == null) player.AddComponent<PlayerController>();
            if (player.GetComponent<Player.Weapon>() == null) player.AddComponent<Player.Weapon>();
            if (player.GetComponent<CallMomSkill>() == null) player.AddComponent<CallMomSkill>();

            // -----------------------------------------------------------------
            // 4) Kamera — yoksa oluştur
            // -----------------------------------------------------------------
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
                cam.transform.position = new Vector3(0, 12, -8);
                cam.transform.rotation = Quaternion.Euler(55, 0, 0);
                if (Object.FindFirstObjectByType<AudioListener>() == null)
                    camGo.AddComponent<AudioListener>();
                var tdc = camGo.AddComponent<TopDownCamera>();
                tdc.target = player.transform;
            }

            // -----------------------------------------------------------------
            // 5) Güneş ışığı — yoksa oluştur
            // -----------------------------------------------------------------
            if (GameObject.Find("Sun") == null)
            {
                var sun = new GameObject("Sun");
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                light.color = new Color(1f, 0.95f, 0.85f);
                sun.transform.rotation = Quaternion.Euler(50, 30, 0);
            }

            // -----------------------------------------------------------------
            // 6) NavMesh — ground'a ekle (yoksa)
            // -----------------------------------------------------------------
            var nav = ground.GetComponent<NavMeshSurface>();
            if (nav == null) nav = ground.AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.All;
            nav.BuildNavMesh();

            // -----------------------------------------------------------------
            // 7) BuildItemData (asset) — her zaman yükle
            // -----------------------------------------------------------------
            var barricade = CreateOrLoadBuildItemData("Assets/_Game/Data/Barricade.asset",
                "Barikat", 80, 0, 15, BuildItemCategory.Barricade, 0f, 1f);
            var trap = CreateOrLoadBuildItemData("Assets/_Game/Data/Trap.asset",
                "Tuzak", 30, 25, 30, BuildItemCategory.Trap, 1.5f, 0.8f);
            var turret = CreateOrLoadBuildItemData("Assets/_Game/Data/Turret.asset",
                "Kule", 60, 8, 50, BuildItemCategory.Turret, 6f, 0.6f);

            // -----------------------------------------------------------------
            // 8) BuildManager — yoksa oluştur, katalog boşsa doldur
            // -----------------------------------------------------------------
            var bmGo = GameObject.Find("BuildManager");
            if (bmGo == null) bmGo = new GameObject("BuildManager");
            var bm = bmGo.GetComponent<BuildManager>();
            if (bm == null) bm = bmGo.AddComponent<BuildManager>();
            if (bm.gridCenter == null) bm.gridCenter = bed.transform;
            if (bm.catalog.Count == 0)
            {
                bm.catalog.Add(barricade);
                bm.catalog.Add(trap);
                bm.catalog.Add(turret);
            }

            // -----------------------------------------------------------------
            // 9) EnemySpawner — yoksa oluştur
            // -----------------------------------------------------------------
            var spawner = GameObject.Find("EnemySpawner");
            if (spawner == null) spawner = new GameObject("EnemySpawner");
            var es = spawner.GetComponent<EnemySpawner>();
            if (es == null) es = spawner.AddComponent<EnemySpawner>();
            es.spawnPointTag = "EnemySpawn";

            // -----------------------------------------------------------------
            // 10) Enemy prefab'ları (asset)
            // -----------------------------------------------------------------
            string enemyPath = "Assets/_Game/Prefabs/Enemy.prefab";
            GameObject enemyPrefab = CreateOrLoadEnemyPrefab(enemyPath, "EnemyPrefab",
                new Vector3(0.6f, 0.8f, 0.6f), new Color(0.4f, 0.1f, 0.1f),
                0.3f, 1.6f, 2.5f, 25, 5, 1f, false);
            es.normalEnemyPrefab = enemyPrefab;

            string bossPath = "Assets/_Game/Prefabs/Boss.prefab";
            GameObject bossPrefab = CreateOrLoadEnemyPrefab(bossPath, "BossPrefab",
                new Vector3(1.5f, 1.8f, 1.5f), new Color(0.7f, 0.1f, 0.3f),
                0.6f, 2.5f, 1.8f, 250, 12, 1.5f, true);
            es.bossPrefab = bossPrefab;

            // -----------------------------------------------------------------
            // 11) GameManager — yoksa oluştur, referansları bağla
            // -----------------------------------------------------------------
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                var go = new GameObject("GameManager");
                gm = go.AddComponent<GameManager>();
            }
            if (gm.bed == null) gm.bed = bedComp;
            if (gm.bossPrefab == null) gm.bossPrefab = bossPrefab;
            // enemySpawnPoints KULLANICIYA BIRAKILDI: kendi dizisini atayabilir
            // (boşsa PathManager.GetAllSpawnPoints() EnemySpawner tarafından kullanılır)

            // -----------------------------------------------------------------
            // 12) HUD / Konsol / Minimap / Paneller — her biri idempotent
            // -----------------------------------------------------------------
            if (GameObject.Find("GameHud") == null)
            {
                var hudGo = new GameObject("GameHud");
                hudGo.AddComponent<GameHud>();
            }
            if (GameObject.Find("DebugConsole") == null)
            {
                var dcGo = new GameObject("DebugConsole");
                dcGo.AddComponent<DebugConsole>();
            }
            if (GameObject.Find("Minimap") == null)
            {
                var mapGo = new GameObject("Minimap");
                mapGo.AddComponent<Minimap>();
            }
            if (GameObject.Find("ControlsPanel") == null)
            {
                var cpGo = new GameObject("ControlsPanel");
                cpGo.AddComponent<ControlsPanel>();
            }
            if (GameObject.Find("PausePanel") == null)
            {
                var ppGo = new GameObject("PausePanel");
                ppGo.AddComponent<PausePanel>();
            }

            // -----------------------------------------------------------------
            // 13) PathManager + EnemyRoute
            // -----------------------------------------------------------------
            var pathMgr = GameObject.Find("PathManager");
            if (pathMgr == null) pathMgr = new GameObject("PathManager");
            var pm = pathMgr.GetComponent<PathManager>();
            if (pm == null) pm = pathMgr.AddComponent<PathManager>();
            if (pm.bed == null) pm.bed = bedComp;
            if (pm.routes == null) pm.routes = new System.Collections.Generic.List<EnemyRoute>();

            // Örnek Route_1 — sadece hiç route yoksa oluştur
            if (pm.routes.Count == 0)
            {
                GameObject route1 = GameObject.Find("Route_1");
                if (route1 == null)
                {
                    route1 = new GameObject("Route_1");
                    route1.transform.position = new Vector3(0, 0, 8);
                    var route1Comp = route1.AddComponent<EnemyRoute>();

                    var spawnPt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    spawnPt.name = "Spawn Point";
                    spawnPt.transform.SetParent(route1.transform, false);
                    spawnPt.transform.position = route1.transform.position + new Vector3(-3, 0, 12);
                    var spCol = spawnPt.GetComponent<Collider>();
                    if (spCol != null) Object.DestroyImmediate(spCol);
                    var spMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    spMat.color = new Color(1f, 0.3f, 0.3f);
                    spawnPt.GetComponent<Renderer>().sharedMaterial = spMat;
                    spawnPt.transform.localScale = Vector3.one * 0.6f;

                    Vector3[] pointOffsets = new Vector3[]
                    {
                        new Vector3(-2, 0, 7),
                        new Vector3(-1, 0, 3),
                        new Vector3( 0, 0, 1),
                    };
                    for (int i = 0; i < pointOffsets.Length; i++)
                    {
                        var pt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        pt.name = $"Point_{i + 1}";
                        pt.transform.SetParent(route1.transform, false);
                        pt.transform.position = route1.transform.position + pointOffsets[i];
                        pt.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
                        var col = pt.GetComponent<Collider>();
                        if (col != null) Object.DestroyImmediate(col);
                        var ptMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        ptMat.color = new Color(1f, 0.85f, 0.2f);
                        pt.GetComponent<Renderer>().sharedMaterial = ptMat;
                    }
                    pm.routes.Add(route1Comp);
                }
                else
                {
                    var c = route1.GetComponent<EnemyRoute>();
                    if (c != null) pm.routes.Add(c);
                }
            }

            // -----------------------------------------------------------------
            // 14) BuildItem prefab'ları
            // -----------------------------------------------------------------
            CreateBuildPrefab(barricade, PrimitiveType.Cube, new Vector3(0.8f, 0.6f, 0.8f),
                new Color(0.5f, 0.35f, 0.2f));
            CreateBuildPrefab(trap, PrimitiveType.Cube, new Vector3(0.5f, 0.2f, 0.5f),
                new Color(0.6f, 0.2f, 0.2f));
            CreateBuildPrefab(turret, PrimitiveType.Cylinder, new Vector3(0.3f, 0.6f, 0.3f),
                new Color(0.4f, 0.4f, 0.5f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] Akıllı mod kurulumu TAMAMLANDI. " +
                      "Mevcut objeler korundu, eksikler eklendi. Play'e bas ve test et.");
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
