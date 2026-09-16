using System;
using UnityEngine;
using YG;

namespace Unpuzzle
{
    /// <summary>
    /// Единственный источник видимых игроку строк. Два языка: русский и английский.
    /// Язык берётся из YG2.lang (SDK Яндекс Игр), а не из Application.systemLanguage:
    /// на WebGL системная культура браузера не совпадает с языком аккаунта игрока.
    /// Ручного переключателя нет — SettingsYG2 Localization.setLanguageMod = EveryGameLaunch.
    /// </summary>
    public static class GameTexts
    {
        /// <summary>Языки, которые Яндекс рекомендует вести на русской локали.</summary>
        private static readonly string[] RussianCodes = { "ru", "be", "kk", "uk", "uz" };

        /// <summary>Панели, которые уже открыты, перерисовывают текст по этому событию.</summary>
        public static event Action OnLanguageChanged;

        private static bool subscribed;
        private static string appliedLang;

        public static bool IsRussian => IsRussianCode(YG2.lang);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnLanguageChanged = null;
            subscribed = false;
            appliedLang = null;
        }

        /// <summary>
        /// YG2 обнуляет свои Action при инициализации (BeforeSceneLoad), поэтому
        /// подписка живёт в AfterSceneLoad — уже после сброса, но до YGSendMessage.Start.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Subscribe();
            appliedLang = Normalize(YG2.lang);
        }

        public static void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            YG2.onSwitchLang -= HandleSwitchLang;
            YG2.onSwitchLang += HandleSwitchLang;
            YG2.onGetSDKData -= HandleSdkData;
            YG2.onGetSDKData += HandleSdkData;
        }

        private static void HandleSwitchLang(string _) => Refresh();

        private static void HandleSdkData() => Refresh();

        /// <summary>Перечитать YG2.lang и разослать событие, если язык действительно сменился.</summary>
        public static void Refresh()
        {
            string lang = Normalize(YG2.lang);
            if (lang == appliedLang) return;

            appliedLang = lang;
            OnLanguageChanged?.Invoke();
        }

        private static string Normalize(string lang)
        {
            return IsRussianCode(lang) ? "ru" : "en";
        }

        private static bool IsRussianCode(string lang)
        {
            if (string.IsNullOrEmpty(lang)) return false;

            string code = lang.Trim().ToLowerInvariant();
            int dash = code.IndexOfAny(new[] { '-', '_' });
            if (dash > 0) code = code.Substring(0, dash);

            for (int i = 0; i < RussianCodes.Length; i++)
            {
                if (code == RussianCodes[i]) return true;
            }

            return false;
        }

        private static string Pick(string ru, string en) => IsRussian ? ru : en;

        // --------------------------------------------------------------- плюралы

        /// <summary>1 / 2-4 / 5+ с учётом 11-14.</summary>
        private static string PluralRu(int count, string one, string few, string many)
        {
            int mod100 = Mathf.Abs(count) % 100;
            if (mod100 >= 11 && mod100 <= 14) return many;

            int mod10 = mod100 % 10;
            if (mod10 == 1) return one;
            if (mod10 >= 2 && mod10 <= 4) return few;
            return many;
        }

        private static string PluralEn(int count, string one, string many)
        {
            return Mathf.Abs(count) == 1 ? one : many;
        }

        // ------------------------------------------------------- меню и заголовки

        /// <summary>
        /// П. 5.1.3: посимвольно те же строки, что в черновике консоли на ru/en
        /// и в Player Settings productName (английское имя).
        /// ru: «Уберите стрелки», en: «Clear the Arrows».
        /// </summary>
        public static string GameTitle => Pick("Уберите стрелки", "Clear the Arrows");

        public static string Play => Pick("Играть", "Play");
        public static string Settings => Pick("Настройки", "Settings");
        public static string Shop => Pick("Магазин", "Shop");
        public static string Close => Pick("Закрыть", "Close");
        public static string Back => Pick("Назад", "Back");
        public static string Ok => Pick("ОК", "OK");

        public static string Level(int index) => Pick($"Уровень {index}", $"Level {index}");
        public static string School(int index) => Pick($"Школа {index}", $"School {index}");
        public static string SchoolTitle => Pick("Школа", "School");

        // ------------------------------------------------------------------- HUD

        public static string Moves(int value) => Pick($"Ходы {value}", $"Moves {value}");
        public static string ArrowsLeft(int value) => Pick($"Стрелок: {value}", $"Arrows: {value}");
        public static string Gold(int value) => Pick($"Золото {value}", $"Gold {value}");

        public static string SoundButton(bool on)
        {
            return on
                ? Pick("Звук: вкл", "Sound: on")
                : Pick("Звук: выкл", "Sound: off");
        }

        // -------------------------------------------------------------- настройки

        public static string OnOff(bool on) => on ? Pick("Вкл", "On") : Pick("Выкл", "Off");
        public static string Sound => Pick("Звук", "Sound");
        public static string Music => Pick("Музыка", "Music");

        // --------------------------------------------------------- выбор режима

        public static string ModeTitle => Pick("Режим", "Mode");
        public static string NormalGame => Pick("Обычная игра", "Normal game");

        /// <summary>
        /// Описание управления. У «Школы» для этого есть туториал, а кампания
        /// оставалась без объяснений. Тот же текст должен стоять в «Как играть» черновика.
        /// </summary>
        public static string CampaignControls => Pick(
            "Тапни по стрелке — она улетит по своему пути. Убери все стрелки за отведённые ходы.",
            "Tap an arrow and it flies along its path. Clear every arrow within the move limit.");

        // --------------------------------------------------- победа и поражение

        public static string Win => Pick("ПОБЕДА", "YOU WIN");
        public static string Lose => Pick("ПОРАЖЕНИЕ", "YOU LOSE");
        public static string NextLevel => Pick("Дальше", "Next");
        public static string Restart => Pick("Заново", "Retry");
        public static string Exit => Pick("Выйти", "Exit");

        // ----------------------------------------------------------- магазин

        public static string TabCharacter => Pick("Персонаж", "Character");
        public static string TabBackground => Pick("Фон", "Background");
        public static string TabGold => Pick("Золото", "Gold");

        /// <summary>
        /// Требование 4.5.1: кнопка rewarded до клика называет и рекламу, и награду.
        /// Сумма берётся из той же константы, что и выдача, чтобы подпись не разъехалась.
        /// </summary>
        public static string WatchAdForGold(int amount)
        {
            return Pick($"Смотреть рекламу — {amount} золота", $"Watch ad — {amount} gold");
        }

        public static string WatchAdForGold() => WatchAdForGold(GameConstants.GoldPerRewardedAd);

        /// <summary>Заодно объясняет, откуда берутся уровни пропуска: сам пропуск об этом молчал.</summary>
        public static string BattlePassLocked => Pick(
            "Награда боевого пропуска. Его уровни даёт режим «Школа»",
            "A battle pass reward. Pass levels come from School mode");

        public static string NotEnoughGold => Pick("Недостаточно золота", "Not enough gold");
        public static string SkinNotFound => Pick("Скин не найден", "Skin not found");
        public static string BackgroundNotFound => Pick("Фон не найден", "Background not found");

        /// <summary>Цена только вместе с валютой: голая цифра читается как цена в рублях.</summary>
        public static string Price(int gold) => Pick($"{gold} золота", $"{gold} gold");

        // ------------------------------------------------------- карточки товаров

        public static string Equipped => Pick("Надет", "Equipped");
        public static string Equip => Pick("Надеть", "Equip");
        public static string Buy => Pick("Купить", "Buy");
        public static string BattlePass => Pick("Боевой пропуск", "Battle pass");
        public static string Premium => Pick("Премиум", "Premium");
        public static string RewardSkin => Pick("Скин", "Skin");
        public static string RewardBackground => Pick("Фон", "Background");

        /// <summary>Вёрстка портретная 1080×1920, в ландшафте на телефоне кнопки нечитаемо мелкие.</summary>
        public static string RotateDevice => Pick(
            "Поверните телефон вертикально",
            "Please rotate your device to portrait");

        /// <summary>
        /// displayName в каталогах записан по-русски и лежит в .asset, поэтому имя
        /// переводится на выдаче по id. Неизвестный id отдаёт значение из каталога.
        /// </summary>
        public static string SkinName(string id, string fallback)
        {
            switch (id)
            {
                case GameConstants.DefaultSkinId: return Pick("Классика", "Classic");
                case CharacterSkinCatalog.WitchId: return Pick("Ведьма", "Witch");
                case CharacterSkinCatalog.DarkElfId: return Pick("Тёмная эльфийка", "Dark Elf");
                case CharacterSkinCatalog.CyberpunkId: return Pick("Киберпанк", "Cyberpunk");
                case CharacterSkinCatalog.TeacherId: return Pick("Учительница", "Teacher");
                default: return fallback;
            }
        }

        public static string BackgroundName(string id, string fallback)
        {
            switch (id)
            {
                case GameConstants.DefaultBackgroundId: return Pick("Классика", "Classic");
                case BackgroundCatalog.WitchGroveId: return Pick("Ведьмин лес", "Witch Forest");
                case BackgroundCatalog.NightElfId: return Pick("Лунный грот", "Moonlit Grotto");
                case BackgroundCatalog.NeonCityId: return Pick("Неоновый город", "Neon City");
                case BackgroundCatalog.ClassroomId: return Pick("Классная доска", "Blackboard");
                default: return fallback;
            }
        }

        // ------------------------------------------------------- боевой пропуск

        public static string BattlePassTitle => Pick("Боевой пропуск", "Battle pass");

        /// <summary>Колонка пропуска в сцене узкая, поэтому строка уровня остаётся короткой.</summary>
        public static string BattlePassLevel(int current, int total) =>
            Pick($"Ур. {current}/{total}", $"Lv. {current}/{total}");

        // ------------------------------------------------------------ подсказки

        public static string HintsTitle => Pick("Подсказки", "Hints");
        public static string UndoTitle => Pick("Отмена хода", "Undo move");
        public static string ExtraMoveTitle => Pick("+1 ход", "+1 move");

        public static string BoosterTitle(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return UndoTitle;
                case BoosterKind.ExtraMove: return ExtraMoveTitle;
                default: return HintsTitle;
            }
        }

        public static string BoosterEmpty(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return Pick("Отмен нет", "No undos left");
                case BoosterKind.ExtraMove: return Pick("Нет зарядов «+1 ход»", "No \"+1 move\" charges left");
                default: return Pick("Подсказок нет", "No hints left");
            }
        }

        /// <summary>«3 подсказки» / «3 hints» — количество вместе с нужной формой слова.</summary>
        public static string BoosterAmount(BoosterKind kind, int amount)
        {
            switch (kind)
            {
                case BoosterKind.Undo:
                    return IsRussian
                        ? $"{amount} {PluralRu(amount, "отмена", "отмены", "отмен")}"
                        : $"{amount} {PluralEn(amount, "undo", "undos")}";
                case BoosterKind.ExtraMove:
                    return IsRussian
                        ? $"{amount} {PluralRu(amount, "ход", "хода", "ходов")}"
                        : $"{amount} {PluralEn(amount, "move", "moves")}";
                default:
                    return IsRussian
                        ? $"{amount} {PluralRu(amount, "подсказка", "подсказки", "подсказок")}"
                        : $"{amount} {PluralEn(amount, "hint", "hints")}";
            }
        }

        /// <summary>
        /// Требование 4.5.1: кнопка rewarded до клика называет и рекламу, и награду.
        /// Количество берётся из той же константы, что и выдача, чтобы подпись не разъехалась.
        /// </summary>
        public static string WatchAdForBooster(BoosterKind kind, int amount)
        {
            return Pick(
                $"Смотреть рекламу — +{BoosterAmount(kind, amount)}",
                $"Watch ad — +{BoosterAmount(kind, amount)}");
        }

        public static string BoosterReceived(BoosterKind kind, int amount)
        {
            return Pick(
                $"Получено +{BoosterAmount(kind, amount)}",
                $"Received +{BoosterAmount(kind, amount)}");
        }

        public static string DailyBonus(int hints, int undos, int extraMoves)
        {
            return IsRussian
                ? $"Ежедневный бонус: +{BoosterAmount(BoosterKind.Hint, hints)}, "
                  + $"+{BoosterAmount(BoosterKind.Undo, undos)}, "
                  + $"+{BoosterAmount(BoosterKind.ExtraMove, extraMoves)}"
                : $"Daily bonus: +{BoosterAmount(BoosterKind.Hint, hints)}, "
                  + $"+{BoosterAmount(BoosterKind.Undo, undos)}, "
                  + $"+{BoosterAmount(BoosterKind.ExtraMove, extraMoves)}";
        }

        public static string DailyBonusAlreadyClaimed => Pick("Бонус на сегодня уже получен", "Today's bonus is already claimed");

        public static string BoosterBalance(int hints, int undos, int extraMoves)
        {
            return IsRussian
                ? $"Подсказки: {hints}\nОтмена: {undos}\n+1 ход: {extraMoves}"
                : $"Hints: {hints}\nUndo: {undos}\n+1 move: {extraMoves}";
        }

        // -------------------------------------------------------------- реклама

        public static string AdFailed => Pick("Не удалось, попробуй ещё раз", "Failed, please try again");

        // ------------------------------------------------------- школьный туториал

        public static string Next => Pick("Далее", "Next");
        public static string GotIt => Pick("Понятно", "Got it");

        /// <summary>Счётчик страниц туториала: цифры одинаковы в обоих языках.</summary>
        public static string PageIndex(int page, int total) => $"{page} / {total}";

        public static string TutorialTitle(SchoolTutorialScheme scheme)
        {
            switch (scheme)
            {
                case SchoolTutorialScheme.KeyBook: return Pick("Книга-ключ", "The Key Book");
                case SchoolTutorialScheme.BigBook: return Pick("Большая книга", "The Big Book");
                case SchoolTutorialScheme.Pointer: return Pick("Указка учителя", "The Teacher's Pointer");
                case SchoolTutorialScheme.Briefcase: return Pick("Портфель", "The Briefcase");
                default: return Pick("Цель", "The Goal");
            }
        }

        public static string[] TutorialLines(SchoolTutorialScheme scheme)
        {
            switch (scheme)
            {
                case SchoolTutorialScheme.KeyBook:
                    return IsRussian
                        ? new[]
                        {
                            "У некоторых стрелок на наконечнике маленькая книжка. Это ключ.",
                            "Убери ключ — большая книга на поле исчезнет."
                        }
                        : new[]
                        {
                            "Some arrows carry a small book on the tip. That arrow is a key.",
                            "Clear the key and the big book on the board disappears."
                        };

                case SchoolTutorialScheme.BigBook:
                    return IsRussian
                        ? new[]
                        {
                            "На пути стрелки стоит большая книга. Через эту клетку нельзя выпустить ни одну стрелку.",
                            "Убери ключ с иконкой книжки — большая книга исчезнет, путь откроется."
                        }
                        : new[]
                        {
                            "A big book stands in the way. No arrow can pass through that cell.",
                            "Clear the key arrow with the book icon: the big book vanishes and the path opens."
                        };

                case SchoolTutorialScheme.Pointer:
                    return IsRussian
                        ? new[]
                        {
                            "Длинная указка занимает несколько клеток и перекрывает путь.",
                            "Её нельзя убрать тапом. После каждой успешно убранной стрелки указка поворачивается на 90° вокруг своего начала.",
                            "Выпускай стрелку, пока указка не перекрыла коридор."
                        }
                        : new[]
                        {
                            "The long pointer covers several cells and blocks the path.",
                            "You cannot remove it by tapping. After every arrow you clear, the pointer turns 90° around its base.",
                            "Clear your arrow while the corridor is still open."
                        };

                case SchoolTutorialScheme.Briefcase:
                    return IsRussian
                        ? new[]
                        {
                            "Портфель занимает две клетки и тоже перекрывает путь. Тапом его не сдвинуть и не выгнать с поля.",
                            "Свайпни портфель в нужную сторону — он проедет одну клетку вдоль своей оси, если место свободно.",
                            "Отодвинь портфель с чужого пути, но не загороди другую стрелку."
                        }
                        : new[]
                        {
                            "The briefcase covers two cells and blocks the path too. A tap will not slide it or push it off the board.",
                            "Swipe the briefcase in the direction you want — it moves one cell along its axis if that cell is free.",
                            "Slide it out of the way without blocking another arrow."
                        };

                default:
                    return IsRussian
                        ? new[]
                        {
                            "Убери все стрелки. Книга — ключ и блокер. Указка крутится сама. Портфель двигаешь ты.",
                            "Каждая пройденная школа поднимает уровень боевого пропуска."
                        }
                        : new[]
                        {
                            "Clear every arrow. The book is both a key and a blocker. The pointer turns on its own. The briefcase is yours to move.",
                            "Every school you finish raises your battle pass level."
                        };
            }
        }

        // ------------------------------------------------------- игровые сообщения

        public static string ColorName(ArrowColor color)
        {
            switch (color)
            {
                case ArrowColor.Red: return Pick("красные", "red");
                case ArrowColor.Blue: return Pick("синие", "blue");
                case ArrowColor.DarkBlue: return Pick("тёмно-синие", "dark blue");
                case ArrowColor.Green: return Pick("зелёные", "green");
                case ArrowColor.Yellow: return Pick("жёлтые", "yellow");
                case ArrowColor.Orange: return Pick("оранжевые", "orange");
                default: return Pick("серые", "grey");
            }
        }

        public static string DirectionName(ArrowDirection direction)
        {
            switch (direction)
            {
                case ArrowDirection.Up: return Pick("вверх", "up");
                case ArrowDirection.Right: return Pick("вправо", "right");
                case ArrowDirection.Down: return Pick("вниз", "down");
                default: return Pick("влево", "left");
            }
        }

        public static string WrongColor(ArrowColor blockingColor)
        {
            return Pick(
                $"Сначала убери {ColorName(blockingColor)}: −1 ход",
                $"Clear {ColorName(blockingColor)} first: −1 move");
        }

        public static string PathBlockedByBook => Pick("Путь закрыт книгой", "The book blocks the path");
        public static string PathBlockedByPointer => Pick("Путь закрыт указкой", "The pointer blocks the path");
        public static string PathBlockedByBackpack => Pick("Путь закрыт портфелем", "The briefcase blocks the path");
        public static string PathBlocked => Pick("Путь перекрыт: −1 ход", "The path is blocked: −1 move");

        public static string PointerCannotBeRemoved => Pick("Указку нельзя убрать", "The pointer cannot be removed");

        public static string BookCannotBeRemoved => Pick(
            "Книгу нельзя убрать. Сначала убери стрелку с иконкой книжки.",
            "The book cannot be removed. Clear the arrow with the book icon first.");

        public static string BackpackHasNoRoom => Pick("Портфель некуда сдвинуть", "No room to slide the briefcase");

        public static string SchoolIdleSwipe => Pick(
            "Свайпни портфель в эту сторону",
            "Swipe the briefcase this way");

        public static string SchoolIdleTap => Pick(
            "Нажми на эту стрелку",
            "Tap this arrow");

        public static string PriorityHint(ArrowColor requiredColor)
        {
            return Pick(
                $"Сейчас можно: {ColorName(requiredColor)}",
                $"Allowed now: {ColorName(requiredColor)}");
        }

        public static string NoSafeMoves => Pick(
            "Безопасных ходов нет — попробуй «+1 ход» или рестарт",
            "No safe moves left — try \"+1 move\" or restart");

        public static string HintFound(ArrowColor color, ArrowDirection direction)
        {
            return Pick(
                $"Подсказка: {ColorName(color)} — {DirectionName(direction)}",
                $"Hint: {ColorName(color)} — {DirectionName(direction)}");
        }

        public static string NothingToUndo => Pick("Отменять пока нечего", "Nothing to undo yet");
        public static string MoveUndone => Pick("Ход отменён", "Move undone");

        public static string ExtraMoveAdded(int amount)
        {
            return IsRussian
                ? $"+{amount} {PluralRu(amount, "ход", "хода", "ходов")}"
                : $"+{amount} {PluralEn(amount, "move", "moves")}";
        }
    }
}
