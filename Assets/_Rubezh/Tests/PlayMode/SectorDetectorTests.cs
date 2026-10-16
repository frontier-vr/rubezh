using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // SectorDetector в коридоре из примитивов по docs/layout.md §5: настоящий луч, настоящее время.
    public class SectorDetectorTests
    {
        private sealed class FakeHeld : MonoBehaviour, IHeldWeapon
        {
            public bool Held = true;
            public bool Loaded = true;
            public bool IsHeld => Held;
            public WeaponState State => Loaded
                ? new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None)
                : new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private ExerciseRunner _runner;
        private SectorDetector _detector;
        private FakeHeld _held;
        private Transform _pistol;
        private List<ViolationRecord> _raised;

        private GameObject Box(string name, Vector3 center, Vector3 size, int layer, bool trigger)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = layer;
            go.transform.position = center; go.transform.localScale = size;
            go.GetComponent<Collider>().isTrigger = trigger;
            _spawned.Add(go);
            return go;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            int env = LayerMask.NameToLayer("Environment");
            int sector = LayerMask.NameToLayer("Sector");
            int target = LayerMask.NameToLayer("Target");
            Assert.That(env, Is.GreaterThan(0)); Assert.That(sector, Is.GreaterThan(0)); Assert.That(target, Is.GreaterThan(0));

            // Коридор: щиты, потолок, пулеулавливатель, пол зала; поверхность сектора — лицо ловушки и пол коридора
            Box("Shield_L", new Vector3(-1.6f, 1.5f, 9f), new Vector3(0.1f, 3f, 8.8f), env, false);
            Box("Shield_R", new Vector3(1.6f, 1.5f, 9f), new Vector3(0.1f, 3f, 8.8f), env, false);
            Box("Ceiling", new Vector3(0f, 3.05f, 7f), new Vector3(8f, 0.1f, 14f), env, false);
            Box("Floor", new Vector3(0f, -0.05f, 7f), new Vector3(8f, 0.1f, 14f), env, false);
            Box("BulletTrap", new Vector3(0f, 1.5f, 13.7f), new Vector3(3f, 3f, 0.6f), env, false);
            Box("Sector_TrapFace", new Vector3(0f, 1.5f, 13.39f), new Vector3(3f, 2.8f, 0.02f), sector, true);
            Box("Sector_Floor", new Vector3(0f, 0.01f, 8.8f), new Vector3(3.1f, 0.02f, 9.2f), sector, true);
            Box("TargetPlane", new Vector3(0f, 1.5f, 11.2f), new Vector3(0.5f, 0.5f, 0.01f), target, false);

            var root = new GameObject("Runner"); _spawned.Add(root);
            _runner = root.AddComponent<ExerciseRunner>();
            _raised = new List<ViolationRecord>();
            _runner.ViolationRaised += _raised.Add;

            var pistol = new GameObject("Pistol"); _spawned.Add(pistol);
            pistol.transform.position = new Vector3(0.2f, 1.2f, 3.6f);
            _pistol = pistol.transform;
            var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(_pistol, false);
            muzzle.localPosition = new Vector3(0f, 0.034f, 0.155f);
            _held = pistol.AddComponent<FakeHeld>();
            _detector = pistol.AddComponent<SectorDetector>();
            _detector.Configure(_runner, muzzle, _held, (1 << sector) | (1 << target), 1 << env);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
            yield return null;
        }

        private void Aim(float yaw, float pitch = 0f) => _pistol.rotation = Quaternion.Euler(pitch, yaw, 0f);

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator PointingDownrange_IsInSector()
        {
            Aim(0f);
            yield return Wait(0.3f);
            Assert.IsTrue(_detector.InSector);
            Aim(0f, 35f);                     // в пол коридора
            yield return Wait(0.3f);
            Assert.IsTrue(_detector.InSector);
            Assert.IsEmpty(_raised);
        }

        [UnityTest]
        public IEnumerator SweepSideways_ShortGivesMinor()
        {
            Aim(0f); yield return Wait(0.2f);
            Aim(80f);                         // вбок, в правый щит и стену
            yield return Wait(0.6f);
            Assert.IsFalse(_detector.InSector);
            Aim(0f); yield return Wait(0.2f);

            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.MuzzleOutOfSector, _raised[0].Type);
            Assert.AreEqual(Severity.Minor, _raised[0].Severity);
            Assert.That(_raised[0].Duration, Is.InRange(0.5, 0.9));
        }

        [UnityTest]
        public IEnumerator SweepSideways_LongWhileLoaded_FailsExercise()
        {
            Aim(-80f);
            yield return Wait(1.8f);
            Assert.AreEqual(Phase.Failed, _runner.Phase);
            Assert.AreEqual(Violation.MuzzleOutOfSector, _runner.Director.Result.FailReason);
        }

        [UnityTest]
        public IEnumerator PointingBackIntoRoom_IsOutOfSector()
        {
            Aim(180f);
            yield return Wait(0.2f);
            Assert.IsFalse(_detector.InSector);
        }

        [UnityTest]
        public IEnumerator PistolNotHeld_DoesNotCount()
        {
            _held.Held = false;
            Aim(90f);
            yield return Wait(2f);
            Assert.IsEmpty(_raised);
            Assert.AreNotEqual(Phase.Failed, _runner.Phase);
        }
    }
}
