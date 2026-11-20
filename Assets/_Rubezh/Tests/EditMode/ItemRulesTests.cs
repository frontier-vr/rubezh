using NUnit.Framework;
using Rubezh.Gameplay;

namespace Rubezh.Tests.EditMode
{
    // Правила предметов NG-09: когда упавший предмет возвращать, сколько патронов выдаёт лоток.
    public class ItemRulesTests
    {
        [Test]
        public void DropWatch_FiresOnceAfterDelay()
        {
            var w = new DropWatch(1.0);
            Assert.IsFalse(w.Tick(0.0, true));
            Assert.IsFalse(w.Tick(0.9, true));
            Assert.IsTrue(w.Tick(1.0, true));
            Assert.IsFalse(w.Tick(1.1, true), "отсчёт заново после возврата");
        }

        [Test]
        public void DropWatch_PickedUp_ResetsTimer()
        {
            var w = new DropWatch(1.0);
            w.Tick(0.0, true);
            w.Tick(0.8, false);
            Assert.IsFalse(w.Tick(1.2, true));
            Assert.IsTrue(w.Tick(2.2, true));
        }

        [Test]
        public void Dispenser_WholeSeriesAtOnce_AndNoMore()
        {
            var d = new RoundDispenser(5);
            Assert.AreEqual(5, d.ToSpawn(freeSlots: 5), "вся серия сразу, а не по одному");
            for (int i = 0; i < 5; i++) d.Spawned();
            Assert.AreEqual(0, d.ToSpawn(freeSlots: 0));
            for (int i = 0; i < 5; i++)
            {
                d.Seat();
                Assert.AreEqual(0, d.ToSpawn(freeSlots: i + 1), "вставленный патрон не возвращается в лоток");
            }
            Assert.AreEqual(5, d.Seated);
            Assert.AreEqual(0, d.Out);
        }

        [Test]
        public void Dispenser_NeverMoreThanFreeSlots()
        {
            var d = new RoundDispenser(5);
            Assert.AreEqual(2, d.ToSpawn(freeSlots: 2));
            Assert.AreEqual(0, d.ToSpawn(freeSlots: -1));
        }

        [Test]
        public void Dispenser_LostRound_IsReissued()
        {
            var d = new RoundDispenser(5);
            for (int i = 0; i < 5; i++) d.Spawned();
            for (int i = 0; i < 4; i++) d.Seat();
            Assert.AreEqual(0, d.ToSpawn(5), "пятый на руках");
            d.Lost();
            Assert.AreEqual(1, d.ToSpawn(5), "упавший заменяется");
        }

        [Test]
        public void Dispenser_Reset()
        {
            var d = new RoundDispenser(5);
            d.Spawned(); d.Seat(); d.Spawned();
            d.Reset();
            Assert.AreEqual(0, d.Seated);
            Assert.AreEqual(0, d.Out);
        }
    }
}
