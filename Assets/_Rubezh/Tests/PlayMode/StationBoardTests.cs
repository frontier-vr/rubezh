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
    // Табло над столами и стойками: табло своего места показывает шаг по пунктам и горит меткой,
    // остальные — какой шаг идёт и куда идти; тексты из сценария, кнопки — по руке.
    public class StationBoardTests
    {
        private GameObject _root, _cabinetGo, _tableGo;
        private ExerciseRunner _runner;
        private StationBoard _cabinet, _table;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
            var asset = new TextAsset(File.ReadAllText("Assets/_Rubezh/Data/Hints/script.ru.json"));
            _cabinetGo = Board(asset, Station.Cabinet, out _cabinet);
            _tableGo = Board(asset, Station.PrepTable, out _table);
            yield return null;
        }

        private GameObject Board(TextAsset asset, Station station, out StationBoard board)
        {
            GameObject go = null;
#if UNITY_EDITOR
            go = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/StationBoard.prefab"));
#endif
            board = go.GetComponent<StationBoard>();
            board.Configure(_runner, asset, null, station);
            return go;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_cabinetGo);
            Object.Destroy(_tableGo);
            Object.Destroy(_root);
            yield return null;
        }

        private static ExerciseFacts InspectedWithPpe()
        {
            var empty = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);
            return new ExerciseFacts(true, empty, 0, Place.Hand, Place.InspectShelf, true, true, false);
        }

        [Test]
        public void Start_CabinetBoardShowsFirstStepInPoints_OtherBoardSendsToCabinet()
        {
            Assert.IsTrue(_cabinet.IsHere);
            Assert.AreEqual("1. Получение оружия", _cabinet.Title);
            StringAssert.StartsWith("1. ", _cabinet.Body);
            StringAssert.Contains("\n2. ", _cabinet.Body, "порядок действий — по пунктам");
            StringAssert.Contains("Grip", _cabinet.Body, "как сделать — с кнопкой контроллера");
            Assert.IsTrue(_cabinetGo.transform.Find("HereMark").gameObject.activeSelf, "метка «шаг здесь»");

            Assert.IsFalse(_table.IsHere);
            StringAssert.Contains("Сейчас: 1. Получение оружия", _table.Title);
            StringAssert.Contains("у оружейного шкафа", _table.Body, "куда идти за текущим шагом");
            Assert.IsFalse(_tableGo.transform.Find("HereMark").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator PhaseChange_MovesStepToItsBoard()
        {
            _runner.Director.UpdateFacts(InspectedWithPpe());
            yield return null;
            Assert.AreEqual(Phase.LoadMagazine, _runner.Phase);
            Assert.IsTrue(_table.IsHere);
            Assert.AreEqual("4. Снаряжение магазина", _table.Title);
            Assert.IsFalse(_cabinet.IsHere);
            StringAssert.Contains("у стола снаряжения", _cabinet.Body);
        }

        [UnityTest]
        public IEnumerator Gross_DoesNotChangeBoards_RestartReturnsFirstStep()
        {
            _runner.Director.UpdateFacts(InspectedWithPpe());
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.AreEqual("4. Снаряжение магазина", _table.Title, "грубое не останавливает упражнение (Р-09)");
            _runner.Restart();
            yield return null;
            Assert.IsTrue(_cabinet.IsHere);
            Assert.AreEqual("1. Получение оружия", _cabinet.Title);
        }

        [Test]
        public void Prefab_TextsAreReadable_NoPlaceholdersLeft()
        {
            foreach (var text in _cabinetGo.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                Assert.IsTrue(text.textWrappingMode != TMPro.TextWrappingModes.NoWrap, text.name + ": длинный пункт переносится");
                StringAssert.DoesNotContain("{", text.text, "кнопки подставлены");
            }
        }
    }
}
