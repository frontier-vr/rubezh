using System.IO;
using NUnit.Framework;
using Rubezh.Gameplay;

namespace Rubezh.Tests.EditMode
{
    // Выноска у контроллера (NG-32): приоритет замечание > подсказка > онбординг, одна выноска,
    // онбординг один раз за сессию, кнопки по руке, тексты из Data/Hints.
    public class CalloutsTests
    {
        private Callouts _c;

        [SetUp]
        public void SetUp()
        {
            Callouts.ForgetSession();
            _c = new Callouts(3, 8);
        }

        [TearDown]
        public void TearDown() => Callouts.ForgetSession();

        [Test]
        public void Priority_RemarkOverHintOverOnboarding()
        {
            _c.Onboarding("onb_grab", "Grip: взять предмет", Hand.Right, 0);
            Assert.AreEqual(Callouts.Kind.Onboarding, _c.Current);
            _c.Hint("Grip: взять пистолет", Hand.Right, 1);
            Assert.AreEqual(Callouts.Kind.Hint, _c.Current, "подсказка важнее онбординга");
            _c.Remark("Палец на спуске", Hand.Left, 2);
            Assert.AreEqual(Callouts.Kind.Remark, _c.Current, "замечание важнее подсказки");
            Assert.AreEqual("Палец на спуске", _c.Text);
            Assert.AreEqual(Hand.Left, _c.Hand);

            _c.Tick(5.1);                                     // замечание держится 3 с
            Assert.AreEqual(Callouts.Kind.Hint, _c.Current, "после замечания — снова подсказка");
            _c.ClearHint(5.2);
            Assert.AreEqual(Callouts.Kind.Onboarding, _c.Current, "онбординг ещё не истёк (8 с)");
            _c.Tick(8.1);
            Assert.AreEqual(Callouts.Kind.None, _c.Current);
        }

        [Test]
        public void Onboarding_OncePerSession_RestartDoesNotBringItBack()
        {
            Assert.IsTrue(_c.Onboarding("onb_teleport", "Стик вперёд: телепорт", Hand.Right, 0));
            _c.DoneOnboarding("onb_teleport", 1);
            Assert.AreEqual(Callouts.Kind.None, _c.Current, "снимается действием");
            _c.Reset(2);                                       // «Повторить»
            Assert.IsFalse(_c.Onboarding("onb_teleport", "Стик вперёд: телепорт", Hand.Right, 3));
            Assert.AreEqual(Callouts.Kind.None, _c.Current);
            var fresh = new Callouts(3, 8);                    // та же сессия, новый объект
            Assert.IsFalse(fresh.Onboarding("onb_teleport", "x", Hand.Right, 4));
            Assert.IsTrue(Callouts.WasShown("onb_teleport"));
        }

        [Test]
        public void Onboarding_WaitsForPrevious_NotLost()
        {
            Assert.IsTrue(_c.Onboarding("onb_teleport", "Стик вперёд: телепорт", Hand.Right, 0));
            Assert.IsFalse(_c.Onboarding("onb_grab", "Grip: взять предмет", Hand.Left, 1), "пока висит первая — вторая ждёт");
            Assert.IsFalse(Callouts.WasShown("onb_grab"), "и не считается показанной");
            _c.DoneOnboarding("onb_teleport", 2);
            Assert.IsTrue(_c.Onboarding("onb_grab", "Grip: взять предмет", Hand.Left, 3), "повод повторился — выходит следом");
            Assert.AreEqual("Grip: взять предмет", _c.Text);
        }

        [Test]
        public void Onboarding_GoesAwayAfter8Seconds()
        {
            _c.Onboarding("onb_slide", "Grip на затворе: отвести затвор", Hand.Left, 10);
            _c.Tick(17.9);
            Assert.AreEqual(Callouts.Kind.Onboarding, _c.Current);
            _c.Tick(18.0);
            Assert.AreEqual(Callouts.Kind.None, _c.Current);
        }

        [Test]
        public void DoneOnboarding_OfOtherId_KeepsCurrent()
        {
            _c.Onboarding("onb_pistol_buttons", "A: выпустить магазин", Hand.Right, 0);
            _c.DoneOnboarding("onb_grab", 1);
            Assert.AreEqual(Callouts.Kind.Onboarding, _c.Current);
        }

        [Test]
        public void Restart_ClearsRemarkAndHint()
        {
            _c.Hint("h", Hand.Right, 0);
            _c.Remark("r", Hand.Right, 0);
            _c.Reset(1);
            Assert.AreEqual(Callouts.Kind.None, _c.Current);
        }

        [Test]
        public void Texts_FromDataHints_WithButtonsByHand()
        {
            var rules = new HintRules(File.ReadAllText("Assets/_Rubezh/Data/Hints/hint_rules.ru.json"));
            Assert.AreEqual(4, rules.OnboardingLines.Count, "4 выноски онбординга");
            string buttons = null;
            foreach (var line in rules.OnboardingLines)
                if (line.id == "onb_pistol_buttons") buttons = line.text;
            Assert.AreEqual("A: выпустить магазин · B: предохранитель", InstructorScript.WithButtons(buttons, Hand.Right));
            Assert.AreEqual("X: выпустить магазин · Y: предохранитель", InstructorScript.WithButtons(buttons, Hand.Left));
            foreach (Rubezh.Core.Violation v in System.Enum.GetValues(typeof(Rubezh.Core.Violation)))
                Assert.IsTrue(rules.HasName(v), "короткое название для замечания: " + v);
        }
    }
}
