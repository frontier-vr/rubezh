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
