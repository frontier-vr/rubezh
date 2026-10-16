using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.Tests.PlayMode
{
    // Магазин, патроны и места (NG-09) на настоящих префабах: шахта, сокеты патронов, лоток,
    // места у якорей, возврат упавшего, Restart, снимок мира для директора.
    public class MagazineSocketsTests
    {
        private sealed class TestHand : XRBaseInteractor { }

        private GameObject _root;
        private XRInteractionManager _manager;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private GameObject _pistol, _magazineGo;
        private WeaponController _weapon;
        private PistolInteraction _interaction;
        private MagazineItem _magazine;
        private XRGrabInteractable _magGrab, _pistolGrab;
        private MagWell _well;
        private PlaceSocket _wellPlace, _cabinet, _shelfMag, _tableMag, _standMag;
        private AmmoTray _tray;
        private AnchorLocator _anchors;
        private Transform _rig, _a1, _a2, _a3;
        private ExerciseFactsCollector _facts;
        private TestHand _hand;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _manager = _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.dropReturnSeconds = 0.2f;

            _rig = Child("Rig", new Vector3(-2.4f, 0f, 1.7f));
            _a1 = Child("A1", new Vector3(-2.4f, 0f, 1.7f));
            _a2 = Child("A2", new Vector3(2.3f, 0f, 2f));
            _a3 = Child("A3", new Vector3(0f, 0f, 3.25f));
            _anchors = _root.AddComponent<AnchorLocator>();
            _anchors.Configure(_rig, _a1, _a2, _a3);

            _cabinet = MakePlace("Cabinet", Rubezh.Core.Place.Cabinet, Anchor.A1, 1, new Vector3(-3.4f, 1.335f, 1.5f));
            _shelfMag = MakePlace("ShelfMag", Rubezh.Core.Place.InspectShelf, Anchor.A1, 2, new Vector3(-2.95f, 1.01f, 1.75f));
            _tableMag = MakePlace("TableMag", Rubezh.Core.Place.PrepTable, Anchor.A2, 2, new Vector3(3.3f, 0.91f, 2.15f));
            _standMag = MakePlace("StandMag", Rubezh.Core.Place.LineStand, Anchor.A3, 2, new Vector3(0.45f, 1.01f, 3.95f));

            _pistol = Object.Instantiate(Load("Pistol_Interactive"), new Vector3(-3.4f, 1.335f, 1.5f), Quaternion.identity);
            _weapon = _pistol.GetComponent<WeaponController>();
            _weapon.Configure(_runner, null);
            _interaction = _pistol.GetComponent<PistolInteraction>();
            _pistolGrab = _pistol.GetComponent<XRGrabInteractable>();
            _pistolGrab.attachEaseInTime = 0f;
            _well = _pistol.GetComponentInChildren<MagWell>();
            _well.Configure(_weapon, _runner, _rules);
            _wellPlace = _well.GetComponent<PlaceSocket>();
            _pistol.GetComponent<ItemReturn>().Configure(_runner, _rules, _anchors, _cabinet);

            _magazineGo = Object.Instantiate(Load("Magazine_Interactive"), _well.transform.position, _well.transform.rotation);
            _magazine = _magazineGo.GetComponent<MagazineItem>();
            _magGrab = _magazineGo.GetComponent<XRGrabInteractable>();
            _magGrab.attachEaseInTime = 0f;
            var magReturn = _magazineGo.GetComponent<ItemReturn>();
            magReturn.Configure(_runner, _rules, _anchors, _wellPlace);
            magReturn.SetHome(Anchor.A1, _shelfMag);
            magReturn.SetHome(Anchor.A2, _tableMag);
            magReturn.SetHome(Anchor.A3, _standMag);

            var spawn = Child("Spawn", new Vector3(3.35f, 0.93f, 1.55f));
            spawn.gameObject.SetActive(false);
            spawn.gameObject.AddComponent<SphereCollider>().isTrigger = true;
            var spawnSocket = spawn.gameObject.AddComponent<XRSocketInteractor>();
            spawnSocket.interactionLayers = Layer(3);
            spawn.gameObject.SetActive(true);
            _tray = _root.AddComponent<AmmoTray>();
            _tray.Configure(_runner, _rules, spawnSocket, Load("Round_Interactive"));
            _magazine.Configure(_runner, _tray);

            _facts = _root.AddComponent<ExerciseFactsCollector>();
            _facts.Configure(_runner, _weapon, _pistol.GetComponent<ItemReturn>(), _magazine, magReturn);

            _hand = Hand();
            yield return null;
            _pistol.GetComponent<ItemReturn>().ToStart();
            magReturn.ToStart();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var round in _tray.Active.ToArray()) Object.Destroy(round.gameObject);
            Object.Destroy(_magazineGo);
            Object.Destroy(_pistol);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
            foreach (var g in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(g.gameObject);
            yield return null;
        }

        private static GameObject Load(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Rubezh/Prefabs/Interactive/{name}.prefab");
#else
            return null;
#endif
        }

        private static InteractionLayerMask Layer(int bit) => new InteractionLayerMask { value = 1 << bit };

        private Transform Child(string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root.transform, false);
            t.position = position;
            return t;
        }

        private PlaceSocket MakePlace(string name, Rubezh.Core.Place place, Anchor anchor, int layer, Vector3 position)
        {
            var t = Child(name, position);
            t.gameObject.SetActive(false);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.2f, 0.1f, 0.3f);
            var socket = t.gameObject.AddComponent<XRSocketInteractor>();
            socket.interactionLayers = Layer(layer);
            var p = t.gameObject.AddComponent<PlaceSocket>();
            p.Configure(place, anchor);
            t.gameObject.SetActive(true);
            return p;
        }

        private TestHand Hand()
        {
            var go = new GameObject("Hand");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(-2.6f, 1.2f, 1.6f);
            go.SetActive(false);
            var hand = go.AddComponent<TestHand>();
            hand.handedness = InteractorHandedness.Right;
            hand.interactionLayers = new InteractionLayerMask { value = -1 };
            go.SetActive(true);
            return hand;
        }

        private IXRSelectInteractor HandI => _hand;

        private IEnumerator TakeMagazineOut()
        {
            _manager.SelectEnter(HandI, _pistolGrab);
            yield return null;
            _interaction.Eject();
            yield return null;
            _manager.SelectExit(HandI, _pistolGrab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_PistolInCabinet_MagazineInWell_NoViolations()
        {
            yield return null;
            var f = _facts.Collect();
            Assert.AreEqual(Rubezh.Core.Place.Cabinet, f.PistolPlace);
            Assert.AreEqual(Rubezh.Core.Place.InWeapon, f.MagazinePlace);
            Assert.IsFalse(f.PistolInHand, "сокет шкафа — не рука");
            Assert.IsTrue(_weapon.State.MagazineInserted);
            Assert.IsTrue(_magazine.InWell);
            Assert.AreEqual(Phase.Receive, _runner.Phase);
            Assert.IsEmpty(_runner.Director.Violations, "системная постановка в шахту — не InsertMagazine");
        }

        [UnityTest]
        public IEnumerator HandCannotPullMagazineFromWell()
        {
            Assert.IsFalse(_manager.IsSelectPossible(HandI, _magGrab), "из шахты — только кнопкой");
            yield return TakeMagazineOut();
            Assert.IsFalse(_magazine.InWell);
            Assert.IsTrue(_manager.IsSelectPossible(HandI, _magGrab));
        }

        [UnityTest]
        public IEnumerator Eject_ReleasesMagazine_KeepsRounds_LocksWell()
        {
            _manager.SelectEnter(HandI, _pistolGrab);
            yield return null;
            _interaction.Eject();
            yield return null;
            var other = Hand();
            _manager.SelectEnter((IXRSelectInteractor)other, _magGrab);   // выпавший магазин поймала другая рука
            yield return new WaitForSeconds(0.6f);                       // шахта снова принимает магазин
            _magazine.SetRounds(3);
            _manager.SelectExit((IXRSelectInteractor)other, _magGrab);    // отпущен у шахты
            _manager.SelectEnter((IXRSelectInteractor)_well.Socket, _magGrab);
            yield return null;
            Assert.IsTrue(_weapon.State.MagazineInserted, "InsertMagazine из шахты");
            Assert.AreEqual(3, _weapon.State.RoundsInMagazine);
            Assert.IsTrue(_runner.Director.Violations.Any(v => v.Type == Violation.StepOrder), "магазин вставлен на шаге 1 — InsertMagazine дошёл до директора");

            _interaction.Eject();
            yield return null;
            Assert.IsFalse(_magGrab.isSelected, "магазин вышел из шахты");
            Assert.AreEqual(3, _magazine.Rounds, "патроны остались в магазине");
            Assert.IsFalse(_well.Socket.socketActive, "шахта не хватает магазин обратно сразу");
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(_well.Socket.socketActive);
        }

        [UnityTest]
        public IEnumerator ForeignItems_NotAcceptedByWellAndPlaces()
        {
            yield return null;
            var round = _tray.Waiting;
            Assert.IsNotNull(round, "лоток выдал патрон");
            Assert.IsFalse(_manager.IsSelectPossible(_well.Socket, round), "патрон в шахту не встаёт");
            Assert.IsFalse(_manager.IsSelectPossible(_shelfMag.Socket, _pistolGrab), "пистолет на место магазина не встаёт");
            Assert.IsFalse(_manager.IsSelectPossible(_well.Socket, _pistolGrab));
            Assert.IsFalse(_manager.IsSelectPossible(_cabinet.Socket, _magGrab), "магазин в гнездо пистолета не встаёт");
            Assert.AreNotEqual(0, _magGrab.interactionLayers.value & _shelfMag.Socket.interactionLayers.value, "магазин на своё место встаёт");
        }

        [UnityTest]
        public IEnumerator FiveRounds_FromTray_IntoMagazine()
        {
            yield return TakeMagazineOut();
            _magazine.GetComponent<ItemReturn>().PutInto(_tableMag);
            yield return null;
            var sockets = _magazine.GetComponentsInChildren<XRSocketInteractor>();
            for (int i = 0; i < 5; i++)
            {
                var round = _tray.Waiting;
                Assert.IsNotNull(round, $"патрон {i + 1} в лотке");
                _manager.SelectEnter(HandI, round);
                yield return null;
                Assert.IsFalse(_tray.Waiting == round, "взятый патрон ушёл из лотка");
                var next = sockets[i];
                Assert.IsTrue(next.socketActive, "открыт следующий свободный сокет");
                Assert.IsFalse(sockets.Where((s, k) => k != i).Any(s => s.socketActive), "остальные закрыты");
                _manager.SelectExit(HandI, round);
                _manager.SelectEnter((IXRSelectInteractor)next, round);
                yield return null;
                yield return null;
                Assert.AreEqual(i + 1, _magazine.Rounds);
                Assert.IsFalse(round.gameObject.activeSelf, "патрон-предмет ушёл в пул");
            }
            yield return null;
            Assert.IsNull(_tray.Waiting, "лоток выдал ровно серию");
            Assert.AreEqual(5, _tray.Seated);
            Assert.AreEqual(5, _facts.Collect().MagazineRounds);
            Assert.IsTrue(sockets.All(s => !s.socketActive), "полный магазин не принимает патроны");
        }

        [UnityTest]
        public IEnumerator DroppedRound_GoesToPool_NextAppears()
        {
            yield return null;
            var round = _tray.Waiting;
            _manager.SelectEnter(HandI, round);
            yield return null;
            var second = _tray.Waiting;
            Assert.IsNotNull(second, "следующий патрон появился, когда предыдущий взят");
            _manager.SelectExit(HandI, round);
            round.GetComponent<Rigidbody>().position = new Vector3(3f, 0.2f, 1.5f);
            round.transform.position = new Vector3(3f, 0.2f, 1.5f);
            yield return null;
            yield return null;
            Assert.IsFalse(round.gameObject.activeSelf, "упавший патрон в пуле");
            Assert.AreEqual(1, _tray.Out, "на руках только патрон в лотке");
        }

        [UnityTest]
        public IEnumerator DroppedMagazine_ReturnsToPlaceOfCurrentAnchor()
        {
            yield return TakeMagazineOut();
            _rig.position = _a2.position;
            _magazineGo.GetComponent<Rigidbody>().position = new Vector3(2.5f, 0.3f, 2f);
            _magazineGo.transform.position = new Vector3(2.5f, 0.3f, 2f);
            Assert.AreEqual(Rubezh.Core.Place.Floor, _magazineGo.GetComponent<ItemReturn>().Place);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_tableMag.Socket.IsSelecting(_magGrab), "магазин на O-19 у якоря A2");
            Assert.AreEqual(Rubezh.Core.Place.PrepTable, _facts.Collect().MagazinePlace);
            Assert.IsFalse(_runner.Director.Violations.Any(v => v.Type != Violation.StepOrder), "без штрафа");
        }

        [UnityTest]
        public IEnumerator Restart_ReturnsEverythingToStart()
        {
            yield return TakeMagazineOut();
            _manager.SelectEnter(HandI, _pistolGrab);
            yield return null;
            _runner.Restart();
            yield return null;
            yield return null;
            Assert.IsTrue(_cabinet.Socket.IsSelecting(_pistolGrab), "пистолет в шкафу");
            Assert.IsTrue(_well.Socket.IsSelecting(_magGrab), "магазин в шахте");
            Assert.IsTrue(_weapon.State.MagazineInserted);
            Assert.AreEqual(0, _magazine.Rounds);
            Assert.IsFalse(_weapon.IsHeld);
            Assert.AreEqual(0, _tray.Seated);
            Assert.AreEqual(1, _tray.Out, "в лотке снова первый патрон");
            Assert.IsEmpty(_runner.Director.Violations);
        }
    }
}
