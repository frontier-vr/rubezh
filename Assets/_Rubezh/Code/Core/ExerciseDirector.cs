using System;
using System.Collections.Generic;

namespace Rubezh.Core
{
    // Директор упражнения: ведёт фазы Receive → … → Return → Done, проверяет порядок шагов,
    // копит нарушения и переводит упражнение в Failed на грубом нарушении.
    // Условия закрытия фаз — docs/user-flow.md §4.1. Время только через IClock.
    public sealed class ExerciseDirector
    {
        private readonly IClock _clock;
        private readonly ExerciseRules _rules;
        private readonly List<ViolationRecord> _violations = new List<ViolationRecord>();
        private readonly HashSet<(Phase, ExerciseAction)> _stepOrderRaised = new HashSet<(Phase, ExerciseAction)>();

        private ExerciseFacts _facts;
        private double _startTime;
        private double _endTime;
        private bool _timerStarted;
        private bool _inspectionReported;
        private int _shots;
        private int _hits;

        public event Action<Phase, Phase> PhaseChanged;      // (было, стало)
        public event Action<ViolationRecord> ViolationRaised;
        public event Action<ExerciseResult> Finished;

        public ExerciseDirector(IClock clock, ExerciseRules rules = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _rules = rules ?? new ExerciseRules();
            Reset();
        }

        public Phase Phase { get; private set; }
        public ExerciseRules Rules => _rules;
        public bool IsRunning => Phase != Phase.Done && Phase != Phase.Failed;
        public IReadOnlyList<ViolationRecord> Violations => _violations;
        public ExerciseResult Result { get; private set; }

        // Секунды от взятия пистолета; до него 0.
        public double Elapsed => !_timerStarted ? 0 : (IsRunning ? _clock.Now : _endTime) - _startTime;

        public void Restart()
        {
            var from = Phase;
            Reset();
            if (from != Phase.Receive) PhaseChanged?.Invoke(from, Phase.Receive);
        }

        // Новый снимок мира: закрываем текущую фазу и все следующие, чьи условия уже выполнены.
        public void UpdateFacts(in ExerciseFacts facts)
        {
            _facts = facts;
            if (!IsRunning) return;

            for (int guard = 0; guard < 16 && IsRunning && IsPhaseClosed(Phase, _facts); guard++)
            {
                if (Phase == Phase.Receive)
                {
                    _timerStarted = true;
                    _startTime = _clock.Now;
                }
                Advance(Next(Phase));
            }
        }

        public void ReportAction(ExerciseAction action, Anchor anchor = Anchor.None)
        {
            if (!IsRunning) return;

            switch (action)
            {
                case ExerciseAction.InsertRound:
                    if (Phase < Phase.LoadMagazine) RaiseStepOrder(action);
                    break;
                case ExerciseAction.InsertMagazine:
                    if (Phase < Phase.Load || Phase > Phase.Fire) RaiseStepOrder(action);
                    break;
                case ExerciseAction.CabinetAttempt:
                    if (Phase > Phase.Receive && Phase < Phase.Return) RaiseStepOrder(action);
                    break;
                case ExerciseAction.PutOnEarmuffs:
                case ExerciseAction.PutOnGlasses:
                    if (Phase == Phase.Inspect) RaiseIncompleteInspection();
                    break;
                case ExerciseAction.Teleport:
                    if (Phase == Phase.Inspect && anchor != Anchor.A1) RaiseIncompleteInspection();
                    break;
            }
        }

        // Нарушение от детектора. Грубое прерывает упражнение.
        public void Raise(Violation type, Severity severity, double duration = 0)
        {
            if (!IsRunning) return;

            var record = new ViolationRecord(type, severity, Elapsed, duration);
            _violations.Add(record);
            ViolationRaised?.Invoke(record);

            if (severity == Severity.Critical) Fail(type);
        }

        // Выстрел от ShotResolver (VP-08): попадание идёт в точность.
        public void RegisterShot(bool hit)
        {
            if (!IsRunning) return;
            _shots++;
            if (hit) _hits++;
        }

        private void Reset()
        {
            Phase = Phase.Receive;
            _facts = ExerciseFacts.Start;
            _violations.Clear();
            _stepOrderRaised.Clear();
            _timerStarted = false;
            _inspectionReported = false;
            _startTime = _endTime = 0;
            _shots = _hits = 0;
            Result = null;
        }

        private bool IsPhaseClosed(Phase phase, in ExerciseFacts f)
        {
            var w = f.Weapon;
            switch (phase)
            {
                case Phase.Receive: return f.PistolInHand;
                case Phase.Inspect: return !w.MagazineInserted && w.SlideLocked && w.Chamber == Chamber.Empty;
                case Phase.Ppe: return f.EarmuffsOn && f.GlassesOn;
                case Phase.LoadMagazine:
                    return f.MagazineRounds >= _rules.SeriesRounds && !w.MagazineInserted;
                case Phase.ToLine:
                    return f.PistolPlace == Place.LineStand && f.MagazinePlace == Place.LineStand && f.TargetSupplied;
                case Phase.Load: return w.MagazineInserted;
                case Phase.Fire:
                    // Серия кончилась: магазин пуст, патронник пуст, затвор встал на задержку.
                    return w.MagazineInserted && w.RoundsInMagazine == 0 && w.Chamber == Chamber.Empty && w.SlideLocked;
                case Phase.Cease: return w.SafetyOn;
                case Phase.Unload: return w.IsClear;
                case Phase.ShowClear:
                    return w.IsClear && f.PistolPlace == Place.LineStand && f.MagazinePlace == Place.LineStand;
                case Phase.Return: return f.PistolPlace == Place.Cabinet && w.IsClear;
                default: return false;
            }
        }

        private static Phase Next(Phase phase) => phase == Phase.Return ? Phase.Done : phase + 1;

        private void Advance(Phase to)
        {
            var from = Phase;
            Phase = to;
            PhaseChanged?.Invoke(from, to);
            if (to == Phase.Done) Finish(failReason: null);
        }

        private void Fail(Violation reason)
        {
            var from = Phase;
            Phase = Phase.Failed;
            PhaseChanged?.Invoke(from, Phase.Failed);
            Finish(reason);
        }

        private void Finish(Violation? failReason)
        {
            _endTime = _clock.Now;
            bool failed = failReason.HasValue;
            var violations = _violations.ToArray();
            var score = ExerciseScoring.Calculate(_rules, violations, _shots, _hits, Elapsed, failed);
            Result = new ExerciseResult
            {
                TotalTime = Elapsed,
                Shots = _shots,
                Hits = _hits,
                Score = score.Score,
                Verdict = score.Verdict,
                FailedCritical = failed,
                FailReason = failReason,
                Violations = violations,
                ViolationPenalty = score.Violations,
                MissPenalty = score.Misses,
                OverTimePenalty = score.OverTime
            };
            Finished?.Invoke(Result);
        }

        private void RaiseStepOrder(ExerciseAction action)
        {
            if (_stepOrderRaised.Add((Phase, action))) Raise(Violation.StepOrder, Severity.Minor);
        }

        private void RaiseIncompleteInspection()
        {
            if (_inspectionReported) return;
            _inspectionReported = true;
            Raise(Violation.IncompleteInspection, Severity.Minor);
        }
    }
}

