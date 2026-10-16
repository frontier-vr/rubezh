using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Палец на спуске FR-31: нарушение вне фазы «Огонь», эпизодами с длительностью.
    public class TriggerDisciplineRuleTests
    {
        private sealed class FakeClock : IClock { public double Now { get; set; } }

        private FakeClock _clock;
        private ExerciseDirector _director;
        private TriggerDisciplineRule _rule;
        private List<ViolationRecord> _raised;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _director = new ExerciseDirector(_clock);
            _rule = new TriggerDisciplineRule(_director, _clock);
            _raised = new List<ViolationRecord>();
            _director.ViolationRaised += _raised.Add;
        }

        private void Run(double seconds, bool held, bool touched)
        {
            double end = _clock.Now + seconds;
            while (_clock.Now < end - 1e-9) { _clock.Now += 0.02; _rule.Tick(held, touched); }
        }

        // Довести директор до фазы Fire: взят, осмотрен, СИЗ, магазин снаряжён, рубеж, магазин в оружии.
        internal static void GoToFire(ExerciseDirector director)
        {
            var empty = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
            director.UpdateFacts(new ExerciseFacts(true, empty, 0, Place.Hand, Place.InspectShelf, true, true, false));
            director.UpdateFacts(new ExerciseFacts(false, empty, 5, Place.LineStand, Place.LineStand, true, true, true));
            var loaded = new WeaponState(true, 5, Chamber.Empty, true, true, Jam.None);
            director.UpdateFacts(new ExerciseFacts(true, loaded, 0, Place.Hand, Place.InWeapon, true, true, true));
            Assert.AreEqual(Phase.Fire, director.Phase);
        }

        [Test]
        public void TouchOutsideFire_GivesMinorWithDuration_WhenReleased()
        {
            Run(0.6, held: true, touched: true);
            Assert.IsEmpty(_raised, "пока палец на спуске, нарушение копится");
            Run(0.1, held: true, touched: false);

            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.FingerOnTrigger, _raised[0].Type);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.AreEqual(0.6, _raised[0].Duration, 0.03);
        }

        [Test]
        public void TouchInFire_IsAllowed()
        {
            GoToFire(_director);
            Run(2, true, true); Run(0.1, true, false);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void TouchContinuingIntoFire_CountsOnlyTimeBeforeFire()
        {
            Run(0.5, true, true);
            GoToFire(_director);
            Run(0.1, true, true);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(0.5, _raised[0].Duration, 0.05);
        }

        [Test]
        public void ShortTouch_IsIgnored()
        {
            Run(0.1, true, true); Run(0.1, true, false);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void NotHeld_DoesNotCount()
        {
            Run(1, held: false, touched: true); Run(0.1, false, false);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void ReleasingPistol_EndsEpisode()
        {
            Run(0.4, true, true); Run(0.1, false, true);
            Assert.AreEqual(1, _raised.Count);
        }

        [Test]
        public void MinDurationComesFromRules()
        {
            _director = new ExerciseDirector(_clock, new ExerciseRules { TriggerTouchMinSeconds = 0.5 });
            _rule = new TriggerDisciplineRule(_director, _clock);
            _director.ViolationRaised += _raised.Add;
            Run(0.4, true, true); Run(0.1, true, false);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void AfterFailed_NothingIsRecorded()
        {
            _director.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            int n = _raised.Count;
            Run(1, true, true); Run(0.1, true, false);
            Assert.AreEqual(n, _raised.Count);
        }
    }
}
