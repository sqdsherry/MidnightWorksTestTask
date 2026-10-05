# Промт для Кодера: Переход на 1-в-1 клонирование боксов из Локации 1

## Контекст и проблемы
На второй локации возникли критические баги из-за процедурной генерации боксов из примитивных кубов:
1. **Нет прогресс-бара:** У процедурного бокса отсутствует компонент `ServicePointHud` с Canvas индикаторов, поэтому прогресс-бар и статус-иконки («!», прогресс, занято) физически не отображаются.
2. **Бесконечное стояние на месте:** Огромный сплошной `BoxCollider` (`bayBox` 5.6 × 6 м) на весь бокс блокировал машине путь к `CarSpot` по NavMesh. Из-за этого `CarView.TickArrival` не мог зафиксировать прибытие машины на точку, событие прибытия не срабатывало, и сервис оставался в статусе `Reserved`, даже не запуская отсчет времени обслуживания.
3. **Площадки смещены:** Вместо стандартных выверенных отступов Локации 1 использовались самодельные координаты.

В Локации 1 (`WhiteboxLocationBuilder.cs`) **все боксы (`loc1_wash_2`, `loc1_oil_1`, `loc1_oil_2`) создаются прямым клонированием эталона `loc1_wash_1`**.
Сделаем точно так же для Локации 2: откроем хелперы в `WhiteboxLocationBuilder.cs` и используем клонирование в `WhiteboxLocation2Builder.cs`.

---

### Шаг 1. В `Assets/_Project/Scripts/Bootstrap/Editor/WhiteboxLocationBuilder.cs`
Сделай следующие поля и методы `internal static` (чтобы `WhiteboxLocation2Builder` мог их переиспользовать):

1. Константы отступов площадок (примерно строки 206–209):
```csharp
internal static readonly Vector3 BayWorkSpotLocal = new Vector3(2.5f, 0f, -3.5f);
internal static readonly Vector3 BayPadFromWorkSpot = new Vector3(0f, 0f, -2f);
```

2. Метод `PlaceBayWorkSpot` (примерно строка 576):
```csharp
internal static void PlaceBayWorkSpot(ServicePointView bay)
```

3. Метод `ApproachOf` (примерно строка 861):
```csharp
internal static Transform ApproachOf(ServicePointView point)
```

4. Метод `FindPoint` (примерно строка 419):
```csharp
internal static ServicePointView FindPoint(LocationLayout layout, string pointId)
```

5. Метод `AddBayPads` (примерно строка 1012):
```csharp
internal static ManagePadView AddBayPads(ServicePointView bay, Material workPad, Material managePad, ServicePointHud referenceHud)
```

*(Методы `CreateBayGhost` и `GetOrCreateGhostMaterial` там уже имеют модификатор `internal static`).*

---

### Шаг 2. В `Assets/_Project/Scripts/Bootstrap/Editor/WhiteboxLocation2Builder.cs`

1. В методе `BuildLocation2Content` найди эталонный бокс мойки с Локации 1 и используй его для создания трех боксов Локации 2:
```csharp
// 5. Service Bays
GameObject plotsObj = CreateChild("BuildPlots", root);
var buildPlots = new List<BuildPlotView>();
var servicePoints = new List<ServicePointView>();

ServicePointView washRef = WhiteboxLocationBuilder.FindPoint(loc1, "loc1_wash_1");
if (washRef == null)
{
    Debug.LogError("[Whitebox] Reference bay 'loc1_wash_1' not found on Location_1!");
    return;
}

// Tires Bay
BuildPlotView tiresPlot = CreateClonedServiceBay(
    washRef, plotsObj.transform, "loc2_build_tires_1", "loc2_tires_1", "tires_loc2",
    new Vector3(10f, 0f, 0f), b0Tires, exitTires,
    ghostMat, workPadMat, managePadMat,
    out ServicePointView tiresPoint, out ManagePadView tiresPad);
buildPlots.Add(tiresPlot);
servicePoints.Add(tiresPoint);
managePads.Add(tiresPad);

// Tuning Bay
BuildPlotView tuningPlot = CreateClonedServiceBay(
    washRef, plotsObj.transform, "loc2_build_tuning_1", "loc2_tuning_1", "tuning_loc2",
    new Vector3(-5f, 0f, 0f), b0Tuning, exitTuning,
    ghostMat, workPadMat, managePadMat,
    out ServicePointView tuningPoint, out ManagePadView tuningPad);
buildPlots.Add(tuningPlot);
servicePoints.Add(tuningPoint);
managePads.Add(tuningPad);

// Paint Bay
BuildPlotView paintPlot = CreateClonedServiceBay(
    washRef, plotsObj.transform, "loc2_build_paint_1", "loc2_paint_1", "paint_loc2",
    new Vector3(-20f, 0f, 0f), b0Paint, exitPaint,
    ghostMat, workPadMat, managePadMat,
    out ServicePointView paintPoint, out ManagePadView paintPad);
buildPlots.Add(paintPlot);
servicePoints.Add(paintPoint);
managePads.Add(paintPad);
```

