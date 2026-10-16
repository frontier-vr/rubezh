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
        public void Dispenser_OneWaitingAtATime_SeriesTotal()
        {
            var d = new RoundDispenser(5);
            int spawned = 0;
            for (int i = 0; i < 20; i++)
            {
                if (d.ShouldSpawn(roundWaiting: false)) { d.Spawned(); spawned++; }
                Assert.IsFalse(d.ShouldSpawn(roundWaiting: true), "пока патрон в лотке, второй не появляется");
                if (d.Out > 0) d.Seat();
            }
            Assert.AreEqual(5, spawned);
            Assert.AreEqual(5, d.Seated);
            Assert.IsFalse(d.ShouldSpawn(false));
        }

        [Test]
        public void Dispenser_LostRound_IsReissued()
        {
            var d = new RoundDispenser(5);
            for (int i = 0; i < 4; i++) { d.Spawned(); d.Seat(); }
            d.Spawned();
            Assert.IsFalse(d.ShouldSpawn(false), "пятый на руках");
            d.Lost();
            Assert.IsTrue(d.ShouldSpawn(false), "упавший заменяется");
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
