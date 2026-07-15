# LadyBugHitTheRoad

Локальный кооперативный (1–2 игрока) endless-runner про божью коровку на
трёхполосной дороге.

## Документация
- [docs/technical-details.md](docs/technical-details.md) — технические детали для
  ИИ-агентов: **как подключиться к проекту агентом** (MCP и запасной вариант
  через файловую систему), как устроена разработка (инструменты, ограничения,
  почему сцена собирается кодом через `Tools → Rebuild Scene`, а не руками),
  и как устроена сама игра (механики, архитектура, файл за файлом).
- [docs/game-brief.md](docs/game-brief.md) — бриф/анкета курса по игре.

## Структура
- `UnityProject/` — чистый Unity-проект, открывать через Unity Hub именно эту
  папку (скрипты в `UnityProject/Assets/Scripts`, генератор сцены в
  `UnityProject/Assets/Editor/SceneSetup.cs`).
- `RawAssets/` — сырые исходники арта (кадры из `.swf`, скачанные картинки) до
  конвертации в спрайты в `UnityProject/Assets/Sprites`.
- `docs/` — вся документация проекта.

## Быстрый старт
Открыть `UnityProject/` в Unity Hub (Unity 6000.0.78f1), затем **Tools →
Rebuild Scene** — соберёт `Assets/Scenes/Main.unity` с нуля из кода (сцена
не хранится вручную). После изменений в `SceneSetup.cs` — повторить Rebuild
Scene; после изменений только в `Assets/Scripts/*.cs` — Unity подхватывает
сама, пересборка сцены не нужна.
