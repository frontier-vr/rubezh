using System;
using System.IO;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;

namespace Rubezh.Tests.EditMode
{
    // Сценарий инструктора Data/Hints/script.ru.json: у каждой фазы есть команда, у каждого
    // грубого нарушения — причина, все подсказки из правил YG-08 есть в сценарии, у каждого шага —
    // табло «что делать и как» над своим местом.
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
        public void EveryExercisePhase_HasStationBoard_WithSteps()
        {
            foreach (Phase phase in Enum.GetValues(typeof(Phase)))
            {
                if (phase == Phase.Failed) continue;      // в обычном прохождении фазы нет (Р-09)
                var card = _script.StationFor(phase);
                Assert.IsNotNull(card, $"нет табло для {phase}");
                Assert.IsNotEmpty(card.Title, phase.ToString());
                Assert.GreaterOrEqual(card.Steps.Count, 2, $"{phase}: порядок действий — по пунктам");
                Assert.LessOrEqual(card.Steps.Count, 5, $"{phase}: на табло помещается до пяти пунктов");
                foreach (var step in card.Steps)
                    Assert.LessOrEqual(step.Length, 90, $"{phase}: пункт длиннее двух строк табло — «{step}»");
                if (card.Station != Station.Any)
                    Assert.IsNotEmpty(_script.Where(card.Station), $"{phase}: не сказано, куда идти к {card.Station}");
            }
            Assert.AreEqual(Station.Any, _script.StationFor(Phase.Done).Station, "конец упражнения — на всех табло");
        }

        [Test]
        public void StationBoards_FollowTheRoute()
        {
            Assert.AreEqual(Station.Cabinet, _script.StationFor(Phase.Receive).Station);
            Assert.AreEqual(Station.Cabinet, _script.StationFor(Phase.Inspect).Station);
            Assert.AreEqual(Station.PpeShelf, _script.StationFor(Phase.Ppe).Station);
            Assert.AreEqual(Station.PrepTable, _script.StationFor(Phase.LoadMagazine).Station);
            foreach (var phase in new[] { Phase.ToLine, Phase.Load, Phase.Fire, Phase.Cease, Phase.Unload, Phase.ShowClear })
                Assert.AreEqual(Station.LineStand, _script.StationFor(phase).Station, phase.ToString());
            Assert.AreEqual(Station.Cabinet, _script.StationFor(Phase.Return).Station);
        }

        [Test]
        public void BoardElsewhere_NamesThePlace()
        {
            StringAssert.Contains("у стола снаряжения", _script.BoardElsewhere(Station.PrepTable));
            StringAssert.DoesNotContain("{", _script.BoardElsewhere(Station.LineStand));
            Assert.AreEqual("Сейчас: 6. Стрельба", _script.BoardNow("6. Стрельба"));
        }

        [Test]
        public void MoveHints_SayWalkWithLeftStick()
        {
            foreach (var id in new[] { "hint_teleport_a1", "hint_teleport_a2", "hint_teleport_a3" })
                StringAssert.StartsWith("Левый стик: идти", _script.Hint(id, Hand.Right), id);
        }

        [Test]
        public void FireCommand_IsShort()
        {
            Assert.AreEqual("Огонь", _script.Command(Phase.Fire));
        }
    }
}
