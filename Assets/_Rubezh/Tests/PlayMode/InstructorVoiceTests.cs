using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Голос инструктора (VP-14): команда каждой фазы звучит своей озвучкой, ID — из сценария
    // Data/Hints, подсказка VP-12 — своей фразой, грубое — «Стоп» и причина.
    public class InstructorVoiceTests
    {
        private GameObject _root;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private InstructorVoice _voice;
        private InstructorScript _script;
        private AudioClip[] _clips;
        private TextAsset _scriptAsset, _hintAsset;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _root.AddComponent<AudioListener>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.hintIdleSeconds = 0.1f;
            _script = new InstructorScript(File.ReadAllText("Assets/_Rubezh/Data/Hints/script.ru.json"));
            var list = new List<AudioClip>();
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Rubezh/Audio/Voice" }))
                list.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));
            _scriptAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Rubezh/Data/Hints/script.ru.json");
            _hintAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Rubezh/Data/Hints/hint_rules.ru.json");
#endif
            _clips = list.ToArray();
            var go = new GameObject("Voice", typeof(AudioSource));
            go.transform.SetParent(_root.transform, false);
            _voice = go.AddComponent<InstructorVoice>();
            _voice.Configure(_runner, null, _scriptAsset, _clips);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        [Test]
        public void EveryCommand_HasItsVoice_FromScript()
        {
            foreach (Phase phase in System.Enum.GetValues(typeof(Phase)))
            {
                if (!_script.HasCommand(phase)) continue;
                var id = _voice.CommandId(phase);
                Assert.AreEqual(_script.VoiceId(phase), id, "ID — из сценария, не хардкодом");
                Assert.IsTrue(_voice.Has(id), phase + ": нет озвучки " + id);
                _voice.Command(phase);
                Assert.AreEqual(id, _voice.Speaking, phase.ToString());
            }
        }

        [UnityTest]
        public IEnumerator Start_SaysFirstCommand_AfterSceneLoads()
        {
            Assert.IsNull(_voice.LastSaid, "не в кадре загрузки");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(_script.VoiceId(Phase.Receive), _voice.LastSaid, "«Получить оружие в шкафу» со старта");
        }

        [UnityTest]
        public IEnumerator Gross_SaysStop_ThenReason()
        {
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.AreEqual(_script.VoiceId(Phase.Failed), _voice.Speaking, "сначала «Стоп! Упражнение прекращено»");
            Assert.AreEqual(1, _voice.Queued.Count);
            _voice.Source.Stop();                     // договорили
            yield return null;
            Assert.AreEqual("fail_LookIntoMuzzle", _voice.Speaking, "затем причина");
        }

        [UnityTest]
        public IEnumerator Hint_IsSpoken_AndWaitsForCommand()
        {
            var rig = new GameObject("Rig").transform; rig.SetParent(_root.transform, false); rig.position = new Vector3(0, 0, 3.25f);
            var a1 = new GameObject("A1").transform; a1.SetParent(_root.transform, false); a1.position = new Vector3(-2.4f, 0, 1.7f);
            var a3 = new GameObject("A3").transform; a3.SetParent(_root.transform, false); a3.position = new Vector3(0, 0, 3.25f);
            var anchors = _root.AddComponent<AnchorLocator>();
            anchors.Configure(rig, a1, null, a3);
            var hints = _root.AddComponent<HintDirector>();
            hints.Configure(_runner, _rules, _hintAsset, _scriptAsset, null, anchors, null);
            _voice.Configure(_runner, hints, _scriptAsset, _clips);
            _voice.Command(Phase.Receive);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual("hint_teleport_a1", hints.CurrentHintId);
            Assert.AreEqual(_script.VoiceId(Phase.Receive), _voice.Speaking, "подсказка не перебивает команду");
            CollectionAssert.Contains(new List<string>(Names(_voice.Queued)), "hint_teleport_a1", "подсказка ждёт своей очереди");
            _voice.Source.Stop();
            yield return null;
            Assert.AreEqual("hint_teleport_a1", _voice.Speaking);
        }

        private static IEnumerable<string> Names(IEnumerable<AudioClip> clips)
        {
            foreach (var c in clips) yield return c.name;
        }
    }
}
