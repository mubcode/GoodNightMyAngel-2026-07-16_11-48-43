// =============================================================================
// PathWaypoint.cs
// =============================================================================
// CS tarzı waypoint sistemi:
//
// Bir "Waypoint" objesi (parent) -> altında sıralı "Point" objeleri (child).
// Sahneye bir "Waypoint" objesi koyarsın, sonra çocuklarına "Point" objeleri
// ekleyip sırayla güzergah oluşturursun. Yaratıklar sırayla bu point'leri takip
// eder.
//
// KULLANIM:
//   1) Boş GameObject oluştur -> "Waypoint_1" adı ver
//   2) "Add Component" -> Path Waypoint
//   3) Bu objenin altına (child) "Point_0", "Point_1", "Point_2" ekle
//   4) Point'leri sahnede güzergah boyunca yerleştir
//   5) PathManager.startPoints'e Waypoint_1'i sürükle
//   6) Yaratıklar Point_0 -> Point_1 -> Point_2 -> bed takip eder
//
// Alternatif: 'next' field'ı ile eski zincir sistemi de desteklenir.
// =============================================================================

using UnityEngine;
using System.Collections.Generic;

namespace GoodNightMyAngel.Enemies
{
    /// <summary>
    /// Bir waypoint grubu. İçindeki çocuk objeler (veya 'next' zinciri) sıralı
    /// noktaları oluşturur.
    /// </summary>
    public class PathWaypoint : MonoBehaviour
    {
        [Header("Bağlantı")]
        [Tooltip("Bir sonraki PathWaypoint. Boşsa bu objenin child'ları (Point_0, Point_1, ...) kullanılır.")]
        public PathWaypoint next;

        [Header("Davranış")]
        [Tooltip("Bu noktaya gelen yaratık kaç saniye beklesin.")]
        [Min(0f)] public float waitTime = 0f;

        [Tooltip("Yavaşlatma (1 = tam hız, 0 = dur).")]
        [Range(0f, 1f)] public float speedMultiplier = 1f;

        [Header("Görsel")]
        public Color gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        [Min(0.1f)] public float gizmoSize = 0.4f;

        [Tooltip("Çocuk point'leri de gizmo olarak çiz.")]
        public bool drawChildren = true;

        // -------------------------------------------------------------------------
        // CHILD-BASED YOL
        // -------------------------------------------------------------------------
        // Eğer 'next' null ise, bu objenin child'ları sıralı nokta olarak
        // kullanılır. Hiyerarşideki sıraya göre (siblings index artan).
        // -------------------------------------------------------------------------
        public List<Vector3> GetPathPoints()
        {
            var pts = new List<Vector3>();
            if (next != null)
            {
                // Eski stil: next zinciri
                var current = this;
                int safety = 100;
                while (current != null && safety-- > 0)
                {
                    pts.Add(current.transform.position);
                    current = current.next;
                }
            }
            else
            {
                // Yeni stil: bu objenin kendi pozisyonu + child'lar
                pts.Add(transform.position);
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    pts.Add(child.position);
                }
            }
            return pts;
        }

        private void OnDrawGizmos()
        {
            // Bu waypoint
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoSize);

            // Çocukları çiz
            if (drawChildren && next == null)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.6f);
                for (int i = 0; i < transform.childCount; i++)
                {
                    var c = transform.GetChild(i);
                    Gizmos.DrawWireCube(c.position, Vector3.one * gizmoSize * 0.7f);
                    // Sırayı göstermek için numara etiketi yerine
                    // bir sonraki child'a çizgi
                    if (i < transform.childCount - 1)
                    {
                        var n = transform.GetChild(i + 1);
                        Gizmos.DrawLine(c.position, n.position);
                    }
                }
            }

            // 'next' zinciri çiz
            if (next != null)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.6f);
                Gizmos.DrawLine(transform.position, next.transform.position);
            }
        }
    }
}
