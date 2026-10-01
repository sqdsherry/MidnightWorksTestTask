# CLAUDE.md — Auto Service Tycoon

3D Idle Tycoon про автосервис. Тестовое задание Unity Developer. **Unity 6000.3.17f1**, URP 17.3, PC (landscape), UI на английском. Дедлайн — 3–4 дня (старт 2026-09-30).

## Источники правды — читать перед работой
- `Docs/TZ.md` — исходное ТЗ (не редактировать требования) + чек-лист сдачи.
- `Docs/GDD.md` — геймдизайн, скоуп (§0.1), лог решений.
- `Docs/TDD.md` — архитектура, контракты модулей, процесс, roadmap со статусами.
- `Docs/Prompts/NN-*.md` — задание на текущий модуль.

Если задача противоречит этим документам или неясна — **остановись и спроси**, не додумывай.

## Роли
- **Архитектор** (отдельная сессия Claude): проектирует, пишет промпты в `Docs/Prompts/`, ревьюит, ведёт Docs.
- **Кодер** (отдельная сессия Claude): пишет **весь** код по промпту, в feature-ветке.
- **Пользователь**: Editor setup в Unity (сцены, префабы, ассеты, NavMesh), ревью, мерж PR.

## Жёсткие правила
1. **Никаких сторонних библиотек/пакетов** (Zenject, UniTask, DOTween, Odin, MCP-мосты и т.п.). Только `com.unity.*` и чистый C#. Не добавлять пакеты в `Packages/manifest.json` без согласования.
2. `Domain` и `Services` — `noEngineReferences: true`: **никакого `UnityEngine`** там. Логи — через `IGameLogger`.
3. Нет синглтонов, статического изменяемого состояния, `FindObjectOfType`, `GameObject.Find`. Зависимости — через конструктор / `Construct(...)` / `Enter(...)`. `ServiceContainer` используется **только в EntryPoint'ах**. Исключение — Editor-утилиты.
4. Ноль аллокаций в `Tick`: без LINQ, замыканий, boxing, конкатенации строк, `GetComponent`. Единственный геймплейный `Update` — `GameLoop`.
5. Каждая подписка на событие имеет отписку.
6. **Код и комментарии — на английском.** XML-доки (`///`) на всём public/protected API; неочевидное — `// Why: ...`. Комментированный код — критерий оценки ТЗ.
7. Стиль: `_camelCase` приватные поля, `[SerializeField] private`, один тип — один файл, `sealed` по умолчанию, явные модификаторы.
8. UI-строки — не хардкодом в логике (конфиги/префабы).

## Архитектура (кратко, детали — TDD §1–4)
```
Presentation ──► Services ──► Domain
Infrastructure ──► Services, Domain   (реализует интерфейсы Services)
Bootstrap ──► все (Composition Root)
```
- Код: `Assets/_Project/Scripts/{Domain,Services,Infrastructure,Presentation,Bootstrap}`, тесты: `Assets/_Project/Tests/EditMode`.
- Namespace = `AutoService.<Слой>.<Папка>`.
- Старт: сцена `Boot` (`ProjectEntryPoint`, project-контейнер) → грузит `Gameplay` → `ISceneEntryPoint.Enter(parent)` → `GameplayEntryPoint` собирает сценовые сервисы. Play Mode всегда стартует с Boot (Editor-утилита).
- Сквозные события — `IEventBus` (`readonly struct` события); локальные — C# `event Action<...>` на сервисе.
- Пауза — `IPauseService` (счётчик, `timeScale`); UI-анимации на unscaled time.

## Ловушки имён
- Не называть namespace/папки `Application`, `Time`, `Random`, `Debug`, `Physics`, `Object`, `Camera`, `Input`, `PlayerInput` — они перекрывают типы `UnityEngine` / Input System внутри `AutoService.*` (поэтому: слой `Services`, папки `Timing`, `Randomness`, `CameraControl`, `Controls`).
- `System.Random` vs `UnityEngine.Random` — в Infrastructure писать явно.

## Unity и git
- **Кодер не создаёт `.meta`, сцены, `.asset`, префабы** — их создаёт Unity/пользователь. В каждом PR — раздел **Editor setup** с шагами для пользователя.
- Пользователь докоммичивает в ту же ветку сгенерированные `.meta`, сцены, SO, префабы — **до мержа** (иначе ломаются ссылки на скрипты у проверяющего).
- Удалять/переименовывать ассеты лучше через окно Project в Unity (открытая сцена может пересохраниться обратно).
- Ветки: `feature/NN-name` от `main` → PR в `main` → ревью → merge-коммит (`--no-ff`, делает архитектор локально; GitHub сам закрывает PR). Прямых коммитов в `main` нет (кроме Docs).
- Мерж при открытом Unity — без `git checkout` туда-обратно (иначе Unity переимпортирует исчезнувшие на секунду файлы): merge-коммит через `git commit-tree <feature>^{tree} -p main -p <feature>` + `update-ref`, когда `main` — предок ветки.
- **Одна рабочая папка на всех:** пока открыта feature-ветка, архитектор не коммитит Docs (коммит уйдёт в чужую ветку).
- Логи Unity (Console, компиляция): `%LOCALAPPDATA%\Unity\Editor\Editor.log`. Шум `ExecutionEngineException: String conversion error` от `QuickInstaller` — из-за кириллицы в пути проекта, на игру не влияет.
- Результаты Test Runner (после Run All в Editor): `%USERPROFILE%\AppData\LocalLow\DefaultCompany\MidnightWorksTestTask\TestResults.xml` — архитектор читает их сам.
- Не коммитить `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`.
