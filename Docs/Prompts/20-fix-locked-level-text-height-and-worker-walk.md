# Задача 20: Поднятие текста уровня в окне покупки, передвижение работника, вывески HUDNAME для сервисов и подсветка склада

## ⚠️ ВАЖНОЕ ПРАВИЛО БЕЗОПАСНОСТИ СЦЕНЫ
**Ни при каких обстоятельствах не сбрасывать, не удалять и не перемещать пользовательские объекты на Локации 2 (`TravelPlot_Travel_To_Loc1`, поднятый плейн дорог, здания и расставленный декор). Все изменения вносятся точечно и сохраняют текущую геометрию сцены.**

---

## 1. Поднятие текста требования уровня (`Requires level X`) в окне покупки

### 1.1. Проблема
В окнах предложений (`BuildPanel` и `OfferPanel` / `OfferPanelView`), если сервис заблокирован по уровню игрока:
1. Текст `Requires level X` расположен на `AnchoredPosition.y = -100` при кнопке на `-116`, из-за чего строка текста наезжает прямо на верхний край зеленой кнопки `Locked (Lv X)`.
2. Цвет шрифта белый (`#FFFFFF`), что делает его плохо различимым на светло-серой плашке карточки Kenney UI.

### 1.2. Решение в `Gameplay.unity`
Для обоих объектов в сцене (`BuildPanel` и `OfferPanel`):
1. **Для `BuildPanel/Panel/Requirement` (RectTransform `171845467`, TextMeshProUGUI `171845468`):**
   - Установить `AnchoredPosition`: `{ x: 0, y: -68 }` (ровно по центру свободного пространства между заголовком сервиса и кнопкой действия).
   - Установить `SizeDelta`: `{ x: -40, y: 28 }`.
   - Задать цвет текста `fontColor`: темный графитово-синий `{ r: 0.12, g: 0.14, b: 0.19, a: 1 }` (как у заголовка `Title`, обеспечивая четкий контраст).
   - Выставить выравнивание: `Center / Middle` (`alignment: Center | Middle`).
2. **Для `OfferPanel/Panel/Requirement` (RectTransform `2018371475`, TextMeshProUGUI `2018371476`):**
   - Установить аналогичные параметры: `AnchoredPosition.y = -68`, цвет темный графитово-синий, выравнивание по центру.

---

## 2. Исправление появления и ходьбы работника на Локации 2

### 2.1. Причина бага («работника не было визуально, но процесс пошел»)
1. В `StaffView.cs` в методе `TickArrival`:
   ```csharp
   bool closeToTarget = _target != null && Vector3.Distance(transform.position, _target.position) <= 1.8f;
   bool reachedAgentEnd = !_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 1.2f;

   if (closeToTarget || reachedAgentEnd)
   {
       StartTurning();
       return false;
   }
   ```
   **Ошибка:** В первый же кадр после вызова `WalkTo(target)` компонент `NavMeshAgent` производит расчет пути асинхронно, поэтому `_agent.hasPath == false`, либо `remainingDistance == 0f`.
   Условие `!_agent.hasPath` срабатывало **мгновенно на первом кадре**!
   `StartTurning()` немедленно сбрасывал путь `_agent.ResetPath()` и переводил агента в фазу прибытия.
   Работник **вообще не начинал идти**, а оставался стоять на точке спавна у двери домика персонала (`X=225, Z=7.8`), в то время как рабочий слот на сервисе (`X=212, Z=-3.5`) уже помечался занятым!
   В результате игрок видел пустой бокс, в котором машина ремонтировалась «призраком».

2. Ошибка компиляции CS0234: `CharacterSetupHelper.cs` был помещен в `Assets/_Project/Scripts/Editor/` (в `Assembly-CSharp-Editor`), из-за чего сборка `AutoService.Bootstrap.Editor` не видела его.

### 2.2. Правка `StaffView.cs`

Заменить логику проверки прибытия в `StaffView.TickArrival`:

