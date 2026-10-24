using UnityEngine;

namespace Rubezh.UI
{
    // Что подсвечивать по ключу подсказки (VP-21, docs/hints.md §3): Pistol, Slide, Anchor_A1 …
    // Один ключ может стоять на нескольких объектах (места осмотра O-08 — два контура).
    public sealed class HighlightTarget : MonoBehaviour
    {
        public enum Mode
        {
            Outline,    // объёмный предмет: светящийся контур вокруг (оболочка, задние грани)
            Fill        // плоская метка (контур места, метка якоря): сама метка пульсирует цветом подсветки
        }

        [SerializeField] private string key;
        [SerializeField] private Mode mode = Mode.Outline;

        public string Key => key;
        public Mode Style => mode;

        public void Configure(string targetKey, Mode targetMode)
        {
            key = targetKey;
            mode = targetMode;
        }
    }
}
