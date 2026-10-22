using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Повтор эпизода нарушения (VP-17): запись идёт, пока идёт упражнение, призраки проигрывают её в
    // окне вокруг нарушения, экран результата показывает повтор грубого нарушения первым и листает.
    public class EpisodeReplayTests
    {
        private const string ReplayPrefab = "Assets/_Rubezh/Prefabs/Interactive/EpisodeReplay.prefab";
        private const string ScreenPrefab = "Assets/_Rubezh/Prefabs/Interactive/ResultScreen.prefab";

        private GameObject _root, _replayGo, _screenGo;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private EpisodeRecorder _recorder;
        private EpisodeReplay _replay;
        private Transform _head, _left, _right, _pistol, _magazine;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _head = Point("Head"); _left = Point("Left"); _right = Point("Right");
            _pistol = Point("Pistol"); _magazine = Point("Magazine");
            _recorder = _root.AddComponent<EpisodeRecorder>();
            _recorder.Configure(_runner, _rules, _head, _left, _right, _pistol, _magazine);
#if UNITY_EDITOR
            _replayGo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ReplayPrefab));
            _screenGo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefab));
#endif
            _replay = _replayGo.GetComponent<EpisodeReplay>();
            var ghosts = new Transform[5];
            string[] names = { "Ghost_Head", "Ghost_Hand_L", "Ghost_Hand_R", "Ghost_Pistol", "Ghost_Magazine" };
            for (int i = 0; i < names.Length; i++) ghosts[i] = _replayGo.transform.Find(names[i]);
            _replay.Configure(_recorder, _rules, _replayGo.GetComponentInChildren<Camera>(true),
                              _replayGo.transform.Find("Ghost_Pistol/Muzzle"), _replayGo.GetComponentInChildren<LineRenderer>(true), ghosts);
            yield return null;
        }

        private Transform Point(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root.transform, false);
            return t;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_screenGo);
            Object.Destroy(_replayGo);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        private Transform Ghost(string name) => _replayGo.transform.Find(name);

        // Пистолет едет вдоль X: в момент t он в (t, 1, 3), голова над ним.
        private void Record(double from, double to, double step = 0.1)
        {
            for (double t = from; t <= to + 1e-9; t += step)
            {
                _pistol.SetPositionAndRotation(new Vector3((float)t, 1f, 3f), Quaternion.Euler(0, 90, 0));
                _head.SetPositionAndRotation(new Vector3((float)t, 1.6f, 2.8f), Quaternion.identity);
                _recorder.Sample(t);
            }
        }

        [Test]
        public void Prefab_GhostsOnReplayLayer_CameraSeesOnlyThem()
        {
            int layer = LayerMask.NameToLayer("Replay");
            Assert.GreaterOrEqual(layer, 0, "слой Replay в TagManager");
            foreach (var t in _replayGo.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(layer, t.gameObject.layer, t.name + ": призрак не должен попадать в основную камеру");
            var cam = _replayGo.GetComponentInChildren<Camera>(true);
            Assert.AreEqual(1 << layer, cam.cullingMask);
            Assert.IsFalse(cam.enabled, "камера повтора снимает вручную, в шлем не рисует");
        }

        [Test]
        public void NoRecording_NothingToPlay()
        {
            var v = new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 5, 0.5);
            Assert.IsFalse(_replay.Play(v));
            Assert.IsFalse(_replay.Playing);
            Assert.IsFalse(Ghost("Ghost_Pistol").gameObject.activeSelf);
        }

        [Test]
        public void Play_GhostsFollowRecording_InsideWindow()
        {
            Record(0, 20);
            var v = new ViolationRecord(Violation.MuzzleOutOfSector, Severity.Critical, 12, 1.5);   // эпизод 10,5–12
            Assert.IsTrue(_replay.Play(v));
            Assert.AreEqual(8.5, _replay.Window.From, 1e-6, "за 2 с до начала эпизода");
            Assert.AreEqual(13.5, _replay.Window.To, 1e-6);
            Assert.IsTrue(Ghost("Ghost_Pistol").gameObject.activeSelf);

            _replay.Apply(11.25);
            Assert.AreEqual(11.25f, Ghost("Ghost_Pistol").position.x, 1e-3f);
            Assert.AreEqual(90f, Ghost("Ghost_Pistol").eulerAngles.y, 0.1f);
            Assert.AreEqual(1.6f, Ghost("Ghost_Head").position.y, 1e-3f);

            var ray = _replayGo.GetComponentInChildren<LineRenderer>(true);
            Assert.IsTrue(ray.enabled, "луч ствола виден");
            var muzzle = Ghost("Ghost_Pistol/Muzzle");
            Assert.Less(Vector3.Distance(muzzle.position, ray.GetPosition(0)), 1e-3f);
            Assert.Greater(Vector3.Dot((ray.GetPosition(1) - ray.GetPosition(0)).normalized, muzzle.forward), 0.99f);
            Assert.IsNotNull(_replay.Texture, "кадр для экрана результата");
        }

        [UnityTest]
        public IEnumerator Play_LoopsOverTime_AndStopHides()
        {
            Record(0, 6);
            _replay.Play(new ViolationRecord(Violation.FingerOnTrigger, Severity.Minor, 3, 0.4));
            double first = _replay.Playhead;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(_replay.Playhead, first, "повтор идёт сам");
            Assert.LessOrEqual(_replay.Playhead, _replay.Window.To + 1e-6);
            _replay.Stop();
            Assert.IsFalse(_replay.Playing);
            Assert.IsFalse(Ghost("Ghost_Pistol").gameObject.activeSelf);
            Assert.IsFalse(Ghost("Ghost_Head").gameObject.activeSelf);
        }

        [Test]
        public void HiddenSource_HidesItsGhost()
        {
            _left.gameObject.SetActive(false);         // контроллер не отслеживается
            Record(0, 4);
            _replay.Play(new ViolationRecord(Violation.StepOrder, Severity.Minor, 2, 0));
            Assert.IsFalse(Ghost("Ghost_Hand_L").gameObject.activeSelf);
            Assert.IsTrue(Ghost("Ghost_Hand_R").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Recorder_WritesWhileRunning_ClearsOnRestart()
        {
            Assert.AreEqual(0, _recorder.RecordedUntil, "до взятия пистолета время упражнения не идёт");
            _runner.UpdateFacts(new ExerciseFacts(true, new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None), 0,
                                                  Place.Hand, Place.InWeapon, false, false, false));
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(_recorder.RecordedUntil, 0.1, "упражнение идёт — запись идёт");
            _runner.Restart();
            Assert.AreEqual(0, _recorder.RecordedUntil, "«Повторить» — запись с нуля");
        }

        [UnityTest]
        public IEnumerator Recorder_KeepsLastFrame_WhenExerciseStops()
        {
            _runner.UpdateFacts(new ExerciseFacts(true, new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None), 0,
                                                  Place.Hand, Place.InWeapon, false, false, false));
            yield return new WaitForSeconds(0.2f);
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical, 0.2);
            var v = _runner.Director.Violations[0];
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(_recorder.RecordedUntil + 1e-6, v.Time, "момент грубого нарушения записан");
            Assert.IsTrue(_replay.Play(v));
        }

        [UnityTest]
        public IEnumerator ResultScreen_ShowsCriticalFirst_NextCycles_HideStops()
        {
            var screen = _screenGo.GetComponent<ResultScreen>();
            screen.ConfigureReplay(_replay);
            Record(0, 30);
            var result = new ExerciseResult
            {
                TotalTime = 30, Verdict = Verdict.NotPassed, FailedCritical = true, FailReason = Violation.MuzzleOutOfSector,
                Violations = new[]
                {
                    new ViolationRecord(Violation.NoGlasses, Severity.Minor, 8, 0),
                    new ViolationRecord(Violation.MuzzleOutOfSector, Severity.Critical, 25, 1.5)
                }
            };
            screen.Show(result, Anchor.A1);
            yield return null;
            var block = _screenGo.transform.Find("Panel/ReplayBlock");
            Assert.IsTrue(block.gameObject.activeSelf);
            Assert.AreEqual(1, screen.ReplayIndex, "первым — грубое");
            StringAssert.Contains("2 из 2", block.Find("ReplayCaption").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains("00:23", block.Find("ReplayCaption").GetComponent<TMPro.TMP_Text>().text, "начало эпизода");
            Assert.AreSame(_replay.Texture, block.Find("ReplayView").GetComponent<UnityEngine.UI.RawImage>().texture);
            Assert.IsTrue(block.Find("ReplayNext").gameObject.activeSelf);

            screen.NextReplay();
            Assert.AreEqual(0, screen.ReplayIndex, "по кругу");
            Assert.AreEqual(8, _replay.Current.Time, 1e-9);

            screen.Hide();
            Assert.IsFalse(_replay.Playing);
            Assert.IsFalse(block.gameObject.activeSelf);
        }

        [Test]
        public void ResultScreen_WithReplay_TableShorter_RestCounted()
        {
            var screen = _screenGo.GetComponent<ResultScreen>();
            screen.ConfigureReplay(_replay);
            Record(0, 60);
            var list = new ViolationRecord[6];
            for (int i = 0; i < list.Length; i++) list[i] = new ViolationRecord(Violation.StepOrder, Severity.Minor, 5 + i * 5, 0);
            screen.Show(new ExerciseResult { TotalTime = 60, Verdict = Verdict.NotPassed, Violations = list }, Anchor.A1);
            var table = _screenGo.transform.Find("Panel/Table").GetComponent<TMPro.TMP_Text>();
            var lines = table.text.Split('\n');
            Assert.AreEqual(5, lines.Length, "4 строки и «ещё»: под таблицей окно повтора");
            Assert.AreEqual("ещё 2", lines[4]);
            var tableRect = table.rectTransform;
            var block = (RectTransform)_screenGo.transform.Find("Panel/ReplayBlock");
            Assert.LessOrEqual(-tableRect.anchoredPosition.y + tableRect.sizeDelta.y, -block.anchoredPosition.y, "таблица не залезает на повтор");

            screen.Hide();
            screen.ConfigureReplay(null);
            screen.Show(new ExerciseResult { TotalTime = 60, Verdict = Verdict.NotPassed, Violations = list }, Anchor.A1);
            Assert.AreEqual(6, table.text.Split('\n').Length, "без повтора — таблица во всю высоту");
        }

        [Test]
        public void ResultScreen_NoViolations_NoReplayBlock()
        {
            var screen = _screenGo.GetComponent<ResultScreen>();
            screen.ConfigureReplay(_replay);
            Record(0, 5);
            screen.Show(new ExerciseResult { TotalTime = 5, Verdict = Verdict.Passed, Violations = new ViolationRecord[0] }, Anchor.A1);
            Assert.IsFalse(_screenGo.transform.Find("Panel/ReplayBlock").gameObject.activeSelf);
            Assert.AreEqual(-1, screen.ReplayIndex);
        }

        [Test]
        public void ResultScreen_SingleViolation_NoNextButton()
        {
            var screen = _screenGo.GetComponent<ResultScreen>();
            screen.ConfigureReplay(_replay);
            Record(0, 5);
            screen.Show(new ExerciseResult
            {
                TotalTime = 5, Verdict = Verdict.PassedWithRemarks,
                Violations = new[] { new ViolationRecord(Violation.CasingLeft, Severity.Minor, 4, 0) }
            }, Anchor.A1);
            var block = _screenGo.transform.Find("Panel/ReplayBlock");
            Assert.IsTrue(block.gameObject.activeSelf);
            Assert.IsFalse(block.Find("ReplayNext").gameObject.activeSelf);
            StringAssert.DoesNotContain("из", block.Find("ReplayCaption").GetComponent<TMPro.TMP_Text>().text.Split('\n')[0]);
        }
    }
}
