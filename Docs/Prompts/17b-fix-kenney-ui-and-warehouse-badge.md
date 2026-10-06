# Задача 17b: Исправление импорта спрайтов Kenney UI, рескина диалогов и бейджа склада

## ⚠️ ВАЖНОЕ НАПОМИНАНИЕ
**Не ломать и не перемещать пользовательские объекты на Локации 2 (`TravelPlot_Travel_To_Loc1`, поднятый плейн дорог и т.д.). Все доработки по коду и ассетам вносятся без деструктивного сброса сцены.**

---

## Проблема, выявленная при ревью
1. **Спрайты Kenney UI не были сконфигурированы как Sprite:**
   - PNG файлы скопированы в `Assets/_Project/Art/Kenney/kenney_ui-pack/`, но их мета-файлы остались с `textureType: 0` (`Texture2D`), а не `Sprite`.
   - `ConfigureUiSprites()` в `KenneyImporter.cs` выполнялся только при условии `if (anyCopied)`. При повторных запусках файлы уже существовали на диске, и конфигуратор никогда не вызывался.
   - Из-за этого `AssetDatabase.LoadAssetAtPath<Sprite>(...)` возвращал `null`.
2. **Рескин UI окон применил дефолтный `Background.psd`, а не Kenney UI:**
   - Из-за п.1 `UiReskinnerEditor.cs` падал на fallback `UI/Skin/Background.psd`. Диалоги и кнопки не получили графику из Kenney UI.
3. **Бейдж уведомления склада не добавлен на сцену:**
   - В `Gameplay.unity` у объектов склада поле `_messageRoot` осталось пустым (`None`), а `MessageBadge` не был создан, так как `Location2SceneUpdater.ApplyFixes()` не был применен к сцене после добавления нового функционала.
4. **Префабы новых авто (`race`, `hatchback-sports`, `suv-luxury`) не сгенерированы в `Prefabs/Art`:**
   - Модели скопированы, но `CreateWrappers` не создал префабы-обертки для них.

---

## 1. Доработка `KenneyImporter.cs`

В `Assets/_Project/Scripts/Bootstrap/Editor/KenneyImporter.cs`:
1. Добавить отдельный пункт меню для принудительной настройки всех спрайтов UI:
   ```csharp
   [MenuItem("AutoService/Setup/Configure Kenney UI Sprites")]
   public static void ConfigureUiSprites()
   ```
2. В `ConfigureUiSprites()` сделать гарантированный обход файлов через `Directory.GetFiles`:
   ```csharp
   string diskDir = Path.Combine(Application.dataPath, "_Project/Art/Kenney/kenney_ui-pack");
   if (!Directory.Exists(diskDir)) return;

   string[] pngFiles = Directory.GetFiles(diskDir, "*.png", SearchOption.AllDirectories);
   foreach (string fullPath in pngFiles)
   {
       string relPath = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
       var importer = AssetImporter.GetAtPath(relPath) as TextureImporter;
       if (importer == null) continue;

       bool changed = false;
       if (importer.textureType != TextureImporterType.Sprite)
       {
           importer.textureType = TextureImporterType.Sprite;
           changed = true;
       }
       if (importer.spriteImportMode != SpriteImportMode.Single)
       {
           importer.spriteImportMode = SpriteImportMode.Single;
           changed = true;
       }
       if (importer.filterMode != FilterMode.Bilinear)
       {
           importer.filterMode = FilterMode.Bilinear;
           changed = true;
       }

       string fileName = Path.GetFileNameWithoutExtension(relPath).ToLowerInvariant();
       if (fileName.StartsWith("button_rectangle") || fileName.StartsWith("input_") || fileName.StartsWith("button_square"))
       {
           Vector4 targetBorder = new Vector4(14, 14, 14, 14);
           if (Vector4.Distance(importer.spriteBorder, targetBorder) > 0.01f)
           {
               importer.spriteBorder = targetBorder;
               changed = true;
           }
       }

       if (changed)
       {
           importer.SaveAndReimport();
       }
   }
   AssetDatabase.Refresh();
   ```
