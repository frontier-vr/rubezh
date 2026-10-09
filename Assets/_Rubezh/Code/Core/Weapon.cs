using System;

namespace Rubezh.Core
{
    // Автомат состояний пистолета по таблице переходов из docs/objects.md.
    // Задержки (строки 12-13 таблицы) назначаются в VP-09 и VP-13; здесь автомат
    // только корректно ведёт себя в состояниях с Jam != None.
    public sealed class Weapon : IWeapon
    {
        // Удержание затвора рукой сзади в WeaponState не входит: наружу оно не нужно.
        private enum Slide { Forward, HeldBack, Locked }

        private bool _magazineInserted;
        private int _rounds;
        private Chamber _chamber;
        private Slide _slide;
        private bool _safetyOn;
        private Jam _jam;
        private bool _triggerPulled;

        public event Action<WeaponState> Changed;
        public event Action Fired;
        public event Action DryFire;
#pragma warning disable CS0067 // поднимается с назначением задержек в VP-09, VP-13
        public event Action<Jam> Jammed;
#pragma warning restore CS0067
        public event Action CasingEjected;

        // Состояние в шкафу на шаге 1: пустой магазин вставлен, затвор впереди, предохранитель включён.
        public Weapon() : this(new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None)) { }

        public Weapon(WeaponState state)
        {
            _magazineInserted = state.MagazineInserted;
            _rounds = state.RoundsInMagazine;
            _chamber = state.Chamber;
            _slide = state.SlideLocked ? Slide.Locked : Slide.Forward;
            _safetyOn = state.SafetyOn;
            _jam = state.Jam;
        }

        public WeaponState State =>
            new WeaponState(_magazineInserted, _rounds, _chamber, _slide == Slide.Locked, _safetyOn, _jam);

        public bool InsertMagazine(int rounds)
        {
            if (rounds < 0) throw new ArgumentOutOfRangeException(nameof(rounds));
            if (_magazineInserted) return false;

            _magazineInserted = true;
            _rounds = rounds;
            Changed?.Invoke(State);
            return true;
        }

        public bool Apply(WeaponCommand command)
        {
            switch (command)
            {
                case WeaponCommand.EjectMagazine: return EjectMagazine();
                case WeaponCommand.SlideBack: return SlideBack();
                case WeaponCommand.SlideForward: return _slide == Slide.HeldBack && ReleaseSlide();
                case WeaponCommand.SlideStopEngage: return SlideStopEngage();
                case WeaponCommand.SlideStopRelease: return _slide == Slide.Locked && ReleaseSlide();
                case WeaponCommand.SafetyOn: return SetSafety(true);
                case WeaponCommand.SafetyOff: return SetSafety(false);
                case WeaponCommand.TriggerPull: return TriggerPull();
                case WeaponCommand.TriggerRelease: return TriggerRelease();
                default: throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private bool EjectMagazine()
        {
            if (!_magazineInserted) return false;

            _magazineInserted = false;
            _rounds = 0;
            Changed?.Invoke(State);
            return true;
        }

        private bool SlideBack()
        {
            if (_slide == Slide.HeldBack) return false;

            bool casing = _chamber == Chamber.Spent;
            // Подаватель пустого магазина поднимает задержку сам; без магазина по VP-03 — тоже.
            Slide target = _magazineInserted && _rounds > 0 ? Slide.HeldBack : Slide.Locked;
            bool changed = _slide != target || _chamber != Chamber.Empty || _jam != Jam.None;

            _chamber = Chamber.Empty;
            _jam = Jam.None;
            _slide = target;

            if (casing) CasingEjected?.Invoke();
            if (changed) Changed?.Invoke(State);
            return true;
        }

        private bool ReleaseSlide()
        {
            _slide = Slide.Forward;
            if (_magazineInserted && _rounds > 0 && _chamber == Chamber.Empty)
            {
                _rounds--;
                _chamber = Chamber.Loaded;
            }
            Changed?.Invoke(State);
            return true;
        }

        private bool SlideStopEngage()
        {
            if (_slide != Slide.HeldBack) return false;

            _slide = Slide.Locked;
            Changed?.Invoke(State);
            return true;
        }

        private bool SetSafety(bool on)
        {
            if (_safetyOn == on) return false;

            _safetyOn = on;
            Changed?.Invoke(State);
            return true;
        }

        private bool TriggerPull()
        {
            // Самовзвода нет: следующий выстрел только после TriggerRelease.
            if (_safetyOn || _triggerPulled) return false;

            _triggerPulled = true;
            if (_slide != Slide.Forward) return true;

            if (_chamber != Chamber.Loaded || _jam != Jam.None)
            {
                DryFire?.Invoke();
                return true;
            }

            if (!_magazineInserted)
            {
                _chamber = Chamber.Empty;
            }
            else if (_rounds > 0)
            {
                _rounds--;
            }
            else
            {
                _chamber = Chamber.Empty;
                _slide = Slide.Locked;
            }

            Fired?.Invoke();
            CasingEjected?.Invoke();
            Changed?.Invoke(State);
            return true;
        }

        private bool TriggerRelease()
        {
            if (!_triggerPulled) return false;

            _triggerPulled = false;
            return true;
        }
    }
}
