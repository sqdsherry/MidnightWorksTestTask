# Промпт 09a (C1) — Оболочка: сцены, загрузка, главное меню, настройки, пауза

**Ветка:** `feature/09a-shell` от `main` · **Папка:** основная (Unity открыт).
**Перед началом:** `CLAUDE.md`, `Docs/TZ.md` (UI/UX: главное меню, настройки, экран загрузки), `Docs/GDD.md` §10, `Docs/TDD.md` §3.3, §4.15–4.16, §4.19.

> **Параллельно** пользователь пишет модуль 07 (прогрессия) в отдельной папке: Domain/Services `Progression`, `ServiceTypeConfig` / `ServiceTypeSettings` (+XP), `GameConfig` (+progression), `ProgressionInstaller`, одна строка в `BuildingInstaller`, список инсталлеров в `GameplayEntryPoint.Enter`. **Эти файлы не трогать.** Если правка `GameplayEntryPoint` неизбежна — только сериализованные поля, без изменения списка инсталлеров. Свои вызовы — внутри `HudInstaller`.

---

## 1. Цель
Закрыть пункты ТЗ «Главное меню», «Настройки», «Экран загрузки» и паузу в игре. Поток сцен: `Boot → MainMenu → Gameplay`, обратно `Gameplay → MainMenu` из паузы.

## 2. Services (без UnityEngine)

### `Services/Scenes/`
```csharp
public enum GameScene { MainMenu, Gameplay }
public interface ISceneLoader
{
    bool IsLoading { get; }
    float Progress { get; }               // 0..1 of the current load
    void Load(GameScene scene);           // ignored while loading
    event Action<GameScene> LoadStarted;
    event Action<GameScene> LoadCompleted;
}
```

### `Services/Menu/MainMenuModel` (C#, тестируемый)
- ctor: `ISaveService`, `ISceneLoader`.
- `bool CanContinue => save.HasSave`.
- `void Continue()` → `Load(Gameplay)`.
- `NewGameResult NewGame()`:
  - сейв есть → `NeedsConfirmation`, ничего не делает;
  - сейва нет → удаляет сейв (на всякий случай) и `Load(Gameplay)` → `Started`.
- `void ConfirmNewGame()` → `save.Delete()` + `Load(Gameplay)`.

### `Services/Menu/PauseMenuModel` (C#, тестируемый)
- ctor: `IPauseService`, `ISceneLoader`.
- `IsOpen`, `Open()` (`pause.Push`), `Close()` (`pause.Pop`; повторный вызов безопасен), `ToMainMenu()` (`Close` + `Load(MainMenu)`).
- `// TODO(08b-save): save before leaving to the menu.`

## 3. Infrastructure — `Infrastructure/Scenes/UnitySceneLoader : ISceneLoader`
- Имена сцен — из конструктора (EntryPoint передаёт `"MainMenu"` / `"Gameplay"` из сериализованных полей).
- `SceneManager.LoadSceneAsync` + `Awaitable`, прогресс = `operation.progress / 0.9`.
- **Минимум 0.6 с** показа экрана загрузки (`// Why:` иначе он мигает). Время unscaled.
- `async void` только в одном месте, с try/catch и логом, как сейчас в `ProjectEntryPoint`.
- После загрузки `ProjectEntryPoint` находит `ISceneEntryPoint` в новой сцене и вызывает `Enter(container)` — **логика переезжает из `ProjectEntryPoint.Start` в загрузчик** (колбэк или событие `LoadCompleted`, на которое подписан `ProjectEntryPoint`).
- Перед загрузкой новой сцены `timeScale` должен быть 1: пауза из старой сцены не должна протечь. Если `IPauseService` на паузе — сбросить (добавить `IPauseService.ResetAll()` с XML-докой и `// Why:`).

## 4. Presentation

