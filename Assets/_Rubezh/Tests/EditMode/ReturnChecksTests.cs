using System.Linq;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Проверки VP-11: СИЗ на рубеже, гильзы при сдаче, попытка сдать заряженное — один раз на подход.
    public class ReturnChecksTests
    {
        private sealed class Clock : IClock { public double Now { get; set; } }

        private Clock _clock;
        private ExerciseDirector _d;

        private static readonly WeaponState Cabinet = new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None);
        private static readonly WeaponState Clear = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
        private static readonly WeaponState Loaded = new WeaponState(true, 3, Chamber.Loaded, false, true, Jam.None);
        private static readonly WeaponState FiredOut = new WeaponState(true, 0, Chamber.Empty, true, true, Jam.None);

        [SetUp]
        public void SetUp()
        {
            _clock = new Clock();
            _d = new ExerciseDirector(_clock);
        }

        private void Facts(bool inHand, WeaponState w, int rounds, Place pistol, Place mag, bool ear, bool gl, bool target, int casings = 0) =>
            _d.UpdateFacts(new ExerciseFacts(inHand, w, rounds, pistol, mag, ear, gl, target, casings));

        // Довести упражнение до фазы ToLine (СИЗ надеты, магазин снаряжён).
        private void ToLine()
        {
            Facts(true, Cabinet, 0, Place.Hand, Place.InWeapon, false, false, false);
            Facts(true, Clear, 0, Place.Hand, Place.InspectShelf, false, false, false);
            Facts(true, Clear, 0, Place.Hand, Place.InspectShelf, true, true, false);
            Facts(true, Clear, 5, Place.Hand, Place.PrepTable, true, true, false);
            Assert.AreEqual(Phase.ToLine, _d.Phase);
        }

        private int Count(Violation v) => _d.Violations.Count(r => r.Type == v);

        [Test]
        public void PpeOn_AtLine_NoViolation()
        {
            ToLine();
            Facts(false, Clear, 5, Place.LineStand, Place.LineStand, true, true, true);
            Assert.AreEqual(Phase.Load, _d.Phase);
            Assert.AreEqual(0, Count(Violation.NoEarmuffs) + Count(Violation.NoGlasses));
        }

        [Test]
        public void TeleportToLine_WithoutGlasses_IsNoGlasses_Once()
        {
            ToLine();
            Facts(true, Clear, 5, Place.Hand, Place.Hand, true, false, false);    // очки сняли
            _d.ReportAction(ExerciseAction.Teleport, Anchor.A3);
            _d.ReportAction(ExerciseAction.Teleport, Anchor.A3);
            Facts(false, Clear, 5, Place.LineStand, Place.LineStand, true, false, true);
            Assert.AreEqual(1, Count(Violation.NoGlasses), "один раз за упражнение");
            Assert.AreEqual(0, Count(Violation.NoEarmuffs));
            Assert.AreEqual(Severity.Minor, _d.Violations.First(r => r.Type == Violation.NoGlasses).Severity);
        }

        [Test]
        public void LeavingToLine_WithoutEarmuffs_IsNoEarmuffs()
        {
            ToLine();
            Facts(false, Clear, 5, Place.LineStand, Place.LineStand, false, true, true);
            Assert.AreEqual(1, Count(Violation.NoEarmuffs));
        }

        [Test]
        public void Restart_ChecksPpeAgain()
        {
            ToLine();
            _d.ReportAction(ExerciseAction.Teleport, Anchor.A3);
            _d.Restart();
            ToLine();
            Facts(false, Clear, 5, Place.LineStand, Place.LineStand, false, false, true);
            Assert.AreEqual(1, Count(Violation.NoEarmuffs));
            Assert.AreEqual(1, Count(Violation.NoGlasses));
        }

        private void ToReturn(int casings)
        {
            ToLine();
            Facts(false, Clear, 5, Place.LineStand, Place.LineStand, true, true, true);           // → Load
            Facts(true, Loaded, 3, Place.Hand, Place.InWeapon, true, true, true);                // → Fire
            var fired = new WeaponState(true, 0, Chamber.Empty, true, false, Jam.None);
            Facts(true, fired, 0, Place.Hand, Place.InWeapon, true, true, true);                 // серия кончилась → Cease
            Facts(true, FiredOut, 0, Place.Hand, Place.InWeapon, true, true, true);              // предохранитель → Unload
            Facts(true, Clear, 0, Place.Hand, Place.Hand, true, true, true);                     // разряжен → ShowClear
            Facts(false, Clear, 0, Place.LineStand, Place.LineStand, true, true, true, casings); // → Return
            Assert.AreEqual(Phase.Return, _d.Phase);
        }

        [Test]
        public void Return_WithCasingsOutsideCatcher_IsCasingLeft()
        {
            ToReturn(2);
            Facts(false, Clear, 0, Place.Cabinet, Place.LineStand, true, true, true, 2);
            Assert.AreEqual(Phase.Done, _d.Phase);
            Assert.AreEqual(1, Count(Violation.CasingLeft));
            Assert.Less(_d.Result.Score, 100, "штраф CasingLeft в балле");
        }

        [Test]
        public void Return_AllCasingsInCatcher_NoCasingLeft()
        {
            ToReturn(0);
            Facts(false, Clear, 0, Place.Cabinet, Place.LineStand, true, true, true, 0);
            Assert.AreEqual(Phase.Done, _d.Phase);
            Assert.AreEqual(0, Count(Violation.CasingLeft));
        }

        [Test]
        public void ReturnLoaded_OneViolationPerApproach()
        {
            ToReturn(0);
            var rule = new ReturnAttemptRule(_d);
            int refused = 0;
            rule.Refused += _ => refused++;
            for (int i = 0; i < 5; i++) rule.Approach(Loaded);      // фильтр/наведение много раз за подход
            Assert.AreEqual(1, Count(Violation.ReturnedLoaded));
            Assert.AreEqual(1, refused, "одна подсказка и вибрация за подход");
            rule.Leave();
            rule.Approach(Loaded);                                   // второй подход
            Assert.AreEqual(2, Count(Violation.ReturnedLoaded));
            Assert.IsFalse(ReturnAttemptRule.Accepts(Loaded));
        }

        [Test]
        public void ReturnClear_IsAccepted_NoViolation()
        {
            ToReturn(0);
            var rule = new ReturnAttemptRule(_d);
            rule.Approach(Clear);
            Assert.IsTrue(ReturnAttemptRule.Accepts(Clear));
            Assert.AreEqual(0, Count(Violation.ReturnedLoaded));
            Assert.AreEqual(0, Count(Violation.StepOrder));
        }

        [Test]
        public void CabinetAttempt_BeforeReturn_IsStepOrder_NotReturnedLoaded()
        {
            ToLine();
            var rule = new ReturnAttemptRule(_d);
            rule.Approach(Loaded);
            Assert.AreEqual(1, Count(Violation.StepOrder));
            Assert.AreEqual(0, Count(Violation.ReturnedLoaded));
        }
    }
}
