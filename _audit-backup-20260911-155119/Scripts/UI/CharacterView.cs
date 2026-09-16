using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Визуал персонажа: один спрайт на root, состояния idle / happy / sad.
    /// </summary>
    public class CharacterView : MonoBehaviour
    {
        public RectTransform root;
        public RectTransform body;
        public RectTransform head;
        public RectTransform armLeft;
        public RectTransform armRight;

        [Header("Арт")]
        public Image display;
        public Sprite idleSprite;
        public Sprite happySprite;
        public Sprite sadSprite;

        private Coroutine routine;
        private Vector3 rootScale = Vector3.one;
        private Color idleTint = Color.white;
        private Color happyTint = Color.white;
        private Color sadTint = Color.white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WatchSkinChanges()
        {
            CharacterSkins.OnChanged -= RefreshAllInScene;
            CharacterSkins.OnChanged += RefreshAllInScene;
        }

        private static void RefreshAllInScene()
        {
            CharacterView[] views = FindObjectsOfType<CharacterView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null)
                    views[i].ApplySelectedSkin();
            }
        }

        private void Awake()
        {
            if (root == null) root = transform as RectTransform;
            rootScale = root != null ? root.localScale : Vector3.one;
            BindArt();
            EnsureDisplay();
            HideStubs();
            ApplySelectedSkin();
        }

        private void OnEnable()
        {
            CharacterSkins.OnChanged -= HandleSkinChanged;
            CharacterSkins.OnChanged += HandleSkinChanged;
            ApplySelectedSkin();
        }

        private void OnDisable()
        {
            CharacterSkins.OnChanged -= HandleSkinChanged;
        }

        private void HandleSkinChanged()
        {
            ApplySelectedSkin();
        }

        public void ApplySelectedSkin()
        {
            EnsureDisplay();
            CharacterSkinData skin = CharacterSkins.Selected;
            ArtLibrary art = ArtLibrary.Current;

            Sprite nextIdle = skin != null ? skin.ResolveIdle(art) : null;
            Sprite nextHappy = skin != null ? skin.ResolveHappy(art) : null;
            Sprite nextSad = skin != null ? skin.ResolveSad(art) : null;

            if (nextIdle == null && art != null) nextIdle = art.characterIdle;
            if (nextHappy == null && art != null) nextHappy = art.characterHappy;
            if (nextSad == null && art != null) nextSad = art.characterSad;

            if (nextIdle == null) nextIdle = idleSprite;
            if (nextHappy == null) nextHappy = happySprite;
            if (nextSad == null) nextSad = sadSprite;

            idleSprite = nextIdle;
            happySprite = nextHappy;
            sadSprite = nextSad;

            if (skin != null)
            {
                idleTint = skin.ColorForIdle();
                happyTint = skin.ColorForHappy();
                sadTint = skin.ColorForSad();
            }
            else
            {
                idleTint = Color.white;
                happyTint = Color.white;
                sadTint = Color.white;
            }

            ApplySprite(idleSprite, idleTint);
        }

        public void ResetPose()
        {
            StopActive();
            ApplyScale(1f);
            SetRotation(root, 0f);
            SetRotation(armLeft, 18f);
            SetRotation(armRight, -18f);
            if (head != null) head.localPosition = new Vector3(0f, 22f, 0f);
            if (root != null) root.anchoredPosition = Vector2.zero;
            ApplySprite(idleSprite, idleTint);
        }

        public void PlayHappy()
        {
            ApplySprite(happySprite != null ? happySprite : idleSprite,
                happySprite != null ? happyTint : idleTint);
            StartAnim(HappyRoutine());
        }

        public void PlaySad()
        {
            ApplySprite(sadSprite != null ? sadSprite : idleSprite,
                sadSprite != null ? sadTint : idleTint);
            StartAnim(SadRoutine());
        }

        public void PlayVictory()
        {
            ApplySprite(happySprite != null ? happySprite : idleSprite,
                happySprite != null ? happyTint : idleTint);
            StartAnim(VictoryRoutine());
        }

        private void BindArt()
        {
            ArtLibrary art = ArtLibrary.Current;
            if (art == null) return;
            if (idleSprite == null) idleSprite = art.characterIdle;
            if (happySprite == null) happySprite = art.characterHappy;
            if (sadSprite == null) sadSprite = art.characterSad;
        }

        private void EnsureDisplay()
        {
            if (display == null) display = GetComponent<Image>();
            if (display == null)
            {
                if (!AuthoredUi.CanBuild)
                {
                    AuthoredUi.Missing("Character Image");
                    return;
                }

                display = gameObject.AddComponent<Image>();
            }

            display.raycastTarget = false;
            display.preserveAspect = true;
            display.type = Image.Type.Simple;
        }

        private void HideStubs()
        {
            SetStubHidden(body);
            SetStubHidden(head);
            SetStubHidden(armLeft);
            SetStubHidden(armRight);
        }

        private static void SetStubHidden(RectTransform rt)
        {
            if (rt == null) return;
            rt.gameObject.SetActive(false);
        }

        private void ApplySprite(Sprite sprite, Color tint)
        {
            if (display == null) return;
            display.sprite = sprite;
            display.color = tint;
            display.preserveAspect = true;
            display.type = Image.Type.Simple;
        }

        private void StartAnim(IEnumerator enumerator)
        {
            if (!isActiveAndEnabled) return;
            StopActive();
            routine = StartCoroutine(enumerator);
        }

        private void StopActive()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private IEnumerator HappyRoutine()
        {
            const float hop = 0.22f;
            for (float t = 0f; t < hop; t += Time.deltaTime)
            {
                float u = t / hop;
                float bounce = Mathf.Sin(u * Mathf.PI);
                ApplyScale(1f + bounce * 0.12f);
                if (root != null)
                    root.anchoredPosition = new Vector2(0f, bounce * 14f);
                yield return null;
            }

            ApplyScale(1f);
            if (root != null) root.anchoredPosition = Vector2.zero;
            routine = null;
        }

        private IEnumerator SadRoutine()
        {
            const float dur = 0.28f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                float wobble = Mathf.Sin(u * Mathf.PI * 3f) * (1f - u) * 10f;
                SetRotation(root, wobble);
                yield return null;
            }

            SetRotation(root, 0f);
            routine = null;
        }

        private IEnumerator VictoryRoutine()
        {
            SetRotation(armLeft, 110f);
            SetRotation(armRight, -110f);
            const float dur = 0.7f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = t / dur;
                float pulse = 1f + Mathf.Sin(u * Mathf.PI * 2f) * 0.08f;
                ApplyScale(pulse);
                if (root != null)
                    root.anchoredPosition = new Vector2(0f, Mathf.Sin(u * Mathf.PI) * 18f);
                yield return null;
            }

            ApplyScale(1.06f);
            if (root != null) root.anchoredPosition = new Vector2(0f, 6f);
            routine = null;
        }

        private void ApplyScale(float k)
        {
            if (root == null) return;
            root.localScale = rootScale * k;
        }

        private static void SetRotation(RectTransform rt, float z)
        {
            if (rt == null) return;
            rt.localEulerAngles = new Vector3(0f, 0f, z);
        }
    }
}
