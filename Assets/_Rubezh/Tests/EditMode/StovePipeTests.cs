using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Прихват гильзы (FR-23, VP-13, NG-29): строка 13 автомата из docs/objects.md, назначение
    // генератором с зерном, правильное устранение и ошибки при нём; связь с LookIntoMuzzle.
    public class StovePipeTests
    {
        private sealed class FakeClock : IClock { public double Now { get; set; } }

        // Отдаёт значения по очереди: первый — выстрел осечки, дальше — розыгрыш прихвата.
        private sealed class SequenceRandom : IRandom
        {
            private readonly Queue<int> _values;
            public SequenceRandom(params int[] values) { _values = new Queue<int>(values); }
            public int Next(int minInclusive, int maxExclusive) => _values.Count > 0 ? _values.Dequeue() : minInclusive;
        }

        private FakeClock _clock;
        private ExerciseDirector _director;
        private Weapon _weapon;
        private JamRule _rule;
        private List<ViolationRecord> _raised;
        private List<string> _events;

        private static ExerciseRules Rules(double chance = 1) => new ExerciseRules { StovePipeChance = chance };

        [SetUp]
        public void SetUp() => Build(Rules());

        private void Build(ExerciseRules rules)
        {
            _rule?.Dispose();
            _clock = new FakeClock();
            _director = new ExerciseDirector(_clock, rules);
            // Магазин на 5: четыре в магазине, один в патроннике, предохранитель снят — фаза «Огонь».
            _weapon = new Weapon(new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None));
            _rule = new JamRule(_director, _clock, _weapon);
            _raised = new List<ViolationRecord>();
            _director.ViolationRaised += _raised.Add;
            _events = new List<string>();
            _weapon.Fired += () => _events.Add("Fired");
            _weapon.DryFire += () => _events.Add("DryFire");
            _weapon.Jammed += j => _events.Add("Jammed:" + j);
            _weapon.CasingEjected += () => _events.Add("Casing");
        }

        [TearDown]
        public void TearDown() => _rule.Dispose();

        private void Shoot()
        {
            Assert.IsTrue(_weapon.Apply(WeaponCommand.TriggerPull));
            Assert.IsTrue(_weapon.Apply(WeaponCommand.TriggerRelease));
        }

        private void Rack()
        {
            Assert.IsTrue(_weapon.Apply(WeaponCommand.SlideBack));
            Assert.IsTrue(_weapon.Apply(WeaponCommand.SlideForward));
        }

        // Осечка на 5-м (до неё не дойдём), прихват на shot-м.
        private void ArmStovePipe(int shot) => _rule.Arm(new SequenceRandom(5, 0, shot));

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void StovePipe_HappensOnScheduledShot_ShotFiredCasingStuck(int shot)
        {
            ArmStovePipe(shot);
            Assert.AreEqual(shot, _rule.StovePipeShot);
            for (int i = 1; i < shot; i++) Shoot();
            int rounds = _weapon.State.RoundsInMagazine;
            _events.Clear();

            Shoot();

            CollectionAssert.AreEqual(new[] { "Fired", "Jammed:StovePipe" }, _events,
                "выстрел был, гильза не вылетела — зажата затвором");
            Assert.AreEqual(Jam.StovePipe, _weapon.State.Jam);
            Assert.AreEqual(Chamber.Spent, _weapon.State.Chamber, "в патроннике стреляная гильза");
            Assert.AreEqual(rounds, _weapon.State.RoundsInMagazine, "следующий патрон не дослан");
            Assert.IsTrue(_rule.IsStovePipe);
            Assert.IsTrue(_rule.IsJammed);
        }

        [Test]
        public void TriggerOnStovePipe_IsDryFire_AndJamMishandledOnce()
        {
            ArmStovePipe(2);
            Shoot(); Shoot();                       // прихват
            _events.Clear();
            Shoot(); Shoot(); Shoot();              // жмёт на спуск снова и снова

            CollectionAssert.AreEqual(new[] { "DryFire", "DryFire", "DryFire" }, _events, "выстрелов нет");
            Assert.AreEqual(1, _raised.Count, "одно нарушение на одну задержку");
            Assert.AreEqual(Violation.JamMishandled, _raised[0].Type);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.AreNotEqual(Phase.Failed, _director.Phase, "мелкое: упражнение продолжается");
        }

        [Test]
        public void RackRightAway_EjectsCasing_NoViolation_FiringContinues()
        {
            ArmStovePipe(2);
            Shoot();
            _clock.Now = 10; Shoot();               // прихват
            _clock.Now = 10.4;                      // сразу, ждать не нужно
            _events.Clear();
            Rack();

            CollectionAssert.AreEqual(new[] { "Casing" }, _events, "гильза вылетела на перезарядке");
            Assert.IsEmpty(_raised, "прихват устраняют сразу — паузы, как у осечки, нет");
            Assert.AreEqual(Jam.None, _weapon.State.Jam);
            Assert.AreEqual(Chamber.Loaded, _weapon.State.Chamber, "дослан следующий патрон");
            Assert.IsFalse(_rule.IsStovePipe);
            _events.Clear();
            Shoot();
            CollectionAssert.AreEqual(new[] { "Fired", "Casing" }, _events);
        }

        [Test]
        public void SecondStovePipeMistake_CountedAgainAfterNewJam()
        {
            // Прихват на 2-м и, после Restart, снова на 2-м: на каждую задержку — своё нарушение.
            ArmStovePipe(2);
            Shoot(); Shoot(); Shoot();              // прихват и лишний спуск
            Rack();
            ArmStovePipe(2);
            Shoot(); Shoot(); Shoot();
            Assert.AreEqual(2, _raised.Count);
        }

        // Прихват учит не заглядывать в ствол: взгляд в дульный срез при устранении — грубое.
        [Test]
        public void LookingIntoMuzzle_WhileClearing_IsGross()
        {
            var look = new MuzzleLookRule(_director, _clock);
            ArmStovePipe(2);
            Shoot(); Shoot();                       // прихват
            _clock.Now = 1.0; look.Tick(true);      // заглянул «что там застряло»
            _clock.Now = 1.3; look.Tick(true);

            Assert.AreEqual(Violation.LookIntoMuzzle, _raised[_raised.Count - 1].Type);
            Assert.AreEqual(Severity.Critical, _raised[_raised.Count - 1].Severity);
            Assert.AreEqual(Phase.Failed, _director.Phase);
        }

        [Test]
        public void MisfireAndStovePipe_InOneSeries()
        {
            int fired = 0;
            _weapon.Fired += () => fired++;
            _rule.Arm(new SequenceRandom(3, 0, 4));    // осечка на 3-м, прихват на 4-м
            Assert.AreEqual(3, _rule.MisfireShot);
            Assert.AreEqual(4, _rule.StovePipeShot);

            Shoot(); Shoot();
            _clock.Now = 10; Shoot();                  // осечка
            Assert.AreEqual(Jam.Misfire, _weapon.State.Jam);
            _clock.Now = 15.5; Rack();                 // выждали, перезарядили
            Shoot();                                   // прихват
            Assert.AreEqual(Jam.StovePipe, _weapon.State.Jam);
            Rack();
            Shoot();                                   // последний патрон, затвор на задержку
            Assert.AreEqual(4, fired, "5 патронов: 4 выстрела и осечный патрон");
            Assert.IsTrue(_weapon.State.SlideLocked);
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void StovePipeNeverOnMisfireShot()
        {
            _rule.Arm(new SequenceRandom(4, 0, 4));    // розыгрыш попал на выстрел осечки
            Assert.AreEqual(4, _rule.MisfireShot);
            Assert.AreNotEqual(_rule.MisfireShot, _rule.StovePipeShot);
            Assert.AreEqual(5, _rule.StovePipeShot, "сдвинут на следующий выстрел");
            _rule.Arm(new SequenceRandom(3, 0, 5));
            Assert.AreEqual(5, _rule.StovePipeShot);
            _rule.Arm(new SequenceRandom(4, 0, 5));
            Assert.AreEqual(5, _rule.StovePipeShot);
        }

        [Test]
        public void Deterministic_ForSeed()
        {
            Build(Rules(0.5));
            for (int seed = 1; seed <= 50; seed++)
            {
                var a = new JamRule(_director, _clock, new Weapon());
                var b = new JamRule(_director, _clock, new Weapon());
                a.Arm(new SeededRandom(seed));
                b.Arm(new SeededRandom(seed));
                Assert.AreEqual(a.MisfireShot, b.MisfireShot);
                Assert.AreEqual(a.StovePipeShot, b.StovePipeShot, "seed " + seed);
                a.Dispose(); b.Dispose();
            }
        }

        [Test]
        public void OverSeeds_AboutHalf_InRange_NotOnMisfire()
        {
            Build(Rules(0.5));
            int with = 0;
            var shots = new HashSet<int>();
            for (int seed = 1; seed <= 500; seed++)
            {
                var r = new JamRule(_director, _clock, new Weapon());
                r.Arm(new SeededRandom(seed));
                if (r.StovePipeShot != 0)
                {
                    with++;
                    shots.Add(r.StovePipeShot);
                    Assert.That(r.StovePipeShot, Is.InRange(2, 5));
                    Assert.AreNotEqual(r.MisfireShot, r.StovePipeShot);
                }
                r.Dispose();
            }
            Assert.That(with, Is.InRange(200, 300), "вероятность 0,5 из правил");
            Assert.Greater(shots.Count, 2, "прихват выпадает на разные выстрелы");
        }

        [Test]
        public void ChanceZero_NoStovePipe()
        {
            Build(new ExerciseRules());                 // по умолчанию в Core прихват выключен
            for (int seed = 1; seed <= 100; seed++)
            {
                _rule.Arm(new SeededRandom(seed));
                Assert.AreEqual(0, _rule.StovePipeShot);
            }
        }

        [Test]
        public void Rearm_ClearsStovePipeState()
        {
            ArmStovePipe(2);
            Shoot(); Shoot();
            Assert.IsTrue(_rule.IsStovePipe);
            _rule.Arm(new SequenceRandom(5, 0, 3));     // Restart: новый розыгрыш
            Assert.IsFalse(_rule.IsStovePipe, "задержка прошлого упражнения забыта");
            Assert.AreEqual(3, _rule.StovePipeShot);
        }
    }
}
