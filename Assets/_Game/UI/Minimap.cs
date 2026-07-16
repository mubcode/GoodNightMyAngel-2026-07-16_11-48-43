// =============================================================================
// Minimap.cs
// -----------------------------------------------------------------------------
// Sağ üst köşede küçük bir harita. Yatak, oyuncu ve tüm yaratıkları gösterir.
// Yaratıklar için yön oku (nereye gidiyor) çizilir.
//
// Inspector'dan:
//   - Harita boyutu (piksel)
//   - Ekran pozisyonu (sağ üst köşeden ofset)
//   - Yakınlaştırma (zoom): 1 dünya birimi = X piksel
//   - Yatak rengi, oyuncu rengi, yaratık rengi, boss rengi
//   - Çerçeve / arka plan
//   - Yön oku uzunluğu
// ayarlanabilir.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.World;
using GoodNightMyAngel.Enemies;
using GoodNightMyAngel.Player;

namespace GoodNightMyAngel.UI
{
    /// <summary>
    /// Ekranda sağ üstte küçük bir minimap. Yatak, oyuncu ve yaratıkları
    /// gerçek zamanlı gösterir. Yaratıklar yön oku ile nereye gittiklerini
    /// belirtir.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("Boyut / Konum")]
        [Tooltip("Minimap kare boyutu (piksel).")]
        [Min(80f)] public float size = 220f;

        [Tooltip("Ekranın sağ üst köşesinden uzaklık (piksel).")]
        public Vector2 screenOffset = new Vector2(20f, 20f);

        [Header("Ölçek")]
        [Tooltip("Minimap'in kapsadığı dünya yarıçapı (birim). 50 = 100x100 alan.")]
        [Min(10f)] public float worldRadius = 35f;

        [Tooltip("Minimap oyuncuyu merkez alır mı? False = harita merkez sabit.")]
        public bool followPlayer = true;

        [Header("Renkler")]
        public Color backgroundColor = new Color(0, 0, 0, 0.55f);
        public Color borderColor = new Color(0.7f, 0.6f, 0.4f, 0.8f);
        public Color bedColor = new Color(0.4f, 0.9f, 1f);
        public Color playerColor = new Color(0.3f, 1f, 0.4f);
        public Color enemyColor = new Color(1f, 0.3f, 0.3f);
        public Color bossColor = new Color(1f, 0.2f, 0.5f);
        public Color pathColor = new Color(0.3f, 0.7f, 0.4f, 0.5f);

        [Header("Yaratık Göstergesi")]
        [Tooltip("Yaratık nokta boyutu (piksel).")]
        [Min(2f)] public float enemyDotSize = 5f;
        [Tooltip("Boss nokta boyutu (piksel).")]
        [Min(3f)] public float bossDotSize = 9f;
        [Tooltip("Yaratık yön oku uzunluğu (piksel).")]
        [Min(2f)] public float arrowLength = 10f;
        [Tooltip("Yaratık yön oku kalınlığı (piksel).")]
        [Min(1f)] public float arrowWidth = 2f;
        [Tooltip("Oyuncu nokta boyutu (piksel).")]
        [Min(3f)] public float playerDotSize = 8f;
        [Tooltip("Yatak nokta boyutu (piksel).")]
        [Min(3f)] public float bedDotSize = 10f;

        // Durum
        private Texture2D _whiteTex;
        private Texture2D _dotTexEnemy;
        private Texture2D _dotTexBoss;
        private Texture2D _dotTexPlayer;
        private Texture2D _dotTexBed;

        private List<EnemyBase> _enemies = new List<EnemyBase>();
        private float _refreshTimer = 0f;

        private void Start()
        {
            // Basit doku: 1x1 beyaz (kare/çizgi için) ve yuvarlak noktalar
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
            _dotTexEnemy = CreateDotTexture(enemyColor, 32);
            _dotTexBoss = CreateDotTexture(bossColor, 48);
            _dotTexPlayer = CreateDotTexture(playerColor, 32);
            _dotTexBed = CreateDotTexture(bedColor, 32);
        }

