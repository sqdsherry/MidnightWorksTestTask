# Модуль 11b: Рефакторинг инсталлеров под мульти-локации и услуги Тюнинга/Покраски

## 1. Контекст и цель
Мы делаем вторую локацию (Тюнинг-ателье). В ветке `11a` мы настроили сцену, навигацию и генерацию конфигов.
Теперь нам нужно переделать инсталлеры, чтобы они запускали геймплей не только для `scene.Location1`, а для всех `LocationLayout` на сцене.
Также мы добавим логику новых услуг Тюнинга (смена типа машины) и Покраски (смена цвета).

## 2. Архитектурные изменения

### 2.1 GameplaySceneRefs и GameplayEntryPoint
- В `GameplayEntryPoint` замени поле `[SerializeField] private LocationLayout _location1;` на `[SerializeField] private LocationLayout[] _locations;`.
- В `GameplaySceneRefs` также замени `Location1` на массив `Locations`.
- Обнови передачу данных в `CreateSceneRefs`.
- В `Editor setup` (или вручную) нужно будет перетащить обе локации в этот массив.

### 2.2 ServiceLoopInstaller
- `IServicePointService` остаётся **один глобальный**.
- `PointRegistrar` можно оставить одним, регистрируя точки из всех локаций.
- Цикл `foreach (var layout in scene.Locations)` (или `for`):
  - Создаём `LocationTraffic` и `CarAgents` для каждой локации.
  - Учитываем, что проверка `TryRegister` шлагбаумов (MainEntrance, ServiceEntrance) должна происходить для каждой локации независимо, и если для какой-то локации шлагбаумов нет (или они не провалидировались), пропускаем запуск трафика именно для неё, но не роняем остальные.

### 2.3 BuildingInstaller
- `IUnlockGate` и `IBuildService` остаются **глобальными**.
- Цикл по локациям:
  - Регистрация плотов из `layout.BuildPlots`.
  - Создание `BuildableBinder` для каждой локации (передаём в него соответствующий `layout` и его `LocationTraffic`). Обрати внимание, что `_serviceLoop.Traffic` больше не единичный, нужно будет сопоставлять `LocationTraffic` с конкретной локацией (например, сохранять их в словарь `Dictionary<string, LocationTraffic>` в `ServiceLoopInstaller`).

### 2.4 StaffSuppliesInstaller
- `ISupplyService`, `IUpgradeService`, `PointIncomeTracker` остаются **глобальными**.
- `IStaffService` принимает агентов. Поскольку `StaffAgents` теперь несколько, `StaffService` должен принимать реестр агентов, например `Dictionary<string, StaffAgents> agentsByLocation` (или `IReadOnlyDictionary`).
- Цикл по локациям:
  - Создаём `StaffAgents` для локации.
  - `layout.Warehouse.Construct(...)`.
  - Создаём `PointStaffSuppliesBinder` для локации.
  - Собираем панели точек.

### 2.5 Логика Тюнинга и Покраски (Services / Presentation)
- Тюнинг (меняет модель на спорткар) и Покраска (меняет цвет машины) в рамках GDD — это просто визуальный эффект по завершении обслуживания на определенных типах точек. 
- Создай презентер или сервис (например, `CarCustomizationPresenter`), который слушает `IEventBus` (событие `ServiceCompleted`).
- В событии `ServiceCompleted` находим точку через `IServicePointService`. Если её тип `ServiceType.Tuning` — находим `CarView` по `CarId` (через реестр машин, если его нет, возможно придется добавить `ICarRegistry` или искать через `CarAgents`) и меняем визуал на спорткар. Если `ServiceType.Painting` — меняем цвет кузова (случайный цвет).

## 3. Правила реализации
- Следовать жестким правилам из `TDD.md` (никаких аллокаций в Tick, LINQ и замыканий).
- Соблюдать Dependency Injection: презентеры взаимодействуют только с событиями и сервисами, не хранят состояние.

## 4. Критерии приёмки
- Код компилируется.
- В инспекторе `GameplayEntryPoint` можно задать список из нескольких локаций.
- Игра запускается, Локация 1 работает как прежде, не выдавая ошибок.
- Написаны контракты для изменения цвета и модели автомобиля, готовые к привязке.

## 5. Ветка
Работаем в ветке `feature/11b-installers-and-services`.
