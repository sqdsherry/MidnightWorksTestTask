# Промпт 08b — Подключение сейва: кошелёк, постройки, апгрейды, персонал, запасы

**Ветка:** `feature/08b-save-wiring` от `main` · **Папка:** основная.
**Перед началом:** `CLAUDE.md`, `Docs/TDD.md` §4.15, `Docs/Prompts/08a-save-core.md`, код:
- `Services/Save/*` (`SaveData`, `PointSaveData`, `ISaveable`, `SaveCoordinator`, `ISaveService`);
- инсталлеры;
- `TODO(08b-save)` в коде (5 мест).

> **Параллельно** пользователь пишет модуль 07 (прогрессия) в отдельной папке. **Не трогать:**
> - `Domain/Progression`, `Services/Progression`;
> - `ServiceTypeConfig` / `ServiceTypeSettings`, `GameConfig`;
> - строку с gate в `BuildingInstaller` (~44).
>
> XP и уровень в этом модуле **не сохраняются** — см. §6. Правки списка инсталлеров в `GameplayEntryPoint.Enter` — минимальные (одна строка в конце списка).

---

## 1. Цель
- Continue продолжает игру с того же места.
- New Game начинает с нуля.
- Прогресс не теряется ни при выходе в меню, ни при закрытии игры.

**Сохраняем** (TDD §4.15):
- деньги;
- построенные участки;
- уровни апгрейдов каждой точки;
- работники точек;
- кладовщики (по записи на каждого);
- запас расходника каждой точки.

**Не сохраняем:**
- машины;
- позиции персонажа и NPC (стартуют как при новой игре);
- ящик в руках.

## 2. `ISaveable`-реализации — по одной на модуль, рядом с сервисом (Services, без UnityEngine)
Каждая — `sealed`, пишет и читает **только свой кусок** `SaveData`, терпит пустые массивы и неизвестные id (сейв от старого лейаута → пропуск + `IGameLogger.Warning` один раз на id).

| Класс | Capture | Restore |
|---|---|---|
| `WalletSaveable` (Services/Economy) | `money = Balance.Amount` | выставить баланс (см. ниже) |
| `BuildSaveable` (Services/Building) | `builtPlotIds = BuiltPlotIds` (массив-копия) | `RestoreBuilt(builtPlotIds)` |
| `PointsSaveable` (Services/Points или Staff — где логичнее) | `points[]`: на каждую зарегистрированную точку — `pointId`, `speedLevel`, `priceLevel`, `supply` (−1 у точек без запаса), `hasWorker` | для каждой записи: апгрейды → `IUpgradeService.Restore`, запас → `SupplyStock.Restore(supply)` (если ≥ 0), работник → `RestoreWorker` |
| `StaffSaveable` | `storekeeperLocationIds`: id локации по разу на каждого кладовщика | `RestoreStorekeeper` для каждой записи |

- **Кошелёк:** `Wallet` не умеет «выставить баланс». Добавить в домен `Wallet.Restore(Money balance)` (поднимает `BalanceChanged`, XML-дока, `// Why:` только для сейва) и в `IWalletService.Restore(Money)`. Тест.
- **Точки, построенные из сейва**, появляются на `BuiltRestored` синхронно. Поэтому **`BuildSaveable` восстанавливается первым**, `PointsSaveable` — после него: точки уже зарегистрированы в сервисах апгрейдов и запасов. Порядок задаётся порядком `coordinator.Add(...)`. Описать его `// Why:` в месте сборки.
- **Количество кладовщиков** берётся из `IStaffService.Staff` (роль Storekeeper → его `LocationId`).

## 3. Сборка — `Bootstrap/Installers/SaveInstaller` (последний в списке)
- Создаёт `SaveCoordinator(ISaveService, IGameLogger, autosave 30 s)` (тикается — `Track`), добавляет saveable'ы в порядке: Wallet → Build → Points → Staff.
- Сразу после добавления: `coordinator.TryRestore()`.
  - Делать **после всех инсталлеров и до `InitializeServices`**: первый тик уже видит восстановленный мир.
  - Если `ISaveService.HasSave == false` — новая игра, ничего не делать.
