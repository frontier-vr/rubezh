using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // MuzzleLookDetector: камера перед дульным срезом — грубое нарушение; сбоку и мимо — нет.
    public class MuzzleLookDetectorTests
    {
        private GameObject _root;
        private ExerciseRunner _runner;
        private Transform _head, _muzzle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            int layer = LayerMask.NameToLayer("Muzzle");
            Assert.That(layer, Is.GreaterThan(0));
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_root.transform, false);
            _muzzle.position = new Vector3(0f, 1.2f, 0f);          // ствол смотрит в +Z
            _muzzle.gameObject.layer = layer;
            var probe = _muzzle.gameObject.AddComponent<SphereCollider>();
            probe.radius = 0.03f; probe.isTrigger = true;
            _head = new GameObject("Head").transform;
            _head.SetParent(_root.transform, false);
            _root.AddComponent<MuzzleLookDetector>().Configure(_runner, _head, _muzzle, probe, 1 << layer);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(_root); yield return null; }

        private static IEnumerator Wait(float s) { float end = Time.time + s; while (Time.time < end) yield return null; }

        private void HeadAt(Vector3 offsetFromMuzzle)
        {
            _head.position = _muzzle.position + offsetFromMuzzle;
            _head.rotation = Quaternion.LookRotation(_muzzle.position - _head.position);
        }

        [UnityTest]
        public IEnumerator HeadInFrontOfMuzzle_LookingIn_Fails()
        {
            HeadAt(new Vector3(0f, 0.05f, 0.35f));                 // перед срезом, смотрит в ствол
            yield return Wait(0.5f);
            Assert.AreEqual(Phase.Failed, _runner.Phase);
            Assert.AreEqual(Violation.LookIntoMuzzle, _runner.Director.Result.FailReason);
        }

        [UnityTest]
        public IEnumerator HeadAtSide_LookingAtMuzzle_IsAllowed()
        {
            HeadAt(new Vector3(0.35f, 0.05f, 0f));                 // сбоку на срез
            yield return Wait(0.5f);
            Assert.AreNotEqual(Phase.Failed, _runner.Phase);
        }

        [UnityTest]
        public IEnumerator HeadInFront_LookingAway_IsAllowed()
        {
            _head.position = _muzzle.position + new Vector3(0f, 0f, 0.35f);
            _head.rotation = Quaternion.LookRotation(Vector3.forward);   // спиной к стволу
            yield return Wait(0.5f);
            Assert.AreNotEqual(Phase.Failed, _runner.Phase);
        }

        [UnityTest]
        public IEnumerator FarAway_IsAllowed()
        {
            HeadAt(new Vector3(0f, 0f, 2.0f));                     // дальше дальности луча
            yield return Wait(0.5f);
            Assert.AreNotEqual(Phase.Failed, _runner.Phase);
        }
    }
}
