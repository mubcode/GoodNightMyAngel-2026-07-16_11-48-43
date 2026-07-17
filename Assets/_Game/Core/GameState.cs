// =============================================================================
// GameState.cs
// -----------------------------------------------------------------------------
// Oyunun farklı durumlarını (gündüz mü, gece build mi, gece savaş mı, vb.)
// temsil eden enum ve yardımcı tipler.
//
// Tüm sistemler o anki durumu buradan okur; kendi aralarında dolaylı olarak
// haberleşir. Bu, "gece savaş bitince" gibi pek çok geçişi tek bir yerden
// yönetmeyi sağlar.
// =============================================================================

namespace GoodNightMyAngel.Core
{
    /// <summary>
    /// Oyundaki üst-düzey zaman dilimleri.
    /// </summary>
    public enum TimeOfDay
    {
        /// <summary>Gündüz: keşif, geliştirme, hikâye olayları.</summary>
        Day = 0,

        /// <summary>Gece build phase: barikat/tuzak yerleştirme.</summary>
        NightBuild = 1,

        /// <summary>Gece savunma: dalgalar geliyor, oyuncu savaşıyor.</summary>
        NightDefense = 2,

        /// <summary>Boss ile savaş (savunma fazının son parçası).</summary>
        NightBoss = 3,

        /// <summary>Gece bitti, sabah oluyor (kısa bir geçiş).</summary>
        Dawn = 4,
    }

    /// <summary>
    /// Oyunun genel durumu — pause, game over vb. için.
    /// </summary>
    public enum GameStatus
    {
        /// <summary>Normal oynanış.</summary>
        Playing = 0,

        /// <summary>Duraklatıldı (P tuşu ile).</summary>
        Paused = 1,

        /// <summary>Çocuk rüyada öldü — yatak canı sıfır.</summary>
        GameOver = 2,

        /// <summary>Geceyi başarıyla tamamladık (sabah oldu).</summary>
        NightCleared = 3,
    }

    /// <summary>
    /// Save/load sistemi için checkpoint verisi. Şimdilik sadece son başarılı
    /// günü tutuyoruz. İleride envanter, geliştirmeler vb. eklenebilir.
    /// </summary>
    [System.Serializable]
    public class CheckpointData
    {
        public int lastClearedDay = 0;        // En son başarıyla tamamlanan gün.
        public int playerCurrency = 0;        // Oyun içi para.
        public int playerUpgrades = 0;        // Yapılmış geliştirme sayısı (placeholder).
    }
}
