using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Шахта магазина (NG-09): сокет на пустышке MagWell принимает только Interaction Layer Magazine.
    // Магазин встал — InsertMagazine(rounds) и действие InsertMagazine директору; A/X (EjectMagazine)
    // — сокет отпускает магазин, тот выходит вниз и полсекунды не принимается обратно.
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class MagWell : MonoBehaviour
    {
        [SerializeField] private WeaponController weapon;
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;

        private XRSocketInteractor _socket;
        private MagazineItem _magazine;
        private Collider[] _pistolColliders;
        private Collider[] _restoreLater;
        private int _lastRounds;
        private bool _wasInserted;
        private double _lockUntil;
        private double _restoreAt;

        public MagazineItem Magazine => _magazine;
        public XRSocketInteractor Socket => _socket;

        public void Configure(WeaponController weaponController, ExerciseRunner exerciseRunner, RulesConfig rulesConfig)
        {
            if (weapon != null) weapon.Changed -= OnWeaponChanged;
            weapon = weaponController;
            runner = exerciseRunner;
            rules = rulesConfig;
            weapon.Changed += OnWeaponChanged;
            Sync(weapon.State);
        }

        private void Awake()
        {
            _socket = GetComponent<XRSocketInteractor>();
            _pistolColliders = weapon != null ? weapon.GetComponentsInChildren<Collider>(true) : new Collider[0];
        }

        private void OnEnable()
        {
            _socket.selectEntered.AddListener(OnSocketed);
            _socket.selectExited.AddListener(OnUnsocketed);
            if (weapon == null) return;
            weapon.Changed -= OnWeaponChanged;
            weapon.Changed += OnWeaponChanged;
        }

        private void Start()
        {
            if (weapon != null) Sync(weapon.State);
        }

        private void OnDisable()
        {
            _socket.selectEntered.RemoveListener(OnSocketed);
            _socket.selectExited.RemoveListener(OnUnsocketed);
            if (weapon != null) weapon.Changed -= OnWeaponChanged;
        }

        private void Update()
        {
            double now = Time.timeAsDouble;
            if (!_socket.socketActive && now >= _lockUntil) _socket.socketActive = true;
            if (_restoreLater != null && now >= _restoreAt)
            {
                IgnoreCollisions(_restoreLater, false);
                _restoreLater = null;
            }
        }

        private void Sync(WeaponState state)
        {
            _wasInserted = state.MagazineInserted;
            if (state.MagazineInserted) _lastRounds = state.RoundsInMagazine;
        }

        private void OnSocketed(SelectEnterEventArgs args)
        {
            _magazine = args.interactableObject.transform.GetComponent<MagazineItem>();
            if (_magazine == null) return;
            if (_pistolColliders == null || _pistolColliders.Length == 0)
                _pistolColliders = weapon.GetComponentsInChildren<Collider>(true);
            if (_restoreLater != null) _restoreLater = null;
            IgnoreCollisions(_magazine.GetComponentsInChildren<Collider>(true), true);

            if (!weapon.State.MagazineInserted)
            {
                weapon.InsertMagazine(_magazine.Rounds);
                if (runner != null && !ItemReturn.IsPlacing) runner.ReportAction(ExerciseAction.InsertMagazine);
            }
            _magazine.EnterWell(weapon);
        }

        private void OnUnsocketed(SelectExitEventArgs args)
        {
            var magazine = args.interactableObject.transform.GetComponent<MagazineItem>();
            if (magazine == null) return;
            // Если магазин увёл не A/X (Restart, возврат на место) — автомат тоже без магазина.
            if (weapon.State.MagazineInserted)
            {
                _lastRounds = weapon.State.RoundsInMagazine;
                weapon.Apply(WeaponCommand.EjectMagazine);
            }
            magazine.LeaveWell(_lastRounds);
            _restoreLater = magazine.GetComponentsInChildren<Collider>(true);
            _restoreAt = Time.timeAsDouble + EjectLock;
            if (_magazine == magazine) _magazine = null;
        }

        private void OnWeaponChanged(WeaponState state)
        {
            bool ejected = _wasInserted && !state.MagazineInserted;
            Sync(state);
            if (!ejected || _magazine == null || !_socket.IsSelecting(_magazine.Grab)) return;

            var magazine = _magazine;
            _socket.socketActive = false;
            _lockUntil = Time.timeAsDouble + EjectLock;
            _socket.interactionManager.SelectExit((IXRSelectInteractor)_socket, magazine.Grab);
            var body = magazine.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) body.linearVelocity = -transform.up * EjectSpeed;
        }

        private float EjectLock => rules != null ? rules.ejectLockSeconds : 0.5f;
        private float EjectSpeed => rules != null ? rules.ejectSpeed : 0.6f;

        private void IgnoreCollisions(Collider[] magazineColliders, bool ignore)
        {
            foreach (var a in magazineColliders)
                foreach (var b in _pistolColliders)
                    if (a != null && b != null && !a.isTrigger && !b.isTrigger) Physics.IgnoreCollision(a, b, ignore);
        }
    }
}
