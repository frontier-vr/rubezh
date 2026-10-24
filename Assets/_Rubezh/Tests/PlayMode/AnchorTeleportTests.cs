using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // #163: смена якоря доходит до директора как Teleport — уход с осмотра даёт IncompleteInspection.
    public class AnchorTeleportTests
    {
        private GameObject _root;
        private ExerciseRunner _runner;
        private Transform _rig;
        private readonly List<ViolationRecord> _raised = new List<ViolationRecord>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            _rig = Point("Rig", new Vector3(-2.4f, 0, 1.7f));
            var locator = _root.AddComponent<AnchorLocator>();
            locator.Configure(_rig, Point("A1", new Vector3(-2.4f, 0, 1.7f)), Point("A2", new Vector3(2.4f, 0, 1.9f)), Point("A3", new Vector3(0, 0, 3.25f)));
            locator.SetRunner(_runner);
            _raised.Clear();
            _runner.ViolationRaised += _raised.Add;
            yield return null;
        }

        private Transform Point(string name, Vector3 p)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root.transform, false);
            t.position = p;
            return t;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingInspection_IsIncompleteInspection()
        {
            // Пистолет взят — шаг «Осмотр».
            _runner.UpdateFacts(new ExerciseFacts(true, new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None), 0,
                                                  Place.Hand, Place.InWeapon, false, false, false));
            Assert.AreEqual(Phase.Inspect, _runner.Phase);
            yield return null;
            Assert.IsEmpty(_raised, "старт сцены у шкафа — не телепорт");
            _rig.position = new Vector3(2.4f, 0, 1.9f);       // ушёл к столу, не закончив осмотр
            yield return null;
            yield return null;
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Violation.IncompleteInspection, _raised[0].Type);
        }

        [UnityTest]
        public IEnumerator StayingAtAnchor_NoReports()
        {
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsEmpty(_raised);
        }
    }
}
