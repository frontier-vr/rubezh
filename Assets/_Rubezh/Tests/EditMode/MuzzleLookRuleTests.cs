using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Взгляд в ствол FR-32: грубое LookIntoMuzzle после короткого порога, одно на эпизод, без стопа (Р-09).
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
        public void LookingIntoMuzzle_IsCritical_OncePerEpisode_WithoutStop()
        {
            Run(2, true);
            Assert.AreEqual(1, _raised.Count, "долгий взгляд — одно нарушение");
            Assert.AreEqual(Violation.LookIntoMuzzle, _raised[0].Type);
            Assert.AreEqual(Severity.Critical, _raised[0].Severity);
            Assert.IsTrue(_director.IsRunning);

            Run(0.5, false); Run(0.4, true);
            Assert.AreEqual(2, _raised.Count, "новый эпизод — новое нарушение");
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
            _director.ViolationRaised += _raised.Add;
            Run(0.8, true);
            Assert.IsEmpty(_raised);
            Run(0.3, true);
            Assert.AreEqual(1, _raised.Count);
        }

        [Test]
        public void NotLooking_NoViolation()
        {
            Run(5, false);
            Assert.IsEmpty(_raised);
        }
    }
}
