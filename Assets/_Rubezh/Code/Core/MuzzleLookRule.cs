namespace Rubezh.Core
{
    // Взгляд в ствол (FR-32): если пользователь смотрит в дульный срез дольше MuzzleLookSeconds —
    // грубое LookIntoMuzzle, один раз за эпизод взгляда. Короткий порог отсекает случайный проход взгляда.
    // Геометрию (луч из камеры шлема) считает MuzzleLookDetector в Gameplay.
    public sealed class MuzzleLookRule
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private bool _looking;
        private double _since;
        private bool _raised;

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
                _raised = false;
                _since = _clock.Now;
            }

            double duration = _clock.Now - _since;
            if (!_raised && duration >= _director.Rules.MuzzleLookSeconds)
            {
                _raised = true;
                _director.Raise(Violation.LookIntoMuzzle, Severity.Critical, duration);
            }
        }

        public void Reset() => _looking = false;
    }
}
