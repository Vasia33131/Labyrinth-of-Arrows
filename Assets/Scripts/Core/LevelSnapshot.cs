using System.Collections.Generic;
using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Снимок поля перед ходом: клетки, направление, книги, указки, портфели, счётчик ходов.
    /// Убранная стрелка не уничтожается, а прячется — бонус «Отмена» возвращает её.
    /// </summary>
    public class LevelSnapshot
    {
        private struct ArrowState
        {
            public ArrowController arrow;
            public Vector3 position;
            public ArrowDirection direction;
            public ArrowColor color;
            public bool manualLock;
            public bool active;
            public bool removing;
            public Vector2Int[] path;
        }

        private struct BookState
        {
            public BookController book;
            public bool active;
        }

        private struct PointerState
        {
            public PointerController pointer;
            public ArrowDirection direction;
            public Vector2Int[] cells;
        }

        private struct BackpackState
        {
            public BackpackController backpack;
            public Vector2Int anchor;
            public ArrowDirection axisDir;
            public ArrowDirection slideDir;
        }

        public int MovesUsed { get; private set; }

        private ArrowState[] arrows;
        private BookState[] books;
        private PointerState[] pointers;
        private BackpackState[] backpacks;

        public static LevelSnapshot Capture(IReadOnlyList<ArrowController> allArrows, int movesUsed)
        {
            return Capture(allArrows, null, null, movesUsed);
        }

        public static LevelSnapshot Capture(
            IReadOnlyList<ArrowController> allArrows,
            IReadOnlyList<BookController> allBooks,
            int movesUsed)
        {
            return Capture(allArrows, allBooks, null, movesUsed);
        }

        public static LevelSnapshot Capture(
            IReadOnlyList<ArrowController> allArrows,
            IReadOnlyList<BookController> allBooks,
            IReadOnlyList<PointerController> allPointers,
            int movesUsed)
        {
            return Capture(allArrows, allBooks, allPointers, null, movesUsed);
        }

        public static LevelSnapshot Capture(
            IReadOnlyList<ArrowController> allArrows,
            IReadOnlyList<BookController> allBooks,
            IReadOnlyList<PointerController> allPointers,
            IReadOnlyList<BackpackController> allBackpacks,
            int movesUsed)
        {
            var snapshot = new LevelSnapshot { MovesUsed = movesUsed };

            int arrowCount = allArrows != null ? allArrows.Count : 0;
            snapshot.arrows = new ArrowState[arrowCount];
            for (int i = 0; i < arrowCount; i++)
            {
                ArrowController arrow = allArrows[i];
                if (arrow == null) continue;

                snapshot.arrows[i] = new ArrowState
                {
                    arrow = arrow,
                    position = arrow.transform.position,
                    direction = arrow.direction,
                    color = arrow.color,
                    manualLock = arrow.manualLock,
                    active = arrow.gameObject.activeSelf,
                    removing = arrow.IsRemoving,
                    path = arrow.CopyCells()
                };
            }

            int bookCount = allBooks != null ? allBooks.Count : 0;
            snapshot.books = new BookState[bookCount];
            for (int i = 0; i < bookCount; i++)
            {
                BookController book = allBooks[i];
                if (book == null) continue;
                snapshot.books[i] = new BookState
                {
                    book = book,
                    active = book.gameObject.activeSelf
                };
            }

            int pointerCount = allPointers != null ? allPointers.Count : 0;
            snapshot.pointers = new PointerState[pointerCount];
            for (int i = 0; i < pointerCount; i++)
            {
                PointerController pointer = allPointers[i];
                if (pointer == null) continue;
                snapshot.pointers[i] = new PointerState
                {
                    pointer = pointer,
                    direction = pointer.Direction,
                    cells = pointer.CopyCells()
                };
            }

            int backpackCount = allBackpacks != null ? allBackpacks.Count : 0;
            snapshot.backpacks = new BackpackState[backpackCount];
            for (int i = 0; i < backpackCount; i++)
            {
                BackpackController backpack = allBackpacks[i];
                if (backpack == null) continue;
                snapshot.backpacks[i] = new BackpackState
                {
                    backpack = backpack,
                    anchor = backpack.Anchor,
                    axisDir = backpack.AxisDir,
                    slideDir = backpack.CurrentSlideDir
                };
            }

            return snapshot;
        }

        public void Apply()
        {
            if (books != null)
            {
                for (int i = 0; i < books.Length; i++)
                {
                    BookState state = books[i];
                    if (state.book == null) continue;
                    if (state.active) state.book.RestoreAlive();
                    else state.book.gameObject.SetActive(false);
                }
            }

            if (pointers != null)
            {
                for (int i = 0; i < pointers.Length; i++)
                {
                    PointerState state = pointers[i];
                    if (state.pointer == null) continue;
                    state.pointer.RestorePose(state.direction, state.cells);
                }
            }

            if (backpacks != null)
            {
                for (int i = 0; i < backpacks.Length; i++)
                {
                    BackpackState state = backpacks[i];
                    if (state.backpack == null) continue;
                    state.backpack.RestorePose(state.anchor, state.axisDir, state.slideDir);
                }
            }

            if (arrows == null) return;

            for (int i = 0; i < arrows.Length; i++)
            {
                ArrowState state = arrows[i];
                if (state.arrow == null) continue;

                state.arrow.gameObject.SetActive(true);
                state.arrow.RestoreState(state.position, state.direction, state.color, state.manualLock, state.removing, state.path);
                if (!state.active) state.arrow.gameObject.SetActive(false);
            }
        }
    }
}
