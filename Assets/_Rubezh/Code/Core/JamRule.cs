using System;

namespace Rubezh.Core
{
    // Осечка (FR-22): назначается на 3-й или 4-й выстрел генератором с зерном. Правильное устранение —
    // выждать MisfireWaitSeconds, не уводя ствол из сектора (это ловит SectorRule), затем перезарядить
    // затвором. Перезарядка раньше — мелкое нарушение JamMishandled с прошедшим временем.
    // Прихват гильзы (FR-23, VP-13): с вероятностью StovePipeChance на выстреле из диапазона, но не на
    // выстреле осечки. Гильза торчит из окна — устранять сразу перезарядкой, ствол к мишеням, в ствол
    // не заглядывать (это ловит MuzzleLookDetector). Ошибка — снова жать на спуск при прихвате:
    // JamMishandled, один раз за задержку.
    public sealed class JamRule : IDisposable
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private readonly IWeapon _weapon;
        private bool _jammed;
        private double _jammedAt;
        private bool _stovePipe;
        private bool _stovePipeMishandled;

        public JamRule(ExerciseDirector director, IClock clock, IWeapon weapon)
        {
            _director = director;
            _clock = clock;
            _weapon = weapon;
            _weapon.Jammed += OnJammed;
            _weapon.Changed += OnChanged;
            _weapon.DryFire += OnDryFire;
        }

        public bool IsJammed => _jammed || _stovePipe;
        public bool IsStovePipe => _stovePipe;
        public int MisfireShot { get; private set; }
        public int StovePipeShot { get; private set; }     // 0 — прихвата в этом упражнении нет

        // Выбрать выстрел осечки и назначить её оружию. Вызывается в начале упражнения и на Restart.
        public int Arm(IRandom random)
        {
            var rules = _director.Rules;
            MisfireShot = random.Next(rules.MisfireShotMin, rules.MisfireShotMax + 1);
            StovePipeShot = PickStovePipe(random, rules);
            if (_weapon is Weapon w)
            {
                w.ScheduleMisfire(MisfireShot);
                w.ScheduleStovePipe(StovePipeShot);
            }
            _jammed = false;
            _stovePipe = false;
            return MisfireShot;
        }

        public void Dispose()
        {
            _weapon.Jammed -= OnJammed;
            _weapon.Changed -= OnChanged;
            _weapon.DryFire -= OnDryFire;
        }

        private int PickStovePipe(IRandom random, ExerciseRules rules)
        {
            // Вероятность — в тысячных: генератор целочисленный.
            if (rules.StovePipeChance <= 0 || random.Next(0, 1000) >= rules.StovePipeChance * 1000) return 0;
            int shot = random.Next(rules.StovePipeShotMin, rules.StovePipeShotMax + 1);
            if (shot != MisfireShot) return shot;
            if (shot < rules.StovePipeShotMax) return shot + 1;
            return shot > rules.StovePipeShotMin ? shot - 1 : 0;
        }

        private void OnJammed(Jam jam)
        {
            if (jam == Jam.StovePipe)
            {
                _stovePipe = true;
                _stovePipeMishandled = false;
                return;
            }
            if (jam != Jam.Misfire) return;
            _jammed = true;
            _jammedAt = _clock.Now;
        }

        private void OnDryFire()
        {
            if (!_stovePipe || _stovePipeMishandled) return;
            _stovePipeMishandled = true;
            _director.Raise(Violation.JamMishandled, Severity.Minor);
        }

        private void OnChanged(WeaponState state)
        {
            if (_stovePipe && state.Jam == Jam.None) _stovePipe = false;
            if (!_jammed || state.Jam != Jam.None) return;
            _jammed = false;
            double waited = _clock.Now - _jammedAt;
            if (waited < _director.Rules.MisfireWaitSeconds)
                _director.Raise(Violation.JamMishandled, Severity.Minor, waited);
        }
    }

    // Случайность в Core подменяемая: в игре — с зерном из ExerciseRunner, в тестах — заданная.
    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SeededRandom : IRandom
    {
        private readonly Random _random;
        public SeededRandom(int seed) { _random = new Random(seed); }
        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }
}
