using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    [Serializable]
    public class BackgroundData
    {
        public string id;
        public string displayName;
        public int priceGold;
        public bool unlockByBattlePass;
        public Sprite sprite;
        public Color tint = Color.white;

        public bool IsFree => !unlockByBattlePass && priceGold <= 0;

        public Color PreviewTint => tint.a > 0f ? tint : Color.white;

        public Color ResolveColor() => sprite != null ? Color.white : PreviewTint;

        public Sprite ResolveSprite(bool menu, ArtLibrary art)
        {
            if (sprite != null) return sprite;
            return DefaultArt(menu, art);
        }

        public static Sprite DefaultArt(bool menu, ArtLibrary art)
        {
            if (art == null) return null;
            return menu ? art.bgMenu : art.bgBoard;
        }
    }

    /// <summary>
    /// Каталог фонов меню и игрового поля. Спрайты можно оставить пустыми и подставить PNG в инспекторе.
    /// </summary>
    [CreateAssetMenu(fileName = "BackgroundCatalog", menuName = "Unpuzzle/Background Catalog")]
    public class BackgroundCatalog : ScriptableObject
    {
        public const string ResourceName = "BackgroundCatalog";

        public const string WitchGroveId = "bg_witch_grove";
        public const string NightElfId = "bg_night_elf";
        public const string NeonCityId = "bg_neon_city";
        public const string ClassroomId = "bg_classroom";

        private static BackgroundCatalog cached;

        public List<BackgroundData> backgrounds = new List<BackgroundData>();

        public IReadOnlyList<BackgroundData> Backgrounds
        {
            get
            {
                EnsureBuiltinEntries();
                return backgrounds;
            }
        }

        public static BackgroundCatalog Current
        {
            get
            {
                if (cached == null)
                    cached = Resources.Load<BackgroundCatalog>(ResourceName);

                if (cached == null)
                {
                    cached = CreateInstance<BackgroundCatalog>();
                    cached.ResetToBuiltin();
                }
                else
                {
                    cached.EnsureBuiltinEntries();
                }

                return cached;
            }
        }

        public BackgroundData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            EnsureBuiltinEntries();
            for (int i = 0; i < backgrounds.Count; i++)
            {
                BackgroundData background = backgrounds[i];
                if (background != null && background.id == id) return background;
            }

            return null;
        }

        public bool Has(string id) => Get(id) != null;

        public void ResetToBuiltin()
        {
            backgrounds = new List<BackgroundData>(BuiltinBackgrounds());
        }

        public void EnsureBuiltinEntries()
        {
            if (backgrounds == null) backgrounds = new List<BackgroundData>();
            RemoveObsoleteBackgrounds();

            BackgroundData[] builtin = BuiltinBackgrounds();
            for (int i = 0; i < builtin.Length; i++)
            {
                if (FindIndex(builtin[i].id) >= 0) continue;
                backgrounds.Add(builtin[i]);
            }
        }

        public void KeepOnlyBuiltinBackgrounds()
        {
            if (backgrounds == null) backgrounds = new List<BackgroundData>();

            BackgroundData[] builtin = BuiltinBackgrounds();
            var allowed = new HashSet<string>();
            for (int i = 0; i < builtin.Length; i++)
            {
                if (builtin[i] != null && !string.IsNullOrEmpty(builtin[i].id))
                    allowed.Add(builtin[i].id);
            }

            for (int i = backgrounds.Count - 1; i >= 0; i--)
            {
                BackgroundData background = backgrounds[i];
                if (background == null || !allowed.Contains(background.id))
                    backgrounds.RemoveAt(i);
            }

            EnsureBuiltinEntries();
        }

        public void RemoveObsoleteBackgrounds()
        {
            if (backgrounds == null) return;

            for (int i = backgrounds.Count - 1; i >= 0; i--)
            {
                BackgroundData background = backgrounds[i];
                if (background == null || IsObsoleteId(background.id))
                    backgrounds.RemoveAt(i);
            }
        }

        private static bool IsObsoleteId(string id)
        {
            return id == "bg_coin_1" || id == "bg_coin_2";
        }

        private int FindIndex(string id)
        {
            for (int i = 0; i < backgrounds.Count; i++)
            {
                if (backgrounds[i] != null && backgrounds[i].id == id) return i;
            }

            return -1;
        }

        private static BackgroundData[] BuiltinBackgrounds()
        {
            return new[]
            {
                new BackgroundData
                {
                    id = GameConstants.DefaultBackgroundId,
                    displayName = "Классика",
                    priceGold = 0,
                    unlockByBattlePass = false,
                    tint = GameConstants.BoardBackground
                },
                new BackgroundData
                {
                    id = WitchGroveId,
                    displayName = "Ведьмин лес",
                    priceGold = 500,
                    unlockByBattlePass = false
                },
                new BackgroundData
                {
                    id = NightElfId,
                    displayName = "Лунный грот",
                    priceGold = 1000,
                    unlockByBattlePass = false
                },
                new BackgroundData
                {
                    id = NeonCityId,
                    displayName = "Неоновый город",
                    priceGold = 1800,
                    unlockByBattlePass = false
                },
                new BackgroundData
                {
                    id = ClassroomId,
                    displayName = "Классная доска",
                    priceGold = 0,
                    unlockByBattlePass = true
                }
            };
        }
    }
}
