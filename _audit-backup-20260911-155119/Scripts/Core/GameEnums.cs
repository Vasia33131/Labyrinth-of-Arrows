namespace Unpuzzle
{
    /// <summary>Направление, куда выезжает стрелка при тапе (сторона наконечника).</summary>
    public enum ArrowDirection
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    /// <summary>Цвет фигуры. Новые значения только в конце — старые JSON не ломаются.</summary>
    public enum ArrowColor
    {
        Grey = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        DarkBlue = 5,
        Orange = 6
    }

    /// <summary>Что кнопка-переключатель делает со стрелкой. Вырезано из игрового цикла, оставлено для старых JSON.</summary>
    public enum SwitchRotation
    {
        Rotate90CW = 0,
        Rotate90CCW = 1,
        Rotate180 = 2
    }

    public enum GameState
    {
        Idle = 0,
        Playing = 1,
        Win = 2,
        Lose = 3
    }

    /// <summary>Глобальные бустеры HUD: подсказка, отмена хода, +1 ход.</summary>
    public enum BoosterKind
    {
        Hint = 0,
        Undo = 1,
        ExtraMove = 2
    }

    /// <summary>Результат тапа по стрелке. Любой исход, кроме Success, тратит ход.</summary>
    public enum MoveResult
    {
        Success = 0,
        Blocked = 1,
        WrongColor = 2
    }
}
