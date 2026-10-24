using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Директор упражнения: порядок фаз, StepOrder, Failed с причиной.
    // Мир собирается из настоящего автомата Weapon, время — FakeClock.
    public class ExerciseDirectorTests
    {
        private sealed class FakeClock : IClock
        {
            public double Now { get; set; }
        }

        // Мир упражнения: оружие и места предметов, из них снимок ExerciseFacts.
        private sealed class World
        {
            public readonly Weapon Weapon = new Weapon();
            public bool InHand;
            public int MagazineRounds;
            public Place Pistol = Place.Cabinet;
            public Place Magazine = Place.InWeapon;
            public bool Earmuffs, Glasses, Target;

            public ExerciseFacts Facts => new ExerciseFacts(InHand, Weapon.State, MagazineRounds,
                Pistol, Magazine, Earmuffs, Glasses, Target);
        }

        private FakeClock _clock;
        private ExerciseDirector _director;
        private World _world;
        private List<Phase> _phases;
        private List<ViolationRecord> _violations;
        private List<ExerciseResult> _finished;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock { Now = 100 };
            _director = new ExerciseDirector(_clock);
            _world = new World();
            _phases = new List<Phase>();
            _violations = new List<ViolationRecord>();
            _finished = new List<ExerciseResult>();
            _director.PhaseChanged += (from, to) => _phases.Add(to);
            _director.ViolationRaised += v => _violations.Add(v);
            _director.Finished += r => _finished.Add(r);
        }

        private void Push() => _director.UpdateFacts(_world.Facts);

        private void Apply(params WeaponCommand[] commands)
        {
            foreach (var c in commands) Assert.IsTrue(_world.Weapon.Apply(c), c.ToString());
        }

        // Шаги 1–9 по docs/user-flow.md §4.1. Каждый шаг доводит мир до закрытия своей фазы.
        private void Receive() { _world.InHand = true; _world.Pistol = Place.Hand; Push(); }

        private void Inspect()
        {
            Apply(WeaponCommand.EjectMagazine);
            _world.Magazine = Place.InspectShelf;
            Push();
            Apply(WeaponCommand.SlideBack);     // пустой магазин снят — затвор сам встаёт на задержку
            Push();
        }

        private void Ppe() { _world.Earmuffs = true; Push(); _world.Glasses = true; Push(); }

        private void LoadMagazine()
        {
            _world.Magazine = Place.PrepTable;
            for (int i = 0; i < 5; i++)
            {
                _director.ReportAction(ExerciseAction.InsertRound);
                _world.MagazineRounds++;
                Push();
            }
        }

        private void ToLine()
        {
            _world.Pistol = Place.LineStand; _world.InHand = false; Push();
            _world.Magazine = Place.LineStand; Push();
            _director.ReportAction(ExerciseAction.PressTargetButton);
            _world.Target = true; Push();
        }

        private void Load()
        {
            _world.Pistol = Place.Hand; _world.InHand = true;
            _director.ReportAction(ExerciseAction.InsertMagazine);
            Assert.IsTrue(_world.Weapon.InsertMagazine(_world.MagazineRounds));
            _world.Magazine = Place.InWeapon; _world.MagazineRounds = 0;
            Push();
        }

        private void Fire()
        {
            Apply(WeaponCommand.SlideStopRelease, WeaponCommand.SafetyOff);   // дослать патрон
            Push();
            for (int i = 0; i < 5; i++)
            {
                Apply(WeaponCommand.TriggerPull, WeaponCommand.TriggerRelease);
                _director.RegisterShot(hit: i != 2);
                Push();
            }
        }

        private void Cease() { Apply(WeaponCommand.SafetyOn); Push(); }

        private void Unload() { Apply(WeaponCommand.EjectMagazine); _world.Magazine = Place.Hand; Push(); }

        private void ShowClear()
        {
            _world.Pistol = Place.LineStand; _world.InHand = false; Push();
            _world.Magazine = Place.LineStand; Push();
        }

        private void Return()
        {
            _world.InHand = true; _world.Pistol = Place.Hand; Push();
            _director.ReportAction(ExerciseAction.Teleport, Anchor.A1);
            _director.ReportAction(ExerciseAction.CabinetAttempt);
            _world.InHand = false; _world.Pistol = Place.Cabinet; Push();
        }

        private static readonly Phase[] FullOrder =
        {
            Phase.Inspect, Phase.Ppe, Phase.LoadMagazine, Phase.ToLine, Phase.Load, Phase.Fire,
            Phase.Cease, Phase.Unload, Phase.ShowClear, Phase.Return, Phase.Done
        };

        [Test]
        public void NewDirector_StartsInReceive()
        {
            Assert.AreEqual(Phase.Receive, _director.Phase);
            Assert.IsTrue(_director.IsRunning);
            Assert.AreEqual(0, _director.Elapsed);
        }

        [Test]
        public void FullCycle_GoesThroughAllPhasesInOrder_AndEndsDone()
        {
            Receive(); Inspect(); Ppe(); LoadMagazine(); ToLine(); Load();
            Fire(); Cease(); Unload(); ShowClear(); Return();

            CollectionAssert.AreEqual(FullOrder, _phases);
            Assert.AreEqual(Phase.Done, _director.Phase);
            Assert.IsEmpty(_violations);
            Assert.AreEqual(1, _finished.Count);
            Assert.IsFalse(_finished[0].FailedCritical);
            Assert.IsNull(_finished[0].FailReason);
            Assert.AreEqual(5, _finished[0].Shots);
            Assert.AreEqual(4, _finished[0].Hits);
        }

        [Test]
        public void EachStep_ClosesExactlyItsPhase()
        {
            var steps = new System.Action[]
            {
                Receive, Inspect, Ppe, LoadMagazine, ToLine, Load, Fire, Cease, Unload, ShowClear, Return
            };
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i]();
                Assert.AreEqual(FullOrder[i], _director.Phase, $"после шага {i + 1}");
            }
        }

        [Test]
        public void Timer_StartsWhenPistolTaken_StopsAtDone()
        {
            _clock.Now = 110;
            Receive();
            Assert.AreEqual(0, _director.Elapsed);

            _clock.Now = 170;
            Assert.AreEqual(60, _director.Elapsed, 1e-9);

            Inspect(); Ppe(); LoadMagazine(); ToLine(); Load(); Fire(); Cease(); Unload(); ShowClear();
            _clock.Now = 300;
            Return();
            _clock.Now = 999;

            Assert.AreEqual(190, _finished[0].TotalTime, 1e-9);
            Assert.AreEqual(190, _director.Elapsed, 1e-9);
        }

        [Test]
        public void Inspect_NotClosed_WhileMagazineInserted()
        {
            Receive();
            Apply(WeaponCommand.SlideBack);     // затвор на задержке, патронник пуст, но магазин в шахте
            Push();
            Assert.AreEqual(Phase.Inspect, _director.Phase);
        }

        [Test]
        public void Ppe_NeedsBothEarmuffsAndGlasses()
        {
            Receive(); Inspect();
            _world.Earmuffs = true; Push();
            Assert.AreEqual(Phase.Ppe, _director.Phase);
            _world.Glasses = true; Push();
            Assert.AreEqual(Phase.LoadMagazine, _director.Phase);
        }

        [Test]
        public void LoadMagazine_NeedsFullMagazineOutsideWeapon()
        {
            Receive(); Inspect(); Ppe();
            _world.MagazineRounds = 4; Push();
            Assert.AreEqual(Phase.LoadMagazine, _director.Phase);
            _world.MagazineRounds = 5; Push();
            Assert.AreEqual(Phase.ToLine, _director.Phase);
        }

        [Test]
        public void ToLine_NeedsTargetSupplied()
        {
            Receive(); Inspect(); Ppe(); LoadMagazine();
            _world.Pistol = Place.LineStand; _world.InHand = false; _world.Magazine = Place.LineStand; Push();
            Assert.AreEqual(Phase.ToLine, _director.Phase);
            _world.Target = true; Push();
            Assert.AreEqual(Phase.Load, _director.Phase);
        }

        [Test]
        public void ShowClear_NeedsPistolAndMagazineOnStand()
        {
            Receive(); Inspect(); Ppe(); LoadMagazine(); ToLine(); Load(); Fire(); Cease(); Unload();
            _world.Pistol = Place.LineStand; Push();
            Assert.AreEqual(Phase.ShowClear, _director.Phase);
            _world.Magazine = Place.LineStand; Push();
            Assert.AreEqual(Phase.Return, _director.Phase);
        }

        [Test]
        public void ConditionsAlreadyMet_CloseSeveralPhasesInOneUpdate()
        {
            Receive();
            _world.Earmuffs = _world.Glasses = true;   // СИЗ надели до конца осмотра
            Inspect();
            Assert.AreEqual(Phase.LoadMagazine, _director.Phase);
        }

        [Test]
        public void SeriesRounds_ComesFromRules()
        {
            var director = new ExerciseDirector(_clock, new ExerciseRules { SeriesRounds = 3 });
            _director = director;
            Receive(); Inspect(); Ppe();
            _world.MagazineRounds = 3; Push();
            Assert.AreEqual(Phase.ToLine, director.Phase);
        }

        // StepOrder: действие следующей фазы раньше времени.

        [Test]
        public void InsertMagazine_DuringInspect_GivesStepOrder()
        {
            Receive();
            _director.ReportAction(ExerciseAction.InsertMagazine);

            Assert.AreEqual(1, _violations.Count);
            Assert.AreEqual(Violation.StepOrder, _violations[0].Type);
            Assert.AreEqual(Severity.Minor, _violations[0].Severity);
            Assert.AreEqual(Phase.Inspect, _director.Phase, "мелкое нарушение упражнение не останавливает");
        }

        [Test]
        public void InsertRound_BeforeLoadMagazine_GivesStepOrder()
        {
            Receive(); Inspect();
            _director.ReportAction(ExerciseAction.InsertRound);
            Assert.AreEqual(Violation.StepOrder, _violations[0].Type);
        }

        [Test]
        public void InsertMagazine_AfterCease_GivesStepOrder()
        {
            Receive(); Inspect(); Ppe(); LoadMagazine(); ToLine(); Load(); Fire();
            _director.ReportAction(ExerciseAction.InsertMagazine);
            Assert.AreEqual(Violation.StepOrder, _violations[0].Type);
        }

        [Test]
        public void CabinetAttempt_BeforeReturn_GivesStepOrder()
        {
            Receive(); Inspect(); Ppe();
            _director.ReportAction(ExerciseAction.CabinetAttempt);
            Assert.AreEqual(Violation.StepOrder, _violations[0].Type);
        }

        [Test]
        public void SameStepOrder_InSamePhase_IsCountedOnce()
        {
            Receive();
            _director.ReportAction(ExerciseAction.InsertMagazine);
            _director.ReportAction(ExerciseAction.InsertMagazine);
            _director.ReportAction(ExerciseAction.InsertMagazine);
            Assert.AreEqual(1, _violations.Count);
        }

        [Test]
        public void ActionsInTheirOwnPhase_GiveNoViolation()
        {
            Receive(); Inspect(); Ppe(); LoadMagazine(); ToLine(); Load(); Fire(); Cease(); Unload(); ShowClear(); Return();
            Assert.IsEmpty(_violations);
        }

        [Test]
        public void PpeDuringInspect_GivesIncompleteInspection_Once()
        {
            Receive();
            _director.ReportAction(ExerciseAction.PutOnEarmuffs);
            _director.ReportAction(ExerciseAction.PutOnGlasses);
            _director.ReportAction(ExerciseAction.Teleport, Anchor.A2);

            Assert.AreEqual(1, _violations.Count);
            Assert.AreEqual(Violation.IncompleteInspection, _violations[0].Type);
        }

        [Test]
        public void TeleportAwayDuringInspect_GivesIncompleteInspection()
        {
            Receive();
            _director.ReportAction(ExerciseAction.Teleport, Anchor.A3);
            Assert.AreEqual(Violation.IncompleteInspection, _violations[0].Type);
        }

        // Грубое нарушение: Failed с причиной.

        [Test]
        public void CriticalViolation_MovesToFailed_WithReason()
        {
            Receive(); Inspect();
            _clock.Now = 130;
            _director.Raise(Violation.LookIntoMuzzle, Severity.Critical);

            Assert.AreEqual(Phase.Failed, _director.Phase);
            Assert.AreEqual(Phase.Failed, _phases[_phases.Count - 1]);
            Assert.AreEqual(1, _finished.Count);
            var result = _finished[0];
            Assert.IsTrue(result.FailedCritical);
            Assert.AreEqual(Violation.LookIntoMuzzle, result.FailReason);
            Assert.AreEqual(Verdict.NotPassed, result.Verdict);
            Assert.AreEqual(0, result.Score);
            Assert.AreEqual(30, result.TotalTime, 1e-9);
            Assert.AreEqual(1, result.Violations.Count);
        }

        [TestCase(Violation.MuzzleOutOfSector)]
        [TestCase(Violation.ShotBeforeFire)]
        [TestCase(Violation.ShotAfterCease)]
        [TestCase(Violation.ShotOutOfSector)]
        public void AnyCriticalViolation_FailsWithItsReason(Violation reason)
        {
            Receive(); Inspect(); Ppe(); LoadMagazine(); ToLine(); Load();
            _director.Raise(reason, Severity.Critical);
            Assert.AreEqual(reason, _director.Result.FailReason);
        }

        [Test]
        public void MinorViolation_IsRecordedWithTime_AndExerciseContinues()
        {
            Receive();
            _clock.Now = 112.5;
            _director.Raise(Violation.FingerOnTrigger, Severity.Minor, duration: 0.8);

            Assert.AreEqual(Phase.Inspect, _director.Phase);
            Assert.AreEqual(12.5, _violations[0].Time, 1e-9);
            Assert.AreEqual(0.8, _violations[0].Duration, 1e-9);
            Assert.IsEmpty(_finished);
        }

        [Test]
        public void AfterFailed_NothingIsRecorded_AndPhasesDoNotMove()
        {
            Receive();
            _director.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            int violations = _violations.Count;

            _director.Raise(Violation.FingerOnTrigger, Severity.Minor);
            _director.ReportAction(ExerciseAction.InsertMagazine);
            Inspect();

            Assert.AreEqual(Phase.Failed, _director.Phase);
            Assert.AreEqual(violations, _violations.Count);
            Assert.AreEqual(1, _finished.Count);
        }

        [Test]
        public void AfterDone_ViolationsAreIgnored()
        {
            Receive(); Inspect(); Ppe(); LoadMagazine(); ToLine(); Load(); Fire(); Cease(); Unload(); ShowClear(); Return();
            _director.Raise(Violation.ShotAfterCease, Severity.Critical);
            Assert.AreEqual(Phase.Done, _director.Phase);
            Assert.IsEmpty(_violations);
        }

        [Test]
        public void Restart_ReturnsToReceive_AndClearsEverything()
        {
            Receive(); Inspect();
            _director.Raise(Violation.FingerOnTrigger, Severity.Minor);
            _director.Raise(Violation.LookIntoMuzzle, Severity.Critical);

            _director.Restart();

            Assert.AreEqual(Phase.Receive, _director.Phase);
            Assert.AreEqual(Phase.Receive, _phases[_phases.Count - 1]);
            Assert.IsEmpty(_director.Violations);
            Assert.IsNull(_director.Result);
            Assert.AreEqual(0, _director.Elapsed);
            Assert.IsTrue(_director.IsRunning);
        }
    }
}
