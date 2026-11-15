using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Дымовой тест главной сцены (NG-18): Main.unity грузится без ошибок, все 9 объектов упражнения
    // на месте, упражнение стартует — фаза «Получение», команда на табло и голосом.
    public class MainSceneSmokeTests
    {
        private Scene _scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
            while (!load.isDone) yield return null;
            _scene = SceneManager.GetSceneByName("Main");
            SceneManager.SetActiveScene(_scene);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var unload = SceneManager.UnloadSceneAsync(_scene);
            while (unload != null && !unload.isDone) yield return null;
            yield return null;
        }

        private static T One<T>(string what) where T : Object
        {
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, all.Length, what + ": ровно один в сцене");
            return all[0];
        }

        private static GameObject Find(string path, string what)
        {
            var go = GameObject.Find(path);
            Assert.IsNotNull(go, what + " (" + path + ")");
            return go;
        }

        // Предмет можно взять: в игре он не обязан оставаться на месте в иерархии (сокеты переносят).
        private static void Grabbable(string name, string what)
        {
            foreach (var g in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(FindObjectsSortMode.None))
                if (g.name == name) return;
            Assert.Fail(what + ": нет предмета " + name);
        }

        [Test]
        public void SceneLoads()
        {
            Assert.IsTrue(_scene.isLoaded);
            Assert.AreEqual("Main", SceneManager.GetActiveScene().name);
        }

        [Test]
        public void AllNineExerciseObjects_ArePresent()
        {
            One<WeaponController>("пистолет");
            One<MagazineItem>("магазин");
            var tray = One<AmmoTray>("лоток с патронами");
            Assert.IsTrue(tray.isActiveAndEnabled);
            Grabbable("Earmuffs", "наушники");
            Grabbable("Glasses", "очки");
            One<TargetRemote>("пульт");
            One<TargetCarriage>("мишенный механизм");
            Find("Environment/Env_LineStand/CasingCatcher", "гильзоуловитель");
            One<CabinetSocket>("шкаф");
            Assert.IsNotNull(Find("Environment/Env_Cabinet", "шкаф (модель)").GetComponentInChildren<Renderer>());
        }

        [Test]
        public void RoundSource_IsThere()
        {
            // Патрон берётся из лотка: раздатчик и префаб патрона подключены.
            var tray = One<AmmoTray>("лоток");
            Assert.IsNotNull(Find("Environment/Env_AmmoTray/Spawn", "место появления патрона"));
            Assert.IsTrue(tray.enabled);
        }

        [Test]
        public void StationBoards_HangOverEveryStation_AboveEyeLevel()
        {
            var boards = Object.FindObjectsByType<StationBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(4, boards.Length, "табло над шкафом, полкой СИЗ, столом и рубежом");
            foreach (var station in new[] { Station.Cabinet, Station.PpeShelf, Station.PrepTable, Station.LineStand })
            {
                int count = 0;
                foreach (var b in boards) if (b.Station == station) count++;
                Assert.AreEqual(1, count, station + ": одно табло");
            }
            foreach (var b in boards)
            {
                Assert.IsTrue(b.isActiveAndEnabled, b.name);
                Assert.Greater(b.transform.position.y, 1.8f, b.name + ": выше уровня глаз — читается, подняв голову");
                Assert.Less(b.transform.position.y, 2.6f, b.name + ": под потолком 3 м с запасом");
                Assert.IsNotEmpty(b.Title, b.name + ": текст шага показан со старта");
            }
        }

        [Test]
        public void StationBoards_AtStart_OnlyCabinetIsHere()
        {
            foreach (var b in Object.FindObjectsByType<StationBoard>(FindObjectsSortMode.None))
                Assert.AreEqual(b.Station == Station.Cabinet, b.IsHere, b.name);
        }

        [Test]
        public void Walking_IsByLeftStick_TeleportStaysOnRightStick()
        {
            // Р-10: плавный ход левым стиком без якорей; правый стик — телепорт и поворот щелчками.
            var move = Find("XR Origin (XR Rig)/Locomotion/Move", "провайдер плавного хода");
            Assert.IsTrue(move.activeInHierarchy, "ход включён");
            var provider = move.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
            Assert.IsNotNull(provider);
            Assert.IsTrue(provider.isActiveAndEnabled);
            Assert.Greater(provider.moveSpeed, 0.5f);
            Assert.IsNotNull(provider.leftHandMoveInput.inputActionReference, "ход привязан к левому стику");
            Assert.IsNotNull(Find("XR Origin (XR Rig)", "риг").GetComponent<CharacterController>(), "стены и мебель не проходятся насквозь");

            var left = Find("XR Origin (XR Rig)/Camera Offset/Left Controller", "левый контроллер")
                .GetComponent<UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager>();
            var right = Find("XR Origin (XR Rig)/Camera Offset/Right Controller", "правый контроллер")
                .GetComponent<UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ControllerInputActionManager>();
            Assert.IsTrue(left.smoothMotionEnabled, "левый стик — ход, а не телепорт");
            Assert.IsFalse(right.smoothMotionEnabled, "правый стик — телепорт на якоря и поворот");
            Assert.IsTrue(Find("XR Origin (XR Rig)/Locomotion/Teleportation", "телепорт").activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator AmmoTray_HoldsWholeSeriesAtStart()
        {
            var tray = One<AmmoTray>("лоток");
            for (int i = 0; i < 5 && tray.WaitingCount < 5; i++) yield return null;
            Assert.AreEqual(5, tray.WaitingCount, "все патроны серии лежат в лотке сразу");
            var bounds = Find("Environment/Env_AmmoTray/Tray", "лоток (модель)").GetComponent<Renderer>().bounds;
            bounds.Expand(new Vector3(0f, 0.1f, 0f));
            foreach (var slot in tray.Slots)
                Assert.IsTrue(bounds.Contains(slot.transform.position), slot.name + ": гнездо внутри лотка");
        }

        [UnityTest]
        public IEnumerator Exercise_Starts()
        {
            yield return null;
            var runner = One<ExerciseRunner>("директор упражнения");
            Assert.IsNotNull(runner.Director, "директор создан");
            Assert.AreEqual(Phase.Receive, runner.Phase, "упражнение стартует с «Получения»");
            var board = One<CommandBoard>("табло команд");
            Assert.AreEqual("Получить оружие в шкафу", board.Current);
            var voice = One<InstructorVoice>("голос");
            yield return new WaitForSeconds(1.3f);
            Assert.AreEqual("cmd_receive", voice.LastSaid, "первая команда голосом");
            Assert.IsFalse(One<ResultScreen>("экран результата").Visible);
            Assert.IsFalse(One<StopScreen>("стоп-экран").Visible);
        }
    }
}