### 4.1 Экран загрузки — `Presentation/Loading/LoadingScreenView` (в сцене Boot, persistent)
- Свой Canvas (`sortingOrder` 100), `DontDestroyOnLoad` вместе с `[Project]`.
- Фон на весь экран, логотип-текст (название игры), прогресс-бар (Image Filled Horizontal), текст совета.
- `Show(string tip)`, `SetProgress(float)`, `Hide()` — с fade 0.2 с через `CanvasGroup`, unscaled.
- Советы — `[SerializeField] string[] _tips` (английский, 6–8 штук: «Hire a worker to automate a bay», «Blue pads open upgrades and hiring», «Storekeepers carry boxes for you» …).
- `LoadingScreenPresenter`: `LoadStarted` → `Show(random tip)` (через `IRandom`); `Progress` — в тике; `LoadCompleted` → `Hide`.

### 4.2 Главное меню — сцена `MainMenu`
- `MainMenuEntryPoint : MonoBehaviour, ISceneEntryPoint` (Bootstrap) — по образцу `GameplayEntryPoint`, но маленький, без инсталлеров.
- `MainMenuView`:
  - заголовок игры;
  - кнопки Continue / New Game / Settings / Quit (вертикальная колонка по центру);
  - Continue неактивна, если `!CanContinue`.
- `ConfirmDialogView` (переиспользуемый):
  - текст, кнопки Yes / No, затемнение фона;
  - для New Game текст «Start a new game? Your progress will be lost.».
- Quit: в Editor — `EditorApplication.isPlaying = false` под `#if UNITY_EDITOR`, иначе `Application.Quit()`.
- Фон меню: камера смотрит на простую сцену-заглушку (плоскость + пара кубов в палитре проекта). В D1 туда поставим ассеты.

### 4.3 Настройки — `Presentation/Settings/SettingsView` + `SettingsPresenter` (переиспользуется в меню и в паузе)
- Music / SFX — слайдеры 0..1 с процентом.
- Quality — Dropdown из `QualitySettings.names`.
- Fullscreen — Toggle.
- Resolution — Dropdown из `Screen.resolutions`: дедуп по w×h, по убыванию, текущее выбрано. Список строк строить **при открытии**, не в тике.
- Изменение → `ISettingsService.Set(current.With…)` сразу (без кнопки Apply). Back — закрыть.
- Подписи — из префаба. Числа — `TMP_Text.SetText`.

