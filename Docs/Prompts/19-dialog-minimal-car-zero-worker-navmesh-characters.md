# Задача 19: Минималистичное окно покупки, высота авто Y = 0, фикс пути работников (PathPartial) и интеграция 3D-персонажей Kenney с анимациями

## ⚠️ ВАЖНОЕ НАПОМИНАНИЕ
**Не ломать и не перемещать пользовательские объекты на Локации 2 (`TravelPlot_Travel_To_Loc1`, поднятый плейн дорог и т.д.). Все доработки по коду и ассетам вносятся аккуратно и без деструктивного сброса сцены.**

---

## 1. Удаление описания из окна покупки (`BuildPanel` / `OfferPanelView`)

В окне покупки услуги (`BuildPanel`) описание сервиса больше не нужно:
1. **Полностью убрать текстовое поле описания (`_description`):**
   - В префабе/сцене отключить или удалить объект `Description` под `BuildPanel/Panel`.
   - В `OfferPanelView.cs` сделать поле `_description` опциональным:
     ```csharp
     if (_description != null)
     {
         _description.gameObject.SetActive(false);
     }
     ```
2. **Итоговый вид окна покупки (`BuildPanel`):**
   - Сверху: Название сервиса (`_title`) в едином стиле с окном апгрейда.
   - Справа вверху: Кнопка закрытия `X` (`_closeButton`, красный квадратный Kenney UI).
   - Внизу: Широкая зеленая кнопка покупки (`_actionButton` с надписью `Buy $500` / `Need $500` / `Locked (Lv 2)`).
   - Компактный размер панели (высота уменьшена, так как текста описания нет).

---

## 2. Спавн и езда машин строго на высоте Y = 0

По требованию машины должны спавниться и передвигаться на **Y = 0** (а не -0.5).

1. **В префабах авто (`Car_Sedan.prefab`, `Car_Sport.prefab`, `Car_Suv.prefab`):**
   - В компоненте `NavMeshAgent` выставить:
     `Base Offset = 0` (вместо -0.5 или +0.5).
2. **В `CarView.cs`:**
   - В `Awake()`:
     ```csharp
     if (_agent != null)
     {
         _agent.baseOffset = 0f;
     }
     ```
   - В `Place(int carId, RoadNode startNode)`:
     ```csharp
     Vector3 position = new Vector3(start.position.x, 0f, start.position.z);
     transform.SetPositionAndRotation(position, rotation);
     if (_agent != null && _agent.isActiveAndEnabled)
     {
         _agent.Warp(position);
     }
     ```
   - В `SnapPosition(Vector3 position)`:
     ```csharp
     position.y = 0f;
     transform.position = position;
     ```

---

## 3. Фикс пути работников: устранение предупреждения `PathPartial` и телепортации

### 3.1. Причина ошибки `PathPartial`
Предупреждение:
`[StaffView] 'Staff_PointWorker_8' has no complete path (PathPartial) to 'WorkSpot'; teleported there.`
происходит из-за двух факторов:
1. `WorkSpot` у боксов находится вплотную к столбам `PillarL` / `PillarR`, на которых висят компоненты `NavMeshObstacle` с вырезанием (carving). Область вокруг `WorkSpot` вырезана из NavMesh, поэтому агент не может дойти до самой точки.
2. В `StaffView.TickArrival`:
   ```csharp
   if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
   {
       SnapToTarget("has no complete path (" + _agent.pathStatus + ")");
       return false;
   }
   ```
   Если путь частичный (`PathPartial`), агент не идет даже до ближайшей точки, а мгновенно телепортируется на первом же кадре!

### 3.2. Доработка `StaffView.cs`
1. Разрешить агенту двигаться по частичному пути (`PathPartial`) в сторону цели:
   - Не телепортировать работника мгновенно при `PathPartial`.
   - Если путь частичный, агент идет до конца доступного пути.
   - Когда агент дошел до конца пути или приблизился к цели на расстояние допуска (`_agent.remainingDistance <= _agent.stoppingDistance + 1.2f` или `Vector3.Distance(transform.position, _target.position) <= 1.8f`), считать, что работник прибыл на рабочее место (`StartTurning()`).
