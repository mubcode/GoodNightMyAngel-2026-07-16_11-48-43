// =============================================================================
// EnemyRoute.cs
// =============================================================================
// Yeni CS-tarzı rota sistemi.
//
// Bir EnemyRoute objesi (parent) şunlardan oluşur:
//   - Spawn Point (child #0): Yaratıkların doğduğu nokta
//   - Point_1, Point_2, ... (child #N): Yaratıkların sırayla takip edeceği noktalar
//   - End Point: yatak (rotanın sonu DEĞİL — yatak zaten sahnede ayrı bir obje).
//     Yaratıklar yatağa yeterince yaklaştıklarında saldırmaya başlar.
//
// Sahneye kurmak için:
//   1) Hierarchy > Create Empty > "Route_1" adı ver
//   2) Add Component > Enemy Route
//   3) Bu objenin altına child objeler ekle:
//        Spawn Point (yuvarlak, kırmızı, kuzeyde)
//        Point_1, Point_2, ... (cylinder, sarı, güzergah boyunca)
//   4) PathManager.routes listesine Route_1'i sürükle
//
// Yaratıklar:
//   - Spawn Point'te doğar
//   - Sırayla Point_1, Point_2, ...'e gider
//   - Yatak bedAttackRange içindeyse saldırmaya başlar (tüm noktaları geçmeden bile)
//
// Editor görselleştirme:
//   - OnDrawGizmos her frame Scene view'da:
//     * Spawn Point'i kırmızı küre olarak
//     * Point'leri sarı küp olarak
//     * Aralarındaki bağlantıyı beyaz çizgi olarak çizer
// =============================================================================

using UnityEngine;
using System.Collections.Generic;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Bir yaratık rotası: spawn noktası + sıralı point'ler. Yatak ayrı bir
    /// sahne objesi olarak kalır, yaratıklar bedAttackRange içindeyse saldırır.
    /// </summary>
    public class EnemyRoute : MonoBehaviour
    {
        [Header("Görsel")]
        [Tooltip("Sahne görünümünde spawn noktası rengi (kırmızı).")]
        public Color spawnColor = new Color(1f, 0.3f, 0.3f, 0.95f);

        [Tooltip("Sahne görünümünde point'lerin rengi (sarı).")]
        public Color pointColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        [Tooltip("Sahne görünümünde bağlantı çizgisi rengi (beyaz).")]
        public Color lineColor = new Color(1f, 1f, 1f, 0.6f);

        [Tooltip("Spawn noktası boyutu.")]
        [Min(0.2f)] public float spawnSize = 0.6f;

        [Tooltip("Point boyutu.")]
        [Min(0.2f)] public float pointSize = 0.5f;

        // Sıralı point listesi (child'lar). İlk child spawn noktası, geri kalanı point'ler.
        public List<Transform> OrderedPoints
        {
            get
            {
                var pts = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var c = transform.GetChild(i);
                    if (c != null) pts.Add(c);
                }
                return pts;
            }
        }

        /// <summary>İlk child (spawn noktası). Hiç child yoksa null.</summary>
        public Transform SpawnPoint
        {
            get
            {
                if (transform.childCount == 0) return null;
                return transform.GetChild(0);
            }
        }

        /// <summary>Point listesi (spawn hariç).</summary>
        public List<Transform> PathPoints
        {
            get
            {
                var pts = new List<Transform>();
                for (int i = 1; i < transform.childCount; i++)
                {
                    pts.Add(transform.GetChild(i));
                }
                return pts;
            }
        }

        /// <summary>Yolun tam pozisyon listesi (spawn dahil). Yatak hariç.</summary>
        public List<Vector3> GetPath()
        {
            var path = new List<Vector3>();
            foreach (var t in OrderedPoints) path.Add(t.position);
            return path;
        }

        private void OnDrawGizmos()
        {
            // Spawn noktası (kırmızı küre)
            var sp = SpawnPoint;
            if (sp != null)
            {
                Gizmos.color = spawnColor;
                Gizmos.DrawWireSphere(sp.position, spawnSize);
            }

            // Point'ler (sarı küp) ve aralarındaki çizgiler
            var all = OrderedPoints;
            Gizmos.color = pointColor;
            for (int i = 0; i < all.Count; i++)
            {
                if (i == 0 && all[i] == sp) continue;     // spawn'ı yukarıda çizdik
                Gizmos.DrawWireCube(all[i].position, Vector3.one * pointSize);
            }

            // Bağlantı çizgileri (spawn -> point_1 -> point_2 -> ...)
            if (all.Count >= 2)
            {
                Gizmos.color = lineColor;
                for (int i = 0; i < all.Count - 1; i++)
                {
                    Gizmos.DrawLine(all[i].position, all[i + 1].position);
                }
            }
        }
    }
}
