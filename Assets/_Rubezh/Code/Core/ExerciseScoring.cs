using System;
using System.Collections.Generic;

namespace Rubezh.Core
{
    // Балл и вердикт (FR-40, FR-41): 100 минус штрафы за мелкие нарушения, обнулённые пункты, промахи
    // и превышение лимита времени, не ниже 0. Грубое нарушение упражнение не останавливает и весь балл
    // не обнуляет (Р-11): оно обнуляет свой пункт правил безопасности — минус CriticalPenalty, один раз
    // за пункт, сколько бы раз его ни нарушили. Вердикт: ≥ PassScore — зачёт, ≥ RemarksScore — зачёт
    // с замечаниями, ниже — не зачёт; с обнулённым пунктом «зачёт» без замечаний не ставится.
    // Все числа — в ExerciseRules (RulesConfig).
    public static class ExerciseScoring
    {
        public readonly struct Breakdown
        {
            public readonly int Violations;   // сумма штрафов мелких нарушений
            public readonly int Misses;       // штраф за промахи
            public readonly int OverTime;     // штраф за превышение лимита
            public readonly int Critical;     // обнулённые пункты: CriticalPenalty за каждый
            public readonly IReadOnlyList<Violation> ZeroedItems;
            public readonly int Score;
            public readonly Verdict Verdict;

            public Breakdown(int violations, int misses, int overTime, int critical,
                             IReadOnlyList<Violation> zeroedItems, int score, Verdict verdict)
            {
                Violations = violations; Misses = misses; OverTime = overTime; Critical = critical;
                ZeroedItems = zeroedItems; Score = score; Verdict = verdict;
            }
        }

        // Штраф одного мелкого нарушения для строки экрана результата. Грубое считается не строкой,
        // а пунктом (ZeroedItems): в строке у него «0 баллов за пункт».
        public static int Penalty(ExerciseRules rules, ViolationRecord v) =>
            v.Severity == Severity.Critical ? 0 : rules.PenaltyFor(v.Type);

        // Пункты с 0 баллов: виды грубых нарушений без повторов, в порядке первого появления.
        public static IReadOnlyList<Violation> ZeroedItems(IReadOnlyList<ViolationRecord> violations)
        {
            var items = new List<Violation>();
            if (violations != null)
                foreach (var record in violations)
                    if (record.Severity == Severity.Critical && !items.Contains(record.Type)) items.Add(record.Type);
            return items;
        }

        public static int OverTimePenalty(ExerciseRules rules, double totalSeconds)
        {
            double over = totalSeconds - rules.TimeLimitSeconds;
            if (over <= 0) return 0;
            int steps = (int)Math.Ceiling(over / rules.OverTimeStepSeconds);
            return Math.Min(rules.OverTimePenaltyMax, steps * rules.OverTimePenaltyPerStep);
        }

        public static Verdict VerdictFor(ExerciseRules rules, int score, bool hasZeroedItems)
        {
            if (score >= rules.PassScore && !hasZeroedItems) return Verdict.Passed;
            if (score >= rules.RemarksScore) return Verdict.PassedWithRemarks;
            return Verdict.NotPassed;
        }

        public static Breakdown Calculate(ExerciseRules rules, IReadOnlyList<ViolationRecord> violations,
                                          int shots, int hits, double totalSeconds)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            int v = 0;
            if (violations != null)
                foreach (var record in violations) v += Penalty(rules, record);
            var zeroed = ZeroedItems(violations);
            int critical = zeroed.Count * rules.CriticalPenalty;
            int misses = Math.Max(0, shots - hits) * rules.MissPenalty;
            int time = OverTimePenalty(rules, totalSeconds);
            int score = Math.Max(0, 100 - v - critical - misses - time);
            return new Breakdown(v, misses, time, critical, zeroed, score, VerdictFor(rules, score, zeroed.Count > 0));
        }
    }
}
