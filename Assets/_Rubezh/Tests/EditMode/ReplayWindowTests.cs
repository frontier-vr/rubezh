using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;

namespace Rubezh.Tests.EditMode
{
    // Повтор эпизода нарушения (VP-17): окно вокруг эпизода, выбор первого эпизода, проигрывание
    // по кругу и запись положений с интерполяцией.
    public class ReplayWindowTests
    {
        [Test]
        public void Window_FromBeforeStart_ToAfterViolation()
        {
            var v = new ViolationRecord(Violation.MuzzleOutOfSector, Severity.Critical, 40, 1.5);   // начало 38,5
            var w = ReplayWindow.For(v, before: 2, after: 1.5, maxLength: 8, recordedUntil: 100);
            Assert.AreEqual(36.5, w.From, 1e-9);
            Assert.AreEqual(41.5, w.To, 1e-9);
        }

        [Test]
        public void Window_ClampedToZeroAndToRecording()
        {
            var v = new ViolationRecord(Violation.StepOrder, Severity.Minor, 1, 0);
            var w = ReplayWindow.For(v, 2, 1.5, 8, recordedUntil: 1.2);
            Assert.AreEqual(0, w.From, 1e-9, "до начала упражнения записи нет");
            Assert.AreEqual(1.2, w.To, 1e-9, "после конца записи показывать нечего");
        }

        [Test]
        public void LongEpisode_KeepsTheEnd()
        {
            var v = new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 60, 20);   // 40–60
            var w = ReplayWindow.For(v, 2, 1.5, 8, 100);
            Assert.AreEqual(8, w.Length, 1e-9);
            Assert.AreEqual(61.5, w.To, 1e-9, "момент, когда нарушение засчитано, в окне");
        }

        [Test]
        public void DefaultIndex_CriticalFirst_ElseFirst_NoneIsMinusOne()
        {
            Assert.AreEqual(-1, ReplayWindow.DefaultIndex(new ViolationRecord[0]));
            Assert.AreEqual(-1, ReplayWindow.DefaultIndex(null));
            var minor = new[] { new ViolationRecord(Violation.StepOrder, Severity.Minor, 5, 0), new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 9, 0.4) };
            Assert.AreEqual(0, ReplayWindow.DefaultIndex(minor));
            var withCritical = new[] { minor[0], new ViolationRecord(Violation.LookIntoMuzzle, Severity.Critical, 7, 0.2) };
            Assert.AreEqual(1, ReplayWindow.DefaultIndex(withCritical), "грубое — причина остановки, его и показываем");
        }

        [Test]
        public void NotVisibleByMotion_Skipped()
        {
            Assert.IsFalse(ReplayWindow.Shows(Violation.NoGlasses));
            Assert.IsFalse(ReplayWindow.Shows(Violation.NoEarmuffs));
            Assert.IsFalse(ReplayWindow.Shows(Violation.CasingLeft));
            Assert.IsTrue(ReplayWindow.Shows(Violation.MuzzleOutOfSector));
            Assert.IsTrue(ReplayWindow.Shows(Violation.ReturnedLoaded), "магазин в пистолете у шкафа виден");

            var ppeOnly = new[] { new ViolationRecord(Violation.NoGlasses, Severity.Minor, 5, 0), new ViolationRecord(Violation.CasingLeft, Severity.Minor, 9, 0) };
            Assert.AreEqual(-1, ReplayWindow.DefaultIndex(ppeOnly), "показывать нечего");
            Assert.AreEqual(-1, ReplayWindow.NextIndex(ppeOnly, 0));
            Assert.AreEqual(0, ReplayWindow.Count(ppeOnly, 0, out _));

            var mixed = new[]
            {
                new ViolationRecord(Violation.NoGlasses, Severity.Minor, 5, 0),
                new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 9, 0.4),
                new ViolationRecord(Violation.CasingLeft, Severity.Minor, 50, 0),
                new ViolationRecord(Violation.StepOrder, Severity.Minor, 60, 0)
            };
            Assert.AreEqual(1, ReplayWindow.DefaultIndex(mixed), "первым — первое видимое");
            Assert.AreEqual(3, ReplayWindow.NextIndex(mixed, 1), "гильзы пропущены");
            Assert.AreEqual(1, ReplayWindow.NextIndex(mixed, 3), "по кругу, «без очков» пропущено");
            Assert.AreEqual(2, ReplayWindow.Count(mixed, 3, out int ordinal));
            Assert.AreEqual(2, ordinal);
        }

        [Test]
        public void Loop_PlaysWindowThenPausesThenRepeats()
        {
            var w = new ReplayWindow(10, 14);
            Assert.AreEqual(10, w.Loop(0, 1), 1e-9);
            Assert.AreEqual(12.5, w.Loop(2.5, 1), 1e-9);
            Assert.AreEqual(14, w.Loop(4.5, 1), 1e-9, "в паузе стоит последний кадр");
            Assert.AreEqual(10.5, w.Loop(5.5, 1), 1e-9, "дальше по кругу");
        }

        [Test]
        public void PoseTrack_InterpolatesBetweenSamples()
        {
            var track = new PoseTrack(100);
            track.Add(0, new Pose(Vector3.zero, Quaternion.identity));
            track.Add(1, new Pose(new Vector3(2, 0, 0), Quaternion.Euler(0, 90, 0)));
            Assert.IsTrue(track.TrySample(0.5, out var p, out bool visible));
            Assert.IsTrue(visible);
            Assert.AreEqual(1f, p.position.x, 1e-4f);
            Assert.AreEqual(45f, p.rotation.eulerAngles.y, 0.01f);
            track.TrySample(-3, out p, out _);
            Assert.AreEqual(0f, p.position.x, 1e-4f, "до записи — первый кадр");
            track.TrySample(9, out p, out _);
            Assert.AreEqual(2f, p.position.x, 1e-4f, "после записи — последний кадр");
        }

        [Test]
        public void PoseTrack_RingBuffer_DropsOldest()
        {
            var track = new PoseTrack(3);
            for (int i = 0; i < 5; i++) track.Add(i, new Pose(new Vector3(i, 0, 0), Quaternion.identity));
            Assert.AreEqual(3, track.Count);
            Assert.AreEqual(2, track.FirstTime, 1e-9);
            Assert.AreEqual(4, track.LastTime, 1e-9);
            track.TrySample(3.5, out var p, out _);
            Assert.AreEqual(3.5f, p.position.x, 1e-4f, "поиск по кругу");
        }

        [Test]
        public void PoseTrack_SameTime_ReplacesLast_AndKeepsVisibility()
        {
            var track = new PoseTrack(10);
            track.Add(0, new Pose(Vector3.zero, Quaternion.identity));
            track.Add(1, new Pose(Vector3.one, Quaternion.identity), visible: false);
            track.Add(1, new Pose(Vector3.one * 2, Quaternion.identity), visible: false);
            Assert.AreEqual(2, track.Count);
            track.TrySample(1, out var p, out bool visible);
            Assert.AreEqual(2f, p.position.x, 1e-4f);
            Assert.IsFalse(visible, "скрытый контроллер — призрак руки скрыт");
        }
    }
}