3. В `ImportAssets()` вызывать `ConfigureUiSprites()` **всегда**, а не только когда `anyCopied == true`.
4. В `KenneyImporter_Wrappers.cs`: убедиться, что `CreateWrappers()` генерирует префабы в `Assets/_Project/Prefabs/Art/` для новых машин (`race`, `hatchback-sports`, `suv-luxury`).

---

## 2. Доработка `UiReskinnerEditor.cs`

1. Проверить пути к спрайтам Kenney UI:
   - Панель окон: `Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_rectangle_depth_flat.png` (или `button_rectangle_border.png`)
   - Зеленая кнопка (Купить/Улучшить): `Assets/_Project/Art/Kenney/kenney_ui-pack/Green/Default/button_rectangle_depth_flat.png`
   - Синяя кнопка (Общие действия): `Assets/_Project/Art/Kenney/kenney_ui-pack/Blue/Default/button_rectangle_depth_flat.png`
   - Красная кнопка (Закрыть/Отмена): `Assets/_Project/Art/Kenney/kenney_ui-pack/Red/Default/button_rectangle_depth_flat.png`
   - Серая кнопка (Настройки/Пауза): `Assets/_Project/Art/Kenney/kenney_ui-pack/Grey/Default/button_rectangle_depth_flat.png`
2. Добавить проверку:
   ```csharp
   if (panelSprite == null || greenBtnSprite == null)
   {
       Debug.LogError("[UiReskinner] Kenney UI sprites are not imported as Sprites! Run 'AutoService -> Setup -> Configure Kenney UI Sprites' first.");
       return;
   }
   ```
3. При рескине кнопок `Button`:
   - Устанавливать `Image.type = Image.Type.Sliced`
   - Устанавливать `Image.color = Color.white` (чтобы цвет спрайта Kenney отображался чисто).
4. При рескине панелей (`Panel`, `PointPanel`, `StorekeeperPanel`, `SettingsPanel`, `PauseMenu`):
   - Устанавливать фон `panelSprite`
   - `Image.type = Image.Type.Sliced`
   - `Image.color = Color.white` (или легкий полупрозрачный оттенок `new Color(1f, 1f, 1f, 0.95f)`).

---

## 3. Обновление сцены через `Location2SceneUpdater.cs`

1. Запустить пункт меню **`AutoService -> Setup -> Apply Location 2 and Warehouse Fixes`**:
   - Он обновит объекты склада на Локации 1 и Локации 2:
     - Создаст дочерний `MessageBadge` со спрайтом `button_rectangle_depth_flat.png` (красный или желтый Kenney UI с 9-slice).
     - Добавит `CanvasGroup` для анимации фейда.
     - Привяжет созданный `MessageBadge` в поле `_messageRoot` компонента `WarehouseView`.
     - Сохранит сцену `Gameplay.unity`.

---

## 4. Чек-лист проверки в Unity

1. Выполнить **`AutoService -> Setup -> Configure Kenney UI Sprites`** (убедиться в консоли, что спрайты переимпортированы как `Sprite`).
2. Выполнить **`AutoService -> Setup -> Import Kenney Assets`** (создадутся префабы оберток для всех новых авто).
3. Выполнить **`Tools -> Reskin UI (12b)`** (окна и кнопки получат спрайты Kenney UI с красивыми скругленными углами и цветами).
4. Выполнить **`AutoService -> Setup -> Apply Location 2 and Warehouse Fixes`** (склады получат 3D бейдж уведомления `MessageBadge`).
5. Запустить игру в Play Mode:
   - Проверить склад: кликнуть при полных руках — над складом должен появиться крупный сочный бейдж Kenney UI с надписью "Hands full" и анимацией punch/shake.
   - Проверить окна: открыть панель точки обслуживания или паузу — окно и кнопки должны быть оформлены графикой Kenney UI.
   - Проверить кастомизацию авто на Локации 2 (шины, тюнинг, покраска) — убедиться, что все эффекты отображаются и накапливаются.
