# Промпт 09b (фикс C1) — замечания ревью

**Ветка:** та же `feature/09a-shell`. Правила `CLAUDE.md`. Файлы модуля 07 по-прежнему не трогать. Закоммить этот файл вместе с фиксами.

## Рантайм

### 1. Сцена меняется раньше, чем экран загрузки стал непрозрачным — `UnitySceneLoader` + `LoadingScreenView`
`LoadSceneAsync` стартует в том же кадре, что `Show()` (fade-in 0.2 с). Маленькая сцена активируется за пару кадров, и игрок видит резкую смену сцены под полупрозрачным экраном.

Исправить:
- `allowSceneActivation = false`, пока fade-in не завершён;
- `Show()` возвращает / сообщает момент полной непрозрачности: `bool IsOpaque` или колбэк;
- прогресс до активации и так упирается в 0.9 (деление на 0.9 уже есть).

`// Why:` в коде.

### 2. Экран загрузки висит вечно, если загрузка упала — `UnitySceneLoader` (~654–661)
Сцены нет в Build Settings или `LoadSceneAsync` бросил исключение → `LoadCompleted` не приходит, экран остаётся с `blocksRaycasts = true`. На первом запуске игра «зависает».

Исправить:
- в `catch` сбросить `IsLoading`, `allowSceneActivation`;
- поднять новое событие `ISceneLoader.LoadFailed(GameScene)`;
- `LoadingScreenPresenter` на нём прячет экран;
- лог с именем сцены и подсказкой про Build Settings.

### 3. Загрузчик может заблокировать сам себя — `UnitySceneLoader` (~620–626)
`IsLoading = true` выставляется до `_pause.ResetAll()` и `LoadStarted?.Invoke`, вне try. Исключение у подписчика → `Run` не стартует, `IsLoading` навсегда true. Перенести эти вызовы внутрь try у `Run`, флаг сбрасывать в `finally` / `catch`.

### 4. Слайдеры громкости дёргают диск и разрешение экрана — `SettingsPresenter` (~169–171) + `UnitySettingsApplier`
Каждое движение слайдера → `Set` → `PlayerPrefs.Save()` и `Screen.SetResolution`.

Исправить:
- `UnitySettingsApplier.Apply` вызывает `SetResolution` / `fullScreenMode` **только если** ширина, высота или режим отличаются от текущих (`Screen.width`, `Screen.height`, `Screen.fullScreenMode`), а `SetQualityLevel` — только если уровень отличается. Это исправление в Infrastructure 08a, его разрешено править.
- Громкость применяется сразу, а **сохраняется** при отпускании слайдера:
  - либо `SettingsService` получает `Preview(GameSettings)` (применить без сохранения) и `Set` (применить + сохранить);
  - либо слайдер сохраняет на `PointerUp` (`IEndDragHandler` / `IPointerUpHandler` на отдельном маленьком компоненте `SliderCommit`) и при закрытии панели.
  - Выбери первый вариант: чище и тестируется. Дополни `SettingsServiceTests`.

### 5. Esc «закрывает» невидимую панель — `PointPanelPresenter`, `BuildPanelPresenter`, `StorekeeperOfferPresenter`
Пока объект за камерой, панель скрыта (`SetOnScreen(false)`), но остаётся в `EscapeRouter`. Первое нажатие Esc закрывает невидимую панель, пауза открывается только со второго.

Исправить: если панель сейчас не на экране, `TryHandleEscape` возвращает false (обработчик не потребляет Esc). Тест в `EscapeRouterTests`: обработчик, вернувший false, пропускает Esc дальше — до паузы.

### 6. Esc в главном меню
В сцене MainMenu тоже нужен `EscapeRouter`: Esc закрывает настройки, затем диалог подтверждения. Без них Esc ничего не делает: главное меню Esc не закрывает и выхода не делает.

## Editor-утилита — `ModuleSetupC1.cs`, `SetupUi.cs`

### 7. Префаб `SettingsPanel` пересоздаётся при каждом запуске (~109–161)
Правки пользователя теряются, у вложенных объектов меняются fileID. Нужно: префаба нет → создать; есть → открыть через `PrefabUtility.LoadPrefabContents`, добавить только недостающее, назначить ссылки, `SaveAsPrefabAsset` + `UnloadPrefabContents`.

### 8. Повторный запуск сносит весь созданный утилитой UI (~196, 232–241, 510–515)
`MenuCanvas`, `PauseButton`, `PauseMenu`, экземпляры `SettingsPanel` удаляются и строятся заново.

Исправить: существующие объекты с маркером **переиспользовать** — найти, дособрать недостающих детей, заново назначить ссылки. Пересоздавать только отсутствующее. Подписи и цвета, уже существующие в сцене, не трогать (как в A2: «заполнять только пустое»).

### 9. Мелочи утилиты
- **~66:** отказываться при `EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling`.
- **~78, 89–92:** сохранять и восстанавливать раскладку сцен через `EditorSceneManager.GetSceneManagerSetup()` / `RestoreSceneManagerSetup()`. Untitled-сцена → после работы открыть Boot.
- **~314, 329:** камеру и свет искать по компоненту (`Camera`, `Light`), не по имени.
- **~431:** `_firstScene` через `enumValueIndex`.
- **`SetupUi.cs:144`:** если `TMP_Settings.defaultFontAsset == null` → `Debug.LogWarning` с подсказкой «Window → TextMeshPro → Import TMP Essential Resources».
- **Undo:** вызовы при `OpenScene` / `NewScene(Single)` бесполезны (стек очищается, сцена сразу сохраняется). Убрать их и написать `// Why:` в шапке утилиты. Заменить на сообщение в итоговом логе «scenes were saved; use git to revert».

## Мелочи рантайма
- **`PauseMenuPresenter` ~721–725:** лишний `Close()` перед `ToMainMenu()` (модель закрывает сама).

## Git
Коммит: `Fix C1 review findings`, push. В отчёте:
- что исправлено, что нет;
- что проверить пользователю.
