# Промпт 11a — Мини-правки UX (парковка и желтые призраки)

**Ветка:** `feature/11a-mini-fixes` от `main`.
**Перед началом:** Убедись, что все предыдущие фиксы залиты. 

---

## 1. Цель
Внести две небольшие правки для улучшения пользовательского опыта (UX):
1. Показывать сразу **2** некупленных парковочных места в виде призраков (вместо одного).
2. Подсвечивать сам **полупрозрачный материал призрака** другим цветом (например, жёлтым), если игрок пока не может купить зону (из-за нехватки уровня).

## 2. Технические задачи

### 2.1. Показ двух парковочных мест
В `Assets/_Project/Scripts/Presentation/Building/BuildableBinder.cs` обнови логику `RefreshParkingOffers`. Вместо флага `nextOffered` используй счетчик, чтобы отображать два слота:

```csharp
        private void RefreshParkingOffers()
        {
            int offeredCount = 0;
            for (int i = 0; i < _parkingViews.Count; i++)
            {
                BuildPlotView view = _parkingViews[i];
                if (view.IsBuilt)
                {
                    continue;
                }

                view.SetOffered(offeredCount < 2);
                offeredCount++;
            }
        }
```

### 2.2. Изменение цвета призрака (BuildPlotView)
В класс `BuildPlotView` добавь метод для динамического изменения цвета материала через `MaterialPropertyBlock` (чтобы не плодить инстансы материалов):

```csharp
        public void SetGhostLockedVisual(bool isLocked)
        {
            if (_ghost == null) return;
            var block = new MaterialPropertyBlock();
            Renderer[] renderers = _ghost.GetComponentsInChildren<Renderer>(true);
            
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                if (isLocked)
                {
                    // Желтый полупрозрачный цвет для URP Lit
                    block.SetColor("_BaseColor", new Color(1f, 0.9f, 0.1f, 0.4f)); 
                }
                else
                {
                    block.Clear();
                }
                r.SetPropertyBlock(block);
            }
        }
```

### 2.3. Привязка состояния в BuildableBinder
Обнови класс `BuildableBinder`, чтобы он реагировал на прогрессию:
1. В конструктор добавь `IProgressionService progression` и сохрани его в `readonly` поле.
2. В `BuildingInstaller.cs` прокинь этот сервис в конструктор `BuildableBinder`: `context.Resolve<AutoService.Services.Progression.IProgressionService>()`.
3. В `BuildableBinder` подпишись на событие уровня:
   ```csharp
   _progression.Changed += UpdateTags;
   ```
4. В методе `Dispose()` обязательно отпишись от него.
5. Удали строку `view.SetPriceTag(...)` из метода `CollectViews()`.
6. Вызови `UpdateTags()` в самом конце метода `ShowCurrentState()`.
7. Реализуй метод `UpdateTags()`, который меняет и цвет призрака, и текст ценника:

```csharp
        private void UpdateTags()
        {
            foreach (KeyValuePair<string, BuildPlotView> pair in _views)
            {
                if (!_build.TryGet(pair.Key, out BuildPlot plot) || plot.IsBuilt || !_config.TryGetBuildable(pair.Key, out BuildableSettings settings))
                {
                    continue;
                }

                BuildAvailability availability = _build.GetAvailability(pair.Key);
                string text = MoneyFormatter.Format(settings.Cost);
                bool isLocked = availability == BuildAvailability.Locked;
                
                if (isLocked)
                {
                    text = $"<color=#FF4D4D>Lv {plot.Definition.RequiredLevel}</color>";
                }
                else if (availability == BuildAvailability.NotEnoughMoney)
                {
                    text = $"<color=#FF4D4D>{text}</color>";
                }

                pair.Value.SetPriceTag(settings.DisplayName, text);
                pair.Value.SetGhostLockedVisual(isLocked);
            }
        }
```

## 3. Git
Создай коммит `Mini UX fixes: 2 parking ghosts and yellow locked visual` и залей PR. После этого сразу переходим к Модулю 11.
