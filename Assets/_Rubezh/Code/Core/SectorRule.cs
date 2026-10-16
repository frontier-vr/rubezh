namespace Rubezh.Core
{
    // Правило сектора (FR-30): пока пистолет в руке, ствол вне сектора запускает таймер.
    // Короткий занос — мелкое MuzzleOutOfSector с длительностью, когда ствол вернулся;
    // занос дольше SectorGraceSeconds при снаряжённом оружии — грубое, директор уходит в Failed.
    // Геометрию (луч из Muzzle) считает SectorDetector в Gameplay, сюда приходит только «в секторе или нет».
    public sealed class SectorRule
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private bool _out;
        private double _outSince;
        private bool _critical;

        public SectorRule(ExerciseDirector director, IClock clock)
        {
            _director = director;
            _clock = clock;
        }

        public bool IsOut => _out;
        public double OutDuration => _out ? _clock.Now - _outSince : 0;

        public void Tick(bool held, bool inSector, bool loaded)
        {
            if (!held || !_director.IsRunning)
            {
                EndEpisode();
                return;
            }

            if (inSector)
            {
                EndEpisode();
                return;
            }

            if (!_out)
            {
                _out = true;
                _outSince = _clock.Now;
                _critical = false;
            }

            double duration = _clock.Now - _outSince;
            if (!_critical && loaded && duration >= _director.Rules.SectorGraceSeconds)
            {
                _critical = true;
                _director.Raise(Violation.MuzzleOutOfSector, Severity.Critical, duration);
            }
        }

        public void Reset()
        {
            _out = false;
            _critical = false;
        }

        private void EndEpisode()
        {
            if (!_out) return;
            _out = false;
            double duration = _clock.Now - _outSince;
            // Дрожание руки на границе сектора за нарушение не считаем.
            if (!_critical && duration >= _director.Rules.SectorJitterSeconds)
                _director.Raise(Violation.MuzzleOutOfSector, Severity.Minor, duration);
        }
    }
}
