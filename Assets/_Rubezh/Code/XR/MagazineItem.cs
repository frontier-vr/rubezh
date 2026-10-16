using System.Collections.Generic;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Магазин (NG-09): патроны по одному в сокеты Round_01…Round_05 (открыт только следующий
    // свободный), подаватель Follower под последним патроном. Вставленный патрон-предмет уходит в
    // пул лотка, в окне магазина видна его копия. В шахте пистолета патроны считает автомат оружия,
    // а рукой магазин не вынимается — только кнопкой A/X (фильтр выбора: из шахты берёт только сокет).
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class MagazineItem : MonoBehaviour, IMagazine, IXRSelectFilter
    {
        private const float RoundPitch = 0.010f;   // шаг пустышек Round_0k

        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private AmmoTray tray;
        [SerializeField] private XRSocketInteractor[] roundSockets = new XRSocketInteractor[0];
        [SerializeField] private GameObject[] roundVisuals = new GameObject[0];
        [SerializeField] private Transform follower;

        private XRGrabInteractable _grab;
        private WeaponController _inWeapon;
        private int _rounds;
        private float _followerTop;
        private readonly List<(XRSocketInteractor socket, IXRSelectInteractable round)> _seated =
            new List<(XRSocketInteractor, IXRSelectInteractable)>();

        public int Capacity => roundSockets.Length;
        public int Rounds => _inWeapon != null ? _inWeapon.State.RoundsInMagazine : _rounds;
        public bool InWell => _inWeapon != null;
        public XRGrabInteractable Grab => _grab;

        public bool canProcess => isActiveAndEnabled;

        public void Configure(ExerciseRunner exerciseRunner, AmmoTray ammoTray)
        {
            if (runner != null) runner.Restarted -= OnRestarted;
            runner = exerciseRunner;
            tray = ammoTray;
            if (runner != null) runner.Restarted += OnRestarted;
        }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _grab.selectFilters.Add(this);
            if (follower != null) _followerTop = follower.localPosition.y;
            foreach (var s in roundSockets) s.selectEntered.AddListener(OnRoundSeated);
            Refresh();
        }

        private void OnEnable()
        {
            if (runner != null) runner.Restarted += OnRestarted;
        }

        private void OnDisable()
        {
            if (runner != null) runner.Restarted -= OnRestarted;
        }

        private void OnDestroy()
        {
            foreach (var s in roundSockets) if (s != null) s.selectEntered.RemoveListener(OnRoundSeated);
        }

        // Из шахты пистолета магазин берёт только сокет (возврат на место, Restart), не рука.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            _inWeapon == null || interactor is XRSocketInteractor;

        public void EnterWell(WeaponController weapon)
        {
            _inWeapon = weapon;
            Refresh();
        }

        public void LeaveWell(int rounds)
        {
            _inWeapon = null;
            _rounds = Mathf.Clamp(rounds, 0, Capacity);
            Refresh();
        }

        public void SetRounds(int rounds)
        {
            _rounds = Mathf.Clamp(rounds, 0, Capacity);
            Refresh();
        }

        private void OnRestarted() => SetRounds(0);

        private void OnRoundSeated(SelectEnterEventArgs args) =>
            _seated.Add(((XRSocketInteractor)args.interactorObject, args.interactableObject));

        private void Update()
        {
            if (_seated.Count == 0) return;
            // Патрон-предмет отдаём пулу кадром позже: из обработчика выбора выходить нельзя.
            foreach (var (socket, round) in _seated)
            {
                if (socket.interactionManager != null && socket.IsSelecting(round))
                    socket.interactionManager.SelectExit((IXRSelectInteractor)socket, round);
                if (tray != null) tray.Seat(round.transform.gameObject);
                else round.transform.gameObject.SetActive(false);
                if (_rounds < Capacity) _rounds++;
                if (runner != null) runner.ReportAction(ExerciseAction.InsertRound);
            }
            _seated.Clear();
            Refresh();
        }

        private void Refresh()
        {
            int rounds = Rounds;
            for (int i = 0; i < roundSockets.Length; i++)
                roundSockets[i].socketActive = _inWeapon == null && i == rounds;
            for (int i = 0; i < roundVisuals.Length; i++)
                if (roundVisuals[i] != null) roundVisuals[i].SetActive(i < rounds);
            if (follower != null)
            {
                var p = follower.localPosition;
                p.y = _followerTop - RoundPitch * rounds;
                follower.localPosition = p;
            }
        }

        private void LateUpdate()
        {
            // В шахте патроны уходят выстрелами: окно магазина и подаватель — по автомату.
            if (_inWeapon != null) Refresh();
        }
    }
}
