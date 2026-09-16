using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// ШАГ 4. Триггер-зона на поле: стрелка, которая проезжает/падает сквозь неё,
    /// разворачивается на 90° или 180°. Направление и путь стрелки после этого считаются заново.
    /// Убранные (уезжающие) стрелки зону не видят — у них выключен коллайдер.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ButtonSwitch : MonoBehaviour
    {
        [Header("Эффект")]
        [Tooltip("На сколько повернуть стрелку, попавшую в зону.")]
        public SwitchRotation rotation = SwitchRotation.Rotate90CW;

        [Tooltip("Кнопка срабатывает один раз за уровень и гаснет.")]
        public bool oneShot = false;

        [Tooltip("Каждую конкретную стрелку зона поворачивает только один раз (иначе стрелка крутится, пока падает).")]
        public bool oncePerArrow = true;

        [Tooltip("Пауза перед повторным срабатыванием по той же стрелке (если oncePerArrow выключен).")]
        [Min(0f)] public float retriggerCooldown = 0.4f;

        [Header("Условия срабатывания")]
        [Tooltip("Реагировать только на движущуюся/падающую стрелку. Лежащая в зоне стрелка ничего не запускает.")]
        public bool requireMovement = true;

        [Min(0f)] public float minSpeed = 0.15f;

        [Tooltip("ON — поворачивает только стрелки цвета Target Color. OFF — любые.")]
        public bool affectOnlyTargetColor = false;

        public ArrowColor targetColor = ArrowColor.Red;

        [Header("Доп. эффект")]
        [Tooltip("При срабатывании снять ручной замок со всех стрелок цвета Target Color.")]
        public bool unlockTargetColorOnUse = false;

        [Header("Ссылки")]
        public SpriteRenderer spriteRenderer;
        public BoxCollider2D triggerCollider;

        [Header("Визуал")]
        public Color idleColor = new Color(0.55f, 0.85f, 0.95f, 0.75f);
        public Color usedColor = new Color(0.35f, 0.42f, 0.48f, 0.55f);
        public Color flashColor = Color.white;
        public float flashDuration = 0.18f;

        [Tooltip("Стрелка-указатель внутри зоны, показывающая сторону поворота (не обязательна).")]
        public Transform iconTransform;

        public bool IsUsed { get; private set; }

        private readonly Dictionary<ArrowController, float> lastUseTime = new Dictionary<ArrowController, float>();
        private Coroutine flashRoutine;

        private void Reset()
        {
            CacheRefs();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
                triggerCollider.size = new Vector2(0.9f, 0.9f);
            }

            GameConstants.SetLayerSafe(gameObject, GameConstants.ButtonLayerName);
        }

        private void Awake()
        {
            CacheRefs();
            if (triggerCollider != null) triggerCollider.isTrigger = true;
            ArrowSpriteFactory.EnsureSprite(spriteRenderer, square: true);
            ApplyIdleVisual();
        }

        private void CacheRefs()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
        }

        // ------------------------------------------------------------ триггер

        private void OnTriggerEnter2D(Collider2D other) => TryActivate(other);

        // Stay нужен для стрелки, которая уже стояла в зоне и только начала падать.
        private void OnTriggerStay2D(Collider2D other) => TryActivate(other);

        private void TryActivate(Collider2D other)
        {
            if (oneShot && IsUsed) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            ArrowController arrow = other.GetComponentInParent<ArrowController>();
            if (arrow == null || arrow.IsRemoving) return;
            if (affectOnlyTargetColor && arrow.color != targetColor) return;

            // Физика вырезана из игрового цикла — ButtonSwitch больше не активируется падением.
            if (requireMovement) return;

            if (lastUseTime.TryGetValue(arrow, out float previousTime))
            {
                if (oncePerArrow) return;
                if (Time.time - previousTime < retriggerCooldown) return;
            }

            lastUseTime[arrow] = Time.time;
            Activate(arrow);
        }

        private void Activate(ArrowController arrow)
        {
            IsUsed = true;

            arrow.RotateBy(RotationSteps(rotation));

            if (unlockTargetColorOnUse) UnlockTargetColor();

            PlayFlash();
            if (GameManager.Instance != null) GameManager.Instance.NotifyFieldChanged();

            GameLog.Info($"[ButtonSwitch] {name}: {arrow.name} → {rotation}, новое направление {arrow.direction}.");
        }

        private void UnlockTargetColor()
        {
            if (GameManager.Instance == null) return;

            IReadOnlyList<ArrowController> arrows = GameManager.Instance.ActiveArrows;
            for (int i = 0; i < arrows.Count; i++)
            {
                ArrowController arrow = arrows[i];
                if (arrow != null && arrow.color == targetColor) arrow.Unlock();
            }
        }

        public static int RotationSteps(SwitchRotation mode)
        {
            switch (mode)
            {
                case SwitchRotation.Rotate90CW: return 1;
                case SwitchRotation.Rotate180: return 2;
                case SwitchRotation.Rotate90CCW: return 3;
                default: return 0;
            }
        }

        // -------------------------------------------------- снимок / Undo

        /// <summary>Стрелки, которые эта зона уже повернула (для снимка уровня).</summary>
        public ArrowController[] GetUsedArrows()
        {
            var result = new ArrowController[lastUseTime.Count];
            lastUseTime.Keys.CopyTo(result, 0);
            return result;
        }

        /// <summary>Возврат состояния кнопки: при Undo и при старте уровня.</summary>
        public void RestoreUsage(bool used, ArrowController[] usedArrows)
        {
            lastUseTime.Clear();
            IsUsed = used;

            if (usedArrows != null)
            {
                for (int i = 0; i < usedArrows.Length; i++)
                {
                    if (usedArrows[i] != null) lastUseTime[usedArrows[i]] = Time.time;
                }
            }

            ApplyIdleVisual();
        }

        public void Configure(SwitchRotation rot, bool oneShotMode, bool oncePerArrowMode, bool filterByColor, ArrowColor color)
        {
            rotation = rot;
            oneShot = oneShotMode;
            oncePerArrow = oncePerArrowMode;
            affectOnlyTargetColor = filterByColor;
            targetColor = color;
            ResetForLevel();
        }

        public void ResetForLevel() => RestoreUsage(false, null);

        // ------------------------------------------------------------ визуал

        private void ApplyIdleVisual()
        {
            if (spriteRenderer != null) spriteRenderer.color = (oneShot && IsUsed) ? usedColor : idleColor;
            if (iconTransform != null) iconTransform.localRotation = Quaternion.Euler(0f, 0f, IconAngle());
        }

        private float IconAngle()
        {
            switch (rotation)
            {
                case SwitchRotation.Rotate90CW: return -90f;
                case SwitchRotation.Rotate90CCW: return 90f;
                default: return 180f;
            }
        }

        private void PlayFlash()
        {
            if (spriteRenderer == null || !gameObject.activeInHierarchy) return;

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            Color to = (oneShot && IsUsed) ? usedColor : idleColor;
            float duration = Mathf.Max(0.01f, flashDuration);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                spriteRenderer.color = Color.Lerp(flashColor, to, t / duration);
                yield return null;
            }

            spriteRenderer.color = to;
            flashRoutine = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheRefs();
            if (Application.isPlaying) return;

            if (triggerCollider != null) triggerCollider.isTrigger = true;
            ApplyIdleVisual();
        }

        private void OnDrawGizmos()
        {
            Vector3 size = triggerCollider != null ? (Vector3)triggerCollider.size : Vector3.one;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);

            Gizmos.color = Color.white;
            float radius = 0.28f;
            Vector3 center = transform.position;
            int steps = RotationSteps(rotation);
            Vector3 from = center + Vector3.up * radius;
            Vector3 to = center + (Vector3)ArrowController.DirectionToVector((ArrowDirection)(steps % 4)) * radius;
            Gizmos.DrawLine(center, from);
            Gizmos.DrawLine(center, to);
        }
#endif
    }
}
