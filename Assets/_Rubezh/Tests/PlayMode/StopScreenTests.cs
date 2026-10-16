using System.Collections;
using System.IO;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Стоп-экран грубого нарушения (VP-22, FR-39): сразу у текущего якоря, своя причина из сценария
    // для каждого грубого нарушения, стоп-кадр; через N с — экран результата на том же месте.
    public class StopScreenTests
    {
        private GameObject _root, _stopGo, _resultGo;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private StopScreen _stop;
        private ResultScreen _result;
        private Transform _r1, _r2, _r3, _a1, _a2, _a3, _rig, _eyes;
        private AnchorLocator _anchors;

        private static readonly Violation[] Gross =
        {
            Violation.MuzzleOutOfSector, Violation.LookIntoMuzzle, Violation.ShotBeforeFire,
            Violation.ShotAfterCease, Violation.ShotOutOfSector
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.failResultDelaySeconds = 0.3f;
            _r1 = Point("R1", new Vector3(-3.35f, 1.55f, 3.4f));
            _r2 = Point("R2", new Vector3(3.6f, 1.55f, 0.5f));
            _r3 = Point("R3", new Vector3(0f, 1.55f, 5.0f));
            _a1 = Point("A1", new Vector3(-2.4f, 0f, 1.7f));
            _a2 = Point("A2", new Vector3(2.4f, 0f, 1.9f));
            _a3 = Point("A3", new Vector3(0f, 0f, 3.25f));
            _rig = Point("Rig", _a2.position);
            _eyes = Point("Eyes", _a2.position + Vector3.up * 1.6f);
            _anchors = _root.AddComponent<AnchorLocator>();
            _anchors.Configure(_rig, _a1, _a2, _a3);
#if UNITY_EDITOR
            _stopGo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/StopScreen.prefab"));
            _resultGo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/ResultScreen.prefab"));
#endif
            _stop = _stopGo.GetComponent<StopScreen>();
            _stop.Configure(_runner, _rules, _anchors, _r1, _r2, _r3, _a1, _a2, _a3, _eyes);
            _result = _resultGo.GetComponent<ResultScreen>();
            _result.Configure(_runner, _rules, _anchors, _r1, _r2, _r3, _a1, _a2, _a3, null);
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
            Object.Destroy(_stopGo);
            Object.Destroy(_resultGo);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        private string Text(string name) => _stopGo.transform.Find("Panel/" + name).GetComponent<TMPro.TMP_Text>().text;

        [Test]
        public void HiddenDuringExercise()
        {
            Assert.IsFalse(_stop.Visible);
        }

        [Test]
        public void EveryGrossViolation_HasItsOwnReason()
        {
            var script = new InstructorScript(File.ReadAllText("Assets/_Rubezh/Data/Hints/script.ru.json"));
            var titles = new System.Collections.Generic.HashSet<string>();
            foreach (var v in Gross)
            {
                _stop.Show(v, 12, Anchor.A2);
                Assert.IsTrue(_stop.Visible);
                Assert.AreEqual(script.FailTitle(v), Text("Title"), v.ToString());
                Assert.AreEqual(script.FailText(v), Text("Reason"), v.ToString());
                Assert.IsNotEmpty(Text("Reason"), v.ToString());
                Assert.IsTrue(titles.Add(Text("Title")), "у каждого нарушения своя причина: " + v);
                _stop.Hide();
            }
        }

        [UnityTest]
        public IEnumerator Gross_StopAtCurrentAnchor_ThenResultInSamePlace()
        {
            _runner.Raise(Violation.ShotBeforeFire, Severity.Critical);
            yield return null;
            Assert.IsTrue(_stop.Visible, "стоп-экран сразу");
            Assert.IsFalse(_result.Visible, "результат — после стоп-экрана");
            Assert.AreEqual("Выстрел до команды «Огонь»", Text("Title"));
            Assert.AreEqual("СТОП", Text("Stop"));
            StringAssert.StartsWith("стоп-кадр · ", Text("FrameCaption"));
            Assert.IsNotNull(_stopGo.transform.Find("Panel/StopFrame").GetComponent<UnityEngine.UI.RawImage>().texture, "стоп-кадр снят");
            Assert.AreEqual(_r2.position, _stopGo.transform.position, "у A2 — экран R2");
            var toScreen = _r2.position - _a2.position; toScreen.y = 0;
            Assert.Greater(Vector3.Dot(_stopGo.transform.forward, toScreen.normalized), 0.99f, "панель смотрит на A2");

            yield return new WaitForSeconds(0.45f);
            Assert.IsFalse(_stop.Visible, "стоп-экран уступил место результату");
            Assert.IsTrue(_result.Visible);
            Assert.AreEqual(_stopGo.transform.position, _resultGo.transform.position, "результат там же, где стоп-экран");
        }

        [UnityTest]
        public IEnumerator MinorFinish_NoStopScreen()
        {
            _runner.Raise(Violation.FingerOnTrigger, Severity.Minor);
            yield return null;
            Assert.IsFalse(_stop.Visible, "мелкое нарушение упражнение не останавливает");
        }

        [UnityTest]
        public IEnumerator Restart_HidesStopScreen()
        {
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.IsTrue(_stop.Visible);
            _runner.Restart();
            yield return null;
            Assert.IsFalse(_stop.Visible);
        }
    }
}
