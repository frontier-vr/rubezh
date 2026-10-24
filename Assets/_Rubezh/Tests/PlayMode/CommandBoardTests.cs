using System.Collections;
using System.IO;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Табло команд: команда своей фазы видна всю фазу, смена — с сигналом.
    public class CommandBoardTests
    {
        private GameObject _root;
        private ExerciseRunner _runner;
        private CommandBoard _board;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            var text = new GameObject("Text").AddComponent<TextMeshPro>();
            text.transform.SetParent(_root.transform, false);
            var source = _root.AddComponent<AudioSource>();
            var asset = new TextAsset(File.ReadAllText("Assets/_Rubezh/Data/Hints/script.ru.json"));
            _board = _root.AddComponent<CommandBoard>();
            _board.Configure(_runner, asset, text, source);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(_root); yield return null; }

        [UnityTest]
        public IEnumerator ShowsReceiveCommandAtStart()
        {
            yield return null;
            Assert.AreEqual("Получить оружие в шкафу", _board.Current);
            Assert.AreEqual(0, _board.Beeps);
        }

        [UnityTest]
        public IEnumerator PhaseChange_UpdatesTextWithBeep()
        {
            var empty = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
            _runner.Director.UpdateFacts(new ExerciseFacts(true, empty, 0, Place.Hand, Place.InspectShelf, true, true, false));
            yield return null;
            // Receive → Inspect → Ppe → LoadMagazine за одно обновление: на табло последняя команда
            Assert.AreEqual(Phase.LoadMagazine, _runner.Phase);
            Assert.AreEqual("Снарядить магазин: пять патронов", _board.Current);
            Assert.AreEqual(3, _board.Beeps);
        }

        [UnityTest]
        public IEnumerator Failed_ShowsStopCommand()
        {
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.AreEqual("Стоп! Упражнение прекращено", _board.Current);
        }

        [UnityTest]
        public IEnumerator Restart_ReturnsToFirstCommand()
        {
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            _runner.Restart();
            yield return null;
            Assert.AreEqual("Получить оружие в шкафу", _board.Current);
        }
    }
}