- Регистрирует `SaveCoordinator` в контейнере сцены (нужен паузе и EntryPoint).
- Закрыть `TODO(08b-save)` в `EconomyInstaller`, `BuildingInstaller` (строка ~60, **не** строка с gate), `StaffSuppliesInstaller`: восстановление идёт через saveable'ы, поэтому TODO заменяются коротким `// Restored by SaveInstaller (see its order).`

## 4. Когда сохраняем
- **Автосейв** каждые 30 с игрового времени (уже в `SaveCoordinator`).
- **`RequestSave()`** (сохранить в следующем тике) после важных действий. Подписка в `SaveInstaller` на события шины / сервисов, отписка в `Dispose` отдельного класса `SaveTriggers : IDisposable`:
  - `BuildCompletedEvent`;
  - `UpgradePurchasedEvent`;
  - `StaffHiredEvent`.
- **`SaveNow()`**:
  - при выходе в меню из паузы — закрыть `TODO(08b-save)` в `PauseMenuModel` / `PauseMenuPresenter` через новый интерфейс `IGameSaver { void SaveNow(); }` (Services), который реализует `SaveCoordinator`. Модель паузы получает его через конструктор (nullable: в MainMenu его нет);
  - при Quit из паузы — так же;
  - в `GameplayEntryPoint.OnApplicationQuit()` и `OnApplicationPause(true)`.
  - **Важно:** `OnApplicationQuit` срабатывает раньше `OnDestroy` — сейв до диспоуза сервисов. Проверь, что после `ResetProgress` сейв не пишется (это уже в `SaveCoordinator`).
- **New Game** в меню уже вызывает `ISaveService.Delete()`. Проверить, что старый сейв после этого не появляется снова: Gameplay стартует без сейва → новая игра.

## 5. Главное меню
`Continue` активна, когда `ISaveService.HasSave`. Это уже так, ничего не менять — только проверить в сценарии.

## 6. Место под прогрессию (модуль 07 пишет пользователь)
- Поля `xp` / `level` в `SaveData` уже есть. **Не** сохранять и не читать их здесь.
- В `SaveInstaller` оставить строку `// TODO(07-progression): coordinator.Add(new ProgressionSaveable(progression)) — after Wallet, before Build (level gates may matter on restore).`
- Пользователь добавит `ProgressionSaveable` при мерже своего модуля.

## 7. Тесты (EditMode, фейки + настоящие сервисы где дёшево)
- `WalletSaveableTests`: Capture/Restore round-trip; `BalanceChanged` при Restore.
- `BuildSaveableTests`: восстановленные участки построены без оплаты; неизвестный id пропущен.
- `PointsSaveableTests`:
  - уровни апгрейдов и запас восстановлены;
  - `supply = -1` не трогает точку;
  - `hasWorker` → `RestoreWorker`;
  - точка, которой нет, пропущена.
- `StaffSaveableTests`: 2 кладовщика → в `Capture` локация дважды → `Restore` даёт 2.
- **Интеграционный** `SaveRoundTripTests`:
  - собрать мини-мир на настоящих сервисах (wallet, points, build, upgrades, supplies, staff с фейковыми агентами), изменить всё, `Capture`;
  - собрать **новый** мир, `Restore`;
  - сравнить: деньги, построенное, уровни, запасы, число работников и кладовщиков;
  - через `JsonUtilitySaveSerializer` не надо, хватит объекта `SaveData`.
- `PauseMenuModelTests`: `ToMainMenu` вызывает `IGameSaver.SaveNow` до загрузки сцены.

## 8. Editor setup (пользователь) — в PR
Сцены не меняются, setup-утилита не нужна. Проверки в Play:
1. New Game → заработать, построить бокс, купить апгрейд, нанять работника и кладовщика, дождаться, пока расход опустит запас.
2. Пауза → Main Menu → **Continue** активна → Continue → всё на месте: деньги, бокс, уровни в панели, работник на месте, кладовщик есть, запасы те же.
3. Stop Play → Play → Continue → то же самое (сейв при выходе).
4. Main Menu → New Game → подтверждение → чистый старт ($100, ничего не построено).
5. Run All.

## 9. Git
Коммиты:
1. `Add wallet restore`
2. `Add saveables for wallet, build, points and staff`
3. `Add save installer, triggers and save on quit`
4. `Save before leaving to the menu`
5. `Add save wiring tests`

PR `08b: Save wiring`.
