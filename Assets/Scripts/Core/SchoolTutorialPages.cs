using UnityEngine;

namespace Unpuzzle
{
    public enum SchoolTutorialScheme
    {
        KeyBook,
        BigBook,
        Pointer,
        Briefcase,
        Goal
    }

    /// <summary>Одна страница школьного обучения. Массив можно расширять новыми элементами.</summary>
    public sealed class SchoolTutorialPage
    {
        public string title;
        public string[] lines;
        public SchoolTutorialScheme scheme;
        public Color stubColor;
    }

    /// <summary>Данные страниц. Порядок и схемы здесь, текст — в GameTexts.</summary>
    public static class SchoolTutorialPages
    {
        public static readonly Color KeyBook = new Color(0.72f, 0.42f, 0.18f, 1f);
        public static readonly Color BigBook = new Color(0.52f, 0.26f, 0.10f, 1f);
        public static readonly Color Pointer = new Color(0.93f, 0.74f, 0.22f, 1f);
        public static readonly Color Briefcase = new Color(0.36f, 0.24f, 0.16f, 1f);

        public static SchoolTutorialPage[] CreateDefault()
        {
            return new[]
            {
                CreatePage(SchoolTutorialScheme.KeyBook, KeyBook),
                CreatePage(SchoolTutorialScheme.BigBook, BigBook),
                CreatePage(SchoolTutorialScheme.Pointer, Pointer),
                CreatePage(SchoolTutorialScheme.Briefcase, Briefcase),
                CreatePage(SchoolTutorialScheme.Goal, GameConstants.PlayBlue)
            };
        }

        private static SchoolTutorialPage CreatePage(SchoolTutorialScheme scheme, Color stubColor)
        {
            return new SchoolTutorialPage
            {
                title = GameTexts.TutorialTitle(scheme),
                scheme = scheme,
                stubColor = stubColor,
                lines = GameTexts.TutorialLines(scheme)
            };
        }
    }
}
