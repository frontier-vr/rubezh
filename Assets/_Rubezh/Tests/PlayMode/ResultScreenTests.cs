using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Экран результата (VP-20) по макету YG-09: вердикт словом и значком, балл, время, точность,
    // таблица нарушений; грубое — через N с у текущего якоря; «Повторить» начинает заново.
    public class ResultScreenTests
    {
        private GameObject _root, _screenGo;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private ResultScreen _screen;
        private Transform _r1, _r3, _a1, _a3, _rig;
        private AnchorLocator _anchors;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.failResultDelaySeconds = 0.2f;
            _r1 = Point("R1", new Vector3(-3.5f, 1.55f, 0.35f));
            _r3 = Point("R3", new Vector3(0f, 1.55f, 5.0f));
            _a1 = Point("A1", new Vector3(-2.4f, 0f, 1.7f));
            _a3 = Point("A3", new Vector3(0f, 0f, 3.25f));
            _rig = Point("Rig", _a3.position);
            _anchors = _root.AddComponent<AnchorLocator>();
            _anchors.Configure(_rig, _a1, null, _a3);
#if UNITY_EDITOR
            _screenGo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/ResultScreen.prefab"));
#endif
            _screen = _screenGo.GetComponent<ResultScreen>();
            _screen.Configure(_runner, _rules, _anchors, _r1, null, _r3, _a1, null, _a3, null);
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
            Object.Destroy(_screenGo);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        private T Find<T>(string path) where T : Component => _screenGo.transform.Find("Panel/" + path).GetComponent<T>();
        private string Text(string path) => Find<TMPro.TMP_Text>(path).text;

        [Test]
        public void HiddenDuringExercise()
        {
            Assert.IsFalse(_screen.Visible);
        }

        [Test]
        public void Passed_ShowsWordIconScoreTimeAccuracy_AtR1FacingA1()
        {
            var result = new ExerciseResult
            {
                TotalTime = 125.4, Shots = 5, Hits = 4, Score = 87, Verdict = Verdict.Passed,
                Violations = new[] { new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 61.4, 0.4) }
            };
            _screen.Show(result, Anchor.A1);
            Assert.IsTrue(_screen.Visible);
            Assert.AreEqual("ЗАЧЁТ", Text("Verdict"));
            Assert.AreEqual("87", Text("Score"));
            Assert.AreEqual("02:05", Text("Time"));
            Assert.AreEqual("4 из 5 · 80%", Text("Accuracy"));
            Assert.AreEqual("1", Text("ViolationsValue"));
            StringAssert.Contains("01:01 · Палец на спуске (0.4 с) · −10".Replace("0.4", 0.4.ToString("0.0")), Text("Table"));
            var icon = Find<UnityEngine.UI.Image>("VerdictIcon");
            Assert.IsNotNull(icon.sprite, "вердикт — значком, не только цветом");
            Assert.AreEqual(_r1.position, _screenGo.transform.position);
            var toScreen = _r1.position - _a1.position; toScreen.y = 0;
            Assert.Greater(Vector3.Dot(_screenGo.transform.forward, toScreen.normalized), 0.99f, "панель смотрит на A1");
        }

        [Test]
        public void Remarks_And_ManyViolations_ShowMoreLine()
        {
            var list = new ViolationRecord[11];
            for (int i = 0; i < list.Length; i++) list[i] = new ViolationRecord(Violation.StepOrder, Severity.Minor, i * 10, 0);
            _screen.Show(new ExerciseResult { Score = 65, Verdict = Verdict.PassedWithRemarks, Violations = list }, Anchor.A1);
            Assert.AreEqual("ЗАЧЁТ С ЗАМЕЧАНИЯМИ", Text("Verdict"));
            StringAssert.EndsWith("ещё 3", Text("Table"), "до 8 строк, дальше «ещё N»");
        }

        [UnityTest]
        public IEnumerator Gross_ShowsAfterDelay_AtCurrentAnchor_WithReason()
        {
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.IsFalse(_screen.Visible, "сначала стоп-экран VP-22");
            yield return new WaitForSeconds(0.35f);
            Assert.IsTrue(_screen.Visible);
            Assert.AreEqual("НЕ ЗАЧЁТ", Text("Verdict"));
            Assert.AreEqual("0", Text("Score"));
            Assert.AreEqual("причина", Text("ViolationsCaption"));
            Assert.AreEqual("Взгляд в ствол", Text("ViolationsValue"));
            StringAssert.Contains("× грубое", Text("Table"));
            StringAssert.Contains("стоп", Text("Table"));
            Assert.AreEqual(_r3.position, _screenGo.transform.position, "у текущего якоря A3 — экран R3");
        }

        [UnityTest]
        public IEnumerator Repeat_RestartsExercise_AndHides()
        {
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(Phase.Failed, _runner.Phase);
            Find<UnityEngine.UI.Button>("Repeat").onClick.Invoke();
            yield return null;
            Assert.AreEqual(Phase.Receive, _runner.Phase, "упражнение с начала без перезапуска");
            Assert.IsFalse(_screen.Visible);
        }
    }
}
