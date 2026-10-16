using System;
using System.Collections.Generic;

namespace Rubezh.Core
{
    // Балл и вердикт (FR-40, FR-41): 100 минус штрафы за мелкие нарушения, промахи и превышение
    // лимита времени, не ниже 0; при грубом нарушении 0 и «не зачёт». Вердикт: ≥ PassScore — зачёт,
    // ≥ RemarksScore — зачёт с замечаниями, ниже — не зачёт. Все числа — в ExerciseRules (RulesConfig).
    public static class ExerciseScoring
    {
        public readonly struct Breakdown
        {
            public readonly int Violations;   // сумма штрафов мелких нарушений
            public readonly int Misses;       // штраф за промахи
            public readonly int OverTime;     // штраф за превышение лимита
            public readonly int Score;
            public readonly Verdict Verdict;

            public Breakdown(int violations, int misses, int overTime, int score, Verdict verdict)
            {
                Violations = violations; Misses = misses; OverTime = overTime; Score = score; Verdict = verdict;
            }
        }

        // Штраф одного нарушения для строки экрана результата; у грубого штрафа нет — упражнение остановлено.
        public static int Penalty(ExerciseRules rules, ViolationRecord v) =>
            v.Severity == Severity.Critical ? 0 : rules.PenaltyFor(v.Type);

        public static int OverTimePenalty(ExerciseRules rules, double totalSeconds)
        {
            double over = totalSeconds - rules.TimeLimitSeconds;
            if (over <= 0) return 0;
            int steps = (int)Math.Ceiling(over / rules.OverTimeStepSeconds);
            return Math.Min(rules.OverTimePenaltyMax, steps * rules.OverTimePenaltyPerStep);
        }

        public static Verdict VerdictFor(ExerciseRules rules, int score, bool failedCritical)
        {
            if (failedCritical) return Verdict.NotPassed;
            if (score >= rules.PassScore) return Verdict.Passed;
            if (score >= rules.RemarksScore) return Verdict.PassedWithRemarks;
            return Verdict.NotPassed;
        }

        public static Breakdown Calculate(ExerciseRules rules, IReadOnlyList<ViolationRecord> violations,
                                          int shots, int hits, double totalSeconds, bool failedCritical)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int v = 0;
            if (violations != null)
                foreach (var record in violations) v += Penalty(rules, record);
            int misses = Math.Max(0, shots - hits) * rules.MissPenalty;
            int time = OverTimePenalty(rules, totalSeconds);
            int score = failedCritical ? 0 : Math.Max(0, 100 - v - misses - time);
            return new Breakdown(v, misses, time, score, VerdictFor(rules, score, failedCritical));
        }
    }
}
