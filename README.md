# LadyBugHitTheRoad

Локальный кооперативный (1–2 игрока) endless-runner про божью коровку на
трёхполосной дороге.

## Документация
- [docs/technical-details.md](docs/technical-details.md) — технические детали для
  ИИ-агентов: подключение к проекту, разработка (`Tools → Rebuild Scene`),
  механики, архитектура кода.
- [docs/game-brief.md](docs/game-brief.md) — бриф/анкета по игре.
- [docs/hardware-wiring.md](docs/hardware-wiring.md) — разводка Arduino,
  прошивки, Serial-протокол, меню и отладка железа.

## Структура
- `UnityProject/` — Unity-проект (скрипты в `Assets/Scripts`, генератор сцены
  в `Assets/Editor/SceneSetup.cs`).
- `ArduinoFirmware/` — прошивки CombinedBoard / GestureSensors / Joystick.
- `RawAssets/` — сырые исходники арта до конвертации в `Assets/Sprites`.
- `docs/` — документация.

## Быстрый старт
Открыть `UnityProject/` в Unity Hub (Unity **6000.0.78f1**), затем
**Tools → Rebuild Scene**. После правок только в `Assets/Scripts/*.cs` —
Unity подхватывает сама; после `SceneSetup.cs` — повторить Rebuild Scene.

**Отладочная цель забега:** сейчас **1 км** (`WinSequence.WinSegmentDistanceKm`);
релиз — 100 км.
