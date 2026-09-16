using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Очередь полётов: окружение рамки поля по часовой → дуга в коробку конфет.
    /// </summary>
    public class ArrowOrbitDirector : MonoBehaviour
    {
        public static ArrowOrbitDirector Instance { get; private set; }

        public ScreenLayoutConfig config = new ScreenLayoutConfig();
        public Camera gameCamera;
        public RectTransform boardFrame;
        public RectTransform candyLandPoint;
        public CandyBoxView candyBox;
        public CharacterView character;
        public SpriteRenderer worldTray;

        public bool IsReady => boardFrame != null || TryResolveBoardFrame();

        private readonly Queue<ArrowController> pending = new Queue<ArrowController>();
        private readonly HashSet<ArrowController> flying = new HashSet<ArrowController>();
        private readonly List<Vector3> cachedPath = new List<Vector3>(64);
        private readonly List<float> cachedCum = new List<float>(64);
        private int launchIndex;

        private void Awake()
        {
            Instance = this;
            if (gameCamera == null) gameCamera = Camera.main;
            if (config == null) config = ScreenLayoutConfig.CreateDefault();
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        public void Enqueue(ArrowController arrow)
        {
            if (arrow == null) return;
            if (flying.Contains(arrow) || pending.Contains(arrow)) return;

            pending.Enqueue(arrow);
            TryLaunchNext();
        }

        public void Cancel(ArrowController arrow)
        {
            if (arrow == null) return;
            flying.Remove(arrow);
            if (pending.Count > 0)
            {
                var kept = new Queue<ArrowController>(pending.Count);
                while (pending.Count > 0)
                {
                    ArrowController next = pending.Dequeue();
                    if (next != arrow) kept.Enqueue(next);
                }

                while (kept.Count > 0) pending.Enqueue(kept.Dequeue());
            }

            TryLaunchNext();
        }

        public void CancelAll()
        {
            pending.Clear();
            flying.Clear();
            launchIndex = 0;
        }

        public void NotifyStopped(ArrowController arrow)
        {
            if (arrow == null) return;
            if (!flying.Remove(arrow) && pending.Count == 0) return;
            TryLaunchNext();
        }

        public void NotifyLanded(ArrowController arrow)
        {
            if (candyBox != null) candyBox.PlayLand();
            flying.Remove(arrow);
            TryLaunchNext();
        }

        private void TryLaunchNext()
        {
            int cap = config != null ? config.maxConcurrentFlights : 3;
            while (flying.Count < cap && pending.Count > 0)
            {
                ArrowController arrow = pending.Dequeue();
                if (arrow == null || !arrow.gameObject.activeInHierarchy) continue;
                flying.Add(arrow);
                float phase = (launchIndex % Mathf.Max(1, cap)) * (config != null ? config.flightPhaseOffset : 0.12f);
                launchIndex++;
                arrow.StartOrbitFlight(this, phase);
            }
        }

        public bool TryBuildOrbitPath(List<Vector3> points, List<float> cumulative, out float length)
        {
            points.Clear();
            cumulative.Clear();
            length = 0f;

            if (!TryGetOrbitRect(out Rect rect, out float radius))
                return false;

            RoundedOrbitPath.BuildClockwise(rect, radius, config != null ? config.orbitCornerSegments : 12, points);
            if (points.Count < 2) return false;

            cumulative.Add(0f);
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
                cumulative.Add(length);
            }

            return length > 0.001f;
        }

        public Vector3 GetLandWorld()
        {
            if (candyBox != null) return candyBox.GetLandWorld(gameCamera);
            if (candyLandPoint != null) return UiWorldUtility.RectToWorld(candyLandPoint, gameCamera);
            return new Vector3(0f, -4f, 0f);
        }

        public float FindNearestT(Vector3 world, List<Vector3> points, List<float> cum)
        {
            if (points == null || points.Count < 2 || cum == null || cum.Count < 2) return 0f;
            float bestDist = float.MaxValue;
            float bestT = 0f;
            float total = cum[cum.Count - 1];
            if (total < 0.0001f) return 0f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 ab = b - a;
                float abSqr = ab.sqrMagnitude;
                float u = abSqr > 0.0001f ? Mathf.Clamp01(Vector3.Dot(world - a, ab) / abSqr) : 0f;
                Vector3 p = a + ab * u;
                float d = (world - p).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    float seg = cum[i + 1] - cum[i];
                    bestT = (cum[i] + seg * u) / total;
                }
            }

            return Mathf.Repeat(bestT, 1f);
        }

        public float FindExitT(List<Vector3> points, List<float> cum, Vector3 land)
        {
            if (points == null || points.Count == 0 || cum == null || cum.Count == 0) return 0f;
            float total = cum[cum.Count - 1];
            if (total < 0.0001f) return 0f;

            int best = 0;
            float bestScore = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                float distLand = (p - land).sqrMagnitude;
                float preferBottom = p.y * 2.2f;
                float preferRight = -p.x * 0.35f;
                float score = distLand + preferBottom + preferRight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return cum[best] / total;
        }

        private bool TryGetOrbitRect(out Rect rect, out float radius)
        {
            rect = default;
            radius = 0.45f;
            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (!TryResolveBoardFrame()) return false;
            if (!UiWorldUtility.TryGetWorldRect(boardFrame, cam, out Rect frame)) return false;

            float gap = config != null ? config.ResolveOrbitGap(frame.width) : 0.4f;
            rect = new Rect(frame.xMin - gap, frame.yMin - gap, frame.width + gap * 2f, frame.height + gap * 2f);
            radius = Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.12f, 0.28f, 0.85f);
            return true;
        }

        private bool TryResolveBoardFrame()
        {
            if (boardFrame != null) return true;
            if (ScreenLayoutBuilder.Instance != null)
                boardFrame = ScreenLayoutBuilder.Instance.boardFrame;
            return boardFrame != null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!TryBuildOrbitPath(cachedPath, cachedCum, out _)) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
            for (int i = 1; i < cachedPath.Count; i++)
                Gizmos.DrawLine(cachedPath[i - 1], cachedPath[i]);
        }
