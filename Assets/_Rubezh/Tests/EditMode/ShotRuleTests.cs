using NUnit.Framework;
using Rubezh.Core;

namespace Rubezh.Tests.EditMode
{
    // Грубые выстрелы FR-35, FR-36: какая фаза и сектор дают какое нарушение.
    public class ShotRuleTests
    {
        [TestCase(Phase.Receive, true, Violation.ShotBeforeFire)]
        [TestCase(Phase.Inspect, true, Violation.ShotBeforeFire)]
        [TestCase(Phase.ToLine, true, Violation.ShotBeforeFire)]
        [TestCase(Phase.Load, true, Violation.ShotBeforeFire)]
        [TestCase(Phase.Load, false, Violation.ShotBeforeFire)]
        [TestCase(Phase.Cease, true, Violation.ShotAfterCease)]
        [TestCase(Phase.Unload, true, Violation.ShotAfterCease)]
        [TestCase(Phase.ShowClear, true, Violation.ShotAfterCease)]
        [TestCase(Phase.Return, true, Violation.ShotAfterCease)]
        [TestCase(Phase.Fire, false, Violation.ShotOutOfSector)]
        public void GrossShot(Phase phase, bool inSector, Violation expected)
        {
            Assert.AreEqual(expected, ShotRule.Classify(phase, inSector));
        }

        [TestCase(Phase.Fire, true)]
        [TestCase(Phase.Done, false)]
        [TestCase(Phase.Failed, true)]
        public void AllowedShot(Phase phase, bool inSector)
        {
            Assert.IsNull(ShotRule.Classify(phase, inSector));
        }
    }
}
