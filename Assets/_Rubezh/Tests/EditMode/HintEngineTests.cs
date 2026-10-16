using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;

namespace Rubezh.Tests.EditMode
{
    // Логика подсказок (VP-12) на настоящих правилах Юли из Data/Hints/hint_rules.ru.json:
    // каждая строка срабатывает в своей ситуации, первая подходящая строка выигрывает,
    // подсказка — только после паузы, действие её снимает.
    public class HintEngineTests
    {
        private HintRules _rules;
        private HintEngine _engine;
        private string _target, _hint;
        private int _cleared;

        private static readonly WeaponState Cabinet = new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None);
        private static readonly WeaponState Clear = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);

        [SetUp]
        public void SetUp()
        {
            _rules = new HintRules(File.ReadAllText("Assets/_Rubezh/Data/Hints/hint_rules.ru.json"));
            _engine = new HintEngine(_rules.Rules, 5, 5);
            _engine.Shown += (t, h) => { _target = t; _hint = h; };
            _engine.Cleared += () => _cleared++;
            _target = _hint = null;
            _cleared = 0;
        }

        private static HintEngine.Context Ctx(Phase phase, Anchor anchor, WeaponState w, Place pistol, Place mag,
                                              bool ear = false, bool gl = false, bool target = false, bool round = false,
                                              int shots = 0, bool jammed = false) =>
            new HintEngine.Context
            {
                Phase = phase, Anchor = anchor, RoundInHand = round, Shots = shots, Jammed = jammed, JammedAt = 0,
                Facts = new ExerciseFacts(pistol == Place.Hand, w, 0, pistol, mag, ear, gl, target)
            };

        private string Pick(HintEngine.Context c, double now = 100) => _engine.Pick(now, c)?.hint;

        [Test]
        public void EveryConditionInRules_IsKnown()
        {
            foreach (var r in _rules.Rules)
            {
                var key = r.condition.Split(':')[0];
                CollectionAssert.Contains(HintEngine.KnownConditions, key, $"{r.step}: {r.condition}");
            }
            Assert.AreEqual(27, _rules.Rules.Count);
        }

        // Каждая строка таблицы Юли срабатывает в своей ситуации (сверху вниз по фазам).
        [Test]
        public void EveryRow_FiresInItsSituation()
        {
            var loaded = new WeaponState(true, 4, Chamber.Loaded, false, true, Jam.None);
            var mag5 = new WeaponState(true, 5, Chamber.Empty, false, true, Jam.None);
            var firing = new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None);
            var jam = new WeaponState(true, 3, Chamber.Loaded, false, false, Jam.Misfire);
            var cease = new WeaponState(true, 0, Chamber.Empty, true, false, Jam.None);
            var afterCease = new WeaponState(true, 0, Chamber.Empty, true, true, Jam.None);
            var noMagChamber = new WeaponState(false, 0, Chamber.Loaded, false, true, Jam.None);

            Assert.AreEqual("hint_teleport_a1", Pick(Ctx(Phase.Receive, Anchor.A3, Cabinet, Place.Cabinet, Place.InWeapon)));
            Assert.AreEqual("hint_take_pistol", Pick(Ctx(Phase.Receive, Anchor.A1, Cabinet, Place.Cabinet, Place.InWeapon)));
            Assert.AreEqual("hint_eject_magazine", Pick(Ctx(Phase.Inspect, Anchor.A1, Cabinet, Place.Hand, Place.InWeapon)));
            Assert.AreEqual("hint_put_magazine_shelf", Pick(Ctx(Phase.Inspect, Anchor.A1, new WeaponState(false, 0, Chamber.Empty, false, true, Jam.None), Place.Hand, Place.Hand)));
            Assert.AreEqual("hint_slide_lock", Pick(Ctx(Phase.Inspect, Anchor.A1, new WeaponState(false, 0, Chamber.Empty, false, true, Jam.None), Place.Hand, Place.InspectShelf)));
            Assert.AreEqual("hint_teleport_a1", Pick(Ctx(Phase.Ppe, Anchor.A2, Clear, Place.Hand, Place.InspectShelf)));
            Assert.AreEqual("hint_take_earmuffs", Pick(Ctx(Phase.Ppe, Anchor.A1, Clear, Place.Hand, Place.InspectShelf)));
            Assert.AreEqual("hint_take_glasses", Pick(Ctx(Phase.Ppe, Anchor.A1, Clear, Place.Hand, Place.InspectShelf, ear: true)));
            Assert.AreEqual("hint_teleport_a2", Pick(Ctx(Phase.LoadMagazine, Anchor.A1, Clear, Place.Hand, Place.InspectShelf, true, true)));
            Assert.AreEqual("hint_magazine_table", Pick(Ctx(Phase.LoadMagazine, Anchor.A2, Clear, Place.PrepTable, Place.Hand, true, true)));
            Assert.AreEqual("hint_take_round", Pick(Ctx(Phase.LoadMagazine, Anchor.A2, Clear, Place.PrepTable, Place.PrepTable, true, true)));
            Assert.AreEqual("hint_round_to_magazine", Pick(Ctx(Phase.LoadMagazine, Anchor.A2, Clear, Place.PrepTable, Place.PrepTable, true, true, round: true)));
            Assert.AreEqual("hint_teleport_a3", Pick(Ctx(Phase.ToLine, Anchor.A2, Clear, Place.Hand, Place.Hand, true, true)));
            Assert.AreEqual("hint_pistol_to_stand", Pick(Ctx(Phase.ToLine, Anchor.A3, Clear, Place.Hand, Place.Hand, true, true)));
            Assert.AreEqual("hint_magazine_to_stand", Pick(Ctx(Phase.ToLine, Anchor.A3, Clear, Place.LineStand, Place.Hand, true, true)));
            Assert.AreEqual("hint_target_button", Pick(Ctx(Phase.ToLine, Anchor.A3, Clear, Place.LineStand, Place.LineStand, true, true)));
            Assert.AreEqual("hint_insert_magazine", Pick(Ctx(Phase.Load, Anchor.A3, Clear, Place.Hand, Place.LineStand, true, true, true)));
            Assert.AreEqual("hint_chamber", Pick(Ctx(Phase.Fire, Anchor.A3, mag5, Place.Hand, Place.InWeapon, true, true, true)));
            Assert.AreEqual("hint_safety_off", Pick(Ctx(Phase.Fire, Anchor.A3, loaded, Place.Hand, Place.InWeapon, true, true, true)));
            Assert.AreEqual("hint_fire", Pick(Ctx(Phase.Fire, Anchor.A3, firing, Place.Hand, Place.InWeapon, true, true, true)));
            Assert.AreEqual("hint_clear_jam", Pick(Ctx(Phase.Fire, Anchor.A3, jam, Place.Hand, Place.InWeapon, true, true, true, shots: 2, jammed: true)));
            Assert.AreEqual("hint_safety_on", Pick(Ctx(Phase.Cease, Anchor.A3, cease, Place.Hand, Place.InWeapon, true, true, true)));
            Assert.AreEqual("hint_unload_magazine", Pick(Ctx(Phase.Unload, Anchor.A3, afterCease, Place.Hand, Place.InWeapon, true, true, true)));
            Assert.AreEqual("hint_unload_slide", Pick(Ctx(Phase.Unload, Anchor.A3, noMagChamber, Place.Hand, Place.Hand, true, true, true)));
            Assert.AreEqual("hint_show_clear", Pick(Ctx(Phase.ShowClear, Anchor.A3, Clear, Place.Hand, Place.LineStand, true, true, true)));
            Assert.AreEqual("hint_teleport_a1", Pick(Ctx(Phase.Return, Anchor.A3, Clear, Place.Hand, Place.LineStand, true, true, true)));
            Assert.AreEqual("hint_return_cabinet", Pick(Ctx(Phase.Return, Anchor.A1, Clear, Place.Hand, Place.LineStand, true, true, true)));
        }

        [Test]
        public void Rows_HaveTargetsAndPhrasesInScript()
        {
            var script = new InstructorScript(File.ReadAllText("Assets/_Rubezh/Data/Hints/script.ru.json"));
            foreach (var r in _rules.Rules)
            {
                Assert.IsNotEmpty(r.target, r.hint);
                Assert.IsTrue(script.HasHint(r.hint), r.hint);
            }
        }

        [Test]
        public void Hint_OnlyAfterIdlePause_AndActivityClearsIt()
        {
            var c = Ctx(Phase.Receive, Anchor.A1, Cabinet, Place.Cabinet, Place.InWeapon);
            _engine.Activity(0);
            _engine.Tick(4.9, c);
            Assert.IsNull(_hint, "раньше 5 с — не подсказываем");
            _engine.Tick(5.0, c);
            Assert.AreEqual("hint_take_pistol", _hint);
            Assert.AreEqual("Pistol", _target, "подсветка — пистолет");
            _engine.Activity(6);
            Assert.AreEqual(1, _cleared, "действие снимает подсказку");
            _hint = null;
            _engine.Tick(8, c);
            Assert.IsNull(_hint, "после действия снова пауза");
        }

        [Test]
        public void Fire_AfterFirstShot_OnlyJamHint()
        {
            var firing = new WeaponState(true, 3, Chamber.Loaded, false, false, Jam.None);
            Assert.IsNull(Pick(Ctx(Phase.Fire, Anchor.A3, firing, Place.Hand, Place.InWeapon, true, true, true, shots: 1)), "после первого выстрела — без подсказок");
            var jam = new WeaponState(true, 2, Chamber.Loaded, false, false, Jam.Misfire);
            var c = Ctx(Phase.Fire, Anchor.A3, jam, Place.Hand, Place.InWeapon, true, true, true, shots: 2, jammed: true);
            Assert.IsNull(_engine.Pick(4.0, c), "после осечки — пауза 5 с");
            Assert.AreEqual("hint_clear_jam", _engine.Pick(5.0, c).hint);
        }

        [Test]
        public void DoneAndFailed_NoHints()
        {
            var c = Ctx(Phase.Done, Anchor.A3, Clear, Place.Cabinet, Place.LineStand);
            _engine.Tick(100, c);
            Assert.IsNull(_hint);
            c.Phase = Phase.Failed;
            _engine.Tick(200, c);
            Assert.IsNull(_hint);
        }
    }
}