#endif
    }

    /// <summary>Скруглённый прямоугольный трек по часовой стрелке.</summary>
    public static class RoundedOrbitPath
    {
        public static void BuildClockwise(Rect rect, float radius, int cornerSegments, List<Vector3> dst)
        {
            dst.Clear();
            float r = Mathf.Min(radius, rect.width * 0.45f, rect.height * 0.45f);
            r = Mathf.Max(0.05f, r);
            int seg = Mathf.Max(6, cornerSegments);

            float left = rect.xMin;
            float right = rect.xMax;
            float bottom = rect.yMin;
            float top = rect.yMax;

            Add(dst, left + r, bottom);
            Add(dst, right - r, bottom);
            AddCorner(dst, new Vector2(right - r, bottom + r), r, 270f, 360f, seg);
            Add(dst, right, top - r);
            AddCorner(dst, new Vector2(right - r, top - r), r, 0f, 90f, seg);
            Add(dst, left + r, top);
            AddCorner(dst, new Vector2(left + r, top - r), r, 90f, 180f, seg);
            Add(dst, left, bottom + r);
            AddCorner(dst, new Vector2(left + r, bottom + r), r, 180f, 270f, seg);

            if (dst.Count > 1)
                dst[dst.Count - 1] = dst[0];
        }

        private static void AddCorner(List<Vector3> dst, Vector2 center, float r, float fromDeg, float toDeg, int segments)
        {
            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)segments) * Mathf.Deg2Rad;
                Add(dst, center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r);
            }
        }

        private static void Add(List<Vector3> dst, float x, float y)
        {
            var p = new Vector3(x, y, 0f);
            if (dst.Count > 0 && (dst[dst.Count - 1] - p).sqrMagnitude < 1e-8f) return;
            dst.Add(p);
        }
    }

    /// <summary>Корутина орбиты + дуги в конфеты. Запускается на ArrowController.</summary>
    public static class ArrowOrbitFlight
    {
        public static IEnumerator Run(ArrowController arrow, ArrowOrbitDirector director, float phaseShift)
        {
            if (arrow == null || director == null) yield break;

            var path = new List<Vector3>(64);
            var cum = new List<float>(64);
            if (!director.TryBuildOrbitPath(path, cum, out float length))
            {
                yield return FlyStraight(arrow, director);
                yield break;
            }

            ScreenLayoutConfig cfg = director.config ?? ScreenLayoutConfig.CreateDefault();
            AnimationCurve orbitEase = cfg.orbitEase != null && cfg.orbitEase.length >= 2
                ? cfg.orbitEase
                : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            AnimationCurve flyEase = cfg.flyEase != null && cfg.flyEase.length >= 2
                ? cfg.flyEase
                : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            Vector3 start = arrow.transform.position;
            float t0 = director.FindNearestT(start, path, cum);
            t0 = Mathf.Repeat(t0 + phaseShift, 1f);
            Vector3 land = director.GetLandWorld();
            float tExit = director.FindExitT(path, cum, land);
            float extra = Mathf.Repeat(tExit - t0, 1f);
            if (extra < 0.001f) extra = 1f;
            float travelT = extra;
            float minLaps = Mathf.Max(0.75f, cfg.orbitMinLaps);
            while (travelT < minLaps - 0.0001f) travelT += 1f;

            arrow.PrepareForOrbitFlight();

            Vector3 join = Sample(path, cum, t0 * length, out Vector3 joinTan);
            const float merge = 0.08f;
            for (float t = 0f; t < merge; t += Time.deltaTime)
            {
                float u = t / merge;
                Vector3 p = Vector3.Lerp(start, join, u);
                Vector3 tan = Vector3.Slerp(joinTan, joinTan, u);
                arrow.SetFlightPose(p, tan.sqrMagnitude > 0.001f ? tan : joinTan, 1f);
                yield return null;
            }

            float orbitDur = Mathf.Max(0.45f, cfg.orbitDuration);
            for (float t = 0f; t < orbitDur; t += Time.deltaTime)
            {
                float u = orbitEase.Evaluate(Mathf.Clamp01(t / orbitDur));
                float param = Mathf.Repeat(t0 + travelT * u, 1f);
                Vector3 p = Sample(path, cum, param * length, out Vector3 tan);
                arrow.SetFlightPose(p, tan, 1f);
                yield return null;
            }

            Vector3 exitPos = Sample(path, cum, tExit * length, out Vector3 exitTan);
            arrow.SetFlightPose(exitPos, exitTan, 1f);

            float flyDur = Mathf.Max(0.2f, cfg.flyDuration);
            float arc = Mathf.Max(cfg.flyArcHeight, Vector3.Distance(exitPos, land) * 0.35f);
            Vector3 control = (exitPos + land) * 0.5f + Vector3.up * arc;
            float landScale = cfg.landScale;

            for (float t = 0f; t < flyDur; t += Time.deltaTime)
            {
                float u = flyEase.Evaluate(Mathf.Clamp01(t / flyDur));
                Vector3 p = QuadBezier(exitPos, control, land, u);
                Vector3 tan = QuadBezierDeriv(exitPos, control, land, u);
                float scale = Mathf.Lerp(1f, landScale, u * u);
                arrow.SetFlightPose(p, tan, scale);
                yield return null;
            }

            arrow.SetFlightPose(land, Vector3.down, landScale);
            director.NotifyLanded(arrow);

            if (GameManager.Instance != null)
                GameManager.Instance.NotifyArrowExited(arrow);

            arrow.Despawn();
        }

        private static IEnumerator FlyStraight(ArrowController arrow, ArrowOrbitDirector director)
        {
            Vector3 from = arrow.transform.position;
            Vector3 to = director.GetLandWorld();
            Vector3 control = (from + to) * 0.5f + Vector3.up * 1.2f;
            arrow.PrepareForOrbitFlight();
            const float dur = 0.45f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / dur);
                u = u * u * (3f - 2f * u);
                Vector3 p = QuadBezier(from, control, to, u);
                arrow.SetFlightPose(p, QuadBezierDeriv(from, control, to, u), Mathf.Lerp(1f, 0.3f, u));
                yield return null;
            }

            director.NotifyLanded(arrow);
            if (GameManager.Instance != null)
                GameManager.Instance.NotifyArrowExited(arrow);
            arrow.Despawn();
        }

        private static Vector3 Sample(List<Vector3> points, List<float> cum, float dist, out Vector3 tangent)
        {
            int n = points.Count;
            tangent = Vector3.right;
            if (n == 0)
                return Vector3.zero;
            if (n == 1 || cum[n - 1] <= 0.0001f)
                return points[0];

            dist = Mathf.Repeat(dist, cum[n - 1]);
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
            float u = segLen > 0.0001f ? (dist - cum[seg]) / segLen : 1f;
            tangent = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.right;
            return Vector3.Lerp(points[seg], points[seg + 1], u);
        }

        private static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Vector3 QuadBezierDeriv(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return 2f * (1f - t) * (b - a) + 2f * t * (c - b);
        }
    }
}