```csharp
case Phase.Walking:
    if (!_agent.isOnNavMesh)
    {
        SnapToTarget("is not on the NavMesh");
        return false;
    }

    // Если путь еще рассчитывается в фоне — ждем завершения расчета
    if (_agent.pathPending)
    {
        return false;
    }

    // Если путь вообще недостижим (нет проходимой сетки)
    if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
    {
        SnapToTarget("has invalid path");
        return false;
    }

    // Если у агента еще нет пути — ждем инициализации NavMesh
    if (!_agent.hasPath)
    {
        return false;
    }

    // Проверяем реальное приближение к цели
    bool closeToTarget = _target != null && Vector3.Distance(transform.position, _target.position) <= 1.8f;
    bool reachedPathEnd = _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, 0.5f);

    if (closeToTarget)
    {
        StartTurning();
        return false;
    }

    if (reachedPathEnd)
    {
        // Дошел до конца доступного пути NavMesh.
        // Если остался небольшой разрыв до объекта из-за obstacle, дотягиваем до точки
        if (_target != null && Vector3.Distance(transform.position, _target.position) > 2.0f)
        {
            SnapToTarget("path ended before target; snapping to work spot");
        }
        else
        {
            StartTurning();
        }
        return false;
    }

    return false;
```

Также в `StaffView.SetRole(StaffRole role)` предусмотреть безопасный фоллбэк:
Если дочерний объект `Visual` отсутствует или в нем нет нужных моделей, не обнулять `_body` и сохранить видимость базовых компонентов:
```csharp
public void SetRole(AutoService.Domain.Staff.StaffRole role)
{
    Transform vis = transform.Find("Visual");
    if (vis != null)
    {
        Transform worker = vis.Find("Worker");
        Transform storekeeper = vis.Find("Storekeeper");
        if (worker != null && storekeeper != null)
        {
            worker.gameObject.SetActive(role == AutoService.Domain.Staff.StaffRole.PointWorker);
            storekeeper.gameObject.SetActive(role == AutoService.Domain.Staff.StaffRole.Storekeeper);
            Renderer r = role == AutoService.Domain.Staff.StaffRole.PointWorker 
                ? worker.GetComponentInChildren<Renderer>() 
                : storekeeper.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                _body = r;
            }
            return;
        }
    }

    // Фоллбэк: если используется базовый префаб с Body/Head
    Transform fallbackBody = transform.Find("Body");
    if (fallbackBody != null)
    {
        fallbackBody.gameObject.SetActive(true);
        if (_body == null)
        {
            _body = fallbackBody.GetComponent<Renderer>();
        }
    }
    Transform fallbackHead = transform.Find("Head");
    if (fallbackHead != null)
    {
        fallbackHead.gameObject.SetActive(true);
    }
}
```

### 2.3. Размещение и запуск `CharacterSetupHelper.cs`

1. **Перемещение файла:**
   Переместить файл из `Assets/_Project/Scripts/Editor/CharacterSetupHelper.cs` в:
   `Assets/_Project/Scripts/Bootstrap/Editor/CharacterSetupHelper.cs`.
2. **Пространство имен:**
   Указать `namespace AutoService.Bootstrap.Editor`.
3. **Вызов в `Location2SceneUpdater.ApplyFixes()`:**
   Вызывать `CharacterSetupHelper.ConfigureAll();`.

---

## 3. Вывески `HUDNAME` над всеми сервисами (Локации 1 и 2, кроме шлагбаума)

### 3.1. Требование
Создать парящие аккуратные вывески `HUDNAME` над каждым сервисом:
- **На Локации 1:**
  - `Wash` (`Bay_loc1_wash_1`): **"Car Wash 1"**
  - `Bay_loc1_wash_2`: **"Car Wash 2"**
  - `Bay_loc1_oil_1`: **"Oil Service 1"**
  - `Bay_loc1_oil_2`: **"Oil Service 2"**
- **На Локации 2:**
  - `Bay_loc2_tires_1`: **"Tire Service"**
  - `Bay_loc2_tuning_1`: **"Tuning"**
  - `Bay_loc2_paint_1`: **"Paint Shop"**
- *(Отказ от шлагбаумов: для `Barrier` и `Barrier_Service` вывески НЕ добавлять согласно указанию пользователя)*.

### 3.2. Визуальный стиль и параметры (в точности как у `Warehouse_1`)
1. **Иерархия:** Вывеска является дочерним объектом целевого объекта бокса (например, `Bay_loc1_wash_2/HUDNAME`).
   - *Благодаря этому вывеска скрыта, пока сервис заблокирован/не куплен, и автоматически загорается при покупке сервиса игроком!*
