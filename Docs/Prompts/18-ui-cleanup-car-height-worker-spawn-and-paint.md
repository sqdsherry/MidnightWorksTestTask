# Задача 18: Очистка Tag (удаление Background и Arrow), унификация окна покупки с окном апгрейда, фикс высоты машин (-0.5), спавн работников и чистая покраска кузова

## ⚠️ ВАЖНОЕ НАПОМИНАНИЕ
**Не ломать и не перемещать пользовательские объекты на Локации 2 (`TravelPlot_Travel_To_Loc1`, поднятый плейн дорог и т.д.). Все доработки по коду и ассетам вносятся аккуратно и без деструктивного сброса сцены.**

---

## 1. Удаление `Background` и `Arrow` из всех `Tag` (World-Space Dwell Ring)

### 1.1. Доработка `Location2SceneUpdater.cs` и `WhiteboxLocationBuilder.cs`
В `EnsureDwellRingCanvas()`:
1. **Полностью исключить создание объектов `Background` и `Arrow`**:
   - Не создавать объект `Background` (темную круглую подложку).
   - Не создавать объект `Arrow` (белую стрелочку).
   - В канвасе `Tag` должен создаваться **только** `Fill` (круговой радиальный индикатор заполнения для `DwellRingView`).
2. В `DwellRingView` поле `_background` должно оставаться `null`.
3. Добавить в `Location2SceneUpdater.cs` метод очистки:
   - Найти все объекты/канвасы с именем `Tag` на сцене `Gameplay.unity` (над синими кругами `ManagePad`, точками телепорта `TravelPoint`, участками стройки `BuildPlot`).
   - Удалить из них дочерние объекты `Background` и `Arrow`.

---

## 2. Унификация стиля диалоговых окон (Окно покупки = Окно апгрейда)

### 2.1. Дизайн окна покупки (`BuildPanel` / `OfferPanelView`)
Окно покупки (`BuildPanel`) должно выглядеть в **том же самом едином стиле**, что и окно апгрейда (`PointPanel`):
1. **Фон панели:**
   - 9-slice спрайт Kenney UI Grey (`Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_rectangle_depth_flat.png`).
   - Размер панели: аккуратный и компактный (например, `360 x 240` пикселей).
2. **Кнопка закрытия (X):**
   - Красный квадратный 9-slice спрайт (`Red/Default/button_square_depth_flat.png`) в правом верхнем углу (как в `PointPanel`).
3. **Минимум текста:**
   - Вверху: крупный заголовок (`_title`), цвет `#FFFFFF` с четким контуром или темно-синий `#1E2430`.
   - В центре: короткое мини-описание в 1-2 строки (`_description`), например: *"Car wash service bay."* или *"Tire replacement bay."*. Цвет шрифта должен быть **хорошо читаемым** на светло-сером фоне (темный `#222B38`, а не блекло-белый!).
   - Скрыть/удалить лишние захламляющие текстовые поля (`_cost`, `_requirement` как отдельные строки). Вся ключевая информация должна быть на кнопке действия.
4. **Кнопка покупки (Action Button):**
   - Широкая зеленая 9-slice кнопка (`Green/Default/button_rectangle_depth_flat.png`) внизу панели.
   - Текст на кнопке:
     - Если хватает денег: `Buy $500` (или `Build $500`).
     - Если не хватает: `Need $500`.
     - Если заблокировано по уровню: `Locked (Lv 2)`.
5. Обновить `UiReskinnerEditor.cs`, чтобы он настраивал как `PointPanel`, так и `BuildPanel` / `StorekeeperPanel` в одинаковом стилистическом оформлении и выставлял читаемые контрастные цвета текста.

---

## 3. Фикс высоты машин: спавн и движение строго на Y = -0.5

Сейчас машины висят в воздухе над дорогой. Требуется, чтобы все машины спавнились и двигались строго на высоте **Y = -0.5**.

### 3.1. Доработка префабов авто (`Car_Sedan`, `Car_Sport`, `Car_Suv`)
1. В префабах `Car_Sedan.prefab`, `Car_Sport.prefab`, `Car_Suv.prefab`:
   - В компоненте `NavMeshAgent` изменить `Base Offset`:
     - Выставить `Base Offset = -0.5` (сейчас там стоит `+0.5`, из-за чего агент поднимает машину на 1 метр над уровнем -0.5).

### 3.2. Доработка `CarView.cs`
1. В `CarView.Awake()`:
   ```csharp
   if (_agent != null)
   {
       _agent.baseOffset = -0.5f;
   }
   ```
2. В `CarView.Place(int carId, RoadNode startNode)`:
   - При спавне принудительно задавать Y = -0.5f:
     ```csharp
     Vector3 position = new Vector3(start.position.x, -0.5f, start.position.z);
     transform.SetPositionAndRotation(position, rotation);
     if (_agent != null && _agent.isActiveAndEnabled)
     {
         _agent.Warp(position);
     }
     ```
3. В `CarView.SnapPosition(Vector3 position)`:
   - `position.y = -0.5f;`
   - `transform.position = position;`
