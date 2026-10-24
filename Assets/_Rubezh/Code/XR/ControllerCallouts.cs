using System.Collections.Generic;
using Rubezh.Core;
using Rubezh.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Выноска у контроллера (NG-32): текст на панели у руки — замечание о мелком нарушении, фраза
    // подсказки FR-26 (VP-12), онбординг (docs/user-flow.md §3). Очередь и приоритет — Callouts.
    // Тексты — из Data/Hints (названия нарушений и онбординг — hint_rules.ru.json, фраза подсказки
    // приходит готовой от HintDirector), кнопки по руке: A/B справа, X/Y слева. Панель — сбоку от
    // кисти, снаружи и чуть ниже, чтобы не закрывать пистолет и мушку; всегда лицом к глазам.
    public sealed class ControllerCallouts : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private HintDirector hints;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private AnchorLocator anchors;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private TextAsset hintRules;
        [SerializeField] private XRInteractionManager manager;
        [Header("Руки и глаза")]
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private Transform head;
        [Header("Панель")]
        [SerializeField] private Transform view;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Transform background;
        [SerializeField] private Vector3 offset = new Vector3(0.11f, -0.04f, 0f);   // вбок наружу, вниз, к глазам
        [SerializeField] private Vector2 padding = new Vector2(0.012f, 0.008f);

        private Callouts _callouts;
        private HintRules _texts;
        private Anchor _lastAnchor;
        private readonly HashSet<IXRInteractable> _watched = new HashSet<IXRInteractable>();
        private readonly List<IXRInteractable> _registered = new List<IXRInteractable>();

        public Callouts Model => _callouts;
        public bool Visible => view != null && view.gameObject.activeSelf;
        public string Shown => label != null ? label.text : null;

        private double Now => Time.timeAsDouble;

        public void Configure(ExerciseRunner exerciseRunner, HintDirector hintDirector, WeaponController weaponController,
                              AnchorLocator locator, RulesConfig rulesConfig, TextAsset rulesAsset, XRInteractionManager interactionManager,
                              Transform left, Transform right, Transform eyes)
        {
            OnDisable();
            runner = exerciseRunner; hints = hintDirector; weapon = weaponController; anchors = locator; rules = rulesConfig;
            hintRules = rulesAsset; manager = interactionManager; leftController = left; rightController = right; head = eyes;
            Build();
            OnEnable();
        }

        private void Awake() => Build();

        private void Build()
        {
            _texts = HintRules.From(hintRules);
            _callouts = new Callouts(rules != null ? rules.calloutRemarkSeconds : 3f, rules != null ? rules.calloutOnboardingSeconds : 8f);
            if (view != null) view.gameObject.SetActive(false);
        }

        private void Start()
        {
            _lastAnchor = anchors != null ? anchors.Current : Anchor.None;
            if (_lastAnchor == Anchor.A1) Onboarding("onb_teleport", Hand.Right);
        }

        private void OnEnable()
        {
            if (runner != null)
            {
                runner.ViolationRaised += OnViolation;
                runner.Restarted += OnRestarted;
            }
            if (hints != null)
            {
                hints.HintShown += OnHintShown;
                hints.HintCleared += OnHintCleared;
            }
            if (weapon != null)
            {
                weapon.HeldChanged += OnHeldChanged;
                weapon.Applied += OnWeaponCommand;
            }
            if (manager != null)
            {
                manager.interactableRegistered += OnRegistered;
                manager.interactableUnregistered += OnUnregistered;
                _registered.Clear();
                manager.GetRegisteredInteractables(_registered);
                foreach (var i in _registered) Watch(i);
            }
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.ViolationRaised -= OnViolation;
                runner.Restarted -= OnRestarted;
            }
            if (hints != null)
            {
                hints.HintShown -= OnHintShown;
                hints.HintCleared -= OnHintCleared;
            }
            if (weapon != null)
            {
                weapon.HeldChanged -= OnHeldChanged;
                weapon.Applied -= OnWeaponCommand;
            }
            if (manager != null)
            {
                manager.interactableRegistered -= OnRegistered;
                manager.interactableUnregistered -= OnUnregistered;
            }
            foreach (var i in _watched) Unwatch(i);
            _watched.Clear();
        }

        // ---------- источники сообщений

        private Hand WeaponHand => weapon != null && weapon.IsHeld ? weapon.PrimaryHand : Hand.Right;

        private void OnViolation(ViolationRecord v)
        {
            if (v.Severity != Severity.Minor) return;     // грубое — стоп-экран VP-22
            _callouts.Remark(_texts != null ? _texts.ViolationName(v.Type) : v.Type.ToString(), WeaponHand, Now);
            Refresh();
        }

        private void OnHintShown(string target, string phrase)
        {
            _callouts.Hint(phrase, WeaponHand, Now);
            Refresh();
        }

        private void OnHintCleared()
        {
            _callouts.ClearHint(Now);
            Refresh();
        }

        private void OnRestarted()
        {
            _callouts.Reset(Now);
            Refresh();
        }

        private void OnHeldChanged(bool held, Hand hand)
        {
            if (held) Onboarding("onb_pistol_buttons", hand);
        }

        private void OnWeaponCommand(WeaponCommand command)
        {
            switch (command)
            {
                case WeaponCommand.EjectMagazine:
                    _callouts.DoneOnboarding("onb_pistol_buttons", Now);
                    // Магазин отделён на осмотре — дальше нужна работа затвором, свободной рукой.
                    if (runner != null && runner.Director != null && runner.Phase == Phase.Inspect)
                        Onboarding("onb_slide", WeaponHand == Hand.Right ? Hand.Left : Hand.Right);
                    break;
                case WeaponCommand.SafetyOn:
                case WeaponCommand.SafetyOff:
                    _callouts.DoneOnboarding("onb_pistol_buttons", Now);
                    break;
                case WeaponCommand.SlideBack:
                    _callouts.DoneOnboarding("onb_slide", Now);
                    break;
            }
            Refresh();
        }

        private void Onboarding(string id, Hand hand)
        {
            if (_texts == null) return;
            foreach (var line in _texts.OnboardingLines)
                if (line.id == id)
                {
                    _callouts.Onboarding(id, InstructorScript.WithButtons(line.text, hand), hand, Now);
                    Refresh();
                    return;
                }
        }

        // Наведение и захват любой рукой — для выноски «Grip: взять предмет».
        private void OnRegistered(InteractableRegisteredEventArgs args) => Watch(args.interactableObject);
        private void OnUnregistered(InteractableUnregisteredEventArgs args)
        {
            if (_watched.Remove(args.interactableObject)) Unwatch(args.interactableObject);
        }

        private void Watch(IXRInteractable interactable)
        {
            if (!(interactable is XRGrabInteractable grab) || !_watched.Add(interactable)) return;
            grab.hoverEntered.AddListener(OnHover);
            grab.selectEntered.AddListener(OnSelect);
        }

        private void Unwatch(IXRInteractable interactable)
        {
            if (!(interactable is XRGrabInteractable grab) || grab == null) return;
            grab.hoverEntered.RemoveListener(OnHover);
            grab.selectEntered.RemoveListener(OnSelect);
        }

        private void OnHover(HoverEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            Onboarding("onb_grab", HandOf(args.interactorObject.transform));
        }

        private void OnSelect(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            _callouts.DoneOnboarding("onb_grab", Now);
            Refresh();
        }

        private Hand HandOf(Transform t) => leftController != null && t != null && t.IsChildOf(leftController) ? Hand.Left : Hand.Right;

        // ---------- отрисовка

        private void Update()
        {
            var anchor = anchors != null ? anchors.Current : Anchor.None;
            if (anchor != _lastAnchor)
            {
                _lastAnchor = anchor;
                _callouts.DoneOnboarding("onb_teleport", Now);       // телепорт сделан
            }
            _callouts.Tick(Now);
            Refresh();
        }

        private void LateUpdate() => Place();

        private void Refresh()
        {
            if (view == null) return;
            bool show = _callouts.Current != Callouts.Kind.None;
            if (view.gameObject.activeSelf != show) view.gameObject.SetActive(show);
            if (!show || label == null || label.text == _callouts.Text) return;
            label.text = _callouts.Text;
            label.ForceMeshUpdate();
            if (background != null)
            {
                var size = label.GetRenderedValues(false);
                background.localScale = new Vector3(size.x + 2 * padding.x, size.y + 2 * padding.y, 1f);
            }
        }

        private void Place()
        {
            if (view == null || !view.gameObject.activeSelf) return;
            var hand = _callouts.Hand == Hand.Left ? leftController : rightController;
            if (hand == null) return;
            var eye = head != null ? head : Camera.main != null ? Camera.main.transform : null;
            Vector3 right = eye != null ? Vector3.ProjectOnPlane(eye.right, Vector3.up).normalized : Vector3.right;
            float side = _callouts.Hand == Hand.Left ? -1f : 1f;
            var pos = hand.position + right * (offset.x * side) + Vector3.up * offset.y;
            if (eye != null) pos += (eye.position - pos).normalized * offset.z;
            view.position = pos;
            if (eye != null) view.rotation = Quaternion.LookRotation(pos - eye.position, Vector3.up);
        }
    }
}
