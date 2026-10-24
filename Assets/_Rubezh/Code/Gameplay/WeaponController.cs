using System;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Пистолет в сцене: автомат Weapon (VP-03), осечка JamRule (VP-09), выстрелы в ShotResolver (VP-08).
    // Команды приходят от интерактивного пистолета (NG-08) через Apply; IHeldWeapon — для детекторов
    // VP-05, VP-06. На Restart — новое оружие в состоянии шкафа и новая осечка, подписки на события
    // контроллера сохраняются.
    public sealed class WeaponController : MonoBehaviour, IWeapon, IHeldWeapon
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private ShotResolver shotResolver;

        private Weapon _weapon;
        private JamRule _jam;

        public event Action<WeaponState> Changed;
        public event Action Fired;
        public event Action DryFire;
        public event Action<Jam> Jammed;
        public event Action CasingEjected;
        // Пистолет взят или отпущен; рука — на Grip_Primary.
        public event Action<bool, Hand> HeldChanged;

        public bool IsHeld { get; private set; }
        public Hand PrimaryHand { get; private set; } = Hand.Right;
        public WeaponState State => _weapon.State;
        public int MisfireShot => _jam != null ? _jam.MisfireShot : 0;

        public void Configure(ExerciseRunner exerciseRunner, ShotResolver resolver)
        {
            Unhook();
            runner = exerciseRunner;
            shotResolver = resolver;
            Build();
            Hook();
        }

        private void Awake()
        {
            Build();
            Hook();
        }

        private void OnDestroy()
        {
            Unhook();
            _jam?.Dispose();
            if (shotResolver != null) shotResolver.Bind(null);
        }

        public bool Apply(WeaponCommand command) => _weapon.Apply(command);

        public bool InsertMagazine(int rounds) => _weapon.InsertMagazine(rounds);

        public void SetHeld(bool held, Hand hand)
        {
            if (held == IsHeld && hand == PrimaryHand) return;
            IsHeld = held;
            PrimaryHand = hand;
            HeldChanged?.Invoke(held, hand);
        }

        private void Hook()
        {
            if (runner != null) runner.Restarted += OnRestarted;
        }

        private void Unhook()
        {
            if (runner != null) runner.Restarted -= OnRestarted;
        }

        private void OnRestarted()
        {
            Build();
            Changed?.Invoke(State);
        }

        private void Build()
        {
            if (_weapon != null)
            {
                _weapon.Changed -= OnChanged;
                _weapon.Fired -= OnFired;
                _weapon.DryFire -= OnDryFire;
                _weapon.Jammed -= OnJammed;
                _weapon.CasingEjected -= OnCasingEjected;
            }
            _jam?.Dispose();
            _jam = null;

            _weapon = new Weapon();
            _weapon.Changed += OnChanged;
            _weapon.Fired += OnFired;
            _weapon.DryFire += OnDryFire;
            _weapon.Jammed += OnJammed;
            _weapon.CasingEjected += OnCasingEjected;

            if (runner != null && runner.Director != null)
            {
                _jam = new JamRule(runner.Director, new UnityClock(), _weapon);
                _jam.Arm(runner.Random);
            }
            if (shotResolver != null) shotResolver.Bind(this);
        }

        private void OnChanged(WeaponState state) => Changed?.Invoke(state);
        private void OnFired() => Fired?.Invoke();
        private void OnDryFire() => DryFire?.Invoke();
        private void OnJammed(Jam jam) => Jammed?.Invoke(jam);
        private void OnCasingEjected() => CasingEjected?.Invoke();
    }
}
