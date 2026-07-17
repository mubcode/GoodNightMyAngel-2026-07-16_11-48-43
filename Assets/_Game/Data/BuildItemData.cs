// =============================================================================
// BuildItemData.cs (ScriptableObject)
// -----------------------------------------------------------------------------
// Inspector'da oluşturulabilen (Create > GoodNight > Build Item Data) bir
// savunma elemanı tanımı. Maliyet, hasar, menzil, görsel prefab vb. burada
// tutulur. BuildItem tarafından referans alınır.
// =============================================================================

using UnityEngine;

namespace GoodNightMyAngel.Build
{
    /// <summary>
    /// Bir savunma elemanının şablonu. Birden fazla BuildItem instance'ı
    /// aynı BuildItemData'yı paylaşabilir (eşya tanımı gibi).
    /// </summary>
    [CreateAssetMenu(menuName = "GoodNight/Build Item Data", fileName = "NewBuildItem")]
    public class BuildItemData : ScriptableObject
    {
        [Header("Kimlik")]
        public string displayName = "Barikat";
        [TextArea] public string description = "Basit bir barikat.";
        public Sprite icon;

        [Header("Görsel")]
        [Tooltip("Yerleştirildiğinde instantiate edilecek prefab. Boşsa basit küp kullanılır.")]
        public GameObject prefab;

        [Header("İstatistikler")]
        [Min(1f)] public float maxHealth = 50f;
        [Min(0f)] public float damage = 0f;             // 0 = sadece blok (barikat)
        [Min(0.1f)] public float attackRange = 3f;
        [Min(0.1f)] public float attackInterval = 1f;
        [Min(0)] public int cost = 25;
        [Min(0f)] public float repairCostPerHp = 0.5f;

        [Header("Kategori")]
        [Tooltip("Sadece kategorize etmek için (UI filtreleme).")]
        public BuildItemCategory category = BuildItemCategory.Barricade;
    }

    public enum BuildItemCategory
    {
        Barricade,    // Pasif blok
        Trap,         // Temaslı patlama
        Turret,       // Menzilli otomatik saldırı
        Slow,         // Yavaşlatıcı
        Special,      // Özel (Call Mom vb.)
    }
}
