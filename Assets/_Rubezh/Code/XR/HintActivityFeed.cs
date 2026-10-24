using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Действия пользователя для подсказок (VP-12): любой захват, отпускание и событие сокета
    // сбрасывают таймер бездействия. Заодно — держит ли рука патрон (условие «патрон в магазин»).
    public sealed class HintActivityFeed : MonoBehaviour
    {
        [SerializeField] private XRInteractionManager manager;
        [SerializeField] private HintDirector hints;
        [Tooltip("Interaction Layer патрона (NG-09)")]
        [SerializeField] private int roundLayer = 3;

        private int _roundsInHand;

        public void Configure(XRInteractionManager interactionManager, HintDirector hintDirector)
        {
            OnDisable();
            manager = interactionManager;
            hints = hintDirector;
            OnEnable();
        }

        private readonly System.Collections.Generic.List<IXRInteractable> _registered = new System.Collections.Generic.List<IXRInteractable>();
        private readonly System.Collections.Generic.HashSet<IXRSelectInteractable> _watched = new System.Collections.Generic.HashSet<IXRSelectInteractable>();

        // У менеджера XRI нет общих событий выбора: подписываемся на каждый зарегистрированный предмет.
        private void OnEnable()
        {
            if (manager == null) return;
            manager.interactableRegistered += OnRegistered;
            manager.interactableUnregistered += OnUnregistered;
            _registered.Clear();
            manager.GetRegisteredInteractables(_registered);
            foreach (var i in _registered) Watch(i);
        }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.interactableRegistered -= OnRegistered;
                manager.interactableUnregistered -= OnUnregistered;
            }
            foreach (var i in _watched)
            {
                if (i == null || (i as Object) == null) continue;
                i.selectEntered.RemoveListener(OnSelectEntered);
                i.selectExited.RemoveListener(OnSelectExited);
            }
            _watched.Clear();
        }

        private void OnRegistered(InteractableRegisteredEventArgs args) => Watch(args.interactableObject);

        private void OnUnregistered(InteractableUnregisteredEventArgs args)
        {
            if (!(args.interactableObject is IXRSelectInteractable s) || !_watched.Remove(s)) return;
            s.selectEntered.RemoveListener(OnSelectEntered);
            s.selectExited.RemoveListener(OnSelectExited);
        }

        private void Watch(IXRInteractable interactable)
        {
            if (!(interactable is IXRSelectInteractable s) || !_watched.Add(s)) return;
            s.selectEntered.AddListener(OnSelectEntered);
            s.selectExited.AddListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (IsRoundInHand(args.interactorObject, args.interactableObject)) _roundsInHand++;
            Push();
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (IsRoundInHand(args.interactorObject, args.interactableObject)) _roundsInHand = Mathf.Max(0, _roundsInHand - 1);
            Push();
        }

        private void Push()
        {
            if (hints == null) return;
            hints.RoundInHand = _roundsInHand > 0;
            hints.Activity();
        }

        private bool IsRoundInHand(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            !(interactor is XRSocketInteractor) && interactable is XRGrabInteractable grab &&
            (grab.interactionLayers.value & (1 << roundLayer)) != 0;
    }
}