2. В `WalkTo(Transform target)`:
   - При сэмпле точки назначения находить ближайшую проходимую точку NavMesh:
     ```csharp
     if (NavMesh.SamplePosition(target.position, out NavMeshHit hit, 3.0f, _agent.areaMask))
     {
         _agent.SetDestination(hit.position);
     }
     ```

### 3.3. Доработка сцены и `Location2SceneUpdater.cs`
1. В `Location2SceneUpdater.cs` вызвать запекание NavMesh для всех поверхностей:
   ```csharp
   var surfaces = UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(
       FindObjectsInactive.Include, FindObjectsSortMode.None);
   foreach (var surface in surfaces)
   {
       surface.BuildNavMesh();
   }
   ```
2. Убедиться, что `StaffRoom/Door` на Локации 2 находится на высоте земли перед домиком, а позиции `WorkSpot` у сервисов отнесены на 0.5м от коллайдеров столбов `Pillar`, чтобы не попадать в зону вырезания `NavMeshObstacle`.

---

## 4. Интеграция 3D-персонажей Kenney, анимаций и префаба коробки

В проекте уже есть пак `Assets/_Project/Art/Kenney/kenney_mini-characters/` с готовыми FBX (внутри которых вшиты анимации `idle`, `walk`, `run`), а также аниматор `AC_Character.controller`.

### 4.1. Префаб персонажа игрока (`Player`)
1. Заменить примитивную белую капсулу игрока в `Gameplay.unity`:
   - Добавить дочернюю 3D-модель из `kenney_mini-characters` (например, `character-male-a` или `character-female-a`).
   - Назначить на модель `Animator` с контроллером `Assets/_Project/Art/Characters/AC_Character.controller`.
   - В `PlayerView.cs` (или отдельном компоненте) связать `CharacterAnimator`, передавая скорость `_agent.velocity.magnitude` и флаг переноски коробки `isCarrying`.
2. Заменить коробку в руках игрока (`PlayerCarryView._boxRenderer`):
   - Использовать префаб `Assets/_Project/Prefabs/Art/box-small.prefab` (или `box.prefab`) вместо стандартного куба Unity.

### 4.2. Префабы работников (`Staff.prefab` / `Staff_0.prefab`)
1. Заменить белые капсулы с шариками в `Staff.prefab`:
   - Работник сервиса (`PointWorker`): 3D-модель `character-male-b` / `character-female-b`.
   - Кладовщик (`Storekeeper`): 3D-модель `character-male-d` / `character-female-c` в кепке/рабочей одежде.
   - Подключить `Animator` с `AC_Character.controller`.
   - Заменить коробку в руках кладовщика на `box-small.prefab`.
2. `StaffAgents.cs` уже поддерживает `CharacterAnimator` — убедиться, что он управляет анимациями `idle` и `walk` во время ходьбы к боксам и обратно к складу.

---

## 5. Порядок проверки

1. Запустить пункт меню **`AutoService -> Setup -> Apply Location 2 and Warehouse Fixes`** (перепечет NavMesh и обновит точки).
2. Запустить пункт меню **`Tools -> Reskin UI (12b)`** (обновит `BuildPanel` без описания).
3. Запустить игру в Play Mode:
   - **Окно покупки:** Открыть участок покупки бокса — окно содержит только заголовок и кнопку `Buy`, без лишнего текста описания.
   - **Машины:** Убедиться, что машины спавнятся и едут строго по дорогам на высоте **Y = 0**.
   - **Спавн работника:** Нанять работника бокса — работник появляется у домика персонала, воспроизводит анимацию ходьбы (`walk`) и аккуратно пешком доходит до бокса без ошибок в консоли.
   - **Визуал персонажей:** Игрок и работники отображаются красивыми 3D-моделями Kenney с анимациями ходьбы, а переносимые ящики — стильными 3D-коробками.
