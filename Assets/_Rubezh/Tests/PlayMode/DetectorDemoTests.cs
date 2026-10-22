using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Демо детекторов (VP-18): выключено в упражнении, по кнопке показывает луч сектора до первой
    // поверхности (зелёный в секторе, красный вне, белый — пистолет не в руке) и конус взгляда в ствол.
    public class DetectorDemoTests
    {
        private sealed class FakeHeld : MonoBehaviour, IHeldWeapon
        {
            public bool Held = true;
            public bool IsHeld => Held;
            public Hand PrimaryHand => Hand.Right;
            public WeaponState State => new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private SectorDetector _sector;
        private DetectorDemo _demo;
        private FakeHeld _held;
        private Transform _pistol, _muzzle;
        private LineRenderer _ray, _cone;
        private GameObject _view;

        private void Box(string name, Vector3 center, Vector3 size, int layer, bool trigger)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = layer;
            go.transform.position = center; go.transform.localScale = size;
            go.GetComponent<Collider>().isTrigger = trigger;
            _spawned.Add(go);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            int env = LayerMask.NameToLayer("Environment");
            int sector = LayerMask.NameToLayer("Sector");
            Box("Shield_R", new Vector3(1.6f, 1.5f, 9f), new Vector3(0.1f, 3f, 8.8f), env, false);
            Box("BulletTrap", new Vector3(0f, 1.5f, 13.7f), new Vector3(3f, 3f, 0.6f), env, false);
            Box("Sector_TrapFace", new Vector3(0f, 1.5f, 13.39f), new Vector3(3f, 2.8f, 0.02f), sector, true);

            var root = new GameObject("Runner"); _spawned.Add(root);
            var runner = root.AddComponent<ExerciseRunner>();
            var pistol = new GameObject("Pistol"); _spawned.Add(pistol);
            pistol.transform.position = new Vector3(0f, 1.5f, 3.6f);
            _pistol = pistol.transform;
            _muzzle = new GameObject("Muzzle").transform; _muzzle.SetParent(_pistol, false);
            _held = pistol.AddComponent<FakeHeld>();
            _sector = root.AddComponent<SectorDetector>();
            _sector.Configure(runner, _muzzle, _held, 1 << sector, 1 << env);

            var demoGo = new GameObject("DetectorDemo"); _spawned.Add(demoGo);
            _view = new GameObject("View"); _view.transform.SetParent(demoGo.transform, false);
            _ray = new GameObject("Ray").AddComponent<LineRenderer>(); _ray.transform.SetParent(_view.transform, false);
            _cone = new GameObject("Cone").AddComponent<LineRenderer>(); _cone.transform.SetParent(_view.transform, false);
            var label = new GameObject("Label").AddComponent<TMPro.TextMeshPro>(); label.transform.SetParent(_view.transform, false);
            var head = new GameObject("Head").transform; head.SetParent(demoGo.transform, false); head.position = new Vector3(0f, 1.6f, 3.2f);
            _demo = demoGo.AddComponent<DetectorDemo>();
            _demo.Configure(_sector, null, null, null, _muzzle, head);
            _demo.ConfigureView(_view, _ray, _cone, label, head);
            _demo.SetVisible(false);
            Physics.SyncTransforms();          // коробки только что созданы: без шага физики луч их не видит
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in _spawned) Object.Destroy(go);
            _spawned.Clear();
            yield return null;
        }

        private IEnumerator Aim(Vector3 direction)
        {
            _pistol.rotation = Quaternion.LookRotation(direction);
            yield return null;
            yield return null;
        }

        [Test]
        public void HiddenInExercise_ShownOnToggle()
        {
            Assert.IsFalse(_demo.Visible, "в обычном упражнении демо выключено");
            _demo.SetVisible(true);
            Assert.IsTrue(_view.activeSelf);
        }

        [UnityTest]
        public IEnumerator InSector_GreenRayToTrapFace()
        {
            _demo.SetVisible(true);
            yield return Aim(Vector3.forward);
            Assert.IsTrue(_sector.InSector);
            Assert.AreEqual(13.38f, _ray.GetPosition(1).z, 0.02f, "луч до лица пулеулавливателя");
            Assert.Greater(_ray.startColor.g, _ray.startColor.r, "зелёный");
            StringAssert.Contains("в секторе", _demo.Text());
        }

        [UnityTest]
        public IEnumerator OutOfSector_RedRayToShield()
        {
            _demo.SetVisible(true);
            yield return Aim(new Vector3(1f, 0f, 1.2f));
            Assert.IsFalse(_sector.InSector);
            Assert.AreEqual(1.55f, _ray.GetPosition(1).x, 0.05f, "луч упёрся в щит");
            Assert.Greater(_ray.startColor.r, _ray.startColor.g, "красный");
            StringAssert.Contains("вне сектора", _demo.Text());
        }

        [UnityTest]
        public IEnumerator NotHeld_WhiteRay_NotJudged()
        {
            _demo.SetVisible(true);
            _held.Held = false;
            yield return Aim(new Vector3(1f, 0f, 1.2f));
            Assert.AreEqual(_ray.startColor.r, _ray.startColor.g, 0.05f, "белый: сектор не судится");
            StringAssert.Contains("не в руке", _demo.Text());
        }

        [UnityTest]
        public IEnumerator LookCone_AtRuleDistanceAndAngle()
        {
            _demo.SetVisible(true);
            yield return Aim(Vector3.forward);
            Assert.AreEqual(17 + 8, _cone.positionCount, "окружность и четыре образующие");
            // Без RulesConfig — значения по умолчанию: 0,7 м и 30°.
            var center = _muzzle.position + _muzzle.forward * 0.7f;
            float radius = Vector3.Distance(_cone.GetPosition(0), center);
            Assert.AreEqual(0.7f * Mathf.Tan(30f * Mathf.Deg2Rad), radius, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Hidden_DoesNotDraw()
        {
            yield return Aim(Vector3.forward);
            Assert.AreEqual(2, _ray.positionCount);
            Assert.AreEqual(Vector3.forward, _ray.GetPosition(1), "скрытое демо ничего не рисует: точки по умолчанию");
        }
    }
}
