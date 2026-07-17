// =============================================================================
// Projectile.cs
// -----------------------------------------------------------------------------
// Savunma birimlerinden çıkan görsel mermiler (projectile). Inspector'dan:
//   - Hız
//   - Maksimum menzil
//   - Hasar
//   - Hedef (EnemyBase)
//   - Görsel mesh/material (null ise küre oluşturulur)
//   - Çarpma VFX
// ayarlanabilir.
//
// Hedefe ulaşınca veya menzil bitince yok olur, hedefe hasar verir.
// =============================================================================

using UnityEngine;
using GoodNightMyAngel.Core;
using GoodNightMyAngel.Enemies;

namespace GoodNightMyAngel.Combat
{
    /// <summary>
    /// Görsel ve fiziksel bir mermi. Spawner tarafından tetiklenir.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("Hareket")]
        [Tooltip("Mermi hızı (birim/saniye).")]
        [Min(0.1f)] public float speed = 18f;

        [Tooltip("Maksimum menzil (saniye olarak da düşünülebilir).")]
        [Min(0.1f)] public float maxLifetime = 2f;

        [Header("Hasar")]
        public float damage = 8f;

        [Header("Hedef")]
        [Tooltip("Mermi otomatik olarak EnemyBase.Hasar alır.")]
        public bool applyDamage = true;

        [Header("Görsel")]
        [Tooltip("Mermi mesh'i. Boşsa küre oluşturulur.")]
        public Transform visual;

        [Tooltip("Çarpma VFX prefab'ı.")]
        public GameObject hitVfxPrefab;

        // Dahili
        private EnemyBase _target;
        private Vector3 _startPos;
        private float _spawnTime;
        private TrailRenderer _trail;

        public void Setup(EnemyBase target, float dmg, Color color)
        {
            _target = target;
            damage = dmg;
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                visual.SetParent(transform, false);
                visual.localScale = Vector3.one * 0.18f;
                var col = visual.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var r = visual.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    mat.color = color;
                    r.sharedMaterial = mat;
                }
                _trail = visual.gameObject.AddComponent<TrailRenderer>();
                _trail.time = 0.3f;
                _trail.startWidth = 0.12f;
                _trail.endWidth = 0.01f;
                var tmat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                tmat.color = color;
                _trail.material = tmat;
                _trail.minVertexDistance = 0.05f;
            }
        }

        private void Start()
        {
            _startPos = transform.position;
            _spawnTime = Time.time;
        }

        private void Update()
        {
            if (_target == null || _target.IsDead) { Destroy(gameObject); return; }
            if (Time.time - _spawnTime > maxLifetime) { Destroy(gameObject); return; }
            if (Vector3.Distance(_startPos, transform.position) > 30f) { Destroy(gameObject); return; }

            Vector3 dir = (_target.transform.position + Vector3.up * 0.5f) - transform.position;
            float dist = dir.magnitude;
            if (dist < 0.3f)
            {
                Hit();
                return;
            }
            dir /= dist;
            transform.position += dir * speed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Hit()
        {
            if (applyDamage && _target != null && !_target.IsDead)
            {
                _target.TakeDamage(damage);
                if (DebugOverlay.Instance != null)
                    DebugOverlay.Instance.Log(LogCategory.Combat,
                        $"Mermi isabet: -{damage:F1} -> {_target.name}", false);
            }
            if (hitVfxPrefab != null) Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
