namespace Rubezh.Core
{
    // Правило сектора (FR-30): пока пистолет в руке, ствол вне сектора запускает таймер.
    // Короткий занос — мелкое MuzzleOutOfSector с длительностью, когда ствол вернулся;
    // занос дольше SectorGraceSeconds при снаряжённом оружии — грубое, директор уходит в Failed.
    // Геометрию (луч из Muzzle) считает SectorDetector в Gameplay, сюда приходит только «в секторе или нет».
    // Вне рубежа (у шкафа и стола, #164) пустой пистолет поворачивают в руках, а сектор оттуда виден
    // узкой щелью: там мелкое нарушение — только если ствол вне безопасного направления дольше
    // SectorGraceSeconds, короткий занос при повороте прощается. На рубеже — порог дрожания, как было.
    public sealed class SectorRule
    {
        private readonly ExerciseDirector _director;
        private readonly IClock _clock;
        private bool _out;
        private double _outSince;
        private bool _critical;
        private bool _onLine = true;   // где начался текущий эпизод: на рубеже или нет

        public SectorRule(ExerciseDirector director, IClock clock)
        {
            _director = director;
            _clock = clock;
        }

        public bool IsOut => _out;
        public double OutDuration => _out ? _clock.Now - _outSince : 0;

        public void Tick(bool held, bool inSector, bool loaded) => Tick(held, inSector, loaded, true);

        public void Tick(bool held, bool inSector, bool loaded, bool onLine)
        {
            // Пришёл на рубеж посреди эпизода вне рубежа: тот эпизод судим по правилам «вне рубежа»,
            // а на рубеже отсчёт начинается заново.
            if (_out && _onLine != onLine) EndEpisode();
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
                _onLine = onLine;
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
            // Дрожание руки на границе сектора за нарушение не считаем; вне рубежа — и короткий занос.
            double threshold = _onLine ? _director.Rules.SectorJitterSeconds : _director.Rules.SectorGraceSeconds;
            if (!_critical && duration >= threshold)
                _director.Raise(Violation.MuzzleOutOfSector, Severity.Minor, duration);
        }
    }
}
