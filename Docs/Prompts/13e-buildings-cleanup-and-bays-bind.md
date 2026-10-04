# Промпт 13e (финальная зачистка D1 сетапа и фикс исчезновения боксов)

**Ветка:** `feature/13a-bays-layout-rebalance`. Тот же стиль и правила `CLAUDE.md`. Закоммить этот файл вместе с фиксами.

## Проблема 1: Снова спавнятся белые стены (`Visual`) под Warehouse_1 и StaffRoom_1
На скриншоте пользователя видно, что под `Warehouse_1` создался дочерний объект `Visual` со спавном:
`box-large`, `door-wide-open`, `structure-wall` (x3). 
То же самое происходит в `StaffRoom_1` (`structure-doorway-wide`, `structure-wall` x3, `detail-parasol-a`).
Эти белые стены не нужны и только захламляют сцену.

**Исправление в `ModuleSetupD1_Buildings.cs`:**
- В методе `SetupBuildings`:
  - **УДАЛИТЬ спавн стен и дверей** под `Warehouse_1` и `StaffRoom_1`. Не нужно инстанцировать `structure-wall`, `door-wide-open`, `detail-parasol-a` и т.д. Если у них уже был дочерний объект `Visual`, удалить его (`Object.DestroyImmediate(vis.gameObject)`).
  - Рендереры складов и комнат персонала не нужно отключать, если нет нормальных замен.

## Проблема 2: Боксы масла и мойки исчезли или не создались призраки
В сцене объекты боксов называются:
- `Wash` (с `PointId = "loc1_wash_1"`)
- `Bay_loc1_wash_2` (с `PointId = "loc1_wash_2"`)
- `Bay_loc1_oil` (с `PointId = "loc1_oil"`, а не `loc1_oil_1`!)
- `Bay_loc1_tires` (с `PointId = "loc1_tires"`, а не `loc1_oil_2`!)

Из-за того, что в `SetupBays` проверка была захардкожена на:
```csharp
if (bay.PointId == "loc1_oil_1") plotId = "loc1_build_oil_1";
else if (bay.PointId == "loc1_oil_2") plotId = "loc1_build_oil_2";
```
Скрипт пропустил `Bay_loc1_oil` и `Bay_loc1_tires`, **НЕ создал для них призраки**, а в `_buildPlots` попали только 2 мойки и 2 парковки. Из-за этого игра в Play Mode не видит полные 4 бокса и падает с ошибками валидации!

**Исправление в `ModuleSetupD1_Bays.cs`:**
1. Переименовать/обновить PointId и ServiceTypeId у 3-го и 4-го бокса в сцене:
   - Бокс на X = -15 (`Bay_loc1_oil`): установить `bay.PointId = "loc1_oil_1"`, `bay.ServiceTypeId = "oil"`, переименовать `bay.gameObject.name = "Bay_loc1_oil_1"`.
   - Бокс на X = -21 (`Bay_loc1_tires`): установить `bay.PointId = "loc1_oil_2"`, `bay.ServiceTypeId = "oil"`, переименовать `bay.gameObject.name = "Bay_loc1_oil_2"`.
   (Использовать `SerializedObject(bay)` чтобы сохранить сериализацию).
2. Создать призраки для ВСЕХ 4 боксов:
   - `loc1_wash_1` -> `loc1_build_wash_1`
   - `loc1_wash_2` -> `loc1_build_wash_2`
   - `loc1_oil_1` -> `loc1_build_oil_1`
   - `loc1_oil_2` -> `loc1_build_oil_2`
   Каждый призрак создается через `WhiteboxLocationBuilder.CreateBayGhost` с именем `Ghost_{bay.PointId}`.
3. Сами 4 бокса (`Wash`, `Bay_loc1_wash_2`, `Bay_loc1_oil_1`, `Bay_loc1_oil_2`) на старте сцены должны быть **активны их призраки** (`Ghost_*`), а сами боксы скрыты (`bay.gameObject.SetActive(false)`), так как они строятся игроком по ходу прогрессии.
4. Синхронизировать массив `_buildPlots` на `LocationLayout`:
   Собрать ВСЕ 6 призраков (4 бокса + 2 парковочных места `Ghost_P2`, `Ghost_P3`), убедиться, что ни один не равен `null`, и записать в `_buildPlots`.

## Проверка
1. Запустить `Run D1 Setup`.
2. Проверить инспектор `Location_1`: в `Build Plots` ровно 6 элементов, ни одного `None / Missing`.
3. В сцене на X = -3, -9 висят призраки моек (с вывесками WASH), на X = -15, -21 висят призраки масла (с вывесками OIL).
4. Под `Warehouse_1` и `StaffRoom_1` НЕТ белых стен `Visual`.
5. В Play Mode игра стартует без единой ошибки `Service loop skipped` или `Build Plots element empty`.
