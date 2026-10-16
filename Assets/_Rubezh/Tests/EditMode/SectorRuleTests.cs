using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Правило сектора FR-30: штраф за короткий занос, грубое после 1,5 с при снаряжённом оружии.
    public class SectorRuleTests
    {
        private sealed class FakeClock : IClock { public double Now { get; set; } }

        private FakeClock _clock;
        private ExerciseDirector _director;
        private SectorRule _rule;
        private List<ViolationRecord> _raised;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _director = new ExerciseDirector(_clock);
            _rule = new SectorRule(_director, _clock);
            _raised = new List<ViolationRecord>();
            _director.ViolationRaised += _raised.Add;
        }

        private void Run(double seconds, bool held, bool inSector, bool loaded, double step = 0.02, bool onLine = true)
        {
            double end = _clock.Now + seconds;
            while (_clock.Now < end - 1e-9)
            {
                _clock.Now += step;
                _rule.Tick(held, inSector, loaded, onLine);
            }
        }

        // #164: вне рубежа короткий занос при повороте пустого пистолета прощается.
        [Test]
        public void OffLine_ShortSweep_Forgiven()
        {
            Run(1.0, held: true, inSector: false, loaded: false, onLine: false);
            Run(0.2, held: true, inSector: true, loaded: false, onLine: false);
            Assert.IsEmpty(_raised, "1 с вне безопасного направления у шкафа — не нарушение");
        }

        [Test]
        public void OffLine_LongHold_IsMinor()
        {
            Run(2.0, held: true, inSector: false, loaded: false, onLine: false);
            Run(0.2, held: true, inSector: true, loaded: false, onLine: false);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.AreEqual(2.0, _raised[0].Duration, 0.05);
        }

        [Test]
        public void EpisodeFromOffLine_JudgedOffLine_WhenArrivingOnLine()
        {
            Run(0.8, held: true, inSector: false, loaded: false, onLine: false);   // у стола ствол в сторону
            Run(0.05, held: true, inSector: false, loaded: false, onLine: true);   // телепорт на рубеж
            Run(0.2, held: true, inSector: true, loaded: false, onLine: true);     // и сразу в сектор
            Assert.IsEmpty(_raised, "занос у стола короткий, на рубеже — в пределах дрожания");
        }

        [Test]
        public void OnLine_ShortSweep_StillMinor()
        {
            Run(0.5, held: true, inSector: false, loaded: false, onLine: true);
            Run(0.2, held: true, inSector: true, loaded: false, onLine: true);
            Assert.AreEqual(1, _raised.Count, "на рубеже порог прежний");
        }

        [Test]
        public void InSector_NoViolation()
        {
            Run(3, held: true, inSector: true, loaded: true);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void ShortSweep_GivesMinorWithDuration_WhenMuzzleReturns()
        {
            Run(1, true, true, true);
            Run(0.8, true, false, true);
            Assert.IsEmpty(_raised, "пока ствол вне сектора, мелкое не пишется");
            Run(0.1, true, true, true);

            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.MuzzleOutOfSector, _raised[0].Type);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.AreEqual(0.8, _raised[0].Duration, 0.03);
            Assert.AreNotEqual(Phase.Failed, _director.Phase);
        }

        [Test]
        public void LongSweep_Loaded_GivesCriticalAtGrace_AndFails()
        {
            Run(1.4, true, false, true);
            Assert.IsEmpty(_raised);
            Run(0.12, true, false, true);

            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Severity.Critical, _raised[0].Severity);
            Assert.AreEqual(1.5, _raised[0].Duration, 0.03);
            Assert.AreEqual(Phase.Failed, _director.Phase);
            Assert.AreEqual(Violation.MuzzleOutOfSector, _director.Result.FailReason);
        }

        [Test]
        public void LongSweep_Unloaded_GivesOnlyMinor()
        {
            Run(3, true, false, false);
            Run(0.1, true, true, false);

            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.AreEqual(3, _raised[0].Duration, 0.05);
        }

        [Test]
        public void NotHeld_NeverCounts()
        {
            Run(5, held: false, inSector: false, loaded: true);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void ReleasingPistol_EndsEpisodeAsMinor()
        {
            Run(0.6, true, false, true);
            Run(0.1, false, false, true);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
        }

        [Test]
        public void Jitter_ShorterThanThreshold_IsIgnored()
        {
            Run(0.06, true, false, true);
            Run(0.1, true, true, true);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void GraceComesFromRules()
        {
            _director = new ExerciseDirector(_clock, new ExerciseRules { SectorGraceSeconds = 0.5 });
            _rule = new SectorRule(_director, _clock);
            Run(0.52, true, false, true);
            Assert.AreEqual(Phase.Failed, _director.Phase);
        }

        [Test]
        public void AfterCritical_NoMinorOnReturn()
        {
            Run(2, true, false, true);
            Run(0.1, true, true, true);
            Assert.AreEqual(1, _raised.Count);
        }

        [Test]
        public void TwoSeparateSweeps_GiveTwoMinors()
        {
            Run(0.3, true, false, true); Run(0.2, true, true, true);
            Run(0.4, true, false, true); Run(0.2, true, true, true);
            Assert.AreEqual(2, _raised.Count);
        }
    }
}
