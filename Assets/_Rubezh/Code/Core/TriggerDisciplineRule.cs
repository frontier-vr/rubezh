namespace Rubezh.Core
{
    // Палец на спуске (FR-31): касание спуска рукой на Grip_Primary вне фазы «Огонь» — нарушение
    // FingerOnTrigger. Считается эпизодами: палец лёг — эпизод начался, снят (или началась «Огонь»,
    // или пистолет отпустили) — мелкое нарушение с длительностью. Касание короче
    // TriggerTouchMinSeconds не считается.
    public sealed class TriggerDisciplineRule
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private bool _touching;
        private double _since;

        public TriggerDisciplineRule(ExerciseDirector director, IClock clock)
        {
            _director = director;
            _clock = clock;
        }

        public bool IsViolating => _touching;

        public void Tick(bool held, bool touched)
        {
            bool violating = held && touched && _director.IsRunning && _director.Phase != Phase.Fire;
            if (violating && !_touching)
            {
                _touching = true;
                _since = _clock.Now;
            }
            else if (!violating && _touching)
            {
                _touching = false;
                double duration = _clock.Now - _since;
                if (duration >= _director.Rules.TriggerTouchMinSeconds)
                    _director.Raise(Violation.FingerOnTrigger, Severity.Minor, duration);
            }
        }

        public void Reset() => _touching = false;
    }
}
