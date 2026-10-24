using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rubezh.Sandbox
{
    // Заглушка ITriggerInput для песочниц без шлема: T — палец на спуске, Space — нажатие.
    public sealed class TriggerInputStub : MonoBehaviour, ITriggerInput
    {
        [SerializeField] private bool touched;
        [SerializeField, Range(0f, 1f)] private float press;

        public float PressValue => press;
        public bool IsTouched => touched || press > 0f;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            touched = kb.tKey.isPressed;
            press = kb.spaceKey.isPressed ? 1f : 0f;
        }
    }
}