2. **Параметры `HUDNAME`:**
   - Компоненты: `RectTransform`, `Canvas` (`RenderMode = WorldSpace`).
   - `localPosition`: `{ x: 0, y: 3.5f, z: -1.55f }` (над центром сервиса на высоте ~3.5м, аналогично положению `PanelAnchor`).
   - `localEulerAngles`: `{ x: 30, y: 0, z: 0 }` (наклон 30 градусов вперед к камере, в точности как `MessageBadge` на `Warehouse_1`).
   - `sizeDelta`: `{ x: 280, y: 70 }`.
   - `localScale`: `{ x: 0.006, y: 0.006, z: 0.006 }`.
3. **Дочерний объект `Background`:**
   - Компоненты: `RectTransform` (растянут по родителю), `Image`.
   - `sprite`: `button_rectangle_depth_flat.png` (`Assets/_Project/Art/Kenney/kenney_ui-pack/PNG/Default/button_rectangle_depth_flat.png`, GUID: `d1e5c95d60b669c438916f98126ffe42`).
   - `type`: `Image.Type.Sliced`.
   - `color`: `{ r: 0.95, g: 0.95, b: 0.98, a: 0.98 }`.
4. **Дочерний объект `Text`:**
   - Компоненты: `RectTransform` (отступы 8px), `TextMeshProUGUI`.
   - `font`: `LiberationSans SDF` (GUID: `8f586378b4e144a9851e7b34d9b748ee`).
   - `fontSize`: 26.
   - `fontStyle`: `FontStyles.Bold`.
   - `alignment`: `TextAlignmentOptions.Center`.
   - `color`: темный графитово-синий `{ r: 0.15, g: 0.18, b: 0.25, a: 1 }`.
   - `text`: Название сервиса.

### 3.3. Автоматизация в `Location2SceneUpdater.cs`
Добавить вспомогательный метод `EnsureServiceBayHudNames()` в `Location2SceneUpdater.cs` и вызывать его в `ApplyFixes()`:
```csharp
private static void EnsureServiceBayHudNames()
{
    var bays = new (string BayName, string DisplayName)[]
    {
        ("Wash", "Car Wash 1"),
        ("Bay_loc1_wash_2", "Car Wash 2"),
        ("Bay_loc1_oil_1", "Oil Service 1"),
        ("Bay_loc1_oil_2", "Oil Service 2"),
        ("Bay_loc2_tires_1", "Tire Service"),
        ("Bay_loc2_tuning_1", "Tuning"),
        ("Bay_loc2_paint_1", "Paint Shop")
    };

    Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
        "Assets/_Project/Art/Kenney/kenney_ui-pack/PNG/Default/button_rectangle_depth_flat.png");
    TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/_Project/Art/Kenney/kenney_ui-pack/Font/LiberationSans SDF.asset");

    foreach (var (bayName, displayName) in bays)
    {
        GameObject bayGo = GameObject.Find(bayName);
        if (bayGo == null)
        {
            // Поиск среди неактивных объектов
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name == bayName && t.gameObject.scene.isLoaded)
                {
                    bayGo = t.gameObject;
                    break;
                }
            }
        }

        if (bayGo == null) continue;

        Transform existing = bayGo.transform.Find("HUDNAME");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var hudGo = new GameObject("HUDNAME", typeof(RectTransform), typeof(Canvas));
        hudGo.transform.SetParent(bayGo.transform, false);
        hudGo.transform.localPosition = new Vector3(0f, 3.5f, -1.55f);
        hudGo.transform.localEulerAngles = new Vector3(30f, 0f, 0f);
        hudGo.transform.localScale = new Vector3(0.006f, 0.006f, 0.006f);

        var canvas = hudGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | 
                                          AdditionalCanvasShaderChannels.Normal | 
                                          AdditionalCanvasShaderChannels.Tangent;

        var rt = hudGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 70f);

        // Background
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(hudGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.95f, 0.95f, 0.98f, 0.98f);
        bgImg.raycastTarget = false;

        // Text
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(bgGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 4f);
        textRt.offsetMax = new Vector2(-8f, -4f);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = displayName;
        tmp.fontSize = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.15f, 0.18f, 0.25f, 1f);
        tmp.raycastTarget = false;

        EditorUtility.SetDirty(bayGo);
    }

    Debug.Log("[Location2SceneUpdater] Successfully created HUDNAME plates for all service bays!");
}
```

---

## 4. Подсветка `Warehouse_1` (и `Warehouse_loc2`) для новых моделей из пака

