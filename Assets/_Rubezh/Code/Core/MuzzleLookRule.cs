namespace Rubezh.Core
{
    // Взгляд в ствол (FR-32): если пользователь смотрит в дульный срез дольше MuzzleLookSeconds —
    // грубое LookIntoMuzzle, упражнение в Failed. Короткий порог отсекает случайный проход взгляда.
    // Геометрию (луч из камеры шлема) считает MuzzleLookDetector в Gameplay.
    public sealed class MuzzleLookRule
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private bool _looking;
        private double _since;

        public MuzzleLookRule(ExerciseDirector director, IClock clock)
        {
            _director = director;
            _clock = clock;
        }

        public bool IsLooking => _looking;

        public void Tick(bool lookingIntoMuzzle)
        {
            if (!lookingIntoMuzzle || !_director.IsRunning)
            {
                _looking = false;
                return;
            }

            if (!_looking)
            {
                _looking = true;
                _since = _clock.Now;
            }

            double duration = _clock.Now - _since;
            if (duration >= _director.Rules.MuzzleLookSeconds)
                _director.Raise(Violation.LookIntoMuzzle, Severity.Critical, duration);
        }

        public void Reset() => _looking = false;
    }
}
