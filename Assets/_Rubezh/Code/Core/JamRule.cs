using System;

namespace Rubezh.Core
{
    // Осечка (FR-22): назначается на 3-й или 4-й выстрел генератором с зерном. Правильное устранение —
    // выждать MisfireWaitSeconds, не уводя ствол из сектора (это ловит SectorRule), затем перезарядить
    // затвором. Перезарядка раньше — мелкое нарушение JamMishandled с прошедшим временем.
    public sealed class JamRule : IDisposable
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private readonly IWeapon _weapon;
        private bool _jammed;
        private double _jammedAt;

        public JamRule(ExerciseDirector director, IClock clock, IWeapon weapon)
        {
            _director = director;
            _clock = clock;
            _weapon = weapon;
            _weapon.Jammed += OnJammed;
            _weapon.Changed += OnChanged;
        }

        public bool IsJammed => _jammed;
        public int MisfireShot { get; private set; }

        // Выбрать выстрел осечки и назначить её оружию. Вызывается в начале упражнения и на Restart.
        public int Arm(IRandom random)
        {
            var rules = _director.Rules;
            MisfireShot = random.Next(rules.MisfireShotMin, rules.MisfireShotMax + 1);
            if (_weapon is Weapon w) w.ScheduleMisfire(MisfireShot);
            _jammed = false;
            return MisfireShot;
        }

        public void Dispose()
        {
            _weapon.Jammed -= OnJammed;
            _weapon.Changed -= OnChanged;
        }

        private void OnJammed(Jam jam)
        {
            if (jam != Jam.Misfire) return;
            _jammed = true;
            _jammedAt = _clock.Now;
        }

        private void OnChanged(WeaponState state)
        {
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
