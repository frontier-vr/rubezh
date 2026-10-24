using System;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Интерактивный пистолет (NG-08). Хват — XRGrabInteractable с Select Mode: Multiple: первая рука
    // на Grip_Primary, вторая на Grip_Secondary. Кнопки читаются только с руки на Grip_Primary:
    // A/X — EjectMagazine, B/Y — предохранитель, нажатие стика — снять затвор с задержки (большой
    // палец на рычаге задержки). Спуск этой руки — TriggerPull/TriggerRelease по порогам RulesConfig.
    // Пока пистолет в руке, стик этой руки не двигает и не поворачивает: менеджер действий руки
    // выключается, иначе он включает локомоцию снова при смене региона Near-Far.
    [RequireComponent(typeof(XRGrabInteractable), typeof(WeaponController))]
    public sealed class PistolInteraction : MonoBehaviour
    {
        [Serializable]
        private sealed class HandBindings
        {
            public TriggerInput trigger;
            public XRInputButtonReader eject = new XRInputButtonReader("Eject Magazine", null, false, XRInputButtonReader.InputSourceMode.InputAction);
            public XRInputButtonReader safety = new XRInputButtonReader("Safety", null, false, XRInputButtonReader.InputSourceMode.InputAction);
            public XRInputButtonReader slideStop = new XRInputButtonReader("Slide Stop", null, false, XRInputButtonReader.InputSourceMode.InputAction);
            [Tooltip("ControllerInputActionManager этой руки: выключается, пока пистолет в руке")]
            public Behaviour locomotionManager;
            [Tooltip("Действия стика этой руки: движение, поворот, телепорт")]
            public InputActionReference[] stickActions = Array.Empty<InputActionReference>();
        }

        [SerializeField] private RulesConfig rules;
        [SerializeField] private HandBindings left = new HandBindings();
        [SerializeField] private HandBindings right = new HandBindings();

        private XRGrabInteractable _grab;
        private WeaponController _weapon;
        private TriggerSqueeze _squeeze;

        public WeaponController Weapon => _weapon;
        public XRGrabInteractable Grab => _grab;

        public void Configure(RulesConfig rulesConfig, TriggerInput leftTrigger, TriggerInput rightTrigger,
                              InputActionReference[] leftStick = null, InputActionReference[] rightStick = null)
        {
            rules = rulesConfig;
            left.trigger = leftTrigger;
            right.trigger = rightTrigger;
            if (leftStick != null) left.stickActions = leftStick;
            if (rightStick != null) right.stickActions = rightStick;
            _squeeze = NewSqueeze();
        }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _weapon = GetComponent<WeaponController>();
            _squeeze = NewSqueeze();
            Bind(left, "{LeftHand}");
            Bind(right, "{RightHand}");
        }

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnSelectChanged);
            _grab.selectExited.AddListener(OnSelectChanged);
            foreach (var h in new[] { left, right })
            {
                h.eject.EnableDirectActionIfModeUsed();
                h.safety.EnableDirectActionIfModeUsed();
                h.slideStop.EnableDirectActionIfModeUsed();
            }
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnSelectChanged);
            _grab.selectExited.RemoveListener(OnSelectChanged);
            foreach (var h in new[] { left, right })
            {
                h.eject.DisableDirectActionIfModeUsed();
                h.safety.DisableDirectActionIfModeUsed();
                h.slideStop.DisableDirectActionIfModeUsed();
            }
        }

        private void Update()
        {
            if (!_weapon.IsHeld) return;
            var h = Bindings(_weapon.PrimaryHand);
            if (h.eject.ReadWasPerformedThisFrame()) Eject();
            if (h.safety.ReadWasPerformedThisFrame()) ToggleSafety();
            if (h.slideStop.ReadWasPerformedThisFrame()) ReleaseSlideStop();
            if (h.trigger != null) Squeeze(h.trigger.PressValue);
        }

        // Команды руки на Grip_Primary; открыты для тестов и песочниц без шлема.
        public bool Eject() => _weapon.IsHeld && _weapon.Apply(WeaponCommand.EjectMagazine);

        public bool ToggleSafety() =>
            _weapon.IsHeld && _weapon.Apply(_weapon.State.SafetyOn ? WeaponCommand.SafetyOff : WeaponCommand.SafetyOn);

        public bool ReleaseSlideStop() => _weapon.IsHeld && _weapon.Apply(WeaponCommand.SlideStopRelease);

        public void Squeeze(float press)
        {
            if (!_weapon.IsHeld) return;
            var command = _squeeze.Update(press);
            if (command.HasValue) _weapon.Apply(command.Value);
        }

        private void OnSelectChanged(BaseInteractionEventArgs _)
        {
            var first = _grab.interactorsSelecting.Count > 0 ? _grab.interactorsSelecting[0] : null;
            bool held = first != null;
            var hand = held ? ToHand(first.handedness) : _weapon.PrimaryHand;
            if (held == _weapon.IsHeld && hand == _weapon.PrimaryHand) return;

            if (_weapon.IsHeld)
            {
                var release = _squeeze.Reset();
                if (release.HasValue) _weapon.Apply(release.Value);
                SetLocomotion(_weapon.PrimaryHand, true);
            }
            _weapon.SetHeld(held, hand);
            if (held) SetLocomotion(hand, false);
        }

        private void SetLocomotion(Hand hand, bool enabled)
        {
            var h = Bindings(hand);
            if (h.locomotionManager != null)
            {
                // Включённый менеджер сам восстановит действия по своим настройкам (OnEnable).
                h.locomotionManager.enabled = enabled;
                if (enabled) return;
            }
            foreach (var reference in h.stickActions)
            {
                var action = reference != null ? reference.action : null;
                if (action == null) continue;
                if (enabled) action.Enable(); else action.Disable();
            }
        }

        private HandBindings Bindings(Hand hand) => hand == Hand.Left ? left : right;

        private static Hand ToHand(InteractorHandedness handedness) =>
            handedness == InteractorHandedness.Left ? Hand.Left : Hand.Right;

        private TriggerSqueeze NewSqueeze() => rules != null
            ? new TriggerSqueeze(rules.triggerPullThreshold, rules.triggerReleaseThreshold)
            : new TriggerSqueeze(0.75f, 0.4f);

        private static void Bind(HandBindings h, string side)
        {
            AddBindingIfEmpty(h.eject.inputActionPerformed, $"<XRController>{side}/{{PrimaryButton}}");
            AddBindingIfEmpty(h.safety.inputActionPerformed, $"<XRController>{side}/{{SecondaryButton}}");
            AddBindingIfEmpty(h.slideStop.inputActionPerformed, $"<XRController>{side}/{{Primary2DAxisClick}}");
        }

        private static void AddBindingIfEmpty(InputAction action, string path)
        {
            if (action != null && action.bindings.Count == 0) action.AddBinding(path);
        }
    }
}
