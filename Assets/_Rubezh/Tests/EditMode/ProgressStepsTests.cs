using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Шкала прогресса O-43 (NG-31): сегменты загораются по таблице docs/user-flow.md §6,
    // на Failed шкала замирает, на Restart обнуляется.
    public class ProgressStepsTests
    {
        private ProgressSteps _steps;
        private List<int> _lit;

        [SetUp]
        public void SetUp()
        {
            _steps = new ProgressSteps();
            _lit = new List<int>();
            _steps.Lit += _lit.Add;
        }

        private void Go(Phase from, Phase to) => _steps.OnPhaseChanged(from, to);

        [Test]
        public void NineNamedSegments()
        {
            Assert.AreEqual(9, ProgressSteps.Count);
            CollectionAssert.AreEqual(new[] { "Получение", "Осмотр", "СИЗ", "Снаряжение", "Рубеж", "Стрельба", "Задержка", "Разряжание", "Сдача" },
                                      ProgressSteps.Names);
        }

        [Test]
        public void WholeRun_WithMisfire_LightsByTable()
        {
            Go(Phase.Receive, Phase.Inspect); Assert.AreEqual(new[] { 0 }, _lit.ToArray());
            Go(Phase.Inspect, Phase.Ppe); Assert.IsTrue(_steps.IsLit(1));
            Go(Phase.Ppe, Phase.LoadMagazine); Assert.IsTrue(_steps.IsLit(2));
            Go(Phase.LoadMagazine, Phase.ToLine); Assert.IsTrue(_steps.IsLit(3));
            Go(Phase.ToLine, Phase.Load); Assert.IsTrue(_steps.IsLit(4));
            Go(Phase.Load, Phase.Fire);
            Assert.AreEqual(5, _steps.LitCount, "«Заряжай» → «Огонь» сегмента не зажигает");

            _steps.OnJammed();
            Assert.IsTrue(_steps.IsLit(5), "6 «Стрельба» — в момент задержки");
            Assert.IsFalse(_steps.IsLit(6));
            _steps.OnJamCleared();
            Assert.IsTrue(_steps.IsLit(6), "7 «Задержка» — после устранения");

            Go(Phase.Fire, Phase.Cease);
            Go(Phase.Cease, Phase.Unload);
            Go(Phase.Unload, Phase.ShowClear);
            Assert.AreEqual(7, _steps.LitCount);
            Go(Phase.ShowClear, Phase.Return); Assert.IsTrue(_steps.IsLit(7), "8 «Разряжание»");
            Go(Phase.Return, Phase.Done); Assert.IsTrue(_steps.IsLit(8), "9 «Сдача»");
            Assert.AreEqual(9, _steps.LitCount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, _lit, "по порядку, каждый один раз");
        }

        [Test]
        public void WithoutMisfire_FireAndJamLightOnCease()
        {
            Go(Phase.Load, Phase.Fire);
            Assert.IsFalse(_steps.IsLit(5));
            Go(Phase.Fire, Phase.Cease);
            Assert.IsTrue(_steps.IsLit(5));
            Assert.IsTrue(_steps.IsLit(6));
        }

        [Test]
        public void JamClearedWithoutJam_DoesNothing()
        {
            _steps.OnJamCleared();
            Assert.AreEqual(0, _steps.LitCount);
        }

        [Test]
        public void Failed_Freezes()
        {
            Go(Phase.Receive, Phase.Inspect);
            Go(Phase.Inspect, Phase.Failed);
            Assert.IsTrue(_steps.Frozen);
            Go(Phase.Inspect, Phase.Ppe);
            _steps.OnJammed();
            Assert.AreEqual(1, _steps.LitCount, "после грубого шкала замерла");
        }

        [Test]
        public void Restart_ClearsAndUnfreezes()
        {
            bool reset = false;
            _steps.Reset += () => reset = true;
            Go(Phase.Receive, Phase.Inspect);
            Go(Phase.Inspect, Phase.Failed);
            _steps.OnRestart();
            Assert.IsTrue(reset);
            Assert.AreEqual(0, _steps.LitCount);
            Assert.IsFalse(_steps.Frozen);
            Go(Phase.Receive, Phase.Inspect);
            Assert.IsTrue(_steps.IsLit(0), "после Restart снова загорается");
        }
    }
}
