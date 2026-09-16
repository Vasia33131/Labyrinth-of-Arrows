# Unpuzzle (Убери стрелки) — Шаг 1: каркас

Проект: Unity **2022.3.62f1**, URP 2D, старый Input Manager (`Input.*`), uGUI. Таргет — WebGL / Яндекс Игры.

## Запуск
1. Открой проект `C:\Users\diman\strelki` в Unity Hub.
2. Дождись компиляции, затем меню: **Tools → Unpuzzle → Setup Project (Step 1)**.
   Скрипт сам создаст: слои `Arrow`, `ButtonSwitch`, спрайты-заглушки, префабы `Arrow` и `ButtonSwitch`,
   сцену `Assets/Scenes/GameScene.unity` со всеми проставленными ссылками, и добавит сцену в Build Settings.
3. Открой `Assets/Scenes/GameScene.unity` → Play. Спавнится тестовая сетка 5×7, тап убирает стрелку,
   счётчики в UI обновляются, при 0 стрелок — панель победы.

## Иерархия сцены
```
GameScene
├─ Main Camera        Orthographic, Size = 6, Pos (0, 0, -10), Solid Color
├─ GameManager        GameManager + LevelGenerator + InputHandler
├─ LevelRoot          (пустой; сюда спавнятся стрелки)
├─ Ground             BoxCollider2D 20×1, Y = -5.5 (ловит падающие стрелки)
├─ UI Canvas          Canvas (Overlay) + CanvasScaler 1080×1920, Match 0.5 + GraphicRaycaster + UIManager
│  ├─ MovesText / ArrowsText
│  ├─ RestartButton
│  ├─ WinPanel  (TitleText, RestartButton, NextLevelButton) — выключена
│  └─ LosePanel (TitleText, RestartButton) — выключена
└─ EventSystem
```

Камера: портрет 1080×1920 (aspect 0.5625), ortho size **6** → видимая область 6.75 × 12 юнитов.
Клетка = 1 юнит (PPU 128), поле 5×7 помещается с полями.
Для ландшафта: ortho size 5 и Canvas reference 1920×1080.

## Что на каком объекте (Inspector)
| Объект | Компонент | Ссылки |
|---|---|---|
| GameManager | `GameManager` | Level Generator → сам объект, Input Handler → сам объект, UI Manager → UI Canvas, Level Root → LevelRoot, Moves Limit = 30 |
| GameManager | `LevelGenerator` | Level Root → LevelRoot, Arrow Prefab → `Prefabs/Arrow`, Button Switch Prefab → `Prefabs/ButtonSwitch`, Grid Size = 5×7 |
| GameManager | `InputHandler` | Game Camera → Main Camera, Game Manager → сам объект, **Arrow Mask = только слой Arrow** |
| UI Canvas | `UIManager` | Game Manager → GameManager, Moves Text, Arrows Text, Win Panel, Lose Panel, Restart Button (все 3) , Next Level Button |

## Префабы
- **Arrow** — SpriteRenderer (sortingOrder 10), Rigidbody2D (**Gravity Scale 0**, Freeze Rotation Z, Interpolate, Continuous),
  BoxCollider2D 0.96×0.96, `ArrowController`, слой **Arrow**.
- **ButtonSwitch** — SpriteRenderer, BoxCollider2D **Is Trigger**, `ButtonSwitch`, слой **ButtonSwitch**.

## Физика
- Raycast ввода идёт строго по маске `Arrow` (`InputHandler.arrowMask`), тапы поверх UI игнорируются.
- Стрелка «на опоре»: `gravityScale = 0`. Опора снята → `ArrowController.ReleaseSupport()` ставит `gravityScale = 3` (домино).
- Тап → `TryRemove()`: коллайдер off, тело Kinematic, стрелка уезжает по своему направлению за экран → `GameManager.OnArrowRemoved`.

## ШАГ 4: кнопки-переключатели и бонусы

Меню: **Tools → Unpuzzle → Setup Bonuses (Step 4)** — вешает `BonusSystem` на GameManager,
добавляет на Canvas кнопки «Подсказка / Отмена / +1 ход» и создаёт корень `Switches`.
**Tools → Unpuzzle → Add Button Switch To Scene (Step 4)** — ставит новую зону-переключатель.

### ButtonSwitch (зона поворота)
Триггер-зона на поле. Стрелка, которая через неё **падает/движется**, поворачивается на 90° или 180°;
`direction` и спрайт обновляются, дальше `CanExit()` считает путь уже по новому направлению.

| Поле | Смысл |
|---|---|
| Rotation | `Rotate90CW` / `Rotate90CCW` / `Rotate180` |
| One Shot | зона срабатывает один раз за уровень и гаснет |
| Once Per Arrow | одну и ту же стрелку зона крутит только раз (иначе она вертится, пока падает) |
| Require Movement + Min Speed | лежащая в зоне стрелка ничего не запускает |
| Affect Only Target Color | фильтр по цвету стрелки |
| Unlock Target Color On Use | доп. эффект: снять ручной замок со стрелок этого цвета |

**Важно:** зоны кладутся в объект `Switches`, а не в `LevelRoot` — LevelRoot очищается при рестарте.

### Бонусы (`BonusSystem` на объекте GameManager)
| Бонус | Заряды по умолчанию | Что делает |
|---|---|---|
| Подсказка | 3 | подсвечивает одну безопасную стрелку (тап по ней проходит все правила Шага 3). Ход не тратит; если безопасных ходов нет — заряд не сгорает |
| Отмена хода | 1 | откат поля к состоянию до последнего хода: позиции, направления, замки, убранные стрелки, состояние зон, счётчик ходов |
| +1 ход | 0 (ставь 1 на сложный уровень) | добавляет ход; если ходы уже кончились — снимает поражение и возвращает в игру |

Отмена работает потому, что убранная стрелка не уничтожается, а прячется (`ArrowController.Despawn`)
и лежит в `LevelRoot` до конца уровня. Снимок — `LevelSnapshot.Capture/Apply`.
После отката гравитация пересчитывается: у кого под собой пусто (`HasSupportBelow`) — снова падает.

## Что дальше (шаги 5+)
- ШАГ 5: генератор 100 уровней + `Resources/Levels/level_XX.json`.
- ШАГ 6: Yandex Games SDK (реклама, лидерборды, сохранения), WebGL-настройки.