        private void OnDestroy()
        {
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_dotTexEnemy != null) Destroy(_dotTexEnemy);
            if (_dotTexBoss != null) Destroy(_dotTexBoss);
            if (_dotTexPlayer != null) Destroy(_dotTexPlayer);
            if (_dotTexBed != null) Destroy(_dotTexBed);
        }

        // Yumuşak daire doku üret (antialias için)
        private Texture2D CreateDotTexture(Color c, int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            float r = size * 0.5f;
            Vector2 ctr = new Vector2(r, r);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), ctr) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            t.Apply();
            return t;
        }

        private void Update()
        {
            // Yaratık listesini güncelle (ucuz, sadece ref)
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 0.4f;
                _enemies.Clear();
                var all = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
                foreach (var e in all) if (e != null && !e.IsDead) _enemies.Add(e);
            }
        }

        private void OnGUI()
        {
            if (GameManager.Instance == null) return;

            // Ekranın sağ üst köşesine konumlandır
            float margin = screenOffset.x;
            float topMargin = screenOffset.y;
            float x = Screen.width - size - margin;
            float y = topMargin;

            // Arka plan
            var bgRect = new Rect(x, y, size, size);
            GUI.color = backgroundColor;
            GUI.DrawTexture(bgRect, _whiteTex);

            // Çerçeve (basit)
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(x, y, size, 1), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + size - 1, size, 1), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, 1, size), _whiteTex);
            GUI.DrawTexture(new Rect(x + size - 1, y, 1, size), _whiteTex);

            // Harita merkezi (player takip ediliyorsa)
            Vector3 center = Vector3.zero;
            var ph = FindFirstObjectByType<PlayerHealth>();
            if (followPlayer && ph != null) center = ph.transform.position;
            else if (GameManager.Instance.bed != null) center = GameManager.Instance.bed.transform.position;

            // Ölçek: size piksel = 2*worldRadius dünya birimi
            float scale = size / (2f * worldRadius);

            // Yollar (PathManager)
            if (PathManager.Instance != null && PathManager.Instance.PathCount > 0)
            {
                GUI.color = pathColor;
                for (int p = 0; p < PathManager.Instance.PathCount; p++)
                {
                    var path = PathManager.Instance.GetPath(p);
                    if (path == null || path.Count < 2) continue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Vector2 a = WorldToMap(path[i], center, scale, x, y);
                        Vector2 b = WorldToMap(path[i + 1], center, scale, x, y);
                        DrawLine(a, b, 1.5f);
                    }
                }
            }

            // Yatak
            if (GameManager.Instance.bed != null)
            {
                Vector2 mp = WorldToMap(GameManager.Instance.bed.transform.position, center, scale, x, y);
                if (IsInsideMap(mp, bgRect))
                {
                    float ds = bedDotSize;
                    var r = new Rect(mp.x - ds * 0.5f, mp.y - ds * 0.5f, ds, ds);
                    GUI.color = bedColor;
                    GUI.DrawTexture(r, _dotTexBed);
                }
            }

            // Yaratıklar + yön okları
            foreach (var e in _enemies)
            {
                if (e == null || e.IsDead) continue;
                Vector2 mp = WorldToMap(e.transform.position, center, scale, x, y);
                if (!IsInsideMap(mp, bgRect)) continue;

                // Nokta
                float ds = e.isBoss ? bossDotSize : enemyDotSize;
                var r = new Rect(mp.x - ds * 0.5f, mp.y - ds * 0.5f, ds, ds);
                GUI.color = e.isBoss ? bossColor : enemyColor;
                GUI.DrawTexture(r, e.isBoss ? _dotTexBoss : _dotTexEnemy);

                // Yön oku (nereye gidiyor)
                Vector3 dirW = e.GetNextWaypointDirection();
                if (dirW.sqrMagnitude > 0.01f)
                {
                    Vector2 dir2 = new Vector2(dirW.x, dirW.y);
                    Vector2 end = mp + dir2.normalized * arrowLength;
                    DrawLine(mp, end, arrowWidth);
                }
            }

            // Oyuncu
            if (ph != null)
            {
                Vector2 mp = WorldToMap(ph.transform.position, center, scale, x, y);
                float ds = playerDotSize;
                var r = new Rect(mp.x - ds * 0.5f, mp.y - ds * 0.5f, ds, ds);
                GUI.color = playerColor;
                GUI.DrawTexture(r, _dotTexPlayer);
            }

            // Başlık
            GUI.color = Color.white;
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.fontSize = 11;
            GUI.Label(new Rect(x, y - 2, size, 20), "MINIMAP", labelStyle);
        }

        // Dünya -> minimap ekran koordinatı
        private Vector2 WorldToMap(Vector3 world, Vector3 center, float scale, float mapX, float mapY)
        {
            float dx = (world.x - center.x) * scale;
            float dz = (world.z - center.z) * scale;
            // Dünya XZ -> ekran XY (Y yukarı, Z aşağı)
            return new Vector2(mapX + size * 0.5f + dx, mapY + size * 0.5f + dz);
        }

        private bool IsInsideMap(Vector2 p, Rect map)
        {
            return p.x >= map.x && p.x <= map.x + map.width &&
                   p.y >= map.y && p.y <= map.y + map.height;
        }

        // Kalın çizgi (kütük/piksel bazlı, OnGUI)
        private void DrawLine(Vector2 a, Vector2 b, float w)
        {
            // Çapraz çizgi için dik ve yatay segmentler
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;
            // Normalize
            float nx = -dy / len;
            float ny = dx / len;
            // 4 köşe
            Vector3[] verts = new Vector3[4];
            float h = w * 0.5f;
            verts[0] = new Vector3(a.x + nx * h, a.y + ny * h);
            verts[1] = new Vector3(b.x + nx * h, b.y + ny * h);
            verts[2] = new Vector3(b.x - nx * h, b.y - ny * h);
            verts[3] = new Vector3(a.x - nx * h, a.y - ny * h);
            // Dörtgen olarak çiz
            for (int i = 0; i < 4; i++)
            {
                Vector2 va = verts[i];
                Vector2 vb = verts[(i + 1) % 4];
                DrawThickSegment(va, vb);
            }
        }

        private void DrawThickSegment(Vector2 a, Vector2 b)
        {
            // Basit: yatay/dikey değilse birkaç ince çizgi çiz
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 0.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(a, b, t);
                GUI.DrawTexture(new Rect(p.x - 0.5f, p.y - 0.5f, 1f, 1f), _whiteTex);
            }
        }
    }
}