### 4.4 Пауза в Gameplay — `Presentation/Pause/`
- **Кнопка паузы** слева сверху в `ScreenHud` (по ТЗ: пауза слева, деньги справа).
- `PauseMenuView`: затемнение, панель с Resume / Settings / Main Menu / Quit.
- Открытие: кнопка или **Esc**. Закрытие: Resume или Esc.
- **Esc-роутер** — `Presentation/Controls/EscapeRouter` (C#, один на сцену, подписан на `GameplayInput.CancelPressed`):
  - стек `IEscapeHandler` (`bool TryHandleEscape()`);
  - Esc отдаётся верхнему обработчику, если его нет — открывается пауза;
  - открытые панели (`BuildPanelPresenter`, `PointPanelPresenter`, `StorekeeperOfferPresenter`) **регистрируются в роутере на Show и снимаются на Hide** вместо прямой подписки на `CancelPressed`;
  - итог: **Esc закрывает панель, если она открыта, иначе ставит паузу**;
  - на паузе Esc закрывает сначала настройки, потом паузу.
- Во время паузы клики по миру не проходят (уже так: `PlayerInputPresenter` проверяет `IsPaused`).

### 4.5 Стиль UI (пока без Kenney UI Pack, его подключим в D1)
- Палитра как у панелей A1/A2:
  - фон панелей `1E2430` α 235, затемнение `000000` α 140;
  - кнопки `43A047` (основная) / `3A4250` (вторичная) / `5A5F66` (disabled);
  - текст белый; заголовки 48 Bold, кнопки 26 Bold.
- Кнопки 360×64 с отступом 16; панели по центру.
- Hover — лёгкий scale 1.05 и подсветка (unscaled, корутина, без DOTween). Компонент `ButtonJuice` (Presentation/Ui) на всех кнопках.
- Все View: `Show()` сам включает цепочку объектов до корня (как `ScreenAnchoredPanel`).

## 5. Bootstrap
- `ProjectEntryPoint`:
  - регистрирует `ISceneLoader`, `LoadingScreenPresenter`;
  - первая сцена — `MainMenu`. Поле `_firstScene` (enum `GameScene`) вместо строки; для отладки в Editor можно поставить `Gameplay`;
  - закрыть `TODO(09-scenes-ui)`.
- `MainMenuEntryPoint` — собирает `MainMenuModel`, `SettingsPresenter`, view.
- В Gameplay — через `HudInstaller`: `PauseMenuModel`, `EscapeRouter`, `SettingsPresenter`, `PauseMenuPresenter`. Новые сериализованные ссылки — добавить на `GameplayEntryPoint` / в `GameplaySceneRefs`.

## 6. Editor — `AutoService/Setup/Run C1 Setup` (`Bootstrap/Editor/ModuleSetupC1.cs`)
Одна кнопка, идемпотентно (маркер `WhiteboxGenerated`), Undo:
1. Сохранить открытые сцены (спросить пользователя через `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo`; отказ → выход).
2. Создать сцену `Assets/_Project/Scenes/MainMenu.unity`, если её нет:
   - камера, свет;
   - фон-заглушка;
   - `[EntryPoint]` с `MainMenuEntryPoint`;
   - Canvas (Scale With Screen Size 1920×1080, match 0.5) + EventSystem (Input System UI module);
   - главное меню, диалог подтверждения, панель настроек — полная раскладка §4.5;
   - все ссылки — через `SerializedObject`.
3. Boot: экран загрузки (Canvas, фон, заголовок, бар, совет) + ссылки на `ProjectEntryPoint`; `_firstScene = MainMenu`.
4. Gameplay: кнопка паузы в `ScreenHud`, `PauseMenu`, вторая панель настроек (или общий префаб `Prefabs/UI/SettingsPanel.prefab`, переиспользуемый в обеих сценах — **предпочтительно**), ссылки на `[EntryPoint]`.
5. Build Settings: `Boot`, `MainMenu`, `Gameplay` — в этом порядке.
6. Сохранить сцены, вернуть исходную открытую сцену. Итоговый лог.

## 7. Тесты (EditMode)
- `MainMenuModelTests` (фейки `ISaveService`, `ISceneLoader`):
  - `CanContinue` ↔ `HasSave`;
  - `NewGame` без сейва → `Load(Gameplay)`;
  - `NewGame` с сейвом → `NeedsConfirmation` и ничего не загружено;
  - `ConfirmNewGame` → `Delete` + `Load`.
- `PauseMenuModelTests`:
  - `Open` / `Close` → `Push` / `Pop` ровно по разу;
  - двойной `Close` безопасен;
  - `ToMainMenu` → пауза снята + `Load(MainMenu)`.
- `EscapeRouterTests`: без обработчиков → пауза; с обработчиком → он, пауза нет; снятый обработчик не вызывается. Роутер — чистый C#: вход — `Action`/метод, не `GameplayInput`, чтобы его можно было тестировать.

## 8. Editor setup (пользователь) — в PR
1. **AutoService → Setup → Run C1 Setup** → без ошибок.
2. Play (стартует с Boot):
   - экран загрузки с советом → меню;
   - New Game → игра;
   - Esc при открытой панели закрывает панель, без панели — пауза;
   - Settings меняет громкость / качество / окно;
   - Main Menu → меню → Continue (активна, если есть сейв: сейв появится в 08b, пока может быть неактивна);
   - Quit останавливает Play.
3. Run All в Test Runner.

## 9. Git
Коммиты:
1. `Add scene loader and menu models`
2. `Add loading screen`
3. `Add main menu and confirm dialog`
4. `Add settings screen`
5. `Add pause menu and escape router`
6. `Wire shell into entry points`
7. `Add shell tests`
8. `Add C1 setup tool`

PR `09a: Shell (C1)` + Editor setup.