4. В процессе движения (`TickArrival` / перемещение агента) следить, чтобы локальная/мировая высота машины не улетала вверх, а оставалась на уровне дороги (-0.5f).

---

## 4. Спавн работника: появление у домика персонала и пеший ход к рабочему месту

### 4.1. Причина бага
При вызове `StaffView.WalkTo(target)`:
Если `_agent.isOnNavMesh` равен `false` (из-за того, что точка спавна `StaffRoom/Door` находилась вне сетки NavMesh или внутри коллайдера здания), метод `WalkTo` сразу делал `Place(target.position, target.rotation)`, мгновенно телепортируя работника прямо на рабочее место бокса.

### 4.2. Доработка `StaffAgents.cs`
В `StaffAgents.Spawn(int staffId, StaffRole role, string locationId)`:
1. Корректировать точку спавна по сетке NavMesh:
   ```csharp
   Transform door = _layout.StaffRoom;
   Vector3 position = door != null ? door.position : _layout.transform.position;
   if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
   {
       position = hit.position;
   }
   Quaternion rotation = door != null ? door.rotation : Quaternion.identity;
   ```

### 4.3. Доработка `StaffView.cs`
В `StaffView.WalkTo(Transform target)`:
1. Если `!_agent.isOnNavMesh`:
   - Попробовать привязать агента к ближайшей точке NavMesh:
     ```csharp
     if (NavMesh.SamplePosition(transform.position, out NavMeshHit curHit, 3.0f, _agent.areaMask))
     {
         _agent.Warp(curHit.position);
     }
     ```
2. Использовать `NavMesh.SamplePosition(target.position, out NavMeshHit hit, 5.0f, _agent.areaMask)` и запускать путь `_agent.SetDestination(hit.position)`.
3. Телепортировать на рабочее место (`Place(...)`) **только** в крайнем случае, если путь невозможно построить в принципе (например, нет связи между островами).

### 4.4. Доработка точки `Door` на Локации 2 (`Location2SceneUpdater.cs`)
1. Убедиться, что на Локации 2 объект `StaffRoom/Door` находится снаружи перед дверью домика на высоте земли, на проходимой области NavMesh.

---

## 5. Чистая покраска кузова машины (без закрашивания стекол и фар)

### 5.1. Причина
Модели авто Kenney используют общую текстуру-палитру `colormap.png`. Когда `MaterialPropertyBlock` меняет `_BaseColor` всего рендера, он умножает всю текстуру, из-за чего стекла (которые должны быть тонированными/голубыми) и фары (белые/желтые) окрашиваются в сплошной цвет кузова.

### 5.2. Реализация чистой покраски кузова
Есть два отличных способа:
1. **Способ через сгенерированную палитру (чистый C#, без шейдеров):**
   - В текстуре `colormap.png` пиксели кузова авто (красный/оранжевый базовый сектор) заменяются на новый выбранный цвет, а пиксели стекол, фар, решеток и хрома остаются исходными.
   - Сгенерированная текстура 512x512 кэшируется в `Dictionary<Color, Texture2D>` (всего 8 цветов палитры).
   - В `CarView.SetBodyColor(Color color)` через `MaterialPropertyBlock` подставляется `_BaseMap` (или `_MainTex`) с кэшированной текстурой для данного цвета:
     ```csharp
     _propertyBlock.SetTexture(BaseMapPropertyId, cachedTexture);
     ```
   - При этом стекла остаются стеклами, фары — фарами, а меняется **только кузов**!
2. **Либо кастомный шейдер/ShaderGraph:**
   - Шейдер берет текстуру `colormap` и тонирует только пиксели с насыщенным цветом кузова, игнорируя нейтральные темные/светлые/голубые оттенки.
*(Рекомендуется Способ 1 через Texture2D кэш палитры — он работает моментально со стандартным URP Lit шейдером, не требует настройки ShaderGraph и дает идеальный результат).*

---

## 6. Порядок проверки

1. Запустить пункт меню **`AutoService -> Setup -> Apply Location 2 and Warehouse Fixes`**:
   - Удалит `Background` и `Arrow` из всех канвасов `Tag`.
   - Обновит `Door` и склад.
2. Запустить пункт меню **`Tools -> Reskin UI (12b)`**:
   - Применит единый стиль к `BuildPanel` (покупка) и `PointPanel` (апгрейд).
3. Запустить игру в Play Mode:
   - **Tag:** Убедиться, что над синими кругами и точками телепорта отображается только круговой радиальный прогресс-бар при нахождении игрока, без черных кругов и стрелок.
   - **Окна:** Открыть окно покупки некупленного сервиса и окно купленного сервиса — оба выглядят в едином аккуратном стиле Kenney UI, в окне покупки минимум понятного текста.
   - **Машины:** Убедиться, что машины спавнятся и едут строго по дороге на высоте -0.5, не парят в воздухе.
   - **Работники:** Нанять работника бокса — работник появляется у домика персонала и пешком идет на свое рабочее место.
   - **Покраска авто:** Обслужить машину на покраске — кузов авто окрашивается в сочный цвет, а лобовое стекло, фары и решетка сохраняют свой естественный вид.
