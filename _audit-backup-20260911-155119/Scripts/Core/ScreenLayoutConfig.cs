using System;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Вариант A «коридоры HUD»: все проценты и отступы экрана.
    /// Пять полос от высоты Safe Area, сумма = 1. Без «на глаз».
    /// </summary>
    [Serializable]
    public class ScreenLayoutConfig
    {
        [Header("Коридоры HUD (доля Safe Area, сумма = 1)")]
        [Range(0.06f, 0.20f)] public float topHudHeight = 0.145f;
        [Range(0.08f, 0.25f)] public float characterHeight = 0.16f;
        [Range(0.30f, 0.70f)] public float boardHeight = 0.475f;
        [Range(0.08f, 0.25f)] public float candyHeight = 0.14f;
        [Range(0.05f, 0.18f)] public float bottomHudHeight = 0.11f;

        [Header("Safe Area / внешние отступы")]
        [Range(0f, 0.20f)] public float horizontalPadding = 0.06f;
        [Range(0f, 0.15f)] public float verticalPadding = 0f;
        [Range(0f, 0.10f)] public float zoneGap = 0f;

        [Header("HUD контролы (логические px, Canvas 1080×1920)")]
        [Range(100f, 168f)] public float hudControlHeight = 148f;
        [Range(80f, 168f)] public float hudSideButtonWidth = 148f;
        [Range(16f, 32f)] public float hudBadgeButtonGap = 16f;
        [Range(80f, 168f)] public float hudBottomButtonSize = 148f;
        [Range(16f, 48f)] public float hudBottomButtonGap = 22f;

        [Header("Внутренние поля")]
        [Range(0f, 0.20f)] public float characterInnerPadding = 0.08f;
        [Range(0f, 0.15f)] public float boardFramePadding = 0.04f;
        [Range(0f, 0.15f)] public float candyBoxHorizontalPad = 0.05f;
        [Range(0f, 0.20f)] public float candyBoxVerticalPad = 0.08f;
        [Range(12f, 24f)] public float candyToBottomHudGapPx = 12f;
        [Range(16f, 24f)] public float cornerRadiusPx = 20f;
        [Range(1f, 3f)] public float strokeWidthPx = 2f;
        [Range(20f, 40f)] public float zoneLabelHeight = 32f;

        [Header("Орбита стрелки")]
        [Range(0.2f, 1f)] public float orbitGapMin = 0.35f;
        [Range(0.2f, 1.2f)] public float orbitGapMax = 0.50f;
        [Range(0.01f, 0.1f)] public float orbitGapFieldPercent = 0.03f;
        [Range(0.5f, 1.5f)] public float orbitMinLaps = 0.85f;
        [Range(0.4f, 1.6f)] public float orbitDuration = 0.85f;
        [Range(8, 24)] public int orbitCornerSegments = 12;

        [Header("Полёт в конфеты")]
        [Range(0.2f, 0.8f)] public float flyDuration = 0.42f;
        [Range(0.4f, 2.5f)] public float flyArcHeight = 1.15f;
        [Range(0.1f, 0.6f)] public float landScale = 0.3f;
        [Range(1, 6)] public int maxConcurrentFlights = 3;
        [Range(0f, 0.3f)] public float flightPhaseOffset = 0.12f;

        [Header("Камера")]
        [Range(0f, 0.15f)] public float boardFitInset = 0.04f;

        [Header("Кривые")]
        public AnimationCurve orbitEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public static readonly Color ScreenBackground = new Color(0.07f, 0.08f, 0.11f, 1f);
        public static readonly Color FrameStroke = new Color(0.88f, 0.91f, 0.96f, 0.62f);
        public static readonly Color FrameFill = new Color(1f, 1f, 1f, 0.055f);
        public static readonly Color BoardFillWorld = new Color(0.18f, 0.20f, 0.26f, 1f);
        public static readonly Color ZoneLabel = new Color(0.92f, 0.94f, 0.98f, 0.70f);
        public static readonly Color CharacterFill = new Color(0.36f, 0.78f, 0.86f, 1f);
        public static readonly Color CharacterAccent = new Color(0.16f, 0.42f, 0.50f, 1f);
        public static readonly Color CandyTray = new Color(0.72f, 0.28f, 0.38f, 0.92f);
        public static readonly Color CandyTrayInner = new Color(0.42f, 0.12f, 0.18f, 0.88f);
        public static readonly Color CandyIcon = new Color(0.98f, 0.42f, 0.55f, 1f);

        public float MiddleShare => Mathf.Max(0.2f, 1f - topHudHeight - bottomHudHeight);

        public void NormalizeHeights()
        {
            topHudHeight = Mathf.Clamp(topHudHeight, 0.06f, 0.20f);
            bottomHudHeight = Mathf.Clamp(bottomHudHeight, 0.05f, 0.18f);
            float mid = 1f - topHudHeight - bottomHudHeight;
            if (mid < 0.2f)
            {
                topHudHeight = 0.12f;
                bottomHudHeight = 0.08f;
                mid = 0.80f;
            }

            float sum = characterHeight + boardHeight + candyHeight;
            if (sum < 0.001f)
            {
                characterHeight = 0.16f * (mid / 0.80f);
                boardHeight = 0.50f * (mid / 0.80f);
                candyHeight = 0.14f * (mid / 0.80f);
                return;
            }

            float scale = mid / sum;
            characterHeight *= scale;
            boardHeight *= scale;
            candyHeight *= scale;
        }

        public float ResolveOrbitGap(float fieldWidth)
        {
            float fromPercent = fieldWidth * orbitGapFieldPercent;
            return Mathf.Clamp(fromPercent, orbitGapMin, orbitGapMax);
        }

        public static ScreenLayoutConfig CreateDefault()
        {
            return new ScreenLayoutConfig();
        }
    }
}