### 4.1. Проблема
Пользователь отключил старый бокс `Body` (`m_IsActive: 0`) и добавил внутрь `Warehouse_1` новые 3D-модели из пака Kenney (`detail-awning.fbx`, ящики и модули склада).
Компонент `InteractableHighlight` на `Warehouse_1` хранил ссылку в массиве `_renderers` только на отключенный `Body`. При наведении курсора скрипт пытался окрасить неактивный меш, и склад визуально не подсвечивался.

### 4.2. Правка кода `InteractableHighlight.cs`
Сделать автопоиск активных рендереров дочерних объектов при старте и перед подсветкой, чтобы замена моделей игроком никогда не ломала ховер:
```csharp
private void Awake()
{
    EnsureRenderers();
}

private void EnsureRenderers()
{
    if (_block == null)
    {
        _block = new MaterialPropertyBlock();
    }

    // Проверяем: есть ли хотя бы один действительный активный рендерер
    bool hasValid = false;
    if (_renderers != null && _renderers.Length > 0)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].gameObject.activeInHierarchy)
            {
                hasValid = true;
                break;
            }
        }
    }

    // Если нет — собираем все активные MeshRenderer в дочерних объектах
    if (!hasValid)
    {
        var found = GetComponentsInChildren<MeshRenderer>(false);
        if (found != null && found.Length > 0)
        {
            _renderers = found;
        }
    }

    _baseColors = new Color[_renderers != null ? _renderers.Length : 0];
    for (int i = 0; i < _baseColors.Length; i++)
    {
        Material material = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
        _baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
    }
}

public void SetHighlighted(bool highlighted, Color color)
{
    if (_block == null || _baseColors == null || _baseColors.Length != (_renderers != null ? _renderers.Length : 0))
    {
        EnsureRenderers();
    }
    // ... далее существующая логика SetHighlighted ...
```

### 4.3. Автоматизация в редакторе (`Location2SceneUpdater.cs`)
В метод `UpdateWarehouses` добавить автоматическую перепривязку `_renderers` и актуализацию размеров `BoxCollider`:
```csharp
// Для каждого склада (Warehouse_1, Warehouse_loc2):
var highlight = wh.GetComponent<InteractableHighlight>();
if (highlight != null)
{
    var activeRenderers = wh.GetComponentsInChildren<MeshRenderer>(false);
    if (activeRenderers.Length > 0)
    {
        var so = new SerializedObject(highlight);
        var rendProp = so.FindProperty("_renderers");
        rendProp.arraySize = activeRenderers.Length;
        for (int r = 0; r < activeRenderers.Length; r++)
        {
            rendProp.GetArrayElementAtIndex(r).objectReferenceValue = activeRenderers[r];
        }
        so.ApplyModifiedProperties();
    }
}

// Актуализация BoxCollider склада по общим границам моделей
var collider = wh.GetComponent<BoxCollider>();
if (collider != null)
{
    var allRenderers = wh.GetComponentsInChildren<MeshRenderer>(false);
    if (allRenderers.Length > 0)
    {
        Bounds b = allRenderers[0].bounds;
        for (int r = 1; r < allRenderers.Length; r++)
        {
            b.Encapsulate(allRenderers[r].bounds);
        }
        collider.center = wh.transform.InverseTransformPoint(b.center);
        collider.size = b.size;
        EditorUtility.SetDirty(collider);
    }
}
```

---

## 5. Критерии приемки
1. **Текст требования уровня:** В окне покупки недоступного сервиса надпись `Requires level X` расположена ровно на `y = -68`, не задевает верх кнопки `Locked (Lv X)`, окрашена в контрастный темно-синий цвет и выровнена по центру.
2. **Ходьба работника:** При найме работника на сервисе Локации 2 работник корректно спавнится у двери `StaffRoom`, плавно идет по NavMesh до рабочего бокса сервиса и встает на `WorkSpot`.
3. **Вывески HUDNAME:** Над каждым сервисом (4 на Локации 1, 3 на Локации 2) аккуратно парит стильная вывеска с названием сервиса в стиле Kenney UI под углом 30° к камере. Вывеска автоматически становится видимой при покупке сервиса.
4. **Подсветка склада:** При наведении курсора мыши на `Warehouse_1` все активные детали новой модели склада Kenney плавно подсвечиваются мягким зеленым цветом (и сбрасывают подсветку при уводе курсора).
5. **Сборка:** Команда `dotnet build MidnightWorksTestTask.sln` выполняется успешно с **0 ошибок**.
