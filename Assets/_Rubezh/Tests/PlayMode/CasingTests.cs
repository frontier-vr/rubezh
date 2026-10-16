using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;

namespace Rubezh.Tests.PlayMode
{
    // Гильзы (VP-19): вылет из EjectionPort на каждый выстрел, дуга в гильзоуловитель, пул,
    // упавшая гильза на полочке O-21, счётчик вне уловителя, Restart.
    public class CasingTests
    {
        private GameObject _root, _pistol;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private WeaponController _weapon;
        private CasingEjector _casings;
        private Transform _catcher, _shelf, _port;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.casingReturnSeconds = 0.2f;
#if UNITY_EDITOR
            _pistol = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/Pistol_Interactive.prefab"),
                                         new Vector3(0.2f, 1.25f, 3.6f), Quaternion.identity);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/Casing_Interactive.prefab");
#else
            GameObject prefab = null;
#endif
            _pistol.GetComponent<Rigidbody>().isKinematic = true;
            _weapon = _pistol.GetComponent<WeaponController>();
            _weapon.Configure(_runner, null);
            _port = _pistol.transform.Find("EjectionPort");
            _catcher = new GameObject("CatcherZone").transform;
            _catcher.SetParent(_root.transform, false);
            _catcher.position = new Vector3(0.95f, 0.85f, 3.95f);
            _shelf = new GameObject("O-21").transform;
            _shelf.SetParent(_root.transform, false);
            _shelf.position = new Vector3(0.6f, 1.03f, 3.78f);
            var shelfTop = GameObject.CreatePrimitive(PrimitiveType.Cube);     // столешница полочки O-21
            shelfTop.transform.SetParent(_root.transform, false);
            shelfTop.transform.position = new Vector3(0.6f, 1.0f, 3.78f);
            shelfTop.transform.localScale = new Vector3(0.3f, 0.02f, 0.2f);
            _root.AddComponent<AudioListener>();
            _casings = _root.AddComponent<CasingEjector>();
            _casings.Configure(_runner, _rules, _weapon, _port, prefab, _catcher, _shelf);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_pistol);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        private void LoadAndFire(int shots)
        {
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _weapon.InsertMagazine(10);
            _weapon.Apply(WeaponCommand.SlideBack);
            _weapon.Apply(WeaponCommand.SlideForward);
            _weapon.Apply(WeaponCommand.SafetyOff);
            for (int i = 0; i < shots; i++)
            {
                _weapon.Apply(WeaponCommand.TriggerPull);
                _weapon.Apply(WeaponCommand.TriggerRelease);
            }
        }

        [Test]
        public void Pool_IsPrewarmed()
        {
            Assert.AreEqual(10, _casings.Created, "пул создан заранее, в бою Instantiate нет");
            Assert.AreEqual(0, _casings.Active);
        }

        [UnityTest]
        public IEnumerator Shot_EjectsCasingFromPort()
        {
            LoadAndFire(1);
            Assert.AreEqual(1, _casings.Active);
            Assert.AreEqual(1, _casings.CasingsOutsideCatcher, "в полёте гильза ещё вне уловителя");
            yield return null;
        }

        [Test]
        public void Launch_ArcEndsInCatcher()
        {
            var from = _port.position;
            var v = _casings.LaunchVelocity(from);
            float t = _rules.casingFlightSeconds;
            var landing = from + v * t + 0.5f * Physics.gravity * t * t;
            Assert.IsTrue(_casings.InCatcher(landing), $"дуга кончается в уловителе: {landing}");
        }

        [Test]
        public void FarFromCatcher_EjectsToTheRight()
        {
            _pistol.transform.position = new Vector3(-2.4f, 1.3f, 1.7f);
            var v = _casings.LaunchVelocity(_port.position);
            Assert.Greater(Vector3.Dot(v, _port.right), 1f);
        }

        [UnityTest]
        public IEnumerator Pool_DoesNotGrow()
        {
            int ejected = 0;
            _weapon.CasingEjected += () => ejected++;
            LoadAndFire(0);
            for (int guard = 0; ejected < 12 && guard < 40; guard++)
            {
                if (_weapon.State.Jam != Jam.None || _weapon.State.Chamber != Chamber.Loaded)
                {
                    if (!_weapon.State.MagazineInserted || _weapon.State.RoundsInMagazine == 0)
                    {
                        _weapon.Apply(WeaponCommand.EjectMagazine);
                        _weapon.InsertMagazine(10);
                    }
                    _weapon.Apply(WeaponCommand.SlideBack);          // осечка и задержка снимаются затвором
                    _weapon.Apply(WeaponCommand.SlideForward);
                    _weapon.Apply(WeaponCommand.SlideStopRelease);
                    continue;
                }
                _weapon.Apply(WeaponCommand.TriggerPull);
                _weapon.Apply(WeaponCommand.TriggerRelease);
            }
            Assert.AreEqual(12, ejected);
            yield return null;
            Assert.AreEqual(10, _casings.Created);
            Assert.AreEqual(10, _casings.Active);
        }

        [UnityTest]
        public IEnumerator FloorCasing_AppearsOnShelf_CatcherCasingStays()
        {
            LoadAndFire(2);
            var all = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            Rigidbody first = null, second = null;
            foreach (var rb in all) if (rb.name.StartsWith("Casing_") && rb.gameObject.activeSelf) { if (first == null) first = rb; else second = rb; }
            first.isKinematic = true; second.isKinematic = true;
            first.position = first.transform.position = new Vector3(1.5f, 0.05f, 3.0f);    // пол
            second.position = second.transform.position = _catcher.position;               // уловитель
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Vector3.Distance(first.transform.position, _shelf.position), 0.12f, "гильза с пола — на полочке O-21");
            Assert.AreEqual(second.transform.position, _catcher.position, "гильзу в уловителе не трогаем");
            Assert.AreEqual(1, _casings.CasingsOutsideCatcher, "вне уловителя — только гильза на полочке");
        }

        [UnityTest]
        public IEnumerator Restart_ReturnsCasingsToPool()
        {
            LoadAndFire(3);
            _runner.Restart();
            yield return null;
            Assert.AreEqual(0, _casings.Active);
            Assert.AreEqual(0, _casings.CasingsOutsideCatcher);
        }
    }
}
