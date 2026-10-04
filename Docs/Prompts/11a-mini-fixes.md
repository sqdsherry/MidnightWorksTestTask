# Промпт 11a — Мини-правки UX (парковка и подсветка ценников)

**Ветка:** `feature/11a-mini-fixes` от `main`.
**Перед началом:** Убедись, что все предыдущие фиксы залиты. 

---

## 1. Цель
Внести две небольшие правки для улучшения пользовательского опыта (UX):
1. Показывать сразу **2** некупленных парковочных места в виде призраков (вместо одного).
2. Подсвечивать красным цветом ценники над зонами, которые игрок пока не может купить (из-за нехватки уровня или денег), чтобы это было видно **до** подхода к зоне и открытия панели.

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

### 2.2. Цветовая индикация недоступности (BuildableBinder)
Обнови класс `BuildableBinder`, чтобы он реагировал на изменение денег и уровня:
1. В конструктор добавь `IProgressionService progression` и `IWalletService wallet`. Сохрани их в `readonly` поля.
2. В `BuildingInstaller.cs` (около строки 55) прокинь эти зависимости в конструктор `BuildableBinder`: `context.Resolve<IProgressionService>()` и `wallet`.
3. В `BuildableBinder` подпишись на события изменения баланса и опыта:
   ```csharp
   _progression.Changed += UpdateTags;
   _wallet.Changed += UpdateTags;
   ```
4. В методе `Dispose()` обязательно отпишись от них.
5. Удали строку `view.SetPriceTag(...)` из метода `CollectViews()`.
6. Вызови `UpdateTags()` в самом конце метода `ShowCurrentState()`.
7. Реализуй метод `UpdateTags()` с использованием Rich Text (`<color>`):

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
                
                if (availability == BuildAvailability.Locked)
                {
                    text = $"<color=#FF4D4D>Lv {plot.Definition.RequiredLevel}</color>";
                }
                else if (availability == BuildAvailability.NotEnoughMoney)
                {
                    text = $"<color=#FF4D4D>{text}</color>";
                }

                pair.Value.SetPriceTag(settings.DisplayName, text);
            }
        }
```
*(Не забудь добавить `using AutoService.Services.Progression;` и `using AutoService.Services.Economy;` если их нет).*

## 3. Git
Создай коммит `Mini UX fixes: 2 parking ghosts and red price tags` и залей PR. После этого сразу переходим к Модулю 11.
