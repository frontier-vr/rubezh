using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace Rubezh.XR
{
    // Ввод спускового крючка одной руки для детектора пальца (VP-06): степень нажатия 0..1 и касание.
    // Касание — ёмкостный датчик <OculusTouchController>/triggerTouched (OpenXR /input/trigger/touch).
    // Если датчика нет на устройстве, касание считается по порогу нажатия из RulesConfig (docs/qa/trigger-touch.md).
    public sealed class TriggerInput : MonoBehaviour, ITriggerInput
    {
        public enum Hand { Left, Right }

        [SerializeField] private Hand hand = Hand.Right;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private TouchSource touchSource = TouchSource.Auto;

        [SerializeField] private XRInputValueReader<float> pressInput =
            new XRInputValueReader<float>("Trigger Value", XRInputValueReader.InputSourceMode.InputAction);

        [SerializeField] private XRInputButtonReader touchInput =
            new XRInputButtonReader("Trigger Touch", null, false, XRInputButtonReader.InputSourceMode.InputAction);

        public Hand Side => hand;
        public float PressValue { get; private set; }
        public bool IsTouched { get; private set; }

        // Датчик есть, если действие касания привязалось к реальному контролу устройства.
        public bool SensorAvailable => touchInput.inputActionPerformed != null
                                       && touchInput.inputActionPerformed.controls.Count > 0;

        private void Awake()
        {
            string side = hand == Hand.Left ? "{LeftHand}" : "{RightHand}";
            AddBindingIfEmpty(pressInput.inputAction, $"<XRController>{side}/trigger");
            AddBindingIfEmpty(touchInput.inputActionPerformed, $"<OculusTouchController>{side}/triggerTouched");
        }

        private void OnEnable()
        {
            pressInput.EnableDirectActionIfModeUsed();
            touchInput.EnableDirectActionIfModeUsed();
        }

        private void OnDisable()
        {
            pressInput.DisableDirectActionIfModeUsed();
            touchInput.DisableDirectActionIfModeUsed();
            PressValue = 0;
            IsTouched = false;
        }

        private void Update()
        {
            PressValue = pressInput.TryReadValue(out float press) ? Mathf.Clamp01(press) : 0f;
            bool sensorTouched = touchInput.ReadIsPerformed();
            float threshold = rules != null ? rules.touchThreshold : 0.05f;
            IsTouched = TriggerTouch.IsTouched(touchSource, SensorAvailable, sensorTouched, PressValue, threshold);
        }

        private static void AddBindingIfEmpty(InputAction action, string path)
        {
            if (action != null && action.bindings.Count == 0) action.AddBinding(path);
        }
    }
}
