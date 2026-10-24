using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.XR;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.Tests.PlayMode
{
    // Пульт и каретка мишени (NG-12): регулятор 3/5/7/9 м, кнопка подаёт мишень, каретка едет и
    // рамка разворачивается к стрелку; Restart — к ближнему краю ребром.
    public class TargetControlTests
    {
        private sealed class Finger : XRBaseInteractor { }

        private GameObject _root, _mech;
        private XRInteractionManager _manager;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private TargetCarriage _target;
        private TargetRemote _remote;
        private XRSimpleInteractable _button, _dial;
        private Transform _carriage, _frame, _plane;
        private TextMeshPro _label;
        private Finger _finger;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _manager = _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.targetCarriageSpeed = 20f;
            _rules.targetTurnSeconds = 0.1f;
#if UNITY_EDITOR
            _mech = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Art/TargetMechanism.prefab"));
#endif
            _carriage = _mech.transform.Find("Carriage");
            _frame = _carriage.Find("TargetFrame");
            _plane = _frame.Find("TargetPlane");
            _target = _mech.AddComponent<TargetCarriage>();
            _target.Configure(_runner, _rules, _carriage, _frame);

            _button = Interactable("Button", new Vector3(-0.55f, 1.06f, 3.88f));
            _dial = Interactable("Dial", new Vector3(-0.45f, 1.06f, 3.88f));
            _label = new GameObject("Label").AddComponent<TextMeshPro>();
            _label.transform.SetParent(_root.transform, false);
            _remote = _root.AddComponent<TargetRemote>();
            _remote.Configure(_target, _button, _dial, _dial.transform, _label);

            var f = new GameObject("Finger");
            f.transform.SetParent(_root.transform, false);
            f.SetActive(false);
            _finger = f.AddComponent<Finger>();
            f.SetActive(true);
            yield return null;
        }

        private XRSimpleInteractable Interactable(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.04f;
            return go.AddComponent<XRSimpleInteractable>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_mech);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        private IEnumerator Press(XRSimpleInteractable what)
        {
            _manager.SelectEnter((IXRSelectInteractor)_finger, what);
            yield return null;
            _manager.SelectExit((IXRSelectInteractor)_finger, what);
            yield return null;
        }

        [Test]
        public void Start_NearEdge_EdgeOn_NotSupplied()
        {
            Assert.AreEqual(4.8f, _carriage.position.z, 0.001f);
            Assert.AreEqual(90f, _frame.localEulerAngles.y, 0.5f, "рамка ребром");
            Assert.IsFalse(_target.Supplied);
            Assert.AreEqual(7f, _target.Distance, "по умолчанию 7 м");
            Assert.AreEqual("7 м", _label.text);
        }

        [UnityTest]
        public IEnumerator Dial_CyclesDistances()
        {
            yield return Press(_dial);
            Assert.AreEqual(9f, _target.Distance);
            Assert.AreEqual("9 м", _label.text);
            yield return Press(_dial);
            Assert.AreEqual(3f, _target.Distance, "после 9 — снова 3");
            yield return Press(_dial);
            Assert.AreEqual(5f, _target.Distance);
        }

        [UnityTest]
        public IEnumerator Button_SuppliesTarget_ToSelectedDistance_FacingShooter()
        {
            yield return Press(_dial);                    // 9 м
            yield return Press(_button);
            Assert.AreEqual(TargetCarriage.State.Moving, _target.Current);
            float t = 0;
            while (!_target.Supplied && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(_target.Supplied, "мишень подана");
            Assert.AreEqual(13.2f, _plane.position.z, 0.01f, "плоскость мишени на 9 м от линии огня (Z 13,2)");
            Assert.AreEqual(1.5f, _plane.position.y, 0.01f);
            Assert.Greater(_plane.forward.z, 0.99f, "лицевая сторона листа — его локальная −Z — смотрит на стрелка (к −Z мира)");
        }

        [UnityTest]
        public IEnumerator Supply_ReportsButtonToDirector_AndRestartResets()
        {
            yield return Press(_button);
            float t = 0;
            while (!_target.Supplied && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(11.2f, _plane.position.z, 0.01f, "7 м по умолчанию");
            _runner.Restart();
            yield return null;
            Assert.AreEqual(4.8f, _carriage.position.z, 0.001f);
            Assert.IsFalse(_target.Supplied);
            Assert.AreEqual(90f, _frame.localEulerAngles.y, 0.5f);
        }

        [UnityTest]
        public IEnumerator Facts_TargetSupplied_ComesFromCarriage()
        {
            ITargetSupply supply = _target;
            Assert.IsFalse(supply.Supplied);
            yield return Press(_button);
            float t = 0;
            while (!supply.Supplied && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(supply.Supplied);
        }
    }
}
