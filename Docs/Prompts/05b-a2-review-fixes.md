# Промпт 05b (фикс A2) — замечания ревью

**Ветка:** та же `feature/05-staff-supplies`. Тот же стиль и правила `CLAUDE.md`. Закоммить этот файл вместе с фиксами.

## Должно быть исправлено

### 1. Игрок занимает WorkSpot нанятого работника — `Presentation/Points/ServicePointView.cs`
`BeginInteraction` после выгрузки ящика всегда вызывает `TryOccupy(Player)`.

Сценарий: нанял мойщика, он ещё идёт из комнаты. Игрок с ящиком подходит к supply drop, выгружает и **занимает место**, мойка работает от игрока. Пришедший работник висит в `WaitingForSpot`. Второй путь: игрок уже шёл на WorkSpot в момент найма, дошёл и занял.

Это нарушает правило GDD «игрок не помогает работнику».

Исправить: если `_staff != null && _staff.HasWorker(_pointId)`, то после выгрузки — `return`, `TryOccupy` не вызывать. Для второго пути то же условие стоит в начале `BeginInteraction`: место нанятого работника игрок не занимает никогда.

Тесты Presentation не нужны, но опиши сценарий в `// Why:`.

### 2. Аллокации в тике у панели точки — `Presentation/Points/Panel/PointPanelPresenter.cs`
`OnBalanceChanged → Refresh` при каждом изменении баланса пересобирает все строки (`StringBuilder.ToString`, `MoneyFormatter.Format` ×3, `string.Format` ×3). Баланс меняется внутри `GameLoop.Tick` (оплата, покупка ящика), значит при открытой панели мусор копится несколько раз в секунду (правило 4).

Исправить как в `StorekeeperOfferPresenter`:
- кешировать по строке (Speed / Price / Hire) последние `availability` и `level`;
- строки цены и подписей форматировать только при смене уровня или открытии панели;
- на `BalanceChanged` переключать только `Available ↔ NotEnoughMoney` (интерактивность кнопки + выбор одной из двух закешированных подписей);
- статус (запас / работник) — только на `SupplyStock.Changed` / `Hired` через `TMP_Text.SetText("{0}/{1}", …)`;
- доход — раз в 1 с, как сейчас, но через `SetText`, без `string.Format`.

### 3. NPC «приезжает» по недостижимому пути — `Presentation/Staff/StaffView.cs`, `TickArrival`
`pathStatus` не проверяется. При `PathPartial` / `PathInvalid` агент останавливается на краю, срабатывает `Arrived`: работник «занимает» место, на котором не стоит, кладовщик выгружает «через стену». Если агент не на NavMesh, `remainingDistance` бросает исключение каждый кадр.

Исправить как в `PlayerMotor`:
- проверять `isOnNavMesh`;
- при `PathPartial` / `PathInvalid` — один `Debug.LogWarning` с именем цели, затем **телепорт** (`Warp` / `Place`) на точку цели и `Arrived`. **Why:** NPC не должен застревать навсегда, а игра — молча ломаться.

### 4. Кладовщик игнорирует порог у склада — `Services/Staff/StaffService.cs` (~306–332, `BuyAtWarehouse`)
Порог `Fill01 < RestockThreshold` проверяется только в `TickIdleStorekeeper`. У склада и в `WaitingForMoney` кладовщик покупает для **любой** точки, куда влезает ящик.

Сценарий: игрок сам пополнил голодную точку, пока кладовщик шёл. Кладовщик всё равно покупает и несёт ящик в точку на 5/10. Без денег он ждёт вечно.

Исправить: один хелпер `TryFindRestockTarget(locationId, out point)` с порогом. Им пользуются обе ветки. Цели нет → `CancelRestock()` → `Idle`.

Тесты:
- `Fill01` ровно 0.5 → кладовщик не идёт;
- игрок пополнил точку, пока кладовщик шёл → кладовщик возвращается в `Idle` без покупки;
- путь «ящик некуда деть» → ящик выброшен, `Idle`, incoming очищен, следующая доставка в эту точку возможна;
- у покупки кладовщика `BoxBoughtEvent.ByPlayer == false`.

### 5. NRE при восстановлении кладовщика без `StaffSection` — `StaffService.RestoreStorekeeper` (~159)
Добавить ту же защиту, что в `GetStorekeeperAvailability`: при `_config.Staff == null` писать лог и не создавать кладовщика.

### 6. Порядок «принять заказ / потратить расходник» — `Domain/Points/ServicePoint.cs` (~243)
Сейчас `TryConsume()`, а с ним `SupplyStock.Changed` и `SupplyDepletedEvent`, срабатывает, пока точка ещё `AwaitingAccept`. Слушатели видят `IsWaitingForSupply == true` у машины, которую уже обслуживают.

Порядок: `SetState(Servicing)` → `TryConsume()` → `OrderAccepted`. Дополнить тест: в обработчике `Changed` состояние уже `Servicing`.

### 7. Editor-утилита `Bootstrap/Editor/ModuleSetupA2.cs`
- **~134:** `(RectTransform)buildPanel.transform.parent` без проверки на null → NRE и setup обрывается. Нужна проверка и запись в список проблем.
- **~370, ящик в руках у Player:** создаётся включённым. Bake (RenderMeshes) запекает его как препятствие в точке спавна. Исправить: создавать неактивным (его включает `PlayerCarryView`) + `Undo.RegisterCreatedObjectUndo`. Те же две правки — для `CarrySocket` в префабе Staff.
- **~421–422:** `_actionLabelFormat` и `_completedLabel` панели кладовщика перезаписываются при каждом запуске. Задавать их **только при создании** панели.
- **~222–263, `Staff.prefab`:** пересоздаётся с нуля при каждом запуске, правки пользователя теряются. Если префаб уже есть — открыть через `PrefabUtility.LoadPrefabContents` и добавить только недостающее, затем `SaveAsPrefabAsset` + `UnloadPrefabContents`.
- **Перенос настроек старой панели:** перенести значения `_screenOffset` / `_screenMargin` со старой `OfferPanelView` (бывшая `BuildPanelView`, `FormerlySerializedAs`) в `ScreenAnchoredPanel`, если они там ещё по умолчанию. `_root` искать сначала по старой сериализованной ссылке, а не по имени «Panel» или первому ребёнку.
- **Undo:** зарегистрировать создание корня `[Staff]`.

## Мелочи (сделать, если быстро)
- **`StorekeeperOfferPresenter`:** при переходе `Locked → Available` с открытой панелью убрать текст требования (перерисовать `Show`).
- **`HireRowView.SetTitle`:** нигде не вызывается. Вызвать из `ShowHire` с `worker.Title`.
- **`UpgradeService.SettingsOf` / `TrackOf`:** заменить на `switch`, неизвестный `UpgradeKind` → `ArgumentOutOfRangeException`.
- **`ISupplyService`:** в XML-доке `TryBuyBoxForHungriest` написать, что при нехватке денег `target` не null (на это опирается `WarehouseView`).
- **`StaffSuppliesInstaller`:** если нет префаба Staff → одно понятное сообщение «NPCs will be invisible», а не ошибка на каждый спавн.
- **`WhiteboxLocationBuilder.AddWallObstacles`:** `// Why:` про `m_IgnoreNavMeshObstacle` (столбы не запекаются, их дырку закрывает carve в рантайме).

## Git
Коммит: `Fix A2 review findings`, push в ту же ветку. В отчёте — что исправлено, что пропущено и почему.
