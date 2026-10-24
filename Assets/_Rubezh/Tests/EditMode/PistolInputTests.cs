using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;

namespace Rubezh.Tests.EditMode
{
    // Перевод хода руки и нажатия спуска в команды оружия (NG-08).
    public class PistolInputTests
    {
        private const float Travel = 0.03f;

        private static SlidePull Pull() => new SlidePull(Travel, 0.9f, 0.2f);

        [Test]
        public void Slide_ShortPull_NoCommand()
        {
            var p = Pull();
            p.Begin(0f);
            Assert.IsNull(p.Drag(0.02f));
            Assert.IsNull(p.Release());
        }

        [Test]
        public void Slide_FullPullAndRelease_BackThenForward()
        {
            var p = Pull();
            p.Begin(0f);
            Assert.AreEqual(WeaponCommand.SlideBack, p.Drag(0.028f));
            Assert.IsNull(p.Drag(0.05f), "SlideBack один раз за ход");
            Assert.AreEqual(Travel, p.Offset, 1e-6f, "ход ограничен упором");
            Assert.AreEqual(WeaponCommand.SlideForward, p.Release());
            Assert.AreEqual(0f, p.Offset);
        }

        [Test]
        public void Slide_RiddenForwardByHand_ForwardBeforeRelease()
        {
            var p = Pull();
            p.Begin(0f);
            p.Drag(0.03f);
            Assert.IsNull(p.Drag(0.015f));
            Assert.AreEqual(WeaponCommand.SlideForward, p.Drag(0.004f));
            Assert.IsNull(p.Release(), "второй SlideForward не нужен");
        }

        [Test]
        public void Slide_FromLock_ShortExtraPullIsEnough()
        {
            var p = Pull();
            p.Begin(Travel * 0.8f);
            Assert.AreEqual(WeaponCommand.SlideBack, p.Drag(0.004f));
            Assert.AreEqual(WeaponCommand.SlideForward, p.Release());
        }

        [Test]
        public void Slide_HandForward_DoesNotPushPastRest()
        {
            var p = Pull();
            p.Begin(0f);
            Assert.IsNull(p.Drag(-0.05f));
            Assert.AreEqual(0f, p.Offset);
        }

        [Test]
        public void Trigger_Hysteresis_OneShotPerSqueeze()
        {
            var t = new TriggerSqueeze(0.75f, 0.4f);
            Assert.IsNull(t.Update(0.5f));
            Assert.AreEqual(WeaponCommand.TriggerPull, t.Update(0.8f));
            Assert.IsNull(t.Update(1f));
            Assert.IsNull(t.Update(0.6f), "между порогами — всё ещё нажат");
            Assert.AreEqual(WeaponCommand.TriggerRelease, t.Update(0.3f));
            Assert.AreEqual(WeaponCommand.TriggerPull, t.Update(0.9f));
        }

        [Test]
        public void Trigger_ResetWhilePulled_Releases()
        {
            var t = new TriggerSqueeze(0.75f, 0.4f);
            t.Update(1f);
            Assert.AreEqual(WeaponCommand.TriggerRelease, t.Reset());
            Assert.IsNull(t.Reset());
        }

        [Test]
        public void Trigger_ThresholdsMustBeOrdered()
        {
            Assert.Throws<System.ArgumentException>(() => new TriggerSqueeze(0.4f, 0.75f));
        }

        [Test]
        public void RulesConfig_PullAboveTouchThreshold()
        {
            var rules = UnityEngine.ScriptableObject.CreateInstance<RulesConfig>();
            Assert.Greater(rules.triggerPullThreshold, rules.touchThreshold, "касание пальцем не стреляет");
            Assert.Greater(rules.triggerPullThreshold, rules.triggerReleaseThreshold);
            UnityEngine.Object.DestroyImmediate(rules);
        }
    }
}
