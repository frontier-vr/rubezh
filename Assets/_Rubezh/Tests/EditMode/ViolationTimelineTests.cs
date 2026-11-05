using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Нарушения со временем (VP-15, FR-43): время — секунды от начала упражнения по IClock,
    // у длящихся — длительность и начало эпизода; список в результате по порядку.
    public class ViolationTimelineTests
    {
        private sealed class FakeClock : IClock { public double Now { get; set; } }

        private FakeClock _clock;
        private ExerciseDirector _director;

        private static ExerciseFacts InHand() =>
            new ExerciseFacts(true, new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None), 0,
                              Place.Hand, Place.InWeapon, false, false, false);

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { Now = 1000 };
            _director = new ExerciseDirector(_clock);
        }

        [Test]
        public void Time_IsSecondsFromStart_ByClock()
        {
            _clock.Now = 1005;
            _director.UpdateFacts(InHand());          // пистолет взят — упражнение пошло
            _clock.Now = 1017.5;
            _director.Raise(Violation.StepOrder, Severity.Minor);
            _clock.Now = 1061.25;
            _director.Raise(Violation.FingerOnTrigger, Severity.Minor, 0.8);

            var v = _director.Violations;
            Assert.AreEqual(2, v.Count);
            Assert.AreEqual(12.5, v[0].Time, 1e-9, "от начала, а не от запуска приложения");
            Assert.AreEqual(56.25, v[1].Time, 1e-9);
            Assert.AreEqual(0.8, v[1].Duration, 1e-9);
            Assert.AreEqual(55.45, v[1].Start, 1e-9, "длящееся: начало эпизода на длительность раньше");
            Assert.AreEqual(12.5, v[0].Start, 1e-9, "мгновенное: начало = время");
        }

        [Test]
        public void BeforeStart_TimeIsZero()
        {
            _director.Raise(Violation.StepOrder, Severity.Minor);
            Assert.AreEqual(0, _director.Violations[0].Time);
        }

        [Test]
        public void Result_HasViolationsInOrder_WithTimes()
        {
            ExerciseResult result = null;
            _director.Finished += r => result = r;
            _director.UpdateFacts(InHand());
            _clock.Now = 1010; _director.Raise(Violation.FingerOnTrigger, Severity.Minor, 0.5);
            _clock.Now = 1020; _director.Raise(Violation.MuzzleOutOfSector, Severity.Minor, 1.2);
            _clock.Now = 1030; _director.Raise(Violation.LookIntoMuzzle, Severity.Critical, 0.3);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Violations.Count);
            for (int i = 1; i < result.Violations.Count; i++)
                Assert.LessOrEqual(result.Violations[i - 1].Time, result.Violations[i].Time, "по времени");
            Assert.AreEqual(30, result.Violations[2].Time, 1e-9);
            Assert.AreEqual(30, result.TotalTime, 1e-9);
        }

        [Test]
        public void Start_NeverNegative()
        {
            var r = new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 0.3, 0.5);
            Assert.AreEqual(0, r.Start);
        }
    }
}
