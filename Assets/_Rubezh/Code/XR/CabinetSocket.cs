using System;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Гнездо пистолета в оружейном шкафу O-05 (NG-14, FR-37). Фильтр выбора пропускает пистолет
    // только разряженным (ReturnAttemptRule.Accepts → IsClear). Исключения: шаг получения (на старте
    // в шкафу пистолет с пустым магазином) и постановка системой (возврат, Restart). Подход руки с
    // пистолетом к гнезду (начало и конец наведения) — одна попытка сдачи: правило VP-11 поднимает
    // ReturnedLoaded один раз, рука получает короткую вибрацию, Refused — для замечания (NG-32).
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class CabinetSocket : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private WeaponController weapon;
        [SerializeField, Range(0f, 1f)] private float refuseAmplitude = 0.6f;
        [SerializeField, Min(0f)] private float refuseSeconds = 0.12f;

        private XRSocketInteractor _socket;
        private ReturnAttemptRule _rule;

        public event Action<WeaponState> Refused;
        public ReturnAttemptRule Rule => _rule;
        public bool canProcess => isActiveAndEnabled;

        public void Configure(ExerciseRunner exerciseRunner, WeaponController weaponController)
        {
            runner = exerciseRunner;
            weapon = weaponController;
            BuildRule();
        }

        private void Awake()
        {
            _socket = GetComponent<XRSocketInteractor>();
            _socket.selectFilters.Add(this);
        }

        private void Start() => BuildRule();

        private void OnEnable()
        {
            _socket.hoverEntered.AddListener(OnHoverEntered);
            _socket.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDisable()
        {
            _socket.hoverEntered.RemoveListener(OnHoverEntered);
            _socket.hoverExited.RemoveListener(OnHoverExited);
        }

        private void BuildRule()
        {
            if (_rule != null || runner == null || runner.Director == null) return;
            _rule = new ReturnAttemptRule(runner.Director);
            _rule.Refused += OnRefused;
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (weapon == null || interactable.transform != weapon.transform) return true;
            if (ItemReturn.IsPlacing) return true;
            if (runner != null && runner.Director != null && runner.Phase == Phase.Receive) return true;
            return ReturnAttemptRule.Accepts(weapon.State);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!IsPistolInHand(args.interactableObject, out _)) return;
            BuildRule();
            _rule?.Approach(weapon.State);
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (weapon != null && args.interactableObject.transform == weapon.transform) _rule?.Leave();
        }

        private bool IsPistolInHand(IXRHoverInteractable interactable, out IXRSelectInteractor hand)
        {
            hand = null;
            if (weapon == null || interactable.transform != weapon.transform) return false;
            if (!(interactable is XRGrabInteractable grab)) return false;
            foreach (var i in grab.interactorsSelecting)
                if (!(i is XRSocketInteractor)) { hand = i; return true; }
            return false;
        }

        private void OnRefused(WeaponState state)
        {
            var grab = weapon != null ? weapon.GetComponent<XRGrabInteractable>() : null;
            if (grab != null)
                foreach (var i in grab.interactorsSelecting)
                {
                    if (i is XRSocketInteractor) continue;
                    var haptics = i.transform.GetComponentInParent<HapticImpulsePlayer>();
                    if (haptics != null) haptics.SendHapticImpulse(refuseAmplitude, refuseSeconds);
                }
            Refused?.Invoke(state);
        }
    }
}
