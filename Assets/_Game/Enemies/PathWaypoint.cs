// =============================================================================
// PathWaypoint.cs
// -----------------------------------------------------------------------------
// Yaratık yolu üzerindeki tek bir nokta. Sahneye boş bir GameObject olarak
// eklenir ve sırasıyla birbirine bağlanır (PathManager tarafından otomatik).
//
// Inspector'dan:
//   - Bekleme süresi (yaratık bu noktada kaç saniye dursun)
//   - Yavaşlatma (0-1: 0 = tam hız, 1 = dur)
//   - Görsel renk
// ayarlanabilir.
// =============================================================================

using UnityEngine;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Yaratık yolu üzerinde bir nokta. Bir sonraki waypoint referansını tutar.
    /// PathManager, waypoint'leri otomatik sırayla takip edecek.
    /// </summary>
    public class PathWaypoint : MonoBehaviour
    {
        [Header("Bağlantı")]
        [Tooltip("Bir sonraki waypoint. Boşsa yolun sonu demektir (yatak).")]
        public PathWaypoint next;

        [Header("Davranış")]
        [Tooltip("Bu noktaya gelen yaratık kaç saniye beklesin.")]
        [Min(0f)] public float waitTime = 0f;

        [Tooltip("Yavaşlatma (1 = tam hız, 0 = dur). Yaratıklar bu noktada yavaşlar.")]
        [Range(0f, 1f)] public float speedMultiplier = 1f;

        [Header("Görsel")]
        [Tooltip("Sahne görünümünde noktanın rengi.")]
        public Color gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.9f);

        [Tooltip("Sahne görünümünde noktanın boyutu.")]
        [Min(0.1f)] public float gizmoSize = 0.4f;

        private void OnDrawGizmos()
        {
            // Noktayı yeşil küp olarak çiz
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoSize);

            // Bir sonraki waypoint'e çizgi
            if (next != null)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.6f);
                Gizmos.DrawLine(transform.position, next.transform.position);
            }
        }
    }
}
