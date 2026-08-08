# LadyBugHitTheRoad

Локальный кооперативный (1–2 игрока) endless-runner про божью коровку на
дороге; число полос выбирается в меню (1–7, по умолчанию 3).

Это **игра 1 из 7** в мега-проекте аркадного автомата — остальные 6 слотов
пока пустые заглушки (см. раздел 3.0 в `docs/technical-details.md`). Отсюда
раскладка ассетов по папке на игру: `Assets/Scripts/lady_bug/`,
`Assets/Sprites/lady_bug/` и т.д.

## Лицензия
Код (`Assets/Scripts`, `Assets/Editor`, `Assets/Shaders`, `ArduinoFirmware`) —
**MIT**. Арт, звук и документация — **CC BY-NC 4.0**. Сторонние материалы
(mixkit, OpenGameArt, шрифт ComicCAT) — под своими лицензиями.
Подробности: [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Документация
- [docs/technical-details.md](docs/technical-details.md) — технические детали для
  ИИ-агентов: подключение к проекту, разработка (`Tools → Rebuild Scene`),
  механики, архитектура кода.
- [docs/game-brief.md](docs/game-brief.md) — бриф/анкета по игре.
- [docs/hardware-wiring.md](docs/hardware-wiring.md) — разводка Arduino,
  прошивки, Serial-протокол, меню и отладка железа.

## Структура
- `UnityProject/` — Unity-проект (рантайм-код в `Assets/Scripts/lady_bug/`,
  экран автомата в `Assets/Scripts/loader/`, генератор сцены в
  `Assets/Editor/SceneSetup.cs`).
- `ArduinoFirmware/` — прошивки CombinedBoard / GestureSensors / Joystick /
  SingleSensorTest.
- `RawAssets/` — сырые исходники арта до конвертации в `Assets/Sprites/lady_bug/`.
- `docs/` — документация.

## Быстрый старт
Открыть `UnityProject/` в Unity Hub (Unity **6000.5.3f1**), затем
**Tools → Rebuild Scene**. После правок только в `Assets/Scripts/*.cs` —
Unity подхватывает сама; после `SceneSetup.cs` — повторить Rebuild Scene.

**Цель забега:** **10 км** (`WinSequence.WinSegmentDistanceKm`) — столько же
добавляет каждое «продолжение» после финиша. Для отладки кат-сцены победы
ставится 1.
