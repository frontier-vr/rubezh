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
    // Выноска у контроллера (NG-32) в сборе: фраза подсказки VP-12 через паузу бездействия и снятие
    // действием, замечание о мелком нарушении, онбординг при старте у A1, панель сбоку от руки.
    public class ControllerCalloutsTests
    {
        private GameObject _root, _go;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private HintDirector _hints;
        private ControllerCallouts _callouts;
        private Transform _left, _right, _head, _rig;
        private AnchorLocator _anchors;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Callouts.ForgetSession();
            _root = new GameObject("Root");
            _root.AddComponent<AudioListener>();
            var manager = _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.hintIdleSeconds = 0.3f;
            _rules.calloutRemarkSeconds = 0.3f;
            _head = Point("Head", new Vector3(-2.4f, 1.6f, 1.7f));
            _left = Point("Left", new Vector3(-2.6f, 1.2f, 1.6f));
            _right = Point("Right", new Vector3(-2.6f, 1.2f, 1.8f));
            _head.LookAt(new Vector3(-3.4f, 1.5f, 1.7f));
            _rig = Point("Rig", new Vector3(-2.4f, 0f, 1.7f));
            var a1 = Point("A1", new Vector3(-2.4f, 0f, 1.7f));
            var a3 = Point("A3", new Vector3(0f, 0f, 3.25f));
            _anchors = _root.AddComponent<AnchorLocator>();
            _anchors.Configure(_rig, a1, null, a3);
            _hints = _root.AddComponent<HintDirector>();
            TextAsset hintRules = null, script = null;
#if UNITY_EDITOR
            hintRules = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Rubezh/Data/Hints/hint_rules.ru.json");
            script = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Rubezh/Data/Hints/script.ru.json");
            _go = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/ControllerCallouts.prefab"));
#endif
            _hints.Configure(_runner, _rules, hintRules, script, null, _anchors, null);
            _callouts = _go.GetComponent<ControllerCallouts>();
            _callouts.Configure(_runner, _hints, null, _anchors, _rules, hintRules, manager, _left, _right, _head);
            yield return null;
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
            Object.Destroy(_go);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            Callouts.ForgetSession();
            yield return null;
        }

        [Test]
        public void Start_AtA1_ShowsTeleportOnboarding_NextToRightHand()
        {
            Assert.IsTrue(_callouts.Visible);
            Assert.AreEqual(Callouts.Kind.Onboarding, _callouts.Model.Current);
            Assert.AreEqual("Стик вперёд: телепорт на жёлтый круг", _callouts.Shown);
        }

        [UnityTest]
        public IEnumerator Panel_IsBesideHand_FacingEyes()
        {
            yield return null;
            var view = _go.transform.Find("View");
            float d = Vector3.Distance(view.position, _right.position);
            Assert.That(d, Is.InRange(0.08f, 0.16f), "сбоку от кисти, не на ней");
            Assert.Less(view.position.y, _right.position.y, "чуть ниже руки — не на линии прицеливания");
            Assert.Greater(Vector3.Dot(view.forward, (view.position - _head.position).normalized), 0.99f, "лицом к глазам");
        }

        [UnityTest]
        public IEnumerator Hint_AfterIdle_ThenClearedByAction()
        {
            _rig.position = new Vector3(0, 0, 3.25f);              // ушёл к A3: телепорт снимает онбординг
            yield return null;
            Assert.AreNotEqual(Callouts.Kind.Onboarding, _callouts.Model.Current);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(Callouts.Kind.Hint, _callouts.Model.Current);
            Assert.AreEqual("Стик вперёд: к шкафу", _callouts.Shown);
            _hints.Activity();
            yield return null;
            Assert.IsFalse(_callouts.Visible, "действие снимает подсказку");
        }

        [UnityTest]
        public IEnumerator MinorViolation_ShowsShortName_Briefly()
        {
            _runner.Raise(Violation.FingerOnTrigger, Severity.Minor);
            yield return null;
            Assert.AreEqual(Callouts.Kind.Remark, _callouts.Model.Current);
            Assert.AreEqual("Палец на спуске", _callouts.Shown);
            yield return new WaitForSeconds(0.45f);
            Assert.AreNotEqual(Callouts.Kind.Remark, _callouts.Model.Current);
        }

        [UnityTest]
        public IEnumerator GrossViolation_NoCallout()
        {
            _rig.position = new Vector3(0, 0, 3.25f);
            yield return null;
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.AreNotEqual(Callouts.Kind.Remark, _callouts.Model.Current, "грубое — на стоп-экране, не у руки");
        }
    }
}
