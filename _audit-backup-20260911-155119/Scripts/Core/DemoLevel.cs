namespace Unpuzzle
{
    /// <summary>
    /// Демо из 4 фигур как на референсе Unpuzzle.
    /// Решение: правая вертикаль вниз → верхняя ломаная вправо → синяя L вверх → красная вверх.
    /// Верхняя ломаная смотрит вправо (не вниз): иначе L вверх и верх вниз в общих колонках
    /// взаимно блокируют друг друга навсегда.
    /// </summary>
    public static class DemoLevel
    {
        public static LevelData Create()
        {
            return new LevelData
            {
                id = 1,
                gridWidth = 6,
                gridHeight = 8,
                movesLimit = 8,
                solution = new[] { "vertical", "topZig", "blueL", "redZig" },
                arrows = new[]
                {
                    new LevelArrowData
                    {
                        id = "vertical",
                        x = 5, y = 6,
                        dir = "Down",
                        color = "DarkBlue",
                        pathX = new[] { 5, 5, 5, 5, 5 },
                        pathY = new[] { 6, 5, 4, 3, 2 }
                    },
                    new LevelArrowData
                    {
                        id = "blueL",
                        x = 1, y = 3,
                        dir = "Up",
                        color = "Blue",
                        pathX = new[] { 1, 1, 1, 2, 3 },
                        pathY = new[] { 3, 4, 5, 5, 5 }
                    },
                    new LevelArrowData
                    {
                        id = "redZig",
                        x = 1, y = 2,
                        dir = "Up",
                        color = "Red",
                        pathX = new[] { 1, 2, 2, 3 },
                        pathY = new[] { 2, 2, 1, 1 }
                    },
                    new LevelArrowData
                    {
                        id = "topZig",
                        x = 1, y = 6,
                        dir = "Right",
                        color = "DarkBlue",
                        pathX = new[] { 1, 1, 2, 3, 4 },
                        pathY = new[] { 6, 7, 7, 7, 7 }
                    }
                },
                buttons = System.Array.Empty<LevelButtonData>()
            };
        }
    }
}
