using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // TriggerDisciplineDetector с заглушками ITriggerInput: считается только рука на Grip_Primary.
    public class TriggerDisciplineDetectorTests
    {
        private sealed class FakeTrigger : ITriggerInput
        {
            public float PressValue { get; set; }
            public bool IsTouched { get; set; }
        }

        private sealed class FakeHeld : IHeldWeapon
        {
            public bool IsHeld { get; set; } = true;
            public Hand PrimaryHand { get; set; } = Hand.Right;
            public WeaponState State => new WeaponState(true, 5, Chamber.Loaded, false, true, Jam.None);
        }

        private GameObject _go;
        private ExerciseRunner _runner;
        private FakeTrigger _left, _right;
        private FakeHeld _held;
        private List<ViolationRecord> _raised;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("Runner");
            _runner = _go.AddComponent<ExerciseRunner>();
            _raised = new List<ViolationRecord>();
            _runner.ViolationRaised += _raised.Add;
            _left = new FakeTrigger(); _right = new FakeTrigger(); _held = new FakeHeld();
            _go.AddComponent<TriggerDisciplineDetector>().Configure(_runner, _held, _left, _right);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(_go); yield return null; }

        private static IEnumerator Wait(float s) { float end = Time.time + s; while (Time.time < end) yield return null; }

        [UnityTest]
        public IEnumerator PrimaryHandTouch_OutsideFire_IsViolation()
        {
            _right.IsTouched = true; yield return Wait(0.5f);
            _right.IsTouched = false; yield return Wait(0.1f);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.FingerOnTrigger, _raised[0].Type);
        }

        [UnityTest]
        public IEnumerator FreeHandTouch_IsNotViolation()
        {
            _left.IsTouched = true; yield return Wait(0.5f);
            _left.IsTouched = false; yield return Wait(0.1f);
            Assert.IsEmpty(_raised);
        }

        [UnityTest]
        public IEnumerator LeftHanded_CountsLeftTrigger()
        {
            _held.PrimaryHand = Hand.Left;
            _left.IsTouched = true; yield return Wait(0.5f);
            _left.IsTouched = false; yield return Wait(0.1f);
            Assert.AreEqual(1, _raised.Count);
        }

        [UnityTest]
        public IEnumerator InFire_TouchIsAllowed()
        {
            var d = _runner.Director;
            var empty = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
            d.UpdateFacts(new ExerciseFacts(true, empty, 0, Place.Hand, Place.InspectShelf, true, true, false));
            d.UpdateFacts(new ExerciseFacts(false, empty, 5, Place.LineStand, Place.LineStand, true, true, true));
            d.UpdateFacts(new ExerciseFacts(true, new WeaponState(true, 5, Chamber.Empty, true, true, Jam.None), 0, Place.Hand, Place.InWeapon, true, true, true));
            Assert.AreEqual(Phase.Fire, d.Phase);
            _right.IsTouched = true; yield return Wait(0.6f);
            _right.IsTouched = false; yield return Wait(0.1f);
            Assert.IsEmpty(_raised);
        }
    }
}
