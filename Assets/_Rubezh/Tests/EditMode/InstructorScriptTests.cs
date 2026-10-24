using System;
using System.IO;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;

namespace Rubezh.Tests.EditMode
{
    // Сценарий инструктора Data/Hints/script.ru.json: у каждой фазы есть команда, у каждого
    // грубого нарушения — причина, все подсказки из правил YG-08 есть в сценарии.
    public class InstructorScriptTests
    {
        private const string ScriptPath = "Assets/_Rubezh/Data/Hints/script.ru.json";
        private const string RulesPath = "Assets/_Rubezh/Data/Hints/hint_rules.ru.json";
        private InstructorScript _script;

        [OneTimeSetUp]
        public void Load() => _script = new InstructorScript(File.ReadAllText(ScriptPath));

        [Test]
        public void EveryPhase_HasCommand()
        {
            foreach (Phase phase in Enum.GetValues(typeof(Phase)))
            {
                Assert.IsTrue(_script.HasCommand(phase), $"нет команды для {phase}");
                Assert.IsNotEmpty(_script.Command(phase));
                Assert.IsNotEmpty(_script.VoiceId(phase), $"нет ID озвучки для {phase}");
            }
        }

        [TestCase(Violation.MuzzleOutOfSector)]
        [TestCase(Violation.LookIntoMuzzle)]
        [TestCase(Violation.ShotBeforeFire)]
        [TestCase(Violation.ShotAfterCease)]
        [TestCase(Violation.ShotOutOfSector)]
        public void EveryGrossViolation_HasFailReason(Violation v)
        {
            Assert.IsTrue(_script.HasFailReason(v));
            Assert.IsNotEmpty(_script.FailTitle(v));
            Assert.IsNotEmpty(_script.FailText(v));
        }

        [Test]
        public void HintRules_ReferenceExistingHints()
        {
            var rules = File.ReadAllText(RulesPath);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(rules, "\"hint\": \"([a-z0-9_]+)\""))
                Assert.IsTrue(_script.HasHint(m.Groups[1].Value), $"в сценарии нет подсказки {m.Groups[1].Value}");
        }

        [Test]
        public void Hint_SubstitutesButtonsByHand()
        {
            Assert.AreEqual("A: отделить магазин", _script.Hint("hint_eject_magazine", Hand.Right));
            Assert.AreEqual("X: отделить магазин", _script.Hint("hint_eject_magazine", Hand.Left));
            StringAssert.StartsWith("Y:", _script.Hint("hint_safety_on", Hand.Left));
        }

        [Test]
        public void FireCommand_IsShort()
        {
            Assert.AreEqual("Огонь", _script.Command(Phase.Fire));
        }
    }
}
