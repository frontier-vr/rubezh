using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // ShotResolver в коридоре из примитивов: попадание в правильную точку мишени, промах,
    // грубые выстрелы до «Огонь», после «Стой» и вне сектора.
    public class ShotResolverTests
    {
        private sealed class FakeHeld : IHeldWeapon
        {
            public bool IsHeld => true;
            public Hand PrimaryHand => Hand.Right;
            public WeaponState State => new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None);
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private ExerciseRunner _runner;
        private ShotResolver _resolver;
        private Transform _pistol;
        private Weapon _weapon;
        private Transform _target;
        private readonly List<Vector3> _hits = new List<Vector3>();

        private GameObject Box(string name, Vector3 c, Vector3 s, int layer, bool trigger)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.layer = layer; g.transform.position = c; g.transform.localScale = s;
            g.GetComponent<Collider>().isTrigger = trigger;
            _spawned.Add(g); return g;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            int env = LayerMask.NameToLayer("Environment"), sector = LayerMask.NameToLayer("Sector"), target = LayerMask.NameToLayer("Target");
            Box("Shield_L", new Vector3(-1.6f, 1.5f, 9f), new Vector3(0.1f, 3f, 8.8f), env, false);
            Box("Shield_R", new Vector3(1.6f, 1.5f, 9f), new Vector3(0.1f, 3f, 8.8f), env, false);
            Box("Floor", new Vector3(0f, -0.05f, 7f), new Vector3(8f, 0.1f, 14f), env, false);
            Box("BulletTrap", new Vector3(0f, 1.5f, 13.7f), new Vector3(3f, 3f, 0.6f), env, false);
            Box("Sector_TrapFace", new Vector3(0f, 1.5f, 13.39f), new Vector3(3f, 2.8f, 0.02f), sector, true);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);   // как TargetPlane в Main.unity
            quad.name = "TargetPlane"; quad.layer = target;
            quad.transform.position = new Vector3(0f, 1.5f, 11.2f); quad.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            _spawned.Add(quad); _target = quad.transform;

            var root = new GameObject("Runner"); _spawned.Add(root);
            _runner = root.AddComponent<ExerciseRunner>();

            var p = new GameObject("Pistol"); _spawned.Add(p);
            _pistol = p.transform; _pistol.position = new Vector3(0f, 1.5f, 3.7f);
            var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(_pistol, false);
            var sectorDetector = p.AddComponent<SectorDetector>();
            sectorDetector.Configure(_runner, muzzle, new FakeHeld(), (1 << sector) | (1 << target), 1 << env);
            _resolver = p.AddComponent<ShotResolver>();
            _resolver.Configure(_runner, muzzle, sectorDetector, 1 << target, 1 << env);
            _resolver.Hit += (local, plane) => _hits.Add(local);
            _weapon = new Weapon(new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None));
            _resolver.Bind(_weapon);
            _hits.Clear();
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _resolver.Bind(null);
            foreach (var g in _spawned) if (g != null) Object.Destroy(g);
            _spawned.Clear();
            yield return null;
        }

        private void AimAt(Vector3 world)
        {
            _pistol.rotation = Quaternion.LookRotation(world - _pistol.position);
            Physics.SyncTransforms();
        }

        private void Shoot()
        {
            Assert.IsTrue(_weapon.Apply(WeaponCommand.TriggerPull));
            Assert.IsTrue(_weapon.Apply(WeaponCommand.TriggerRelease));
        }

        private void GoTo(Phase phase)
        {
            var d = _runner.Director;
            var empty = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
            d.UpdateFacts(new ExerciseFacts(true, empty, 0, Place.Hand, Place.InspectShelf, true, true, false));
            d.UpdateFacts(new ExerciseFacts(false, empty, 5, Place.LineStand, Place.LineStand, true, true, true));
            d.UpdateFacts(new ExerciseFacts(true, new WeaponState(true, 5, Chamber.Empty, true, true, Jam.None), 0, Place.Hand, Place.InWeapon, true, true, true));
            if (phase == Phase.Cease)
                d.UpdateFacts(new ExerciseFacts(true, new WeaponState(true, 0, Chamber.Empty, true, false, Jam.None), 0, Place.Hand, Place.InWeapon, true, true, true));
            Assert.AreEqual(phase, d.Phase);
        }

        [UnityTest]
        public IEnumerator InFire_HitIsAtRightPointOfTarget()
        {
            GoTo(Phase.Fire);
            AimAt(new Vector3(0.1f, 1.55f, 11.2f));      // 10 см правее и 5 см выше центра
            Shoot();
            yield return null;

            Assert.AreEqual(1, _hits.Count);
            // Quad 0,5 × 0,5 м: локальные координаты −0,5…0,5 → (0,2; 0,1)
            Assert.AreEqual(0.2f, _hits[0].x, 0.01f);
            Assert.AreEqual(0.1f, _hits[0].y, 0.01f);
            Assert.AreEqual(1, _resolver.Hits);
            Assert.AreEqual(Phase.Fire, _runner.Phase);
        }

        [UnityTest]
        public IEnumerator InFire_Miss_GivesNoHit()
        {
            GoTo(Phase.Fire);
            AimAt(new Vector3(0.9f, 2.4f, 13.4f));        // в пулеулавливатель мимо мишени
            Shoot();
            yield return null;
            Assert.IsEmpty(_hits);
            Assert.AreEqual(1, _resolver.Shots);
            Assert.AreEqual(0, _resolver.Hits);
            Assert.AreEqual(Phase.Fire, _runner.Phase, "промах — не нарушение");
        }

        [UnityTest]
        public IEnumerator ShotBeforeFire_Fails()
        {
            AimAt(new Vector3(0f, 1.5f, 11.2f));
            Shoot();
            yield return null;
            Assert.AreEqual(Phase.Failed, _runner.Phase);
            Assert.AreEqual(Violation.ShotBeforeFire, _runner.Director.Result.FailReason);
        }

        [UnityTest]
        public IEnumerator ShotAfterCease_Fails()
        {
            GoTo(Phase.Cease);
            AimAt(new Vector3(0f, 1.5f, 11.2f));
            Shoot();
            yield return null;
            Assert.AreEqual(Violation.ShotAfterCease, _runner.Director.Result.FailReason);
        }

        [UnityTest]
        public IEnumerator ShotOutOfSector_Fails()
        {
            GoTo(Phase.Fire);
            AimAt(new Vector3(1.6f, 1.5f, 6f));           // в боковой щит
            Shoot();
            yield return null;
            Assert.IsEmpty(_hits);
            Assert.AreEqual(Violation.ShotOutOfSector, _runner.Director.Result.FailReason);
            Assert.AreEqual(1, _runner.Director.Result.Shots, "выстрел учтён до остановки");
        }
    }
}
