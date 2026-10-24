using NUnit.Framework;
using Rubezh.XR;

namespace Rubezh.Tests.EditMode
{
    // Правило касания спуска: датчик triggerTouched или запасной порог (NG-05, docs/qa/trigger-touch.md).
    public class TriggerTouchTests
    {
        private const float Threshold = 0.05f;

        [TestCase(TouchSource.Auto, true, true, 0f, true, TestName = "Auto: датчик есть, палец касается")]
        [TestCase(TouchSource.Auto, true, false, 0f, false, TestName = "Auto: датчик есть, палец снят")]
        [TestCase(TouchSource.Auto, true, false, 0.4f, true, TestName = "Auto: датчик молчит, но спуск нажат")]
        [TestCase(TouchSource.Auto, false, false, 0.04f, false, TestName = "Auto: датчика нет, нажатие ниже порога")]
        [TestCase(TouchSource.Auto, false, false, 0.06f, true, TestName = "Auto: датчика нет, нажатие выше порога")]
        [TestCase(TouchSource.Sensor, true, true, 0f, true, TestName = "Sensor: касание")]
        [TestCase(TouchSource.Sensor, true, false, 0.9f, false, TestName = "Sensor: нажатие без датчика не считается")]
        [TestCase(TouchSource.Threshold, true, true, 0f, false, TestName = "Threshold: датчик игнорируется")]
        [TestCase(TouchSource.Threshold, false, false, 0.06f, true, TestName = "Threshold: выше порога")]
        [TestCase(TouchSource.Threshold, false, false, 0.05f, false, TestName = "Threshold: ровно порог — не касание")]
        public void IsTouched(TouchSource source, bool sensorAvailable, bool sensorTouched, float press, bool expected)
        {
            Assert.AreEqual(expected, TriggerTouch.IsTouched(source, sensorAvailable, sensorTouched, press, Threshold));
        }
    }
}
