using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Пробоины из пула (VP-23): появляются в точке попадания, перед бумагой, едут с мишенью,
    // пул не растёт, Restart убирает.
    public class BulletHolesTests
    {
        private GameObject _root, _plane;
        private ExerciseRunner _runner;
        private BulletHoles _holes;
        private ShotResolver _resolver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            _plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _plane.name = "TargetPlane"; _plane.layer = LayerMask.NameToLayer("Target");
            _plane.transform.position = new Vector3(0f, 1.5f, 11.2f);
            _plane.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var pistol = new GameObject("Pistol"); pistol.transform.SetParent(_root.transform, false);
            pistol.transform.position = new Vector3(0f, 1.5f, 3.7f);
            _resolver = pistol.AddComponent<ShotResolver>();
            _resolver.Configure(_runner, pistol.transform, null, 1 << _plane.layer, 0);
            var holesGo = new GameObject("BulletHoles"); holesGo.transform.SetParent(_root.transform, false);
            _holes = holesGo.AddComponent<BulletHoles>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _holes.Configure(_runner, _resolver, mat, mat);
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_plane); Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoleAppearsAtHitPoint_InFrontOfPaper()
        {
            _holes.Place(new Vector3(0.2f, 0.1f, 0f), _plane.transform);
            yield return null;
            var hole = _holes.Pool[0];
            Assert.IsTrue(hole.gameObject.activeInHierarchy);
            Assert.AreEqual(_plane.transform, hole.parent, "пробоина — дочерняя плоскости мишени");
            Assert.AreEqual(0.1f, hole.position.x, 0.002f);
            Assert.AreEqual(1.55f, hole.position.y, 0.002f);
            Assert.Less(hole.position.z, 11.2f, "перед бумагой, со стороны стрелка");
            var normal = hole.TransformDirection(hole.GetComponent<MeshFilter>().sharedMesh.normals[0]);
            Assert.Less(normal.z, -0.9f, "лицевая сторона пробоины смотрит на стрелка (−Z), иначе её отсечёт culling");
            Assert.AreEqual(0.03f, hole.GetComponent<MeshRenderer>().bounds.size.x, 0.003f, "диаметр по умолчанию RulesConfig");
        }

        [UnityTest]
        public IEnumerator HoleMovesWithTarget()
        {
            _holes.Place(Vector3.zero, _plane.transform);
            _plane.transform.position += new Vector3(0f, 0f, -2f);   // каретка подъехала
            yield return null;
            Assert.AreEqual(9.2f, _holes.Pool[0].position.z, 0.01f);
        }

        [UnityTest]
        public IEnumerator PoolDoesNotGrow_OldestHoleIsReused()
        {
            for (int i = 0; i < 12; i++) _holes.Place(new Vector3(0.01f * i, 0f, 0f), _plane.transform);
            yield return null;
            Assert.AreEqual(10, _holes.Pool.Count);
            Assert.AreEqual(10, _holes.Visible);
            Assert.AreEqual(0.005f * 10, _holes.Pool[0].position.x, 0.002f, "11-я пробоина заняла первую");
        }

        [UnityTest]
        public IEnumerator Restart_ClearsHoles()
        {
            _holes.Place(Vector3.zero, _plane.transform);
            _runner.Restart();
            yield return null;
            Assert.AreEqual(0, _holes.Visible);
            Assert.IsFalse(_holes.Pool[0].gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ShotResolverHit_PlacesHole()
        {
            var weapon = new Weapon(new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None));
            _resolver.Bind(weapon);
            _resolver.transform.rotation = Quaternion.LookRotation(new Vector3(0.05f, 1.5f, 11.2f) - _resolver.transform.position);
            Physics.SyncTransforms();
            weapon.Apply(WeaponCommand.TriggerPull);
            yield return null;
            Assert.AreEqual(1, _holes.Visible);
            Assert.AreEqual(0.05f, _holes.Pool[0].position.x, 0.005f);
            _resolver.Bind(null);
        }
    }
}
