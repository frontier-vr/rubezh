using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Взгляд в ствол FR-32: грубое LookIntoMuzzle после короткого порога, упражнение в Failed.
    public class MuzzleLookRuleTests
    {
        private sealed class FakeClock : IClock { public double Now { get; set; } }

        private FakeClock _clock;
        private ExerciseDirector _director;
        private MuzzleLookRule _rule;
        private List<ViolationRecord> _raised;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _director = new ExerciseDirector(_clock);
            _rule = new MuzzleLookRule(_director, _clock);
            _raised = new List<ViolationRecord>();
            _director.ViolationRaised += _raised.Add;
        }

        private void Run(double seconds, bool looking)
        {
            double end = _clock.Now + seconds;
            while (_clock.Now < end - 1e-9) { _clock.Now += 0.02; _rule.Tick(looking); }
        }

        [Test]
        public void LookingIntoMuzzle_FailsWithReason()
        {
            Run(0.4, true);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.LookIntoMuzzle, _raised[0].Type);
            Assert.AreEqual(Severity.Critical, _raised[0].Severity);
            Assert.AreEqual(Phase.Failed, _director.Phase);
            Assert.AreEqual(Violation.LookIntoMuzzle, _director.Result.FailReason);
        }

        [Test]
        public void GlanceShorterThanThreshold_IsIgnored()
        {
            Run(0.1, true); Run(0.5, false); Run(0.1, true); Run(0.5, false);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void ThresholdComesFromRules()
        {
            _director = new ExerciseDirector(_clock, new ExerciseRules { MuzzleLookSeconds = 1.0 });
            _rule = new MuzzleLookRule(_director, _clock);
            Run(0.8, true);
            Assert.AreNotEqual(Phase.Failed, _director.Phase);
            Run(0.3, true);
            Assert.AreEqual(Phase.Failed, _director.Phase);
        }

        [Test]
        public void NotLooking_NoViolation()
        {
            Run(5, false);
            Assert.IsEmpty(_raised);
        }
    }
}
