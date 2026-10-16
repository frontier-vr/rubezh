using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Балл и вердикт FR-40, FR-41 на границах (NG-24): 80, 79, 60, 59 и грубое нарушение.
    // Штрафы берутся из ExerciseRules (в проекте — из RulesConfig), чтобы тест не зависел от весов.
    public class ScoringTests
    {
        private ExerciseRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = new ExerciseRules();
            // Удобные для границ веса: одно нарушение — 1, 20 или 21 балл.
            _rules.Penalties = new Dictionary<Violation, int>
            {
                { Violation.StepOrder, 1 },
                { Violation.FingerOnTrigger, 20 },
                { Violation.NoGlasses, 21 },
            };
        }

        private static ViolationRecord Minor(Violation v) => new ViolationRecord(v, Severity.Minor, 10, 0);

        private ExerciseScoring.Breakdown Score(params ViolationRecord[] v) =>
            ExerciseScoring.Calculate(_rules, v, shots: 5, hits: 5, totalSeconds: 200, failedCritical: false);

        [Test]
        public void NoViolations_Is100_Passed()
        {
            var s = Score();
            Assert.AreEqual(100, s.Score);
            Assert.AreEqual(Verdict.Passed, s.Verdict);
        }

        [Test]
        public void Exactly80_IsPassed()
        {
            var s = Score(Minor(Violation.FingerOnTrigger));
            Assert.AreEqual(80, s.Score);
            Assert.AreEqual(Verdict.Passed, s.Verdict);
        }

        [Test]
        public void Score79_IsPassedWithRemarks()
        {
            var s = Score(Minor(Violation.NoGlasses));
            Assert.AreEqual(79, s.Score);
            Assert.AreEqual(Verdict.PassedWithRemarks, s.Verdict);
        }

        [Test]
        public void Exactly60_IsPassedWithRemarks()
        {
            var s = Score(Minor(Violation.FingerOnTrigger), Minor(Violation.FingerOnTrigger));
            Assert.AreEqual(60, s.Score);
            Assert.AreEqual(Verdict.PassedWithRemarks, s.Verdict);
        }

        [Test]
        public void Score59_IsNotPassed()
        {
            var s = Score(Minor(Violation.FingerOnTrigger), Minor(Violation.NoGlasses));
            Assert.AreEqual(59, s.Score);
            Assert.AreEqual(Verdict.NotPassed, s.Verdict);
        }

        [Test]
        public void GrossViolation_IsZero_NotPassed_EvenWithHighScore()
        {
            var gross = new ViolationRecord(Violation.LookIntoMuzzle, Severity.Critical, 30, 0.3);
            var s = ExerciseScoring.Calculate(_rules, new[] { gross }, 5, 5, 60, failedCritical: true);
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(Verdict.NotPassed, s.Verdict);
            Assert.AreEqual(0, ExerciseScoring.Penalty(_rules, gross), "у грубого в таблице «стоп», не штраф");
        }

        [Test]
        public void Director_GrossViolation_ResultNotPassed_FailedCritical()
        {
            var clock = new ManualClock();
            var d = new ExerciseDirector(clock, _rules);
            d.Raise(Violation.ShotBeforeFire, Severity.Critical);
            Assert.AreEqual(Phase.Failed, d.Phase);
            Assert.AreEqual(0, d.Result.Score);
            Assert.AreEqual(Verdict.NotPassed, d.Result.Verdict);
            Assert.IsTrue(d.Result.FailedCritical);
            Assert.AreEqual(Violation.ShotBeforeFire, d.Result.FailReason);
        }

        [Test]
        public void Misses_CostMissPenaltyEach()
        {
            _rules.MissPenalty = 3;
            var s = ExerciseScoring.Calculate(_rules, new ViolationRecord[0], shots: 5, hits: 2, totalSeconds: 100, failedCritical: false);
            Assert.AreEqual(9, s.Misses);
            Assert.AreEqual(91, s.Score);
        }

        [TestCase(360.0, 0)]
        [TestCase(360.5, 2)]
        [TestCase(370.0, 2)]
        [TestCase(370.1, 4)]
        [TestCase(1000.0, 20)]
        public void OverTime_StepsAndCap(double seconds, int penalty)
        {
            Assert.AreEqual(penalty, ExerciseScoring.OverTimePenalty(_rules, seconds));
        }

        [Test]
        public void Score_NeverBelowZero()
        {
            var many = new List<ViolationRecord>();
            for (int i = 0; i < 10; i++) many.Add(Minor(Violation.NoGlasses));
            Assert.AreEqual(0, Score(many.ToArray()).Score);
        }

        [Test]
        public void Thresholds_ComeFromRules()
        {
            _rules.PassScore = 90;
            Assert.AreEqual(Verdict.PassedWithRemarks, Score(Minor(Violation.FingerOnTrigger)).Verdict, "80 < 90");
        }

        [Test]
        public void DefaultPenalties_CoverEveryMinorViolation()
        {
            var rules = new ExerciseRules();
            foreach (var v in new[] { Violation.MuzzleOutOfSector, Violation.FingerOnTrigger, Violation.NoEarmuffs,
                                      Violation.NoGlasses, Violation.StepOrder, Violation.IncompleteInspection,
                                      Violation.ReturnedLoaded, Violation.CasingLeft, Violation.JamMishandled })
                Assert.Greater(rules.PenaltyFor(v), 0, v.ToString());
        }

        private sealed class ManualClock : IClock
        {
            public double Now { get; set; }
        }
    }
}
