# GoodNight My Angel — Geliştirici Notları

> Unity 6000.3.19f1 + URP 17.3.0 + Input System 1.19 ile geliştirilmiş,
> PSX/retro/pixel tarzında, hafif karanlık bir hayatta kalma oyunu.

## Hikâye Özeti

ABD'de yaşayan 3 kişilik bir aileyiz (anne, baba, çocuk). Her gece anne
çocuğu yatağına yatırır ve "Goodnight, my angel" der. Çocuk uykusunda
evlerinin uzağındaki ormandan akın akın gelen yaratıkları görür — bu
**rüya** döngüsünde, çocuğun **ruhu** bedeninden ayrılarak ormandan
gelen yaratıklara karşı savaşır. Amaç, yatağındaki **gerçek bedenini**
(canı olan yatağı) korumaktır.

- **Gündüz:** gerçek bedenimizi kontrol ederiz. Evde geliştirme yaparız.
- **Gece (build phase):** orman kenarından gelecek yaratıkları engellemek
  için barikat/tuzak/kule yerleştiririz. Yaratık yolu tırtıklı bir
  çizgi ile gösterilir.
- **Gece (savunma):** ruh formumuzla yaratıkları öldürürüz.
- **Boss:** bazı geceler son dalganın ardından büyük bir boss gelir.

### Ultimate: Call Mom
Her gece sadece **1 kez** kullanılabilir. Harita dışına yakın bir noktada
devasa bir kapı açılır, ışık hüzmesi tüm haritayı yıkar, annenin sesi
gelir: *"Honey, are you okay? I'm here for you."* Tüm düşmanlar anında
ölür. **Ama henüz spawn olmamış düşmanlar gelmeye devam eder** — zamanlama
önemli.

## Hızlı Başlangıç

1. Projeyi Unity **6000.3.19f1** ile aç.
2. `Assets/Scenes/SampleScene.unity` sahnesini aç.
3. Unity menüsünden: **GoodNight → Build Demo Scene** tıkla.
   - Bu komut tüm sistemleri (kamera, ışık, zemin, yatak, oyuncu, spawn
     noktaları, BuildManager, EnemySpawner, HUD, DebugConsole) sahnede
     oluşturur.
   - Ayrıca `Enemy`, `Boss`, `Barricade`, `Trap`, `Turret` prefab'larını
     ve `BuildItemData` assetlerini otomatik kaydeder.
4. **GoodNight → Add Layers and Tags** ile gerekli tag'leri ekle
   (Player, Enemy, EnemySpawn).
5. **Play** tuşuna bas.

## Kontroller

