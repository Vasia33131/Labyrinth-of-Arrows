using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unpuzzle.EditorTools
{
    /// <summary>
    /// Собирает коридоры HUD + три средние зоны, орбиту и ссылки.
    /// Меню: Tools/Unpuzzle/Setup Visible Layout (HUD corridors)
    /// </summary>
    public static class ScreenLayoutSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string RoundedSpritePath = "Assets/Art/Sprites/ui_rounded.png";
        private const string CandySpritePath = "Assets/Art/Sprites/candy_icon.png";
        private const string FaceSpritePath = "Assets/Art/Sprites/character_face.png";
        private const int PixelsPerUnit = 128;

        private const string LayoutVersionKey = "Unpuzzle.ScreenLayoutVersion";
        private const int LayoutVersion = 2;

        [InitializeOnLoadMethod]
        private static void AutoEmbedAfterCompile()
        {
            EditorPrefs.SetInt(LayoutVersionKey, LayoutVersion);
        }

        [MenuItem("Tools/Unpuzzle/Setup Screen Layout (Character / Board / Candies)", priority = 5)]
        public static void SetupScreenLayout()
        {
            VisibleLayoutSetup.SetupVisibleLayout();
        }

        public static void EmbedIntoOpenScene()
        {
            EnsureArt();
            EnsureFolders();

            Camera cam = EnsureCamera();
            Transform levelRoot = EnsureLevelRoot();
            GameManager gameManager = EnsureGameManager(levelRoot, cam);
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            OrphanHudToCanvas(canvas.transform);
            DestroyNamed("TopHud");
            DestroyNamed("BottomHud");
            DestroyNamed("ScreenRoot");
            DestroyNamed("WorldDecor");

            Sprite rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            Sprite candySprite = AssetDatabase.LoadAssetAtPath<Sprite>(CandySpritePath);
            Sprite faceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FaceSpritePath);

            var config = ScreenLayoutConfig.CreateDefault();
            RectTransform screenRoot;
            ScreenLayoutBuilder builder = BuildScreenRoot(canvas.transform, rounded, config, out screenRoot);
            BuildHudCorridors(canvas.transform, builder);
            Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/character_idle.png");
            CharacterView character = BuildCharacter(builder.characterSlot, rounded, faceSprite, idle);
            CandyBoxView candy = BuildCandy(builder.candyBox, builder.candyLandPoint, candySprite);
            WorldDecor world = BuildWorldDecor(rounded);

            candy.worldTray = world.candyTray;
            candy.gameCamera = cam;
            candy.candySprite = candySprite;

            Directory.CreateDirectory("Assets/Prefabs/UI");
            PrefabUtility.SaveAsPrefabAsset(character.gameObject, "Assets/Prefabs/UI/Character.prefab");

            var fitter = cam.GetComponent<BoardCameraFitter>();
            if (fitter == null) fitter = cam.gameObject.AddComponent<BoardCameraFitter>();
            fitter.gameCamera = cam;
            fitter.boardPlayArea = builder.boardPlayArea;
            fitter.boardBackdrop = world.boardBackdrop;
            fitter.config = config;

            var director = gameManager.GetComponent<ArrowOrbitDirector>();
            if (director == null) director = gameManager.gameObject.AddComponent<ArrowOrbitDirector>();
            director.config = config;
            director.gameCamera = cam;
            director.boardFrame = builder.boardFrame;
            director.candyLandPoint = builder.candyLandPoint;
            director.candyBox = candy;
            director.character = character;
            director.worldTray = world.candyTray;

            gameManager.screenLayout = builder;
            gameManager.orbitDirector = director;
            gameManager.characterView = character;
            gameManager.candyBoxView = candy;
            if (gameManager.levelRoot == null) gameManager.levelRoot = levelRoot;
            if (gameManager.inputHandler != null) gameManager.inputHandler.gameCamera = cam;

            cam.orthographic = true;
            cam.backgroundColor = ScreenLayoutConfig.ScreenBackground;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            ConfigureBuildSettings();

            builder.applyRuntimeLayout = true;
            Canvas.ForceUpdateCanvases();
            builder.Apply();
            Canvas.ForceUpdateCanvases();
            builder.Apply();
            fitter.Fit(gameManager.levelGenerator, cam);

            LevelProgressSetup.EmbedIntoOpenScene();
            builder.Apply();
            builder.applyRuntimeLayout = false;
            SceneCanvasSetup.PrepareOpenScene();

            EditorUtility.SetDirty(gameManager);
            EditorUtility.SetDirty(builder);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(fitter);
            EditorUtility.SetDirty(character);
            EditorUtility.SetDirty(candy);
        }

        // -------------------------------------------------------------- scene objects

        private static Camera EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.backgroundColor = ScreenLayoutConfig.ScreenBackground;
                if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                    cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                return cam;
            }

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ScreenLayoutConfig.ScreenBackground;
            cam.allowHDR = false;
            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static Transform EnsureLevelRoot()
        {
            GameObject found = GameObject.Find("LevelRoot");
            if (found != null) return found.transform;
            return new GameObject("LevelRoot").transform;
        }

        private static GameManager EnsureGameManager(Transform levelRoot, Camera cam)
        {
            var gm = Object.FindObjectOfType<GameManager>();
            if (gm == null)
            {
                var go = new GameObject("GameManager");
                gm = go.AddComponent<GameManager>();
                go.AddComponent<LevelGenerator>();
                go.AddComponent<InputHandler>();
                go.AddComponent<BonusSystem>();
            }

            if (gm.levelGenerator == null) gm.levelGenerator = gm.GetComponent<LevelGenerator>();
            if (gm.inputHandler == null) gm.inputHandler = gm.GetComponent<InputHandler>();
            if (gm.bonuses == null) gm.bonuses = gm.GetComponent<BonusSystem>();
            if (gm.levelRoot == null) gm.levelRoot = levelRoot;
            if (gm.levelGenerator != null && gm.levelGenerator.levelRoot == null)
                gm.levelGenerator.levelRoot = levelRoot;
            if (gm.inputHandler != null)
            {
                gm.inputHandler.gameCamera = cam;
                gm.inputHandler.gameManager = gm;
            }

            if (gm.arrowPrefab == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<ArrowController>("Assets/Prefabs/Arrow.prefab");
                gm.arrowPrefab = prefab;
                if (gm.levelGenerator != null) gm.levelGenerator.arrowPrefab = prefab;
            }

            return gm;
        }

        private static Canvas EnsureCanvas()
        {
            var ui = Object.FindObjectOfType<UIManager>();
            if (ui != null) return ui.GetComponent<Canvas>();

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                    return canvases[i];
            }

            var go = new GameObject("UI Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            var manager = go.AddComponent<UIManager>();
            var gm = Object.FindObjectOfType<GameManager>();
            if (gm != null)
            {
                manager.gameManager = gm;
                gm.uiManager = manager;
            }

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void DestroyNamed(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        // -------------------------------------------------------------- UI hierarchy

        private static ScreenLayoutBuilder BuildScreenRoot(Transform canvas, Sprite rounded, ScreenLayoutConfig config, out RectTransform screenRoot)
        {
            var rootGo = new GameObject("ScreenRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas, false);
            rootGo.transform.SetAsFirstSibling();
            screenRoot = rootGo.GetComponent<RectTransform>();
            Stretch(screenRoot);

            var group = rootGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.ignoreParentGroups = false;

            var builder = rootGo.AddComponent<ScreenLayoutBuilder>();
            builder.config = config;
            builder.screenRoot = screenRoot;
            builder.hostCanvas = canvas.GetComponent<Canvas>();

            builder.characterZone = CreateZone(screenRoot, "CharacterZone", rounded, ScreenLayoutConfig.FrameFill, true);
            builder.characterLabel = null;
            builder.characterSlot = CreateFrame(builder.characterZone, "CharacterSlot", rounded,
                new Color(1f, 1f, 1f, 0.08f), true);

            builder.boardZone = CreateEmpty(screenRoot, "BoardZone");
            builder.boardLabel = null;
            builder.boardFrame = CreateEmpty(builder.boardZone, "BoardFrame");
            builder.boardPlayArea = CreateEmpty(builder.boardFrame, "BoardPlayArea");
            builder.orbitPath = CreateEmpty(builder.boardZone, "OrbitPath");

            builder.candyZone = CreateZone(screenRoot, "CandyZone", rounded, ScreenLayoutConfig.FrameFill, true);
            builder.candyLabel = null;
            builder.candyBox = CreateFrame(builder.candyZone, "CandyBox", rounded, ScreenLayoutConfig.CandyTrayInner, true);
            builder.candyLandPoint = CreateEmpty(builder.candyBox, "CandyLandPoint");
            builder.progressRow = null;

            TintStroke(builder.characterZone, ScreenLayoutConfig.FrameStroke);
            TintStroke(builder.characterSlot, new Color(0.75f, 0.88f, 0.94f, 0.7f));
            TintStroke(builder.candyZone, ScreenLayoutConfig.FrameStroke);
            TintStroke(builder.candyBox, ScreenLayoutConfig.CandyTray);

            builder.Apply();
            return builder;
        }

        private static CharacterView BuildCharacter(RectTransform slot, Sprite rounded, Sprite face, Sprite idle = null)
        {
            var root = new GameObject("Character", typeof(RectTransform));
            root.transform.SetParent(slot, false);
            var rootRt = root.GetComponent<RectTransform>();
            Stretch(rootRt);
            rootRt.offsetMin = new Vector2(10f, 10f);
            rootRt.offsetMax = new Vector2(-10f, -10f);

            var view = root.AddComponent<CharacterView>();
            view.root = rootRt;
            view.idleSprite = idle != null ? idle : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/character_idle.png");
            view.happySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/character_happy.png");
            view.sadSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/character_sad.png");

            var display = root.AddComponent<Image>();
            display.sprite = view.idleSprite;
            display.color = Color.white;
            display.preserveAspect = true;
            display.raycastTarget = false;
            view.display = display;

            view.body = CreateShape(rootRt, "Body", rounded, ScreenLayoutConfig.CharacterFill,
                new Vector2(0.5f, 0.32f), new Vector2(118f, 92f));
            view.head = CreateShape(rootRt, "Head", face != null ? face : rounded, ScreenLayoutConfig.CharacterFill,
                new Vector2(0.5f, 0.68f), new Vector2(86f, 86f));
            CreateShape(view.head, "EyeL", rounded, ScreenLayoutConfig.CharacterAccent,
                new Vector2(0.32f, 0.52f), new Vector2(14f, 18f));
            CreateShape(view.head, "EyeR", rounded, ScreenLayoutConfig.CharacterAccent,
                new Vector2(0.68f, 0.52f), new Vector2(14f, 18f));
            view.armLeft = CreateShape(rootRt, "ArmL", rounded, ScreenLayoutConfig.CharacterAccent,
                new Vector2(0.12f, 0.40f), new Vector2(22f, 56f));
            view.armRight = CreateShape(rootRt, "ArmR", rounded, ScreenLayoutConfig.CharacterAccent,
                new Vector2(0.88f, 0.40f), new Vector2(22f, 56f));
            if (view.body != null) view.body.gameObject.SetActive(false);
            if (view.head != null) view.head.gameObject.SetActive(false);
            if (view.armLeft != null) view.armLeft.gameObject.SetActive(false);
            if (view.armRight != null) view.armRight.gameObject.SetActive(false);
            view.ResetPose();
            return view;
        }

        private static CandyBoxView BuildCandy(RectTransform box, RectTransform land, Sprite candy)
        {
            var view = box.gameObject.AddComponent<CandyBoxView>();
            view.boxRoot = box;
            view.landPoint = land;
            view.candySprite = candy;
            view.bowlEmpty = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/bowl_0.png");
            view.bowl25 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/bowl_25.png");
            view.bowl50 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/bowl_50.png");
            view.bowl75 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/bowl_75.png");
            view.bowlFull = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/bowl_100.png");
            view.bowlImage = box.GetComponent<Image>();
            if (view.bowlImage != null)
            {
                view.bowlImage.sprite = view.bowlEmpty;
                view.bowlImage.color = Color.white;
                view.bowlImage.type = Image.Type.Simple;
                view.bowlImage.preserveAspect = true;
            }
            view.iconPoolSize = 0;

            view.countText = CreateText(box, "CandyCountText", "0",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -10f), new Vector2(120f, 64f),
                TextAnchor.UpperRight, 48, Color.white, FontStyle.Bold);

            CreateText(box, "TrayHint", "лоток",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 28f),
                TextAnchor.MiddleCenter, 18, new Color(1f, 1f, 1f, 0.45f));

            land.sizeDelta = new Vector2(24f, 24f);
            land.anchorMin = new Vector2(0.5f, 0.42f);
            land.anchorMax = new Vector2(0.5f, 0.42f);
            land.anchoredPosition = Vector2.zero;
            return view;
        }

        private struct WorldDecor
        {
            public SpriteRenderer boardBackdrop;
            public SpriteRenderer candyTray;
        }

        private static WorldDecor BuildWorldDecor(Sprite rounded)
        {
            var root = new GameObject("WorldDecor");
            var boardGo = new GameObject("BoardBackdrop");
            boardGo.transform.SetParent(root.transform, false);
            var board = boardGo.AddComponent<SpriteRenderer>();
            board.sprite = rounded;
            board.drawMode = SpriteDrawMode.Sliced;
            board.size = new Vector2(6f, 8f);
            board.color = ScreenLayoutConfig.BoardFillWorld;
            board.sortingOrder = -8;
            AssignUrpSpriteMaterial(board);

            var trayGo = new GameObject("CandyTrayWorld");
            trayGo.transform.SetParent(root.transform, false);
            var tray = trayGo.AddComponent<SpriteRenderer>();
            tray.sprite = rounded;
            tray.drawMode = SpriteDrawMode.Sliced;
            tray.size = new Vector2(4f, 1.6f);
            tray.color = ScreenLayoutConfig.CandyTrayInner;
            tray.sortingOrder = -4;
            AssignUrpSpriteMaterial(tray);

            return new WorldDecor { boardBackdrop = board, candyTray = tray };
        }

        private static void BuildHudCorridors(Transform canvas, ScreenLayoutBuilder builder)
        {
            RectTransform topHud = CreateHudStrip(canvas, "TopHud");
            RectTransform bottomHud = CreateHudStrip(canvas, "BottomHud");

            builder.topHud = topHud;
            builder.bottomHud = bottomHud;
            builder.menuButton = ReparentControl(canvas, topHud, "MenuButton");
            builder.settingsButton = ReparentControl(canvas, topHud, "SettingsButton");
            builder.levelBadge = ReparentControl(canvas, topHud, "LevelBadge");
            builder.hintButton = bottomHud.Find("HintButton") as RectTransform;
            builder.undoButton = bottomHud.Find("UndoButton") as RectTransform;
            builder.extraMoveButton = bottomHud.Find("ExtraMoveButton") as RectTransform;
            builder.restartButton = ReparentControl(canvas, bottomHud, "RestartButton");

            if (builder.screenRoot != null) builder.screenRoot.SetAsFirstSibling();
            topHud.SetSiblingIndex(1);
            bottomHud.SetSiblingIndex(2);

            GameObject progress = GameObject.Find("ProgressRow");
            if (progress != null) progress.SetActive(false);

            SetAnchored(canvas, "MessageText", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f));
            SetAnchored(canvas, "HintText", new Vector2(0.5f, 0.5f), Vector2.zero);
        }

        private static RectTransform CreateHudStrip(Transform canvas, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        private static void OrphanHudToCanvas(Transform canvas)
        {
            if (canvas == null) return;
            string[] names =
            {
                "MenuButton", "SettingsButton", "LevelBadge",
                "HintButton", "UndoButton", "ExtraMoveButton", "RestartButton"
            };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rt = FindHudControl(canvas, names[i]);
                if (rt != null && rt.parent != canvas)
                    rt.SetParent(canvas, true);
            }
        }

        private static RectTransform ReparentControl(Transform canvas, RectTransform parent, string name)
        {
            RectTransform rt = FindHudControl(canvas, name);
            if (rt == null || parent == null) return rt;
            rt.SetParent(parent, false);
            return rt;
        }

        private static RectTransform FindHudControl(Transform canvas, string name)
        {
            if (canvas == null) return null;

            var direct = canvas.Find(name) as RectTransform;
            if (direct != null) return direct;

            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                if (child.name == "WinPanel" || child.name == "LosePanel"
                    || child.name == "SettingsOverlay" || child.name == "SettingsPanel"
                    || child.name == "ShopOverlay"
                    || child.name == DailyHintsPanel.OverlayName)
                    continue;
                var found = child.Find(name) as RectTransform;
                if (found != null) return found;
            }

            return null;
        }

        private static void SetAnchored(Transform canvas, string name, Vector2 anchor, Vector2 pos)
        {
            Transform t = canvas.Find(name);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
        }

        // -------------------------------------------------------------- widgets

        private static RectTransform CreateZone(RectTransform parent, string name, Sprite sprite, Color fill, bool withStroke)
        {
            return CreateFrame(parent, name, sprite, fill, withStroke);
        }

        private static RectTransform CreateFrame(RectTransform parent, string name, Sprite sprite, Color fill, bool withStroke)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);

            if (withStroke)
            {
                var stroke = go.AddComponent<Image>();
                stroke.sprite = sprite;
                stroke.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                stroke.color = ScreenLayoutConfig.FrameStroke;
                stroke.raycastTarget = false;

                var fillGo = new GameObject("Fill", typeof(RectTransform));
                fillGo.transform.SetParent(go.transform, false);
                var fillRt = fillGo.GetComponent<RectTransform>();
                Stretch(fillRt);
                fillRt.offsetMin = new Vector2(2f, 2f);
                fillRt.offsetMax = new Vector2(-2f, -2f);
                var fillImage = fillGo.AddComponent<Image>();
                fillImage.sprite = sprite;
                fillImage.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                fillImage.color = fill;
                fillImage.raycastTarget = false;
            }
            else
            {
                var image = go.AddComponent<Image>();
                image.sprite = sprite;
                image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                image.color = fill;
                image.raycastTarget = false;
            }

            return rt;
        }

        private static void TintStroke(RectTransform frame, Color color)
        {
            if (frame == null) return;
            var image = frame.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private static RectTransform CreateEmpty(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        private static Text CreateZoneLabel(RectTransform parent, string name, string text)
        {
            return CreateText(parent, name, text,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 32f),
                TextAnchor.UpperCenter, 22, ScreenLayoutConfig.ZoneLabel, FontStyle.Bold);
        }

        private static RectTransform CreateShape(RectTransform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return rt;
        }

        private static Text CreateText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, TextAnchor alignment,
            int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (Mathf.Approximately(anchorMin.x, 1f) && Mathf.Approximately(anchorMax.x, 1f))
                rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Font GetDefaultFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { /* ignore */ }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { /* ignore */ }
            }

            return font;
        }

        private static void AssignUrpSpriteMaterial(SpriteRenderer renderer)
        {
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            Material lit = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (unlit != null) renderer.sharedMaterial = unlit;
            else if (lit != null) renderer.sharedMaterial = lit;
        }

        // -------------------------------------------------------------- art / settings

        private static void EnsureFolders()
        {
            string[] folders = { "Assets/Art", "Assets/Art/Sprites", "Assets/Scenes", "Assets/Prefabs" };
            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }

        private static void EnsureArt()
        {
            EnsureFolders();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath) == null)
            {
                WritePng(RoundedSpritePath, MakeRoundedRectTexture(256, 56));
                ConfigureSprite(RoundedSpritePath, new Vector4(56, 56, 56, 56));
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(CandySpritePath) == null)
            {
                WritePng(CandySpritePath, MakeCandyTexture(128));
                ConfigureSprite(CandySpritePath, Vector4.zero);
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(FaceSpritePath) == null)
            {
                WritePng(FaceSpritePath, MakeFaceTexture(128));
                ConfigureSprite(FaceSpritePath, Vector4.zero);
            }

            UnpuzzleArtApply.ConfigureImportedSprites();
        }

        private static void WritePng(string path, Texture2D tex)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                Object.DestroyImmediate(tex);
                return;
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Texture2D MakeRoundedRectTexture(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= radius && x < size - radius || y >= radius && y < size - radius;
                    if (!inside)
                    {
                        int cx = x < radius ? radius : size - radius - 1;
                        int cy = y < radius ? radius : size - radius - 1;
                        float dx = x - cx;
                        float dy = y - cy;
                        inside = dx * dx + dy * dy <= radius * radius;
                    }

                    pixels[y * size + x] = inside ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeCandyTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var wrap = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            float cx = 0.5f, cy = 0.5f;
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float dx = (nx - cx) / 0.28f;
                    float dy = (ny - cy) / 0.20f;
                    bool body = dx * dx + dy * dy < 1f;
                    float wing = Mathf.Abs(nx - 0.5f) - 0.22f;
                    bool bow = ny > 0.42f && ny < 0.58f && wing > 0f && wing < 0.18f;
                    pixels[y * size + x] = (body || bow) ? wrap : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeFaceTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            float cx = 0.5f, cy = 0.5f;
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float dx = nx - cx;
                    float dy = ny - cy;
                    bool circle = dx * dx + dy * dy < 0.46f * 0.46f;
                    pixels[y * size + x] = circle ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(MenuScenePath))
                scenes.Add(new EditorBuildSettingsScene(MenuScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));

            EditorBuildSettingsScene[] previous = EditorBuildSettings.scenes;
            for (int i = 0; i < previous.Length; i++)
            {
                string path = previous[i].path;
                if (path == MenuScenePath || path == GameScenePath) continue;
                scenes.Add(new EditorBuildSettingsScene(path, previous[i].enabled));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateLayout()
        {
            VisibleLayoutSetup.Validate();
            if (GameObject.Find("ScreenRoot") == null)
                Debug.LogError("[Unpuzzle] ScreenRoot не создан.");
            if (Object.FindObjectOfType<ScreenLayoutBuilder>() == null)
                Debug.LogError("[Unpuzzle] ScreenLayoutBuilder не найден.");
            if (Object.FindObjectOfType<ArrowOrbitDirector>() == null)
                Debug.LogError("[Unpuzzle] ArrowOrbitDirector не найден.");
            if (Object.FindObjectOfType<CharacterView>() == null)
                Debug.LogError("[Unpuzzle] CharacterView не найден.");
            if (Object.FindObjectOfType<CandyBoxView>() == null)
                Debug.LogError("[Unpuzzle] CandyBoxView не найден.");

            var gm = Object.FindObjectOfType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[Unpuzzle] GameManager отсутствует.");
                return;
            }

            if (gm.orbitDirector == null || gm.characterView == null || gm.candyBoxView == null || gm.screenLayout == null)
                Debug.LogError("[Unpuzzle] На GameManager не проставлены ссылки layout.");
            if (gm.levelRoot == null || gm.arrowPrefab == null || gm.levelGenerator == null)
                Debug.LogError("[Unpuzzle] Базовые ссылки GameManager потеряны — пересобери Tools/Unpuzzle/Setup Project.");
        }
    }
}
