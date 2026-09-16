using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Лоток конфет: миска по проценту собранных стрелок, bounce, пул иконок.
    /// </summary>
    public class CandyBoxView : MonoBehaviour
    {
        public RectTransform boxRoot;
        public RectTransform landPoint;
        public Text countText;
        public SpriteRenderer worldTray;
        public Camera gameCamera;
        public Sprite candySprite;
        public int iconPoolSize = 0;

        [Header("Миска")]
        public Image bowlImage;
        public Sprite bowlEmpty;
        public Sprite bowl25;
        public Sprite bowl50;
        public Sprite bowl75;
        public Sprite bowlFull;

        public int Count { get; private set; }
        public int Total { get; private set; }

        private readonly List<Image> icons = new List<Image>(8);
        private Coroutine bounceRoutine;
        private Vector3 boxScale = Vector3.one;

        private void Awake()
        {
            if (boxRoot == null) boxRoot = transform as RectTransform;
            boxScale = boxRoot != null ? boxRoot.localScale : Vector3.one;
            BindArt();
            EnsureBowlImage();
            HideLooseCandies();
            SetCount(0, Total);
        }

        private void LateUpdate()
        {
            SyncWorldTray();
        }

        public Vector3 GetLandWorld(Camera cam = null)
        {
            cam = cam != null ? cam : (gameCamera != null ? gameCamera : Camera.main);
            if (landPoint != null) return UiWorldUtility.RectToWorld(landPoint, cam);
            if (boxRoot != null) return UiWorldUtility.RectToWorld(boxRoot, cam);
            return new Vector3(0f, -4f, 0f);
        }

        public void ResetCount()
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalArrows : Total;
            SetCount(0, total);
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] != null) icons[i].gameObject.SetActive(false);
            }
        }

        public void SetCount(int value)
        {
            SetCount(value, Total);
        }

        public void SetCount(int value, int total)
        {
            Count = Mathf.Max(0, value);
            if (total >= 0) Total = total;
            if (countText != null) countText.gameObject.SetActive(false);
            RefreshBowl();
        }

        public void SyncFromArrows(IReadOnlyList<ArrowController> allArrows)
        {
            int collected = 0;
            int total = 0;
            if (allArrows != null)
            {
                total = allArrows.Count;
                for (int i = 0; i < allArrows.Count; i++)
                {
                    ArrowController arrow = allArrows[i];
                    if (arrow == null) continue;
                    if (!arrow.gameObject.activeSelf) collected++;
                }
            }

            SetCount(collected, total);
        }

        public void PlayLand()
        {
            SetCount(Count + 1, Total);
            HideLooseCandies();
            PlayBounce(0.14f, 0.10f);
            GameAudio.Play(GameAudio.Sfx.CandyLand);
        }

        public void PlayWinBounce()
        {
            PlayBounce(0.28f, 0.16f);
        }

        public void SyncWorldTray()
        {
            if (worldTray == null) return;
            if (bowlImage != null)
            {
                worldTray.enabled = false;
                return;
            }

            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null || boxRoot == null) return;
            if (!UiWorldUtility.TryGetWorldRect(boxRoot, cam, out Rect world)) return;

            worldTray.enabled = true;
            worldTray.sprite = CurrentBowlSprite();
            worldTray.color = Color.white;
            worldTray.drawMode = SpriteDrawMode.Simple;
            worldTray.transform.position = new Vector3(world.center.x, world.center.y, 0.15f);
            worldTray.size = new Vector2(world.width, world.height);
            worldTray.sortingOrder = -4;
        }

        private void BindArt()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art == null) return;

            if (candySprite == null) candySprite = art.candyIcon;
            if (bowlEmpty == null) bowlEmpty = art.bowl0;
            if (bowl25 == null) bowl25 = art.bowl25;
            if (bowl50 == null) bowl50 = art.bowl50;
            if (bowl75 == null) bowl75 = art.bowl75;
            if (bowlFull == null) bowlFull = art.bowl100;
        }

        private void EnsureBowlImage()
        {
            if (bowlImage == null)
            {
                Transform existing = transform.Find("Bowl");
                if (existing != null) bowlImage = existing.GetComponent<Image>();
            }

            if (bowlImage == null)
            {
                Image own = GetComponent<Image>();
                if (own != null) bowlImage = own;
            }

            if (bowlImage == null)
            {
                if (!AuthoredUi.CanBuild)
                {
                    AuthoredUi.Missing("Bowl");
                    return;
                }

                var go = new GameObject("Bowl", typeof(RectTransform));
                go.transform.SetParent(boxRoot != null ? boxRoot : transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.SetAsFirstSibling();
                bowlImage = go.AddComponent<Image>();
            }

            bowlImage.color = Color.white;
            bowlImage.raycastTarget = false;
            bowlImage.type = Image.Type.Simple;
            bowlImage.preserveAspect = true;
            if (!Application.isPlaying) HideFrameBehindBowl();
            HideLooseCandies();
            RefreshBowl();
        }

        private void HideFrameBehindBowl()
        {
            Transform fill = transform.Find("Fill");
            if (fill != null)
            {
                Image fillImage = fill.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.enabled = false;
                    fillImage.color = new Color(1f, 1f, 1f, 0f);
                    fillImage.raycastTarget = false;
                }
            }

            Transform hint = transform.Find("TrayHint");
            if (hint != null) hint.gameObject.SetActive(false);
        }

        private void HideLooseCandies()
        {
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] != null) icons[i].gameObject.SetActive(false);
            }

            if (landPoint == null) return;
            for (int i = 0; i < landPoint.childCount; i++)
            {
                Transform child = landPoint.GetChild(i);
                if (child != null && child.name.StartsWith("CandyIcon_"))
                    child.gameObject.SetActive(false);
            }
        }

        private void RefreshBowl()
        {
            Sprite sprite = CurrentBowlSprite();
            if (bowlImage != null && sprite != null)
            {
                bowlImage.sprite = sprite;
                bowlImage.color = Color.white;
                bowlImage.preserveAspect = true;
            }

            if (worldTray != null && sprite != null)
            {
                worldTray.sprite = sprite;
                worldTray.color = Color.white;
            }
        }

        private Sprite CurrentBowlSprite()
        {
            float percent = Total > 0 ? (float)Count / Total : 0f;
            Sprite local = BowlFromSlots(percent);
            if (local != null) return local;

            ArtLibrary art = ArtLibrary.Current;
            return art != null ? art.BowlForFill(percent) : null;
        }

        private Sprite BowlFromSlots(float percent)
        {
            if (percent <= 0f) return bowlEmpty;
            if (percent < 0.25f) return bowl25 != null ? bowl25 : bowlEmpty;
            if (percent < 0.50f) return bowl50 != null ? bowl50 : bowl25;
            if (percent < 0.75f) return bowl75 != null ? bowl75 : bowl50;
            return bowlFull != null ? bowlFull : bowl75;
        }

        private void PlayBounce(float duration, float strength)
        {
            if (!isActiveAndEnabled || boxRoot == null) return;
            if (bounceRoutine != null) StopCoroutine(bounceRoutine);
            bounceRoutine = StartCoroutine(BounceRoutine(duration, strength));
        }

        private IEnumerator BounceRoutine(float duration, float strength)
        {
            duration = Mathf.Max(0.05f, duration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                float k = 1f + Mathf.Sin(u * Mathf.PI) * strength;
                boxRoot.localScale = new Vector3(boxScale.x * (2f - k), boxScale.y * k, 1f);
                yield return null;
            }

            boxRoot.localScale = boxScale;
            bounceRoutine = null;
        }

        private void ShowNextIcon()
        {
            EnsureIconPool();
            if (icons.Count == 0) return;

            int index = (Count - 1) % icons.Count;
            Image icon = icons[index];
            if (icon == null) return;

            icon.gameObject.SetActive(true);
            var rt = icon.rectTransform;
            float ox = ((index % 4) - 1.5f) * 28f;
            float oy = (index / 4) * 22f - 8f;
            rt.anchoredPosition = new Vector2(ox, oy);
            rt.localScale = Vector3.one * 0.2f;
            StartCoroutine(PopIcon(rt));
        }

        private IEnumerator PopIcon(RectTransform rt)
        {
            const float dur = 0.18f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                float k = 1f + Mathf.Sin(u * Mathf.PI) * 0.25f;
                if (rt != null) rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, u) * k;
                yield return null;
            }

            if (rt != null) rt.localScale = Vector3.one;
        }

        private void EnsureIconPool()
        {
            if (icons.Count >= iconPoolSize) return;
            if (landPoint == null) return;
            if (!AuthoredUi.CanBuild) return;

            Sprite sprite = candySprite;
            if (sprite == null && ArtLibrary.Current != null) sprite = ArtLibrary.Current.candyIcon;

            Transform parent = landPoint;
            for (int i = icons.Count; i < iconPoolSize; i++)
            {
                var go = new GameObject("CandyIcon_" + i, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(36f, 36f);
                var image = go.AddComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;
                image.raycastTarget = false;
                image.preserveAspect = true;
                go.SetActive(false);
                icons.Add(image);
            }
        }
    }
}
