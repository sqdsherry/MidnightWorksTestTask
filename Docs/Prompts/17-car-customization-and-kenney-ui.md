# Задача 17: Импорт авто и Kenney UI, визуальная кастомизация машин (Шиномонтаж, Тюнинг, Покраска) и заметные уведомления склада

## ⚠️ ВАЖНОЕ НАПОМИНАНИЕ
**Не ломать и не перемещать пользовательские объекты на Локации 2 (`TravelPlot_Travel_To_Loc1`, поднятый плейн дорог и т.д.). Все доработки по коду и ассетам вносятся без деструктивного сброса сцены.**

---

## Цели задачи
1. **Импорт дополнительных авто и Kenney UI:**
   - Расширить импортер `KenneyImporter.cs`, чтобы он подтянул из папки `Donwload/kenney_car-kit` дополнительные варианты авто (`race`, `hatchback-sports`, `suv-luxury`, `wheel-racing`, спойлеры `debris-spoiler-a/b`).
   - Добавить в `KenneyImporter.cs` автоматический импорт спрайтов из папки `Donwload/kenney_ui-pack` (папки `Blue`, `Green`, `Grey`, `Red`, `Yellow`, `Extra`) в проект `Assets/_Project/Art/Kenney/kenney_ui-pack/` с корректной настройкой Sprite и 9-slice border (нарезка краев для кнопок и панелей).
2. **Визуальная кастомизация машин на сервисах Локации 2 (`CarCustomizationPresenter.cs` и `CarView.cs`):**
   - **Шиномонтаж (`tires_loc2`):** Визуальная замена колес на спортивные диски (`wheel-dark` или `wheel-racing` из Kenney) либо перекраска дисков в спортивный темный/золотой цвет.
   - **Тюнинг (`tuning_loc2`):** Замена кузова на спорткар со спойлером (`Car_Sport` / `sedan-sports` или гоночный болид `race`) либо установка спойлера на крышку багажника.
   - **Покраска (`paint_loc2`):** Перекраска кузова машины в яркий, сочный цвет из палитры (красный, неоновый синий, салатовый, оранжевый, фиолетовый, желтый).
   - **Накопление модификаций:** Если машина последовательно проходит несколько сервисов (например, шиномонтаж $\to$ тюнинг $\to$ покраску), все изменения на ней должны сохраняться и суммироваться!
   - **Возврат в пул (`ResetForPool`):** Когда машина уезжает с карты и возвращается в пул, она должна сбрасывать все кастомизации (кузов, колеса, цвет) к стоковому виду.
3. **Заметные уведомления об ошибках на складе (Kenney UI):**
   - Создать над складом красивый и заметный 3D-бейдж/попап с использованием 9-slice спрайта из `kenney_ui-pack` (например, `button_rectangle_depth_flat` красного/желтого цвета или `input_rectangle`).
   - Если ящик недоступен ("Hands full", "All stocked", "Need $X"), показывать этот бейдж с плавной анимацией всплытия и покачивания (shake/scale punch), чтобы игрок сразу видел причину отказа.
4. **Рескин диалоговых окон через ассеты Kenney UI:**
   - Обновить `UiReskinnerEditor.cs` (пункт меню `Tools -> Reskin UI`), чтобы он применял импортированные 9-slice спрайты Kenney UI (`Grey` для панелей/окон, `Green`/`Blue` для кнопок, `Red` для закрытия/отмены) вместо стандартного `Background.psd` Unity.

---

## 1. Импорт дополнительных авто и Kenney UI пака

### 1.1. Доработка `KenneyImporter.cs`
В `Assets/_Project/Scripts/Bootstrap/Editor/KenneyImporter.cs`:
1. В массиве `Whitelist` обновить регулярное выражение для `kenney_car-kit`:
   ```csharp
   ("kenney_car-kit", "^(sedan|suv|sedan-sports|suv-luxury|hatchback-sports|race|race-future|taxi|police|ambulance|truck|van|wheel-default|wheel-dark|wheel-racing|debris-tire|debris-spoiler-.*|cone|box)\\.fbx$")
   ```
2. Добавить логику импорта спрайтов из `Donwload/kenney_ui-pack/PNG`:
   - Целевая папка в проекте: `Assets/_Project/Art/Kenney/kenney_ui-pack/`.
   - Скопировать папки со спрайтами (минимум `Blue`, `Green`, `Grey`, `Red`, `Yellow`).
   - Настроить для скопированных PNG `TextureImporter`:
     - `textureType = TextureImporterType.Sprite`
     - `spriteImportMode = SpriteImportMode.Single`
     - Для прямоугольных кнопок и рамок (`button_rectangle_*.png`, `input_*.png`) настроить `spriteBorder`: `new Vector4(14, 14, 14, 14)` (или `16, 16, 16, 16`), чтобы они корректно масштабировались через 9-slice без размытия углов.
     - `filterMode = FilterMode.Bilinear`
3. Вызвать эту логику в `KenneyImporter.ImportAssets()` (меню **`AutoService -> Setup -> Import Kenney Assets`**).

---

## 2. Механика визуальной кастомизации авто на Локации 2

### 2.1. Доработка `CarCustomizationPresenter.cs`
В `Assets/_Project/Scripts/Presentation/Traffic/CarCustomizationPresenter.cs`:
1. Расширить enum `ServiceType`:
   ```csharp
   public enum ServiceType
   {
       None = 0,
       Tires = 1,
       Tuning = 2,
       Painting = 3
   }
   ```