2. Замени старый метод `CreateServiceBay` и все старые хелперы процедурных кубов (`CreateFramePart`, `CreateGhostPart`, `CreateTagCanvas`, структуру `GhostTag`) на компактный метод клонирования:
```csharp
private static BuildPlotView CreateClonedServiceBay(
    ServicePointView washRef,
    Transform parent,
    string plotId,
    string pointId,
    string serviceTypeId,
    Vector3 localPos,
    RoadNode bufferNode,
    RoadNode exitNode,
    Material ghostMat,
    Material workPadMat,
    Material managePadMat,
    out ServicePointView bay,
    out ManagePadView pad)
{
    GameObject copy = UnityEngine.Object.Instantiate(washRef.gameObject, parent);
    copy.name = "Bay_" + pointId;
    copy.transform.localPosition = localPos;
    copy.transform.localRotation = Quaternion.identity;

    // Убираем визуальные вращающиеся щетки мойки (на сервисах 2 локации они не нужны)
    WashFx fx = copy.GetComponent<WashFx>();
    if (fx != null)
    {
        UnityEngine.Object.DestroyImmediate(fx);
    }

    bay = copy.GetComponent<ServicePointView>();

    // Конфигурируем ServicePointView
    var viewSo = new SerializedObject(bay);
    viewSo.FindProperty("_pointId").stringValue = pointId;
    viewSo.FindProperty("_serviceTypeId").stringValue = serviceTypeId;
    SetArrayProp(viewSo.FindProperty("_bufferSlots"), new UnityEngine.Object[] { bufferNode });
    viewSo.ApplyModifiedPropertiesWithoutUndo();

    // Соединяем CarSpot в дорожный граф
    RoadNode carSpotNode = bay.CarSpot.GetComponent<RoadNode>();
    Link(bufferNode, carSpotNode);
    Link(carSpotNode, exitNode);

    // Ставим рабочую точку и площадки (1-в-1 как на Локации 1: желтый квадрат и синий круг с иконкой апгрейда)
    WhiteboxLocationBuilder.PlaceBayWorkSpot(bay);
    pad = WhiteboxLocationBuilder.AddBayPads(bay, workPadMat, managePadMat, washRef.Hud);

    // Создаем призрак и BuildPlotView через проверенный метод Локации 1
    BuildPlotView plot = WhiteboxLocationBuilder.CreateBayGhost(washRef, bay, plotId, pointId, Vector3.zero, ghostMat, washRef.Hud);

    // Целевой бокс скрыт до покупки участка
    bay.gameObject.SetActive(false);

    return plot;
}
```

---

### Проверка
После внесения изменений выполни в меню Unity: `AutoService -> Whitebox -> Build Location 2 (and Travel Points)`.
Все боксы на Локации 2 станут точными копиями работающих боксов первой локации (с готовым Canvas HUD, рабочим прогресс-баром, свободным для заезда машин пространством и правильными желтыми и синими площадками).
