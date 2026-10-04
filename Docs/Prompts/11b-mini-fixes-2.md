# Промпт 11b — Фикс конфликта цветов и светло-зеленый hover

**Ветка:** `feature/11a-mini-fixes` (или новая от нее).

## 1. Цель
1. Изменить цвет наведения (hover) на светло-зеленый.
2. Исправить баг, из-за которого `InteractableHighlight` сбрасывал желтый цвет заблокированного призрака (возвращал синий) при уводе мышки, потому что он полностью очищал `MaterialPropertyBlock`.

## 2. Технические задачи

### 2.1. Доработка InteractableHighlight
В `Assets/_Project/Scripts/Presentation/Interaction/InteractableHighlight.cs`:
1. Измени дефолтный цвет на светло-зеленый:
   ```csharp
   private Color _highlightColor = new Color(0.6f, 1f, 0.4f, 1f); // Светло-зеленый
   ```
2. Добавь поле для хранения переопределенного цвета и флаг состояния:
   ```csharp
   private Color? _overrideBaseColor;
   private bool _isHighlighted;
   ```
3. Добавь метод для установки постоянного цвета:
   ```csharp
   public void SetOverrideBaseColor(Color? color)
   {
       _overrideBaseColor = color;
       SetHighlighted(_isHighlighted); // Переотрисовать текущее состояние
   }
   ```
4. В методе `SetHighlighted(bool highlighted, Color color)` обнови логику применения блока, чтобы он учитывал `_overrideBaseColor`:

```csharp
        public void SetHighlighted(bool highlighted, Color color)
        {
            if (_block == null) return;
            
            _isHighlighted = highlighted;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer target = _renderers[i];
                if (target == null) continue;

                if (!highlighted && _overrideBaseColor == null)
                {
                    // Если нет ховера и нет переопределения цвета — сбрасываем в дефолтный мат
                    target.SetPropertyBlock(null);
                    continue;
                }

                _block.Clear();
                if (highlighted)
                {
                    if (_property == HighlightProperty.Emission)
                    {
                        _block.SetColor(EmissionColorId, color * _intensity);
                    }
                    else
                    {
                        Color baseCol = _overrideBaseColor ?? _baseColors[i];
                        _block.SetColor(BaseColorId, Color.Lerp(baseCol, color, _intensity));
                    }
                }
                else if (_overrideBaseColor.HasValue)
                {
                    // Если ховера нет, но есть переопределенный цвет (желтый призрак)
                    _block.SetColor(BaseColorId, _overrideBaseColor.Value);
                }

                target.SetPropertyBlock(_block);
            }
        }
```

### 2.2. Упрощение в BuildPlotView
Теперь в `Assets/_Project/Scripts/Presentation/Building/BuildPlotView.cs` нам не нужно вручную перебирать рендереры, мы можем просто делегировать это нашему хайлайту:

```csharp
        public void SetGhostLockedVisual(bool isLocked)
        {
            if (_highlight != null)
            {
                _highlight.SetOverrideBaseColor(isLocked ? new Color(1f, 0.9f, 0.1f, 0.4f) : (Color?)null);
            }
        }
```

## 3. Git
Создай коммит `Fix highlight overriding locked ghost color`. После этого — точно Модуль 11!
