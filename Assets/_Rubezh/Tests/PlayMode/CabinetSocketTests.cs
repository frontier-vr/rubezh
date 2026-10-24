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
    // Шкаф O-05 (NG-14): заряженный пистолет не принимает, разряженный принимает, одна попытка —
    // одно нарушение; на старте и при постановке системой гнездо пистолет держит.
    public class CabinetSocketTests
    {
        private sealed class TestHand : XRBaseInteractor { }

        private GameObject _root, _pistol;
        private XRInteractionManager _manager;
        private ExerciseRunner _runner;
        private WeaponController _weapon;
        private XRGrabInteractable _grab;
        private XRSocketInteractor _socket;
        private CabinetSocket _cabinet;
        private TestHand _hand;
        private int _refused;

        private static readonly WeaponState Clear = new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _manager = _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
#if UNITY_EDITOR
            _pistol = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/Pistol_Interactive.prefab"),
                                         new Vector3(-3.4f, 1.335f, 1.5f), Quaternion.identity);
#endif
            _weapon = _pistol.GetComponent<WeaponController>();
            _weapon.Configure(_runner, null);
            _grab = _pistol.GetComponent<XRGrabInteractable>();
            _grab.attachEaseInTime = 0f;

            var slot = new GameObject("O-05");
            slot.transform.SetParent(_root.transform, false);
            slot.transform.position = new Vector3(-3.4f, 1.335f, 1.5f);
            slot.SetActive(false);
            var trigger = slot.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.25f, 0.2f, 0.3f);
            _socket = slot.AddComponent<XRSocketInteractor>();
            _socket.interactionLayers = new InteractionLayerMask { value = 1 << 1 };
            _socket.recycleDelayTime = 0f;   // в тестах подход сразу после взятия из шкафа
            slot.AddComponent<PlaceSocket>().Configure(Place.Cabinet, Anchor.A1);
            _cabinet = slot.AddComponent<CabinetSocket>();
            slot.SetActive(true);
            _cabinet.Configure(_runner, _weapon);
            _cabinet.Refused += _ => _refused++;

            var h = new GameObject("Hand");
            h.transform.SetParent(_root.transform, false);
            h.transform.position = new Vector3(-2.8f, 1.3f, 1.5f);
            h.SetActive(false);
            _hand = h.AddComponent<TestHand>();
            _hand.interactionLayers = new InteractionLayerMask { value = -1 };
            h.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_pistol);
            Object.Destroy(_root);
            yield return null;
        }

        private void Facts(bool inHand, WeaponState w, int rounds, Place pistol, Place mag, bool target = true) =>
            _runner.UpdateFacts(new ExerciseFacts(inHand, w, rounds, pistol, mag, true, true, target));

        // Директор до шага сдачи.
        private void ToReturn()
        {
            var cabinet = new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None);
            Facts(true, cabinet, 0, Place.Hand, Place.InWeapon, false);
            Facts(true, Clear, 0, Place.Hand, Place.InspectShelf, false);
            Facts(true, Clear, 5, Place.Hand, Place.PrepTable, false);
            Facts(false, Clear, 5, Place.LineStand, Place.LineStand);
            Facts(true, new WeaponState(true, 3, Chamber.Loaded, false, true, Jam.None), 3, Place.Hand, Place.InWeapon);
            Facts(true, new WeaponState(true, 0, Chamber.Empty, true, false, Jam.None), 0, Place.Hand, Place.InWeapon);
            Facts(true, new WeaponState(true, 0, Chamber.Empty, true, true, Jam.None), 0, Place.Hand, Place.InWeapon);
            Facts(true, Clear, 0, Place.Hand, Place.Hand);
            Facts(false, Clear, 0, Place.LineStand, Place.LineStand);
            Assert.AreEqual(Phase.Return, _runner.Phase);
        }

        private void Hover()
        {
            if (!_socket.IsHovering(_grab)) _manager.HoverEnter((IXRHoverInteractor)_socket, _grab);
        }

        private int ReturnedLoaded => _runner.Director.Violations.Count(v => v.Type == Violation.ReturnedLoaded);

        private IEnumerator TakeInHand()
        {
            _manager.SelectEnter((IXRSelectInteractor)_hand, _grab);
            yield return null;
            Assert.IsTrue(_weapon.IsHeld);
        }

        [UnityTest]
        public IEnumerator AtStart_CabinetHoldsPistolWithEmptyMagazine()
        {
            _manager.SelectEnter((IXRSelectInteractor)_socket, _grab);
            yield return null;
            yield return null;
            Assert.IsTrue(_socket.IsSelecting(_grab), "на шаге получения пистолет в шкафу с пустым магазином");
            Assert.IsFalse(_weapon.State.IsClear);
        }

        [UnityTest]
        public IEnumerator Loaded_IsRefused_OneViolationPerApproach()
        {
            yield return TakeInHand();
            yield return null;
            if (_socket.IsHovering(_grab)) _manager.HoverExit((IXRHoverInteractor)_socket, _grab);   // увёл от шкафа
            ToReturn();
            Assert.IsFalse(_weapon.State.IsClear, "в руке пистолет с магазином");
            Assert.IsFalse(_cabinet.Process(_socket, _grab), "фильтр гнезда не пропускает заряженный");
            Hover();
            for (int i = 0; i < 5; i++) _cabinet.Process(_socket, _grab);    // XRI проверяет фильтр много раз за подход
            yield return null;
            Assert.AreEqual(1, ReturnedLoaded, "одна попытка — одно нарушение");
            Assert.AreEqual(1, _refused);
            if (_socket.IsHovering(_grab)) _manager.HoverExit((IXRHoverInteractor)_socket, _grab);   // увёл от гнезда
            Hover();
            yield return null;
            Assert.AreEqual(2, ReturnedLoaded, "новый подход — новое нарушение");
            Assert.IsTrue(_weapon.IsHeld, "пистолет остался в руке");
        }

        [UnityTest]
        public IEnumerator Clear_IsAccepted()
        {
            yield return TakeInHand();
            ToReturn();
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _weapon.Apply(WeaponCommand.SlideBack);
            Assert.IsTrue(_weapon.State.IsClear);
            Hover();
            _manager.SelectExit((IXRSelectInteractor)_hand, _grab);          // отпустил у гнезда
            Assert.IsTrue(_manager.IsSelectPossible(_socket, _grab), "разряженный шкаф принимает");
            _manager.SelectEnter((IXRSelectInteractor)_socket, _grab);
            yield return null;
            Assert.IsTrue(_socket.IsSelecting(_grab));
            Assert.AreEqual(0, ReturnedLoaded);
        }

        [UnityTest]
        public IEnumerator Restart_PutsCabinetStatePistolBack()
        {
            yield return TakeInHand();
            ToReturn();
            var ret = _pistol.GetComponent<ItemReturn>();
            ret.Configure(_runner, null, null, _socket.GetComponent<PlaceSocket>());
            _runner.Restart();                   // «Повторить»: пистолет в шкаф в состоянии шкафа
            yield return null;
            yield return null;
            Assert.IsTrue(_socket.IsSelecting(_grab), "после Restart пистолет снова в шкафу");
            Assert.IsFalse(_weapon.IsHeld);
        }
    }
}
