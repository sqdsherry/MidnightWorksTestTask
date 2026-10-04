# Промпт 13c (фикс 13a) — исправление валидации Build Plots

**Ветка:** та же `feature/13a-bays-layout-rebalance`. Тот же стиль и правила `CLAUDE.md`. Закоммить этот файл вместе с фиксами.

## Проблема

В рантайме игра падает со следующей цепочкой ошибок при старте сцены `Gameplay`:
```
[Gameplay] Service loop skipped: LocationLayout 'Location_1': Build Plots element 0 is empty.
[Gameplay] Building skipped: the service loop of location 1 is not running.
[Gameplay] Staff & supplies skipped: the service loop of location 1 is not running.
```
В инспекторе `Location_1` в поле `_buildPlots` висят пустые ссылки `None (Missing)` вместо актуальных компонентов призраков стройки.

## Причина

В `ModuleSetupD1_Bays.cs` при пересоздании призраков боксов (строки ~131–146):
1. Старые объекты призраков удаляются через `Object.DestroyImmediate(ghost.gameObject)`, после чего создаются новые через `WhiteboxLocationBuilder.CreateBayGhost`.
2. Однако массив `_buildPlots` на компоненте `LocationLayout` не обновляется, и ссылки в сериализованном массиве сцены остаются висеть на удалённых объектах (`{fileID: 0}`).
3. Кроме того, первая автомойка (`loc1_wash_1`), которая теперь стала покупаемой за $200 (`loc1_build_wash_1`), при пересоздании призраков не связывается с массивом плотов на `LocationLayout`.

## Что должно быть исправлено

### 1. Обновление массива `_buildPlots` в `ModuleSetupD1_Bays.cs`
В конце метода `SetupBays(LocationLayout layout)`:
- Найти все актуальные `BuildPlotView` под `layout` (включая неактивные):
  ```csharp
  var allPlots = layout.GetComponentsInChildren<BuildPlotView>(true);
  ```
- Отфильтровать любые null-элементы.
- Сериализованно обновить свойство `_buildPlots` на объекте `LocationLayout` через `SerializedObject`:
  ```csharp
  var serializedLayout = new SerializedObject(layout);
  var plotsProp = serializedLayout.FindProperty("_buildPlots");
  plotsProp.arraySize = allPlots.Length;
  for (int i = 0; i < allPlots.Length; i++)
  {
      plotsProp.GetArrayElementAtIndex(i).objectReferenceValue = allPlots[i];
  }
  serializedLayout.ApplyModifiedProperties();
  EditorUtility.SetDirty(layout);
  ```
  `// Why:` гарантирует, что массив плотов на `LocationLayout` всегда строго синхронизирован с фактически существующими объектами призраков, исключая битые ссылки `None`.

### 2. Призрак первой мойки `loc1_wash_1`
- Убедиться, что для `loc1_wash_1` в `SetupBays` создаётся призрак `Ghost_loc1_wash_1` с `PlotId = "loc1_build_wash_1"`, если его ещё нет на сцене.
- Мойка `washBay.gameObject.SetActive(false);` должна быть выключена со старта, пока призрак не построен.

### 3. Верификация
- Выполнить `AutoService -> Setup -> Run D1 Setup` в Editor.
- Убедиться, что `ValidateBuildPlots` проходит без ошибок.
- Запустить сцену в Play Mode: ошибки `Build Plots element 0 is empty` и сопутствующие `Service loop skipped` должны полностью исчезнуть.
