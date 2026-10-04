# Промпт 13d (фикс валидации призраков и удаление scanner-high)

**Ветка:** `feature/13a-bays-layout-rebalance`. Тот же стиль и правила `CLAUDE.md`. Закоммить этот файл вместе с фиксами.

## Проблема 1: Ошибка валидации Build Plots
В инспекторе компонента `LocationLayout` на объекте `Location_1` массив `_buildPlots` содержит пустые ссылки:
```
Element 0: None (Missing)
Element 1: None (Missing)
Element 2: None (Missing)
Element 3: Ghost_P2
Element 4: Ghost_P3
```
Причина: в сцене объекты призраков называются `Ghost_loc1_wash_2`, `Ghost_loc1_oil` (старое имя), `Ghost_loc1_tires` (старое имя), а `Ghost_loc1_wash_1` вообще отсутствует. Метод `SetupBays` искал `Ghost_loc1_oil_1` и не находил старый `Ghost_loc1_oil`, из-за чего старые призраки не удалялись, новые не добавлялись в массив, а массив в сцене остался с битыми ссылками.

## Проблема 2: Дефектный префаб scanner-high
В `ModuleSetupD1_Bays.cs` на строках ~70–73:
```csharp
GameObject scanner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Art/scanner-high.prefab");
if (scanner != null) PrefabUtility.InstantiatePrefab(scanner, fxObj.transform);
```
Префаб `scanner-high` отображается как белый нетекстурированный блок-дефект над боксами. Пользователь снес его руками, но при сетапе он спавнится заново.

## Что должно быть сделано

### 1. Удалить спавн `scanner-high` в `ModuleSetupD1_Bays.cs`
- Полностью удалить загрузку и инстанцирование `scanner-high.prefab` из `ModuleSetupD1_Bays.cs`.
- При сетапе боксов мойки и масла этот объект не должен создаваться ни в рабочем боксе, ни в призраке.

### 2. Чистая сборка и синхронизация призраков (`_buildPlots`)
В `ModuleSetupD1_Bays.cs`:
1. **Удалить все устаревшие призраки боксов перед пересозданием:**
   Найти и уничтожить (`Object.DestroyImmediate`) любые объекты под `layout.transform`, чьи имена начинаются на `Ghost_loc1_` (включая старые `Ghost_loc1_oil`, `Ghost_loc1_tires`, `Ghost_loc1_wash_1`, `Ghost_loc1_wash_2`).
2. **Создать актуальные призраки для всех 4 боксов:**
   - Для `loc1_wash_1`: плот `loc1_build_wash_1`
   - Для `loc1_wash_2`: плот `loc1_build_wash_2`
   - Для `loc1_oil_1`: плот `loc1_build_oil_1`
   - Для `loc1_oil_2`: плот `loc1_build_oil_2`
   Использовать `WhiteboxLocationBuilder.CreateBayGhost(...)`.
   Все 4 бокса на старте должны быть неактивны: `bay.gameObject.SetActive(false)`.
3. **Обновить сериализованный массив `_buildPlots` на `LocationLayout`:**
   Собрать все существующие на сцене `BuildPlotView` (4 бокса + 2 парковочных места `Ghost_P2`, `Ghost_P3`):
   ```csharp
   var allPlots = layout.GetComponentsInChildren<BuildPlotView>(true);
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
4. **Запустить `Run D1 Setup` и сохранить сцену `Gameplay.unity` в коммит:**
   Убедиться, что в сериализованном массиве `_buildPlots` в сцене нет `{fileID: 0}`, и в Play Mode игра стартует без ошибки `Build Plots element 0 is empty`.
