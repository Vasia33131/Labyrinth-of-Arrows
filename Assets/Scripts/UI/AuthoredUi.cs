using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Жёсткое правило: в Play UI уже стоит в сцене. Не создаём, не чиним иерархией.
    /// Сборку — только в редакторе через Tools/Unpuzzle/Bake Authored UI.
    /// </summary>
    public static class AuthoredUi
    {
        public static bool CanBuild => !Application.isPlaying;

        public static void Missing(string name)
        {
            GameLog.Error("[Unpuzzle] В сцене нет UI «" + name + "». Tools/Unpuzzle/Bake Authored UI.");
        }

        public static T Require<T>(T value, string name) where T : Object
        {
            if (value == null) Missing(name);
            return value;
        }

        public static T ExistingComponent<T>(GameObject go, string name) where T : Component
        {
            if (go == null)
            {
                Missing(name);
                return null;
            }

            T component = go.GetComponent<T>();
            if (component != null) return component;
            if (!CanBuild)
            {
                Missing(name);
                return null;
            }

            return go.AddComponent<T>();
        }

        public static T FindNamed<T>(Transform root, string name) where T : Component
        {
            Transform named = FindDeep(root, name);
            if (named == null) return null;
            return named.GetComponent<T>();
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;

            Transform direct = root.Find(name);
            if (direct != null) return direct;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
