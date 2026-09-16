using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    [Serializable]
    public class CharacterSkinData
    {
        public string id;
        public string displayName;
        public int priceGold;
        public bool unlockByRewarded;
        public bool unlockByBattlePass;
        public Sprite idle;
        public Sprite happy;
        public Sprite sad;
        public Color tint = Color.white;

        public bool IsFree => !unlockByRewarded && !unlockByBattlePass && priceGold <= 0;

        public Color PreviewTint => tint.a > 0f ? tint : Color.white;

        public Sprite ResolveIdle(ArtLibrary art)
        {
            if (idle != null) return idle;
            return art != null ? art.characterIdle : null;
        }

        public Sprite ResolveHappy(ArtLibrary art)
        {
            if (happy != null) return happy;
            return art != null ? art.characterHappy : null;
        }

        public Sprite ResolveSad(ArtLibrary art)
        {
            if (sad != null) return sad;
            return art != null ? art.characterSad : null;
        }

        public Color ColorForIdle() => idle != null ? Color.white : PreviewTint;

        public Color ColorForHappy() => happy != null ? Color.white : PreviewTint;

        public Color ColorForSad() => sad != null ? Color.white : PreviewTint;
    }

    /// <summary>
    /// Каталог скинов персонажа. Спрайты можно оставить пустыми и подставить PNG в инспекторе.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSkinCatalog", menuName = "Unpuzzle/Character Skin Catalog")]
    public class CharacterSkinCatalog : ScriptableObject
    {
        public const string ResourceName = "CharacterSkinCatalog";

        public const string WitchId = "skin_witch";
        public const string DarkElfId = "skin_dark_elf";
        public const string CyberpunkId = "skin_cyberpunk";
        public const string TeacherId = "skin_teacher";

        public const string Coin1Id = "skin_coin_1";
        public const string Coin2Id = "skin_coin_2";
        public const string Ad1Id = "skin_ad_1";
        public const string Ad2Id = "skin_ad_2";

        private static CharacterSkinCatalog cached;

        public List<CharacterSkinData> skins = new List<CharacterSkinData>();

        public IReadOnlyList<CharacterSkinData> Skins
        {
            get
            {
                EnsureBuiltinEntries();
                return skins;
            }
        }

        public static void InvalidateCache()
        {
            cached = null;
        }

        public static CharacterSkinCatalog Current
        {
            get
            {
                if (cached == null)
                    cached = Resources.Load<CharacterSkinCatalog>(ResourceName);

                if (cached == null)
                {
                    var created = CreateInstance<CharacterSkinCatalog>();
                    created.ResetToBuiltin();
                    return created;
                }

                cached.EnsureBuiltinEntries();
                return cached;
            }
        }

        public CharacterSkinData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            EnsureBuiltinEntries();
            for (int i = 0; i < skins.Count; i++)
            {
                CharacterSkinData skin = skins[i];
                if (skin != null && skin.id == id) return skin;
            }

            return null;
        }

        public bool Has(string id) => Get(id) != null;

        public void ResetToBuiltin()
        {
            skins = new List<CharacterSkinData>(BuiltinSkins());
        }

        public void EnsureBuiltinEntries()
        {
            if (skins == null) skins = new List<CharacterSkinData>();
            RemoveObsoleteSkins();

            CharacterSkinData[] builtin = BuiltinSkins();
            for (int i = 0; i < builtin.Length; i++)
            {
                if (FindIndex(builtin[i].id) >= 0) continue;
                skins.Add(builtin[i]);
            }
        }

        public void KeepOnlyBuiltinSkins()
        {
            if (skins == null) skins = new List<CharacterSkinData>();

            CharacterSkinData[] builtin = BuiltinSkins();
            var allowed = new HashSet<string>();
            for (int i = 0; i < builtin.Length; i++)
            {
                if (builtin[i] != null && !string.IsNullOrEmpty(builtin[i].id))
                    allowed.Add(builtin[i].id);
            }

            for (int i = skins.Count - 1; i >= 0; i--)
            {
                CharacterSkinData skin = skins[i];
                if (skin == null || !allowed.Contains(skin.id))
                    skins.RemoveAt(i);
            }

            EnsureBuiltinEntries();
        }

        public void RemoveObsoleteSkins()
        {
            if (skins == null) return;

            for (int i = skins.Count - 1; i >= 0; i--)
            {
                CharacterSkinData skin = skins[i];
                if (skin == null || IsObsoleteId(skin.id))
                    skins.RemoveAt(i);
            }
        }

        private static bool IsObsoleteId(string id)
        {
            return id == Coin1Id || id == Coin2Id || id == Ad1Id || id == Ad2Id;
        }

        private int FindIndex(string id)
        {
            for (int i = 0; i < skins.Count; i++)
            {
                if (skins[i] != null && skins[i].id == id) return i;
            }

            return -1;
        }

        private static CharacterSkinData[] BuiltinSkins()
        {
            return new[]
            {
                new CharacterSkinData
                {
                    id = GameConstants.DefaultSkinId,
                    displayName = "Классика",
                    priceGold = 0,
                    unlockByRewarded = false,
                    unlockByBattlePass = false,
                    tint = Color.white
                },
                new CharacterSkinData
                {
                    id = WitchId,
                    displayName = "Ведьма",
                    priceGold = 700,
                    unlockByRewarded = false,
                    unlockByBattlePass = false,
                    tint = Color.white
                },
                new CharacterSkinData
                {
                    id = DarkElfId,
                    displayName = "Тёмная эльфийка",
                    priceGold = 1400,
                    unlockByRewarded = false,
                    unlockByBattlePass = false,
                    tint = Color.white
                },
                new CharacterSkinData
                {
                    id = CyberpunkId,
                    displayName = "Киберпанк",
                    priceGold = 2200,
                    unlockByRewarded = false,
                    unlockByBattlePass = false,
                    tint = Color.white
                },
                new CharacterSkinData
                {
                    id = TeacherId,
                    displayName = "Учительница",
                    priceGold = 0,
                    unlockByRewarded = false,
                    unlockByBattlePass = true,
                    tint = Color.white
                }
            };
        }
    }
}