| Tuş | İşlev |
|-----|-------|
| WASD | Hareket |
| Sol Shift | Koşma (sprint) |
| Sağ analog / Mouse | Kamera ofset |
| Mouse wheel | Zoom in/out |
| Sol tık (build phase) | Eşya yerleştir |
| Sağ tık (build phase) | Eşya seç |
| 1 / 2 / 3 | Katalogdan eşya seç |
| R | Seçili eşyayı tamir et |
| Q | **Call Mom** ultimate |
| F1 | Debug HUD aç/kapa |
| F2 | Tüm debug'u aç/kapa |
| F3 | Gün/gece hızlı geçiş (test) |
| ` (backtick) | Debug konsolu aç |
| ESC / P | Duraklatma |

### Debug Konsolu Komutları

```
help                    Komut listesi
god                     Ölümsüz mod aç/kapa
day N                   N. güne atla
night                   Hemen geceye geç
day                     Hemen güne dön
wave                    Build phase'i atla
killall                 Tüm düşmanları öldür
heal                    Tüm canları doldur
```

## Klasör Yapısı

```
Assets/_Game/
├── Core/       GameManager, GameState, GameBootstrap
├── Camera/     TopDownCamera
├── Player/     PlayerController, PlayerHealth
├── Build/      BuildManager, BuildItem
├── Data/       BuildItemData (ScriptableObject)
├── Enemies/    EnemyBase, EnemySpawner
├── World/      Bed (yatak)
├── Skills/     CallMomSkill
├── UI/         GameHud (OnGUI)
├── Debug/      DebugOverlay, DebugConsole
├── Input/      (InputSystem_Actions buraya da taşınabilir)
├── Materials/
└── Prefabs/    (SceneBuilder tarafından otomatik oluşturulur)
```

## Inspector'dan Ayarlanabilir Tüm Değerler

- **GameManager:** gün/başlangıç durumu, build phase süresi ve alt sınırı,
  dalga sayıları, artış miktarları, boss prefab'ı, spawn noktaları, yatak
  referansı, geçiş süreleri.
- **Bed:** can, hasar aralığı, saldırı menzili, görsel flash.
- **PlayerController:** hız, sprint çarpanı, dönüş hızı, yer çekimi.
- **TopDownCamera:** yükseklik, mesafe, pitch, zoom sınırları, harita
  sınırları.
- **BuildManager:** grid boyutu, yarıçap, katalog (BuildItemData listesi),
  başlangıç parası, yol göstergesi renk/kalınlığı.
- **BuildItemData:** can, hasar, menzil, maliyet, kategori.
- **EnemyBase:** can, hasar, hız, hedef, saldırı menzili, boss bayrağı.
- **EnemySpawner:** prefab'lar, spawn aralığı, tag.
- **CallMomSkill:** kullanım/limit, kapı/ışık prefab'ları, ses klibi.
- **DebugOverlay:** kategori aç/kapa, renkler, HUD ayarları.
- **GameHud:** can çubuğu renk/pozisyon, font boyutu.

## Build Phase Akışı

1. GameManager `OnBuildPhaseStarted` event'ini tetikler (60s varsayılan).
2. BuildManager yolu çizer (LineRenderer, tırtıklı interpolasyon).
3. Oyuncu mouse ile grid üzerinde gezinir. Hücre yeşil (yerleştirilebilir)
   veya kırmızı (çakışma) boyanır.
4. 1/2/3 ile katalog seçilir. Sol tık → yerleştir. Sağ tık → seç.
5. Build phase süresi bittiğinde dalgalar başlar.
6. Yaratıklar orman kenarındaki spawn noktalarından gelir.
7. Oyuncu ruh formu ile yaratıkları öldürür.
8. Yaratıklar yatağa ulaşırsa yatağın canı azalır. Sıfıra düşerse
   **Game Over**.

## Dalga Sistemi

- `baseEnemiesPerWave` + (waveIndex-1) * `enemiesIncreasePerWave`
- Düşman canı: `maxHealth * (1 + (day-1) * enemyHealthMultiplierPerNight)`
- Her gece `wavesPerNight + (day-1)*wavesIncreasePerNight` dalga.
- Son dalgadan sonra boss spawn olur.

## Yapılacaklar (TODO)

- [ ] Gündüz ev etkileşimleri (gardrop, workbench, çöp, envanter UI)
- [ ] Pet sistemi (gündüz hediye → gece dönüşüm)
- [ ] Ses sistemi (ambiyan, ayak sesleri, savaş)
- [ ] PSX/pixel shader'ı (düşük çözünürlük, renk paleti, dithering)
- [ ] Düşman çeşitliliği (farklı AI ve özellikler)
- [ ] Hikâye diyalogları (anne "goodnight" sesleri)
- [ ] Save/load sistemi (JSON)
- [ ] Ebeveynlerin günlük pozisyon varyasyonu
- [ ] Multi-lane savunma (birden fazla yön)

## Mimari Notlar

- Her şey **Inspector'dan ayarlanabilir**. Hız, can, hasar, süre, renk,
  ses — hiçbir "magic number" kodda gömülü değil.
- **Debug log'ları** kategorize (12 kategori) ve renkli (Unity rich text).
  `DebugOverlay.cs` üzerinden her kategorinin açık/kapalı durumu
  Inspector'dan kontrol edilir.
- **Event-driven**: sistemler doğrudan birbirine bağımlı değil. Hep
  GameManager üzerinden geçer (`OnDayStarted`, `OnWaveStarted` vb.).
- **Singleton + DontDestroyOnLoad**: GameManager ve DebugOverlay sahneler
  arası yaşar.
- **Statik API**: `EnemySpawner.SpawnWave`, `Checkpoint.LastClearedDay`
  gibi statik metodlar/özellikler, kolay test edilebilirlik için.

## Lisans

Bu proje özel/kişisel bir geliştirmedir. Tüm hakları saklıdır.