2. Обновить `ResolveServiceType(string pointId, string serviceTypeId)`:
   - Если `pointId` или `serviceTypeId` содержит `"tires"` (например, `tires_loc2` или `loc2_tires_1`) $\to$ возвращать `ServiceType.Tires`.
   - Если содержит `"tuning"` (например, `tuning_loc2` или `loc2_tuning_1`) $\to$ `ServiceType.Tuning`.
   - Если содержит `"paint"` (например, `paint_loc2` или `loc2_paint_1`) $\to$ `ServiceType.Painting`.
3. В `OnServiceCompleted(ServiceCompletedEvent evt)`:
   - Находить `view = FindCarView(evt.CarId)`.
   - В зависимости от `serviceType`:
     - `ServiceType.Tires` $\to$ вызывать `view.ApplyTiresUpgrade(_darkWheelPrefab)` (или перекрашивать колеса).
     - `ServiceType.Tuning` $\to$ вызывать `view.SetSportModel(_sportPrefab)` (или ставить спойлер).
     - `ServiceType.Painting` $\to$ вызывать `view.SetBodyColor(color)` с рандомным цветом из расширенной сочной палитры.

### 2.2. Доработка `CarView.cs`
В `Assets/_Project/Scripts/Presentation/Traffic/CarView.cs`:
1. **Шиномонтаж (`ApplyTiresUpgrade`):**
   - Добавить логику смены колес:
     - Если у машины колеса представлены дочерними объектами или можно подменить колеса на `wheel-dark.prefab` / `wheel-racing.prefab` (на позициях 4 колес), либо наложить на рендеры колес `MaterialPropertyBlock` со спортивным темным/металлическим цветом.
     - Сохранять флаг `_hasCustomWheels = true;`.
2. **Тюнинг (`SetSportModel`):**
   - Убедиться, что при замене на `Visual_Sport` (`sedan-sports`):
     - Если на машине уже была применена кастомная покраска или кастомные колеса, они переносятся на новую спортивную модель.
     - Размеры и масштаб спортивного визуала соответствуют машине.
3. **Покраска (`SetBodyColor`):**
   - Применяет цвет кузова через `MaterialPropertyBlock`.
   - Гарантирует, что цвет применяется к актуальному визуалу (даже если ранее была заменена модель на `Visual_Sport`).
4. **Сброс в пул (`ResetForPool`):**
   - При возврате машины в пул:
     - `ResetVisualModel()` (восстанавливает стоковый кузов `sedan`).
     - `ClearCustomColor()` (сбрасывает покраску).
     - Сбрасывать кастомные колеса к базовому виду (`_hasCustomWheels = false`).

---

## 3. Заметные уведомления об ошибках на складе (Kenney UI)

### 3.1. Доработка `WarehouseView.cs`
1. Вместо незаметного текста в мире создать заметный плавающий бейдж:
   - Использовать плашку из `kenney_ui-pack` (например, `button_rectangle_depth_flat` красного/оранжевого цвета с 9-slice).
   - Внутри плашки — текст ошибки:
     - `"Hands full"` (руки уже заняты ящиком).
     - `"All stocked"` (все сервисы локации полностью обеспечены расходниками).
     - `"Need $X"` (недостаточно денег на покупку ящика).
2. При вызове `ShowMessage(string text)`:
   - Включать объект бейджа над складом на высоте ~2.8–3.2м (отлично виден с изометрической камеры).
   - Запускать корутину с анимацией: легкий scale punch (1.0 $\to$ 1.15 $\to$ 1.0) и небольшое покачивание, показ в течение 2 секунд, затем плавное исчезновение.

---

## 4. Рескин диалоговых окон через Kenney UI (`UiReskinnerEditor.cs`)

1. Обновить `Assets/_Project/Scripts/Editor/UiReskinnerEditor.cs`:
   - Найти импортированные спрайты Kenney UI в `Assets/_Project/Art/Kenney/kenney_ui-pack/`:
     - Фоновая панель окон: серый 9-slice (`Grey/Default/button_rectangle_depth_flat.png` или `button_rectangle_border.png`).
     - Основные кнопки действий (Купить, Улучшить, Нанять): зеленый или синий 9-slice (`Green/Default/button_rectangle_depth_flat.png`).
     - Второстепенные кнопки / Закрыть: серый или красный 9-slice.
   - Заменить в префабах окон (`PointPanel`, `StorekeeperPanel`, `OfferPanel`, `SettingsPanel`, `PauseMenu`) дефолтные текстуры на ассеты Kenney UI.
   - Убедиться, что отступы и размеры подогнаны аккуратно и элементы не наезжают друг на друга.

---

## 5. Порядок проверки

1. Запустить пункт меню **`AutoService -> Setup -> Import Kenney Assets`** (импортирует все авто, колеса и Kenney UI спрайты).
2. Запустить пункт меню **`Tools -> Reskin UI (12b)`** (обновит стиль панелей и попапов).
3. Запустить игру в Play Mode:
   - Перейти на Локацию 2.
   - Обслужить машину на **Шиномонтаже**: проверить, что колеса авто визуально заменились на спортивные темные диски.
   - Обслужить машину на **Тюнинге**: проверить, что седан превратился в спорткар со спойлером.
   - Обслужить машину на **Покраске**: проверить, что кузов перекрасился в случайный яркий цвет из палитры.
   - Проверить комбинированный заезд: машина со спортивными дисками после тюнинга и покраски сохраняет все три улучшения одновременно.
   - Кликнуть по складу с полными руками или когда все боксы заполнены: убедиться, что над складом всплывает крупный стильный бейдж с понятным уведомлением ("Hands full" / "All stocked").
   - Открыть панели улучшения бокса и склада: убедиться, что они оформлены спрайтами Kenney UI и имеют аккуратные границы.
