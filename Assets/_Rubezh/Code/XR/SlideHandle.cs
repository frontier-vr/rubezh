using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Затвор под второй рукой (NG-08): Grip на Slide, потянуть назад вдоль оси ствола (−Z пистолета),
    // отпустить. Ход руки переводится в SlideBack/SlideForward (SlidePull), задержка — по автомату
    // оружия (VP-03). Без хвата за Grip_Primary затвор не берётся: рука берёт сам пистолет.
    // Визуал: затвор ходит за рукой, на задержке стоит сзади.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class SlideHandle : MonoBehaviour
    {
        [SerializeField] private WeaponController weapon;
        [SerializeField] private RulesConfig rules;

        private XRSimpleInteractable _interactable;
        private SlidePull _pull;
        private IXRSelectInteractor _hand;
        private Vector3 _rest;
        private float _grabZ;

        public float Offset => _hand != null ? _pull.Offset : (weapon.State.SlideLocked ? LockedOffset : 0f);
        public bool IsGrabbed => _hand != null;

        private float Travel => rules != null ? rules.slideTravel : 0.03f;
        private float LockedOffset => Travel * (rules != null ? rules.slideLockedFraction : 0.8f);
        private Transform Pistol => weapon.transform;

        public void Configure(WeaponController weaponController, RulesConfig rulesConfig)
        {
            if (weapon != null) weapon.HeldChanged -= OnHeldChanged;
            weapon = weaponController;
            rules = rulesConfig;
            _pull = NewPull();
            weapon.HeldChanged += OnHeldChanged;
            OnHeldChanged(weapon.IsHeld, weapon.PrimaryHand);
        }

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _rest = transform.localPosition;
            _pull = NewPull();
        }

        private void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnGrab);
            _interactable.selectExited.AddListener(OnRelease);
            if (weapon == null) return;
            weapon.HeldChanged -= OnHeldChanged;
            weapon.HeldChanged += OnHeldChanged;
            OnHeldChanged(weapon.IsHeld, weapon.PrimaryHand);
        }

        private void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnGrab);
            _interactable.selectExited.RemoveListener(OnRelease);
            if (weapon != null) weapon.HeldChanged -= OnHeldChanged;
        }

        private void Update()
        {
            if (weapon == null) return;
            if (_hand != null)
            {
                float z = Pistol.InverseTransformPoint(_hand.GetAttachTransform(_interactable).position).z;
                var command = _pull.Drag(_grabZ - z);
                if (command.HasValue) weapon.Apply(command.Value);
            }
            transform.localPosition = _rest - Vector3.forward * Offset;
        }

        private void OnHeldChanged(bool held, Hand hand)
        {
            // Пистолет отпустили — затвор отпускается вместе с ним.
            if (!held && _hand != null && _interactable.interactionManager != null)
                _interactable.interactionManager.SelectExit(_hand, _interactable);
            _interactable.enabled = held;
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _hand = args.interactorObject;
            _grabZ = Pistol.InverseTransformPoint(_hand.GetAttachTransform(_interactable).position).z;
            _pull.Begin(weapon.State.SlideLocked ? LockedOffset : 0f);
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (args.interactorObject != _hand) return;
            _hand = null;
            var command = _pull.Release();
            if (command.HasValue) weapon.Apply(command.Value);
        }

        private SlidePull NewPull() => rules != null
            ? new SlidePull(rules.slideTravel, rules.slideBackFraction, rules.slideForwardFraction)
            : new SlidePull(0.03f, 0.9f, 0.2f);
    }
}
