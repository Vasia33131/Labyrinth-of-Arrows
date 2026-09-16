using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Unpuzzle
{
    /// <summary>
    /// Фигура из нескольких клеток. Без Rigidbody2D, без коллайдеров, без Physics2D.
    /// Визуал: LineRenderer пути + SpriteRenderer наконечника.
    /// </summary>
    public class ArrowController : MonoBehaviour
    {
        [Serializable]
        public class ArrowRemovedUnityEvent : UnityEvent<ArrowController> { }

        [Header("Arrow")]
        public ArrowDirection direction = ArrowDirection.Up;
        public ArrowColor color = ArrowColor.Grey;

        /// <summary>Ключ для школьной книги (JSON lockParents). На тап не влияет.</summary>
        public List<ArrowController> lockParents = new List<ArrowController>();
        public bool manualLock;
        public GameObject lockIcon;

        [Header("Книга")]
        public GameObject bookIcon;
        public SpriteRenderer bookIconRenderer;
        public Sprite bookIconSprite;

        [Header("Movement")]
        public float moveSpeed = 10f;
        public float pathWidth = GameConstants.PathWidthInCells;

        [Header("Отказ")]
        public Color rejectFlashColor = new Color(1f, 0.35f, 0.35f);
        public float rejectFlashDuration = 0.18f;

        [Header("Подсказка")]
        public Color hintColor = new Color(1f, 0.95f, 0.45f);
        [Min(0.5f)] public float hintPulsesPerSecond = 3f;

        [Header("References")]
        public LineRenderer pathRenderer;
        public SpriteRenderer headRenderer;
        public Material pathMaterial;

        [Header("Events")]
        public ArrowRemovedUnityEvent onArrowRemovedUnity = new ArrowRemovedUnityEvent();

        public static event Action<ArrowController> OnArrowRemoved;

        public bool IsRemoving { get; private set; }
        public bool IsPlayingIntro { get; private set; }
        public bool IsPlayingBlockedBump { get; private set; }
        /// <summary>На сетке для occupancy / CanExit. Bump не снимает клетки.</summary>
        public bool OccupiesGrid => !IsRemoving || IsPlayingBlockedBump;
        public Vector2Int GridPosition { get; set; }
        /// <summary>На наконечнике живая книга-ключ.</summary>
        public bool HasKeyedBook => HasAliveKeyedBook();

        public IReadOnlyList<Vector2Int> Cells => cells;

        // Хвост короткий: линия толстая, и её скруглённый колпачок сам добавляет половину толщины.
        private const float SingleCellTail = 0.27f;
        private const float PolylineTail = 0.26f;

        /// <summary>Насколько остриё выходит за центр последней клетки.</summary>
        private const float TipReach = 0.48f;

        /// <summary>Конец линии пути — основание наконечника, а не остриё.</summary>
        private const float HeadJoint = TipReach - GameConstants.ArrowHeadLengthInCells;

        private const int LineRoundVertices = 12;

        private readonly List<Vector2Int> cells = new List<Vector2Int>(8);
        private readonly List<BookController> blockingBooks = new List<BookController>(2);
        private readonly List<BookController> keyedBooks = new List<BookController>(2);
        private float cellSize = GameConstants.CellSize;
        private Coroutine colorRoutine;
        private Coroutine moveRoutine;
        private Coroutine shakeRoutine;

        private void Reset()
        {
            CacheRefs();
            GameConstants.SetLayerSafe(gameObject, GameConstants.ArrowLayerName);
        }

        private void Awake()
        {
            CacheRefs();
            EnsurePathMaterial();
            ApplyColorSprite();
        }

        private void CacheRefs()
        {
            if (pathRenderer == null)
            {
                Transform path = transform.Find("Path");
                if (path != null) pathRenderer = path.GetComponent<LineRenderer>();
                if (pathRenderer == null) pathRenderer = GetComponentInChildren<LineRenderer>(true);
            }

            if (headRenderer == null)
            {
                Transform head = transform.Find("Head");
                if (head != null) headRenderer = head.GetComponent<SpriteRenderer>();
                if (headRenderer == null)
                {
                    SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
                    if (renderers.Length > 0) headRenderer = renderers[0];
                }
            }

            GameConstants.SetLayerSafe(gameObject, GameConstants.ArrowLayerName);
            if (pathRenderer != null)
                GameConstants.SetLayerSafe(pathRenderer.gameObject, GameConstants.ArrowLayerName);
            if (headRenderer != null)
                GameConstants.SetLayerSafe(headRenderer.gameObject, GameConstants.ArrowLayerName);
        }

        public void Setup(ArrowDirection dir, ArrowColor arrowColor, Vector2Int gridPos, bool locked = false)
        {
            Setup(dir, arrowColor, new[] { gridPos }, locked);
        }

        public void Setup(ArrowDirection dir, ArrowColor arrowColor, IReadOnlyList<Vector2Int> path, bool locked = false, OccupancyGrid grid = null)
        {
            direction = dir;
            color = arrowColor;
            manualLock = locked;
            IsRemoving = false;
            cellSize = grid != null ? grid.CellSize : GameConstants.CellSize;

            blockingBooks.Clear();
            keyedBooks.Clear();
            cells.Clear();
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++) cells.Add(path[i]);
            }

            if (cells.Count == 0) cells.Add(Vector2Int.zero);
            GridPosition = cells[0];

            if (grid != null) transform.position = grid.CellToWorld(cells[0]);
            transform.rotation = Quaternion.identity;

            StopRoutines();
            RebuildVisual();
            RefreshLockState();
        }

        public void SetCells(IReadOnlyList<Vector2Int> path)
        {
            cells.Clear();
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++) cells.Add(path[i]);
            }

            if (cells.Count == 0) cells.Add(Vector2Int.zero);
            GridPosition = cells[0];
        }

        public Vector2Int[] CopyCells()
        {
            var copy = new Vector2Int[cells.Count];
            cells.CopyTo(copy);
            return copy;
        }

        public void BindBlockingBook(BookController book)
        {
            if (book == null || blockingBooks.Contains(book)) return;
            blockingBooks.Add(book);
        }

        public void BindKeyedBook(BookController book)
        {
            if (book == null || keyedBooks.Contains(book)) return;
            keyedBooks.Add(book);
        }

        public void RefreshLockState()
        {
            if (lockIcon != null) lockIcon.SetActive(false);
            RefreshBookIcon();
            if (colorRoutine == null) ApplyVisualColors(CurrentBaseColor());
        }

        public void Unlock()
        {
            manualLock = false;
            RefreshLockState();
        }

        private bool HasAliveKeyedBook()
        {
            for (int i = 0; i < keyedBooks.Count; i++)
            {
                BookController book = keyedBooks[i];
                if (book != null && book.IsAlive) return true;
            }

            return false;
        }

        private void RefreshBookIcon()
        {
            bool show = PlayProgress.IsSchool && !IsRemoving && HasAliveKeyedBook();
            if (!show)
            {
                if (bookIcon != null) bookIcon.SetActive(false);
                return;
            }

            EnsureBookIcon();
            if (bookIcon != null) bookIcon.SetActive(true);
        }

        private void EnsureBookIcon()
        {
            if (bookIconRenderer == null && bookIcon != null)
                bookIconRenderer = bookIcon.GetComponent<SpriteRenderer>();

            if (bookIcon == null)
            {
                Transform parent = headRenderer != null ? headRenderer.transform : transform;
                bookIcon = new GameObject("BookIcon");
                bookIcon.transform.SetParent(parent, false);
                bookIconRenderer = bookIcon.AddComponent<SpriteRenderer>();
            }

            if (bookIconRenderer == null)
                bookIconRenderer = bookIcon.GetComponent<SpriteRenderer>();
            if (bookIconRenderer == null)
                bookIconRenderer = bookIcon.AddComponent<SpriteRenderer>();

            Sprite sprite = bookIconSprite != null ? bookIconSprite : ArrowSpriteFactory.GetSquare();
            if (sprite == null) sprite = ArrowSpriteFactory.GetSquare();
            bookIconRenderer.sprite = sprite;
            bookIconRenderer.color = bookIconSprite != null ? Color.white : BookController.StubIcon;
            bookIconRenderer.sortingLayerID = 0;
            bookIconRenderer.sortingOrder = 14;

            GameConstants.SetLayerSafe(bookIcon, GameConstants.ArrowLayerName);

            float world = cellSize * 0.35f;
            float parentScale = bookIcon.transform.parent != null
                ? Mathf.Abs(bookIcon.transform.parent.lossyScale.x)
                : 1f;
            if (parentScale < 0.0001f) parentScale = 1f;
            float spriteSize = 1f;
            if (sprite != null)
                spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (spriteSize < 0.0001f) spriteSize = 1f;
            bookIcon.transform.localScale = Vector3.one * (world / (parentScale * spriteSize));
            bookIcon.transform.localPosition = new Vector3(0.28f, 0.18f, 0f);
            bookIcon.transform.localRotation = Quaternion.identity;
        }

        private Color CurrentBaseColor()
        {
            return ColorToUnityColor(color);
        }

        public void SetDirection(ArrowDirection dir)
        {
            direction = dir;
            RebuildVisual();
        }

        public void RotateBy(int quarterTurnsClockwise)
        {
            int steps = ((quarterTurnsClockwise % 4) + 4) % 4;
            if (steps == 0) return;
            SetDirection((ArrowDirection)(((int)direction + steps) % 4));
        }

        public bool CanExit()
        {
            OccupancyGrid grid = GameManager.Instance != null ? GameManager.Instance.Occupancy : null;
            if (grid == null) return true;
            return grid.CanExit(this);
        }

        /// <summary>
        /// Выезд до препятствия на луче CanExit, удар, возврат на спавн.
        /// Occupancy не снимается — двигается только визуал.
        /// </summary>
        public bool PlayBlockedBump()
        {
            if (IsRemoving || IsPlayingBlockedBump) return false;
            if (!gameObject.activeInHierarchy) return false;

            OccupancyGrid grid = GameManager.Instance != null ? GameManager.Instance.Occupancy : null;
            if (grid == null) return false;

            ExitObstruction kind = grid.GetExitObstruction(this, out Vector2Int obstructionCell);
            if (kind == ExitObstruction.None || cells.Count == 0) return false;
            if (!grid.InBounds(obstructionCell)) return false;

            StopRoutines();
            IsRemoving = true;
            IsPlayingBlockedBump = true;
            if (GameManager.Instance != null)
                GameManager.Instance.NotifyBlockedBumpStarted();

            moveRoutine = StartCoroutine(BlockedBumpRoutine(grid, obstructionCell));
            return true;
        }

        public bool TryRemove()
        {
            if (IsRemoving) return false;

            IsRemoving = true;
            if (lockIcon != null) lockIcon.SetActive(false);
            if (bookIcon != null) bookIcon.SetActive(false);

            if (GameManager.Instance != null && GameManager.Instance.Occupancy != null)
                GameManager.Instance.Occupancy.Unregister(this);

            OnArrowRemoved?.Invoke(this);
            onArrowRemovedUnity?.Invoke(this);

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            IsPlayingIntro = false;
            if (gameObject.activeInHierarchy)
            {
                if (ArrowOrbitDirector.Instance != null && ArrowOrbitDirector.Instance.IsReady)
                    ArrowOrbitDirector.Instance.Enqueue(this);
                else
                    moveRoutine = StartCoroutine(ExitRoutine());
            }
            else
            {
                Despawn();
            }

            return true;
        }

        public void StartOrbitFlight(ArrowOrbitDirector director, float phaseShift)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            IsPlayingIntro = false;
            if (!gameObject.activeInHierarchy)
            {
                Despawn();
                return;
            }

            moveRoutine = StartCoroutine(OrbitFlightRoutine(director, phaseShift));
        }

        private IEnumerator OrbitFlightRoutine(ArrowOrbitDirector director, float phaseShift)
        {
            yield return ArrowOrbitFlight.Run(this, director, phaseShift);
            moveRoutine = null;
        }

        public void PrepareForOrbitFlight()
        {
            if (lockIcon != null) lockIcon.SetActive(false);
            if (bookIcon != null) bookIcon.SetActive(false);
            if (headRenderer != null) headRenderer.sortingOrder = 22;
            if (pathRenderer != null) pathRenderer.sortingOrder = 20;
            SetFlightPose(transform.position, DirectionToVector(direction), 1f);
        }

        public void SetFlightPose(Vector3 worldHead, Vector3 tangent, float scale)
        {
            if (tangent.sqrMagnitude < 0.0001f) tangent = DirectionToVector(direction);
            else tangent.Normalize();

            transform.position = worldHead;
            PlaceHead(worldHead, tangent);
            if (headRenderer != null)
            {
                headRenderer.transform.localScale = Vector3.one * CurrentHeadScale(scale);
                headRenderer.sortingOrder = 22;
            }

            if (pathRenderer == null) return;

            float tail = cellSize * 0.5f * Mathf.Max(0.15f, scale);
            var slice = new List<Vector3>(2) { worldHead - tangent * tail, worldHead };
            SetLineWorldPoints(slice);
            float width = Mathf.Clamp(pathWidth, GameConstants.PathWidthMinInCells, GameConstants.PathWidthMaxInCells)
                          * cellSize * Mathf.Max(0.15f, scale);
            pathRenderer.startWidth = width;
            pathRenderer.endWidth = width;
            pathRenderer.sortingOrder = 20;
        }

        public void PlayRejectFeedback()
        {
            if (!gameObject.activeInHierarchy) return;
            if (colorRoutine != null) StopCoroutine(colorRoutine);
            colorRoutine = StartCoroutine(RejectFlashRoutine());
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(RejectShakeRoutine());
        }

        private IEnumerator RejectShakeRoutine()
        {
            Vector3 origin = transform.position;
            const float duration = 0.16f;
            const float amplitude = 0.08f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float falloff = 1f - t / duration;
                transform.position = origin + new Vector3(
                    Mathf.Sin(t * 70f) * amplitude * falloff,
                    Mathf.Cos(t * 55f) * amplitude * 0.4f * falloff,
                    0f);
                yield return null;
            }

            transform.position = origin;
            shakeRoutine = null;
        }

        private IEnumerator RejectFlashRoutine()
        {
            Color from = rejectFlashColor;
            Color to = CurrentBaseColor();
            float duration = Mathf.Max(0.01f, rejectFlashDuration);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                ApplyVisualColors(Color.Lerp(from, to, t / duration));
                yield return null;
            }

            ApplyVisualColors(CurrentBaseColor());
            colorRoutine = null;
        }

        public void PlayHintHighlight(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            if (colorRoutine != null) StopCoroutine(colorRoutine);
            colorRoutine = StartCoroutine(HintHighlightRoutine(Mathf.Max(0.2f, duration)));
        }

        private IEnumerator HintHighlightRoutine(float duration)
        {
            float speed = Mathf.Max(0.5f, hintPulsesPerSecond) * Mathf.PI * 2f;

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * speed);
                ApplyVisualColors(Color.Lerp(CurrentBaseColor(), hintColor, k));
                yield return null;
            }

            ApplyVisualColors(CurrentBaseColor());
            colorRoutine = null;
        }

        public void Despawn()
        {
            StopRoutines();
            gameObject.SetActive(false);
        }

        public void RestoreState(Vector3 position, ArrowDirection dir, ArrowColor arrowColor, bool manualLocked, bool removing, Vector2Int[] path)
        {
            StopRoutines();

            color = arrowColor;
            manualLock = manualLocked;
            IsRemoving = removing;
            direction = dir;

            if (path != null && path.Length > 0) SetCells(path);

            transform.position = position;
            transform.rotation = Quaternion.identity;
            RebuildVisual();
            RefreshLockState();
        }

        public void PlayIntro(float delay = 0f)
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }

            if (!gameObject.activeInHierarchy)
            {
                IsPlayingIntro = false;
                RebuildVisual();
                RefreshLockState();
                return;
            }

            IsPlayingIntro = true;
            moveRoutine = StartCoroutine(IntroRoutine(delay));
        }

        private IEnumerator IntroRoutine(float delay)
        {
            IsPlayingIntro = true;
            if (lockIcon != null) lockIcon.SetActive(false);
            if (bookIcon != null) bookIcon.SetActive(false);

            var worldPath = new List<Vector3>(cells.Count + 2);
            AppendWorldVisualPoints(worldPath);
            var cum = new List<float>(worldPath.Count);
            BuildCumulative(worldPath, cum);
            float bodyLength = cum.Count > 0 ? cum[cum.Count - 1] : 0f;
            if (bodyLength < 0.0001f)
                bodyLength = cellSize * (SingleCellTail + Mathf.Abs(HeadJoint));

            var slice = new List<Vector3>(worldPath.Count);
            ApplySnakeVisual(worldPath, cum, 0f, 0f, slice);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float headDist = 0f;
            while (headDist < bodyLength - 0.0001f)
            {
                headDist += moveSpeed * Time.deltaTime;
                if (headDist > bodyLength) headDist = bodyLength;
                ApplySnakeVisual(worldPath, cum, 0f, headDist, slice);
                yield return null;
            }

            RebuildVisual();
            RefreshLockState();
            IsPlayingIntro = false;
            moveRoutine = null;
        }

        private IEnumerator ExitRoutine()
        {
            var worldPath = new List<Vector3>(cells.Count + 4);
            AppendWorldVisualPoints(worldPath);
            float bodyLength = PolylineLength(worldPath);
            if (bodyLength < 0.0001f)
                bodyLength = cellSize * (SingleCellTail + HeadJoint);

            Vector3 dir = DirectionToVector(direction);
            Vector3 tip = worldPath[worldPath.Count - 1];
            float extra = DistancePastView(tip, dir) + bodyLength + cellSize;
            worldPath.Add(tip + dir * extra);

            var cum = new List<float>(worldPath.Count);
            BuildCumulative(worldPath, cum);
            float pathLength = cum[cum.Count - 1];

            float headDist = bodyLength;
            var slice = new List<Vector3>(worldPath.Count);

            while (true)
            {
                headDist += moveSpeed * Time.deltaTime;
                float tailDist = headDist - bodyLength;
                if (tailDist >= pathLength - 0.0001f)
                    break;

                ApplySnakeVisual(worldPath, cum, tailDist, headDist, slice);
                if (IsOutsideView())
                    break;

                yield return null;
            }

            moveRoutine = null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyArrowExited(this);
                Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator BlockedBumpRoutine(OccupancyGrid grid, Vector2Int obstructionCell)
        {
            Vector3 spawnPos = transform.position;
            Quaternion spawnRot = transform.rotation;

            try
            {
                if (headRenderer != null) headRenderer.sortingOrder = 16;
                if (pathRenderer != null) pathRenderer.sortingOrder = 14;

                var worldPath = new List<Vector3>(cells.Count + 4);
                AppendWorldVisualPoints(worldPath);
                float bodyLength = PolylineLength(worldPath);
                if (bodyLength < 0.0001f)
                    bodyLength = cellSize * (SingleCellTail + Mathf.Abs(HeadJoint));

                Vector3 dir = DirectionToVector(direction);
                Vector3 tip = worldPath[worldPath.Count - 1];
                Vector3 obstacleWorld = grid != null ? grid.CellToWorld(obstructionCell) : tip + dir * cellSize;
                Vector3 nearFace = obstacleWorld - dir * (cellSize * 0.5f);
                Vector3 headContact = nearFace - dir * (cellSize * GameConstants.ArrowHeadLengthInCells);
                float contactTravel = Vector3.Dot(headContact - tip, dir);
                if (contactTravel < 0f) contactTravel = 0f;

                const float OvershootCells = 0.16f;
                float overshoot = cellSize * OvershootCells;
                worldPath.Add(tip + dir * (contactTravel + overshoot));

                var cum = new List<float>(worldPath.Count);
                BuildCumulative(worldPath, cum);
                float pathLength = cum.Count > 0 ? cum[cum.Count - 1] : 0f;
                float contactHead = Mathf.Min(bodyLength + contactTravel, pathLength);
                float impactHead = Mathf.Min(contactHead + overshoot, pathLength);
                var slice = new List<Vector3>(worldPath.Count);

                yield return MoveSnakeAlong(worldPath, cum, bodyLength, bodyLength, contactHead, slice);

                if (colorRoutine != null) StopCoroutine(colorRoutine);
                colorRoutine = StartCoroutine(RejectFlashRoutine());

                yield return MoveSnakeTimed(worldPath, cum, bodyLength, contactHead, impactHead, 0.07f, slice);
                yield return MoveSnakeTimed(worldPath, cum, bodyLength, impactHead, contactHead, 0.09f, slice);

                yield return MoveSnakeAlong(worldPath, cum, bodyLength, contactHead, bodyLength, slice);
            }
            finally
            {
                CompleteBlockedBump(spawnPos, spawnRot);
            }
        }

        private void CompleteBlockedBump(Vector3 spawnPos, Quaternion spawnRot)
        {
            if (!IsPlayingBlockedBump) return;

            IsRemoving = false;
            IsPlayingBlockedBump = false;
            moveRoutine = null;
            transform.position = spawnPos;
            transform.rotation = spawnRot;
            RebuildVisual();
            RefreshLockState();
            if (GameManager.Instance != null)
                GameManager.Instance.NotifyBlockedBumpFinished();
        }

        private IEnumerator MoveSnakeAlong(
            List<Vector3> worldPath,
            List<float> cum,
            float bodyLength,
            float fromHead,
            float toHead,
            List<Vector3> slice)
        {
            float pathLength = cum.Count > 0 ? cum[cum.Count - 1] : 0f;
            fromHead = Mathf.Clamp(fromHead, 0f, pathLength);
            toHead = Mathf.Clamp(toHead, 0f, pathLength);
            float headDist = fromHead;
            float delta = toHead - fromHead;
            if (Mathf.Abs(delta) < 0.0001f)
            {
                ApplySnakeVisual(worldPath, cum, Mathf.Max(0f, headDist - bodyLength), headDist, slice);
                yield break;
            }

            float sign = Mathf.Sign(delta);
            while ((toHead - headDist) * sign > 0.0001f)
            {
                headDist += sign * moveSpeed * Time.deltaTime;
                if ((toHead - headDist) * sign <= 0f) headDist = toHead;
                ApplySnakeVisual(worldPath, cum, Mathf.Max(0f, headDist - bodyLength), headDist, slice);
                yield return null;
            }
        }

        private IEnumerator MoveSnakeTimed(
            List<Vector3> worldPath,
            List<float> cum,
            float bodyLength,
            float fromHead,
            float toHead,
            float duration,
            List<Vector3> slice)
        {
            duration = Mathf.Max(0.01f, duration);
            float pathLength = cum.Count > 0 ? cum[cum.Count - 1] : 0f;
            fromHead = Mathf.Clamp(fromHead, 0f, pathLength);
            toHead = Mathf.Clamp(toHead, 0f, pathLength);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / duration);
                u = u * u * (3f - 2f * u);
                float headDist = Mathf.Lerp(fromHead, toHead, u);
                ApplySnakeVisual(worldPath, cum, Mathf.Max(0f, headDist - bodyLength), headDist, slice);
                yield return null;
            }

            ApplySnakeVisual(worldPath, cum, Mathf.Max(0f, toHead - bodyLength), toHead, slice);
        }

        private bool IsOutsideView()
        {
            Camera cam = Camera.main;
            if (cam == null) return true;

            const float margin = 0.22f;

            if (headRenderer != null && headRenderer.enabled
                && !IsViewportOutside(cam, headRenderer.transform.position, margin))
                return false;

            if (pathRenderer != null && pathRenderer.positionCount > 0)
            {
                for (int i = 0; i < pathRenderer.positionCount; i++)
                {
                    Vector3 p = pathRenderer.GetPosition(i);
                    Vector3 world = pathRenderer.useWorldSpace ? p : pathRenderer.transform.TransformPoint(p);
                    if (!IsViewportOutside(cam, world, margin)) return false;
                }
            }

            return true;
        }

        private static bool IsViewportOutside(Camera cam, Vector3 world, float margin)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            return vp.x < -margin || vp.x > 1f + margin || vp.y < -margin || vp.y > 1f + margin;
        }

        public void RebuildVisual()
        {
            CacheRefs();
            EnsurePathMaterial();
            ApplyColorSprite();
            ConfigureLine();
            PlaceHead();
            ApplyVisualColors(CurrentBaseColor());
            if (pathRenderer != null) pathRenderer.enabled = true;
            if (headRenderer != null) headRenderer.enabled = true;
        }

        private void ConfigureLine()
        {
            if (pathRenderer == null) return;

            pathRenderer.useWorldSpace = false;
            pathRenderer.alignment = LineAlignment.View;
            pathRenderer.textureMode = LineTextureMode.Stretch;
            pathRenderer.numCapVertices = LineRoundVertices;
            pathRenderer.numCornerVertices = LineRoundVertices;
            pathRenderer.sortingLayerID = 0;
            pathRenderer.sortingOrder = 10;
            pathRenderer.widthMultiplier = 1f;
            pathRenderer.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            pathRenderer.colorGradient = WhiteLineGradient();
            float width = Mathf.Clamp(pathWidth, GameConstants.PathWidthMinInCells, GameConstants.PathWidthMaxInCells) * cellSize;
            pathRenderer.startWidth = width;
            pathRenderer.endWidth = width;
            pathRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pathRenderer.receiveShadows = false;

            Vector3[] points = BuildLocalPoints();
            pathRenderer.positionCount = points.Length;
            pathRenderer.SetPositions(points);
        }

        private Vector3[] BuildLocalPoints()
        {
            var world = new List<Vector3>(cells.Count + 2);
            AppendWorldVisualPoints(world);
            Transform space = pathRenderer != null ? pathRenderer.transform : transform;
            var local = new Vector3[world.Count];
            for (int i = 0; i < world.Count; i++)
                local[i] = space.InverseTransformPoint(world[i]);
            return local;
        }

        private void AppendWorldVisualPoints(List<Vector3> dst)
        {
            dst.Clear();
            Vector3 dir = DirectionToVector(direction);
            Vector3 origin = transform.position;

            if (cells.Count <= 1)
            {
                dst.Add(origin + dir * (-cellSize * SingleCellTail));
                dst.Add(origin + dir * (cellSize * HeadJoint));
                return;
            }

            Vector2Int originCell = cells[0];
            for (int i = 0; i < cells.Count; i++)
            {
                dst.Add(origin + new Vector3(
                    (cells[i].x - originCell.x) * cellSize,
                    (cells[i].y - originCell.y) * cellSize,
                    0f));
            }

            Vector3 firstDelta = dst[1] - dst[0];
            if (firstDelta.sqrMagnitude > 0.0001f)
                dst[0] -= firstDelta.normalized * (cellSize * PolylineTail);
            else
                dst[0] -= dir * (cellSize * PolylineTail);

            Vector3 lastDelta = dst[dst.Count - 1] - dst[dst.Count - 2];
            if (lastDelta.sqrMagnitude > 0.0001f)
                dst[dst.Count - 1] += lastDelta.normalized * (cellSize * HeadJoint);
            else
                dst[dst.Count - 1] += dir * (cellSize * HeadJoint);
        }

        private void ApplySnakeVisual(List<Vector3> worldPath, List<float> cum, float tailDist, float headDist, List<Vector3> slice)
        {
            SliceArc(worldPath, cum, tailDist, headDist, slice);
            SetLineWorldPoints(slice);
            SampleArc(worldPath, cum, headDist, out Vector3 headPos, out Vector3 tangent);
            PlaceHead(headPos, tangent);
        }

        private void SetLineWorldPoints(List<Vector3> worldPoints)
        {
            if (pathRenderer == null) return;
            if (worldPoints == null || worldPoints.Count == 0)
            {
                pathRenderer.positionCount = 0;
                return;
            }

            Transform space = pathRenderer.transform;
            int count = worldPoints.Count;
            if (count == 1)
            {
                Vector3 local = space.InverseTransformPoint(worldPoints[0]);
                pathRenderer.positionCount = 2;
                pathRenderer.SetPosition(0, local);
                pathRenderer.SetPosition(1, local);
                return;
            }

            pathRenderer.positionCount = count;
            for (int i = 0; i < count; i++)
                pathRenderer.SetPosition(i, space.InverseTransformPoint(worldPoints[i]));
        }

        private void PlaceHead()
        {
            if (headRenderer == null) return;

            Vector3 tangent = DirectionToVector(direction);
            Vector3 world;
            if (TryGetLineEndWorld(out world, out Vector3 lineTangent))
            {
                PlaceHead(world, lineTangent);
                return;
            }

            Vector2Int originCell = cells.Count > 0 ? cells[0] : Vector2Int.zero;
            Vector2Int tipCell = cells.Count > 0 ? cells[cells.Count - 1] : Vector2Int.zero;
            world = transform.position + new Vector3(
                (tipCell.x - originCell.x) * cellSize,
                (tipCell.y - originCell.y) * cellSize,
                0f);
            world += tangent * (cellSize * HeadJoint);
            PlaceHead(world, tangent);
        }

        private void PlaceHead(Vector3 worldPosition, Vector3 tangent)
        {
            if (headRenderer == null) return;
            if (tangent.sqrMagnitude < 0.0001f) tangent = DirectionToVector(direction);
            else tangent.Normalize();

            headRenderer.transform.position = worldPosition;
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - 90f;
            headRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            headRenderer.transform.localScale = Vector3.one * CurrentHeadScale();
            headRenderer.sortingOrder = 12;
        }

        private bool TryGetLineEndWorld(out Vector3 world, out Vector3 tangent)
        {
            world = transform.position;
            tangent = DirectionToVector(direction);
            if (pathRenderer == null || pathRenderer.positionCount <= 0) return false;

            Vector3 last = pathRenderer.GetPosition(pathRenderer.positionCount - 1);
            world = pathRenderer.useWorldSpace ? last : pathRenderer.transform.TransformPoint(last);
            if (pathRenderer.positionCount >= 2)
            {
                Vector3 prev = pathRenderer.GetPosition(pathRenderer.positionCount - 2);
                Vector3 prevWorld = pathRenderer.useWorldSpace ? prev : pathRenderer.transform.TransformPoint(prev);
                Vector3 delta = world - prevWorld;
                if (delta.sqrMagnitude > 0.0001f) tangent = delta.normalized;
            }

            return true;
        }

        private float DistancePastView(Vector3 from, Vector3 dir)
        {
            Camera cam = Camera.main;
            if (cam == null) return 20f * cellSize;

            const float margin = 0.35f;
            float maxScan = 80f * cellSize;
            float step = Mathf.Max(0.25f, cellSize * 0.5f);
            float t = 0f;
            while (t < maxScan)
            {
                if (IsViewportOutside(cam, from + dir * t, margin))
                    return t + cellSize;
                t += step;
            }

            return maxScan;
        }

        private static float PolylineLength(List<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        private static void BuildCumulative(List<Vector3> points, List<float> cum)
        {
            cum.Clear();
            cum.Add(0f);
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                cum.Add(total);
            }
        }

        private static void SampleArc(List<Vector3> points, List<float> cum, float dist, out Vector3 position, out Vector3 tangent)
        {
            int n = points.Count;
            Vector3 fallback = n > 0 ? points[n - 1] - points[Mathf.Max(0, n - 2)] : Vector3.up;
            if (n == 0)
            {
                position = Vector3.zero;
                tangent = Vector3.up;
                return;
            }

            if (n == 1 || cum[n - 1] <= 0.0001f)
            {
                position = points[0];
                tangent = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.up;
                return;
            }

            dist = Mathf.Clamp(dist, 0f, cum[n - 1]);
            int seg = n - 2;
            for (int i = 1; i < n; i++)
            {
                if (dist < cum[i] - 0.000001f)
                {
                    seg = i - 1;
                    break;
                }
            }

            Vector3 delta = points[seg + 1] - points[seg];
            float segLen = cum[seg + 1] - cum[seg];
            if (segLen < 0.0001f)
            {
                position = points[seg + 1];
                tangent = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.up;
                return;
            }

            float u = (dist - cum[seg]) / segLen;
            position = Vector3.Lerp(points[seg], points[seg + 1], u);
            tangent = delta / segLen;
        }

        private static void SliceArc(List<Vector3> points, List<float> cum, float from, float to, List<Vector3> dst)
        {
            dst.Clear();
            float pathLength = cum.Count > 0 ? cum[cum.Count - 1] : 0f;
            from = Mathf.Clamp(from, 0f, pathLength);
            to = Mathf.Clamp(to, 0f, pathLength);
            if (to < from) to = from;

            SampleArc(points, cum, from, out Vector3 a, out _);
            SampleArc(points, cum, to, out Vector3 b, out _);
            dst.Add(a);

            const float eps = 0.0001f;
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (cum[i] > from + eps && cum[i] < to - eps)
                    dst.Add(points[i]);
            }

            if (dst.Count == 1 || (b - dst[dst.Count - 1]).sqrMagnitude > 1e-8f)
                dst.Add(b);
        }

        private float CurrentHeadScale(float extra = 1f)
        {
            float spriteWidth = 1f;
            if (headRenderer != null && headRenderer.sprite != null)
                spriteWidth = headRenderer.sprite.bounds.size.x;
            if (spriteWidth < 0.0001f) spriteWidth = 1f;

            float triangleWidth = spriteWidth * ArrowSpriteFactory.TriangleWidthNormalized;
            float target = GameConstants.ArrowHeadWidthInCells * cellSize;
            return target / triangleWidth * Mathf.Max(0.05f, extra);
        }

        private void EnsurePathMaterial()
        {
            if (pathRenderer == null) return;

            Shader shader = FindPathShader();
            Material source = pathMaterial != null ? pathMaterial : pathRenderer.sharedMaterial;
            if (shader != null && (source == null || source.shader != shader))
            {
                pathMaterial = new Material(shader) { name = "ArrowPathRuntime" };
                if (pathMaterial.HasProperty("_MainTex") && pathMaterial.GetTexture("_MainTex") == null)
                    pathMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
                if (pathMaterial.HasProperty("_Color"))
                    pathMaterial.SetColor("_Color", Color.white);
                if (pathMaterial.HasProperty("_RendererColor"))
                    pathMaterial.SetColor("_RendererColor", Color.white);
            }
            else if (pathMaterial == null)
            {
                pathMaterial = source;
            }

            if (pathMaterial != null)
                pathRenderer.sharedMaterial = pathMaterial;
        }

        private static Shader FindPathShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }

        private void ApplyColorSprite()
        {
            if (headRenderer == null) return;
            Sprite sprite = ArrowSpriteFactory.GetColored(color);
            if (sprite != null) headRenderer.sprite = sprite;
        }

        private void ApplyVisualColors(Color pathTint)
        {
            if (pathRenderer != null)
            {
                pathRenderer.startColor = pathTint;
                pathRenderer.endColor = pathTint;
            }

            if (headRenderer != null) headRenderer.color = pathTint;
        }

        private static Gradient WhiteLineGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private void StopRoutines()
        {
            bool bump = IsPlayingBlockedBump;
            IsPlayingIntro = false;
            IsPlayingBlockedBump = false;

            if (colorRoutine != null)
            {
                StopCoroutine(colorRoutine);
                colorRoutine = null;
            }

            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }

            if (ArrowOrbitDirector.Instance != null)
                ArrowOrbitDirector.Instance.NotifyStopped(this);

            if (bump)
            {
                IsRemoving = false;
                RebuildVisual();
                if (GameManager.Instance != null)
                    GameManager.Instance.NotifyBlockedBumpFinished();
            }
        }

        public static float DirectionToAngle(ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up: return 0f;
                case ArrowDirection.Left: return 90f;
                case ArrowDirection.Down: return 180f;
                case ArrowDirection.Right: return -90f;
                default: return 0f;
            }
        }

        public static Vector2 DirectionToVector(ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up: return Vector2.up;
                case ArrowDirection.Right: return Vector2.right;
                case ArrowDirection.Down: return Vector2.down;
                case ArrowDirection.Left: return Vector2.left;
                default: return Vector2.up;
            }
        }

        public static Vector2Int DirectionToCellStep(ArrowDirection dir)
        {
            switch (dir)
            {
                case ArrowDirection.Up: return new Vector2Int(0, 1);
                case ArrowDirection.Right: return new Vector2Int(1, 0);
                case ArrowDirection.Down: return new Vector2Int(0, -1);
                case ArrowDirection.Left: return new Vector2Int(-1, 0);
                default: return new Vector2Int(0, 1);
            }
        }

        public static Color ColorToUnityColor(ArrowColor arrowColor)
        {
            switch (arrowColor)
            {
                case ArrowColor.Red: return new Color(1.00f, 0.22f, 0.72f);
                case ArrowColor.Blue: return new Color(0.15f, 0.90f, 1.00f);
                case ArrowColor.DarkBlue: return new Color(0.62f, 0.32f, 1.00f);
                case ArrowColor.Green: return new Color(0.20f, 0.95f, 0.45f);
                case ArrowColor.Yellow: return new Color(1.00f, 0.86f, 0.18f);
                case ArrowColor.Orange: return new Color(1.00f, 0.38f, 0.14f);
                default: return new Color(0.82f, 0.85f, 0.92f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheRefs();
            if (Application.isPlaying) return;
            ApplyColorSprite();
            ApplyVisualColors(CurrentBaseColor());
        }
#endif
    }
}
