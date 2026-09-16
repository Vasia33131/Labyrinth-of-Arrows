using UnityEngine;
using UnityEngine.UI;

namespace Unpuzzle
{
    /// <summary>
    /// Подписи в сцене собраны заранее (Bake Authored UI), поэтому в рантайме текст
    /// не создаётся, а переписывается на нужном языке. Вёрстку и размеры не трогаем.
    /// </summary>
    public static class UiLabel
    {
        /// <summary>Text кнопки: сначала дочерний «Label», иначе первый Text внутри.</summary>
        public static Text Of(Button button)
        {
            if (button == null) return null;

            Transform label = button.transform.Find("Label");
            Text text = label != null ? label.GetComponent<Text>() : null;
            return text != null ? text : button.GetComponentInChildren<Text>(true);
        }

        public static void Set(Button button, string value)
        {
            Set(Of(button), value);
        }

        public static void Set(Text text, string value)
        {
            if (text == null || value == null) return;
            if (text.text == value) return;
            text.text = value;
        }

        /// <summary>Text по имени объекта внутри поддерева. Нет объекта — ничего не делаем.</summary>
        public static void SetNamed(Transform root, string objectName, string value)
        {
            Set(FindNamed(root, objectName), value);
        }

        public static Text FindNamed(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName)) return null;

            Transform found = AuthoredUi.FindDeep(root, objectName);
            return found != null ? found.GetComponent<Text>() : null;
        }

        /// <summary>Text у кнопки, найденной по имени объекта.</summary>
        public static void SetNamedButton(Transform root, string objectName, string value)
        {
            if (root == null) return;

            Transform found = AuthoredUi.FindDeep(root, objectName);
            if (found == null) return;

            var button = found.GetComponent<Button>();
            if (button != null)
            {
                Set(button, value);
                return;
            }

            Set(found.GetComponent<Text>(), value);
        }
    }
}
