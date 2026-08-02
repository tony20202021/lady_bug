# Celebration FX — сырые ассеты (конфетти / салют)

Скачано автоматически из бесплатных источников с прямой загрузкой.
Папка для выбора и импорта в Unity (`WinSequence`, Particle System и т.д.).

## Скачано ✅

### OpenGameArt (`opengameart/`)

| Файл | Автор | Лицензия | Описание |
|------|-------|----------|----------|
| `jellyfizh_Confetti.png` | jellyfizh | CC0 | Анимированный spritesheet конфетти 4096×4096, **альфа 0–255** |
| `davididev_confetti_spritesheet.png` | davididev | атрибуция `davididev` | Белые частицы конфетти 768×768, перекрашиваются в Unity |
| `party_confetti_sprite10_strip10.png` | OpenGameArt | свободно | 10 кадров, полоска 640×64, pixel confetti |
| `Firework_spritesheet.png` | OpenGameArt | см. страницу | Большой spritesheet салюта 1536×1280, **альфа** |
| `pixel_fireworks_4colors.zip` | myriad / Stealthix-style | CC0 (салют) | 4 цвета: red/blue/yellow/violet, кадры `firework_red0..7.png` |

Распаковано: `pixel_fireworks_4colors_extracted/`

### Kenney (`kenney/`)

| Файл | Лицензия | Описание |
|------|----------|----------|
| `kenney_particlePack.zip` | CC0 | 80+ PNG 512×512: circle, star, spark, magic, flare, fire… |
| `kenney_particlePack_extracted/` | | Отдельные PNG + Unity sample package внутри zip |

**Примечание:** у Kenney частицы часто на чёрном фоне (альфа везде 255) — в Unity использовать **Additive** / **Alpha Blend** материал.

### GitHub (`github/`)

| Файл | Описание |
|------|----------|
| `calinou_kenney-particle-pack.zip` | Тот же Kenney pack, упакован для Godot (дубликат, можно не использовать) |

---

## Не удалось скачать автоматически ⚠️ (только itch.io, нужен браузер)

Itch.io отдаёт файлы только после «покупки» за $0 — curl/API не работает без сессии.

| Пак | Ссылка | Размер | Лицензия |
|-----|--------|--------|----------|
| **Stealthix Animated Fireworks** (оригинал 164 KB) | https://stealthix.itch.io/animated-fireworks | 164 KB | CC0 |
| **Kronbits 1000 Particles** | https://kronbits.itch.io/particle-pack | 92 MB | CC0 |
| **Mochi Lab Game FX Starter Vol.1** | https://mochilab-studio.itch.io/game-fx-starter-vol1 | 94 MB | Royalty-free |
| **CodeManu VFX Free Pack** | https://codemanu.itch.io/vfx-free-pack | 90 MB | Public domain |
| **Pixogen Pixel RPG VFX Lite** (есть fireworks 64×64) | https://pixogenassets.itch.io/pixel-art-rpg-vfx-lite | ~150 KB | Free |
| **pewas Pixel RPG VFX Pack** | https://pewas.itch.io/pixel-rpg-vfx-pack-free-animated-effects | 23 MB | Royalty-free |

Положить сюда: `RawAssets/CelebrationFX/itchio/` после ручной загрузки.

---

## Рекомендации для экрана победы

1. **Дождь конфетти:** `jellyfizh_Confetti.png` или Kenney `circle_*/star_*` + Particle System.
2. **Вспышки салюта:** `pixel_fireworks_4colors_extracted/` или `Firework_spritesheet.png`.
3. **UI-эффект «milestone»:** после ручной загрузки — Mochi Lab Starter (эффект `milestone` / confetti celebration).

## Импорт в Unity

1. Скопировать выбранные PNG в `UnityProject/Assets/Sprites/` или `Assets/VFX/`.
2. Texture Type: **Sprite (2D and UI)** или **Default** для Particle System.
3. Spritesheet → **Sprite Editor** → Slice (Grid By Cell / Automatic).
4. Particle System → **Texture Sheet Animation** + материал с прозрачностью.
