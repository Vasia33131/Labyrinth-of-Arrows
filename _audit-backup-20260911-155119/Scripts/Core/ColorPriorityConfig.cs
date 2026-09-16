using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Правило цветового приоритета: цвет, который стоит выше в priorityOrder,
    /// должен быть полностью убран с поля раньше тех, что ниже.
    /// Цвета, которых нет в списке (по умолчанию Grey), убираются в любой момент.
    /// Asset: Create > Unpuzzle > Color Priority Config.
    /// </summary>
    [CreateAssetMenu(menuName = "Unpuzzle/Color Priority Config", fileName = "ColorPriorityConfig")]
    public class ColorPriorityConfig : ScriptableObject
    {
        [Tooltip("Выключи, чтобы правило цвета не работало (отладка).")]
        public bool rulesEnabled = true;

        [Tooltip("Сверху — самый приоритетный цвет. Пока на поле есть цвет выше, нижние убрать нельзя.")]
        public List<ArrowColor> priorityOrder = new List<ArrowColor> { ArrowColor.Blue, ArrowColor.Red };

        /// <summary>Ранг цвета: 0 = самый приоритетный, -1 = цвет вне правила (можно убирать всегда).</summary>
        public int GetRank(ArrowColor color)
        {
            if (!rulesEnabled || priorityOrder == null) return -1;
            return priorityOrder.IndexOf(color);
        }

        /// <summary>Цвет, который сейчас обязателен к уборке. false — правило ничего не требует.</summary>
        public bool TryGetRequiredColor(IReadOnlyList<ArrowController> arrowsOnField, out ArrowColor requiredColor)
        {
            requiredColor = ArrowColor.Grey;
            if (!rulesEnabled || arrowsOnField == null) return false;

            int bestRank = int.MaxValue;
            for (int i = 0; i < arrowsOnField.Count; i++)
            {
                ArrowController arrow = arrowsOnField[i];
                if (arrow == null || !arrow.OccupiesGrid) continue;

                int rank = GetRank(arrow.color);
                if (rank < 0 || rank >= bestRank) continue;

                bestRank = rank;
                requiredColor = arrow.color;
            }

            return bestRank != int.MaxValue;
        }

        /// <summary>Можно ли по правилу цвета убрать стрелку такого цвета прямо сейчас.</summary>
        public bool IsRemovalAllowed(ArrowColor color, IReadOnlyList<ArrowController> arrowsOnField, out ArrowColor blockingColor)
        {
            blockingColor = color;

            int rank = GetRank(color);
            if (rank < 0) return true;

            if (!TryGetRequiredColor(arrowsOnField, out ArrowColor requiredColor)) return true;
            if (GetRank(requiredColor) >= rank) return true;

            blockingColor = requiredColor;
            return false;
        }

    }
}
