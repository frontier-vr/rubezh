using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Таблица переходов автомата оружия. Номера строк — из карточки пистолета в docs/objects.md.
    public class WeaponTransitionTests
    {
        private const WeaponCommand Eject = WeaponCommand.EjectMagazine;
        private const WeaponCommand Back = WeaponCommand.SlideBack;
        private const WeaponCommand Forward = WeaponCommand.SlideForward;
        private const WeaponCommand StopEngage = WeaponCommand.SlideStopEngage;
        private const WeaponCommand StopRelease = WeaponCommand.SlideStopRelease;
        private const WeaponCommand SafeOn = WeaponCommand.SafetyOn;
        private const WeaponCommand SafeOff = WeaponCommand.SafetyOff;
        private const WeaponCommand Pull = WeaponCommand.TriggerPull;
        private const WeaponCommand Release = WeaponCommand.TriggerRelease;

        private static WeaponState S(bool mag, int rounds, Chamber chamber, bool locked, bool safety, Jam jam = Jam.None)
            => new WeaponState(mag, rounds, chamber, locked, safety, jam);

        private static TestCaseData Row(string name, WeaponState start, WeaponCommand[] setup,
                                        WeaponCommand command, bool accepted, WeaponState expected, string events)
            => new TestCaseData(start, setup, command, accepted, expected, events).SetName(name);

        private static readonly WeaponCommand[] None = new WeaponCommand[0];

        private static IEnumerable<TestCaseData> Transitions()
        {
            // Строка 2: EjectMagazine
            yield return Row("02 EjectMagazine: магазин вставлен",
                S(true, 3, Chamber.Loaded, false, true), None, Eject, true,
                S(false, 0, Chamber.Loaded, false, true), "Changed");
            yield return Row("02 EjectMagazine: магазина нет",
                S(false, 0, Chamber.Empty, false, true), None, Eject, false,
                S(false, 0, Chamber.Empty, false, true), "");

            // Строка 3: SlideBack
            yield return Row("03 SlideBack: затвор впереди, патроны в магазине — удерживается сзади",
                S(true, 5, Chamber.Empty, false, true), None, Back, true,
                S(true, 5, Chamber.Empty, false, true), "Changed");
            yield return Row("03 SlideBack: патрон в патроннике извлечён",
                S(true, 4, Chamber.Loaded, false, true), None, Back, true,
                S(true, 4, Chamber.Empty, false, true), "Changed");
            yield return Row("03 SlideBack: стреляная гильза выброшена, прихват снят",
                S(true, 3, Chamber.Spent, false, false, Jam.StovePipe), None, Back, true,
                S(true, 3, Chamber.Empty, false, false), "CasingEjected,Changed");
            yield return Row("03 SlideBack: осечка снята, патрон извлечён",
                S(true, 2, Chamber.Loaded, false, false, Jam.Misfire), None, Back, true,
                S(true, 2, Chamber.Empty, false, false), "Changed");
            yield return Row("03 SlideBack: с задержки при патронах в магазине",
                S(true, 5, Chamber.Empty, true, true), None, Back, true,
                S(true, 5, Chamber.Empty, false, true), "Changed");
            yield return Row("03 SlideBack: без магазина затвор сам встаёт на задержку",
                S(false, 0, Chamber.Empty, false, true), None, Back, true,
                S(false, 0, Chamber.Empty, true, true), "Changed");
            yield return Row("03 SlideBack: с пустым магазином затвор сам встаёт на задержку",
                S(true, 0, Chamber.Empty, false, true), None, Back, true,
                S(true, 0, Chamber.Empty, true, true), "Changed");
            yield return Row("03 SlideBack: без магазина патрон извлечён, затвор на задержке",
                S(false, 0, Chamber.Loaded, false, true), None, Back, true,
                S(false, 0, Chamber.Empty, true, true), "Changed");
            yield return Row("03 SlideBack: на задержке без магазина ничего не меняет",
                S(false, 0, Chamber.Empty, true, true), None, Back, true,
                S(false, 0, Chamber.Empty, true, true), "");
            yield return Row("03 SlideBack: затвор уже удерживается сзади",
                S(true, 5, Chamber.Empty, false, true), new[] { Back }, Back, false,
                S(true, 5, Chamber.Empty, false, true), "");

            // Строка 4: SlideForward
            yield return Row("04 SlideForward: досылает патрон",
                S(true, 5, Chamber.Empty, false, true), new[] { Back }, Forward, true,
                S(true, 4, Chamber.Loaded, false, true), "Changed");
            yield return Row("04 SlideForward: последний патрон из магазина",
                S(true, 1, Chamber.Empty, false, true), new[] { Back }, Forward, true,
                S(true, 0, Chamber.Loaded, false, true), "Changed");
            yield return Row("04 SlideForward: затвор впереди",
                S(true, 5, Chamber.Empty, false, true), None, Forward, false,
                S(true, 5, Chamber.Empty, false, true), "");
            yield return Row("04 SlideForward: затвор на задержке",
                S(false, 0, Chamber.Empty, true, true), None, Forward, false,
                S(false, 0, Chamber.Empty, true, true), "");

            // Строка 5: SlideStopEngage
            yield return Row("05 SlideStopEngage: затвор удерживается сзади",
                S(true, 5, Chamber.Empty, false, true), new[] { Back }, StopEngage, true,
                S(true, 5, Chamber.Empty, true, true), "Changed");
            yield return Row("05 SlideStopEngage: затвор впереди",
                S(true, 5, Chamber.Empty, false, true), None, StopEngage, false,
                S(true, 5, Chamber.Empty, false, true), "");
            yield return Row("05 SlideStopEngage: уже на задержке",
                S(false, 0, Chamber.Empty, true, true), None, StopEngage, false,
                S(false, 0, Chamber.Empty, true, true), "");

            // Строка 6: SlideStopRelease
            yield return Row("06 SlideStopRelease: досылает патрон",
                S(true, 5, Chamber.Empty, true, false), None, StopRelease, true,
                S(true, 4, Chamber.Loaded, false, false), "Changed");
            yield return Row("06 SlideStopRelease: пустой магазин, затвор уходит вперёд",
                S(true, 0, Chamber.Empty, true, true), None, StopRelease, true,
                S(true, 0, Chamber.Empty, false, true), "Changed");
            yield return Row("06 SlideStopRelease: без магазина, затвор уходит вперёд",
                S(false, 0, Chamber.Empty, true, true), None, StopRelease, true,
                S(false, 0, Chamber.Empty, false, true), "Changed");
            yield return Row("06 SlideStopRelease: затвор впереди",
                S(true, 5, Chamber.Empty, false, true), None, StopRelease, false,
                S(true, 5, Chamber.Empty, false, true), "");
            yield return Row("06 SlideStopRelease: затвор удерживается рукой",
                S(true, 5, Chamber.Empty, false, true), new[] { Back }, StopRelease, false,
                S(true, 5, Chamber.Empty, false, true), "");

            // Строки 7-8: предохранитель
            yield return Row("07 SafetyOn: был выключен",
                S(true, 4, Chamber.Loaded, false, false), None, SafeOn, true,
                S(true, 4, Chamber.Loaded, false, true), "Changed");
            yield return Row("07 SafetyOn: уже включён",
                S(true, 4, Chamber.Loaded, false, true), None, SafeOn, false,
                S(true, 4, Chamber.Loaded, false, true), "");
            yield return Row("08 SafetyOff: был включён",
                S(true, 4, Chamber.Loaded, false, true), None, SafeOff, true,
                S(true, 4, Chamber.Loaded, false, false), "Changed");
            yield return Row("08 SafetyOff: уже выключен",
                S(true, 4, Chamber.Loaded, false, false), None, SafeOff, false,
                S(true, 4, Chamber.Loaded, false, false), "");

            // Строки 9-11: спуск без выстрела
            yield return Row("09 TriggerPull: предохранитель включён",
                S(true, 4, Chamber.Loaded, false, true), None, Pull, false,
                S(true, 4, Chamber.Loaded, false, true), "");
            yield return Row("10 TriggerPull: затвор на задержке",
                S(true, 5, Chamber.Empty, true, false), None, Pull, true,
                S(true, 5, Chamber.Empty, true, false), "");
            yield return Row("10 TriggerPull: затвор удерживается сзади",
                S(true, 5, Chamber.Empty, false, false), new[] { Back }, Pull, true,
                S(true, 5, Chamber.Empty, false, false), "");
            yield return Row("11 TriggerPull: патронник пуст",
                S(true, 5, Chamber.Empty, false, false), None, Pull, true,
                S(true, 5, Chamber.Empty, false, false), "DryFire");
            yield return Row("11 TriggerPull: осечка не устранена",
                S(true, 2, Chamber.Loaded, false, false, Jam.Misfire), None, Pull, true,
                S(true, 2, Chamber.Loaded, false, false, Jam.Misfire), "DryFire");
            yield return Row("11 TriggerPull: прихват не устранён",
                S(true, 2, Chamber.Spent, false, false, Jam.StovePipe), None, Pull, true,
                S(true, 2, Chamber.Spent, false, false, Jam.StovePipe), "DryFire");

            // Строки 14-16: выстрел
            yield return Row("14 TriggerPull: выстрел, дослан следующий патрон",
                S(true, 4, Chamber.Loaded, false, false), None, Pull, true,
                S(true, 3, Chamber.Loaded, false, false), "Fired,CasingEjected,Changed");
            yield return Row("15 TriggerPull: последний выстрел, затвор на задержке",
                S(true, 0, Chamber.Loaded, false, false), None, Pull, true,
                S(true, 0, Chamber.Empty, true, false), "Fired,CasingEjected,Changed");
            yield return Row("16 TriggerPull: выстрел без магазина, затвор впереди",
                S(false, 0, Chamber.Loaded, false, false), None, Pull, true,
                S(false, 0, Chamber.Empty, false, false), "Fired,CasingEjected,Changed");

            // Строка 17: TriggerRelease и запрет самовзвода
            yield return Row("17 TriggerRelease: спуск нажат",
                S(true, 4, Chamber.Loaded, false, false), new[] { Pull }, Release, true,
                S(true, 3, Chamber.Loaded, false, false), "");
            yield return Row("17 TriggerRelease: спуск не нажат",
                S(true, 4, Chamber.Loaded, false, false), None, Release, false,
                S(true, 4, Chamber.Loaded, false, false), "");
            yield return Row("17 TriggerPull: повторно без TriggerRelease",
                S(true, 4, Chamber.Loaded, false, false), new[] { Pull }, Pull, false,
                S(true, 3, Chamber.Loaded, false, false), "");
            yield return Row("17 TriggerPull: после TriggerRelease следующий выстрел",
                S(true, 4, Chamber.Loaded, false, false), new[] { Pull, Release }, Pull, true,
                S(true, 2, Chamber.Loaded, false, false), "Fired,CasingEjected,Changed");
        }

        [TestCaseSource(nameof(Transitions))]
        public void Apply_FollowsTransitionTable(WeaponState start, WeaponCommand[] setup, WeaponCommand command,
                                                 bool accepted, WeaponState expected, string events)
        {
            var weapon = new Weapon(start);
            foreach (var step in setup)
                Assert.IsTrue(weapon.Apply(step), "подготовка: " + step);
            var log = Subscribe(weapon);

            bool result = weapon.Apply(command);

            Assert.AreEqual(accepted, result, "результат Apply");
            AssertState(expected, weapon.State);
            Assert.AreEqual(events, string.Join(",", log), "события");
        }

        // Строка 1: InsertMagazine
        [TestCase(0)]
        [TestCase(5)]
        public void InsertMagazine_WithoutMagazine_TakesRounds(int rounds)
        {
            var weapon = new Weapon(S(false, 0, Chamber.Empty, true, true));
            var log = Subscribe(weapon);

            Assert.IsTrue(weapon.InsertMagazine(rounds));

            // Затвор с задержки сам не срывается.
            AssertState(S(true, rounds, Chamber.Empty, true, true), weapon.State);
            Assert.AreEqual("Changed", string.Join(",", log));
        }

        [Test]
        public void InsertMagazine_WhenInserted_IsRejected()
        {
            var weapon = new Weapon(S(true, 2, Chamber.Empty, false, true));
            var log = Subscribe(weapon);

            Assert.IsFalse(weapon.InsertMagazine(5));

            AssertState(S(true, 2, Chamber.Empty, false, true), weapon.State);
            Assert.IsEmpty(log);
        }

        [Test]
        public void NewWeapon_IsInCabinetState()
        {
            AssertState(S(true, 0, Chamber.Empty, false, true), new Weapon().State);
        }

        [Test]
        public void Changed_CarriesNewState()
        {
            var weapon = new Weapon(S(true, 5, Chamber.Empty, true, false));
            WeaponState? received = null;
            weapon.Changed += state => received = state;

            weapon.Apply(StopRelease);

            Assert.IsTrue(received.HasValue);
            AssertState(weapon.State, received.Value);
        }

        // Шаги 2, 6 и 8 упражнения подряд, как в карточке пистолета.
        [Test]
        public void ExerciseSequence_EndsClear()
        {
            var weapon = new Weapon();
            int fired = 0, casings = 0;
            weapon.Fired += () => fired++;
            weapon.CasingEjected += () => casings++;

            // Шаг 2, контрольный осмотр
            Assert.IsTrue(weapon.Apply(Eject));
            Assert.IsTrue(weapon.Apply(Back));
            Assert.IsTrue(weapon.State.IsClear);

            // Шаг 6: «Заряжай», «Огонь»
            Assert.IsTrue(weapon.InsertMagazine(5));
            Assert.IsTrue(weapon.State.IsLoaded);
            Assert.IsTrue(weapon.Apply(SafeOff));
            Assert.IsTrue(weapon.Apply(StopRelease));
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(weapon.Apply(Pull));
                Assert.IsTrue(weapon.Apply(Release));
            }
            Assert.AreEqual(5, fired);
            Assert.AreEqual(5, casings);
            AssertState(S(true, 0, Chamber.Empty, true, false), weapon.State);

            // Шаг 8: «Стой», «Разряжай»
            Assert.IsTrue(weapon.Apply(SafeOn));
            Assert.IsTrue(weapon.Apply(Eject));
            Assert.IsTrue(weapon.State.IsClear);
            Assert.IsFalse(weapon.State.IsLoaded);
        }

        private static List<string> Subscribe(Weapon weapon)
        {
            var log = new List<string>();
            weapon.Changed += _ => log.Add("Changed");
            weapon.Fired += () => log.Add("Fired");
            weapon.DryFire += () => log.Add("DryFire");
            weapon.Jammed += jam => log.Add("Jammed");
            weapon.CasingEjected += () => log.Add("CasingEjected");
            return log;
        }

        private static void AssertState(WeaponState expected, WeaponState actual)
        {
            Assert.AreEqual(Describe(expected), Describe(actual));
        }

        private static string Describe(WeaponState s)
            => $"mag={s.MagazineInserted} rounds={s.RoundsInMagazine} chamber={s.Chamber} " +
               $"locked={s.SlideLocked} safety={s.SafetyOn} jam={s.Jam}";
    }
}
