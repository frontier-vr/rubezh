using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Осечка на 3-м или 4-м выстреле и её устранение (FR-22, VP-09):
    // детерминизм через генератор с зерном, время через IClock, порог 5 с из ExerciseRules.
    public class JamTests
    {
        private sealed class FakeClock : IClock { public double Now { get; set; } }

        private sealed class FixedRandom : IRandom
        {
            private readonly int _value;
            public FixedRandom(int value) { _value = value; }
            public int Next(int minInclusive, int maxExclusive) => _value;
        }

        private FakeClock _clock;
        private ExerciseDirector _director;
        private Weapon _weapon;
        private JamRule _rule;
        private List<ViolationRecord> _raised;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _director = new ExerciseDirector(_clock);
            // Магазин на 5, патрон дослан, предохранитель снят — как в фазе «Огонь».
            _weapon = new Weapon(new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None));
            _rule = new JamRule(_director, _clock, _weapon);
            _raised = new List<ViolationRecord>();
            _director.ViolationRaised += _raised.Add;
            _events = new List<string>();
            _weapon.Fired += () => _events.Add("Fired");
            _weapon.DryFire += () => _events.Add("DryFire");
            _weapon.Jammed += j => _events.Add("Jammed:" + j);
        }

        [TearDown]
        public void TearDown() => _rule.Dispose();

        private void Shoot()
        {
            Assert.IsTrue(_weapon.Apply(WeaponCommand.TriggerPull));
            Assert.IsTrue(_weapon.Apply(WeaponCommand.TriggerRelease));
        }

        private void ClearJam()
        {
            Assert.IsTrue(_weapon.Apply(WeaponCommand.SlideBack));
            Assert.IsTrue(_weapon.Apply(WeaponCommand.SlideForward));
        }

        [TestCase(3)]
        [TestCase(4)]
        public void Misfire_HappensOnScheduledShot(int shot)
        {
            Assert.AreEqual(shot, _rule.Arm(new FixedRandom(shot)));
            for (int i = 1; i < shot; i++) Shoot();
            _events.Clear();

            Shoot();

            CollectionAssert.AreEqual(new[] { "DryFire", "Jammed:Misfire" }, _events, "осечка: сухой щелчок, без выстрела и отдачи");
            Assert.AreEqual(Jam.Misfire, _weapon.State.Jam);
            Assert.AreEqual(Chamber.Loaded, _weapon.State.Chamber, "патрон остаётся в патроннике");
            Assert.IsTrue(_rule.IsJammed);
        }

        [Test]
        public void Misfire_IsDeterministicForSeed()
        {
            var a = new JamRule(_director, _clock, new Weapon()).Arm(new SeededRandom(42));
            var b = new JamRule(_director, _clock, new Weapon()).Arm(new SeededRandom(42));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Misfire_IsAlwaysOnThirdOrFourthShot()
        {
            var seen = new HashSet<int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                int shot = new JamRule(_director, _clock, new Weapon()).Arm(new SeededRandom(seed));
                Assert.That(shot, Is.InRange(3, 4));
                seen.Add(shot);
            }
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, seen, "по разным зёрнам встречаются оба варианта");
        }

        [Test]
        public void TriggerAgainWhileJammed_IsOnlyDryFire()
        {
            _rule.Arm(new FixedRandom(1));
            Shoot();
            _events.Clear();
            Shoot();
            CollectionAssert.AreEqual(new[] { "DryFire" }, _events);
        }

        [Test]
        public void ClearingAfterWait_GivesNoViolation_AndFiringContinues()
        {
            _rule.Arm(new FixedRandom(3));
            Shoot(); Shoot();
            _clock.Now = 10; Shoot();           // осечка
            _clock.Now = 15.2;                  // выждали 5,2 с
            ClearJam();

            Assert.IsEmpty(_raised);
            Assert.AreEqual(Jam.None, _weapon.State.Jam);
            Assert.AreEqual(Chamber.Loaded, _weapon.State.Chamber, "дослан следующий патрон");
            _events.Clear();
            Shoot();
            Assert.Contains("Fired", _events);
        }

        [Test]
        public void ClearingTooEarly_GivesJamMishandled()
        {
            _rule.Arm(new FixedRandom(3));
            Shoot(); Shoot();
            _clock.Now = 10; Shoot();
            _clock.Now = 12;                    // только 2 с
            ClearJam();

            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.JamMishandled, _raised[0].Type);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.AreEqual(2, _raised[0].Duration, 1e-9);
            Assert.AreNotEqual(Phase.Failed, _director.Phase, "мелкое: упражнение продолжается");
        }

        [Test]
        public void WaitThreshold_ComesFromRules()
        {
            _rule.Dispose();
            _director = new ExerciseDirector(_clock, new ExerciseRules { MisfireWaitSeconds = 1 });
            _director.ViolationRaised += _raised.Add;
            _rule = new JamRule(_director, _clock, _weapon);
            _rule.Arm(new FixedRandom(1));
            _clock.Now = 0; Shoot();
            _clock.Now = 1.5; ClearJam();
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void NoMisfireScheduled_AllShotsFire()
        {
            int fired = 0;
            _weapon.Fired += () => fired++;
            for (int i = 0; i < 5; i++) Shoot();
            Assert.AreEqual(5, fired);
        }

        [Test]
        public void Rearm_ResetsShotCounter()
        {
            _rule.Arm(new FixedRandom(3));
            Shoot(); Shoot();
            _rule.Arm(new FixedRandom(3));     // Restart упражнения
            Shoot(); Shoot();
            Assert.AreEqual(Jam.None, _weapon.State.Jam);
            Shoot();
            Assert.AreEqual(Jam.Misfire, _weapon.State.Jam);
        }
    }
}
