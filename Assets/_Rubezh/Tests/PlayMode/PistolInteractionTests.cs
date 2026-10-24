using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace Rubezh.Tests.PlayMode
{
    // Интерактивный пистолет (NG-08) без шлема: руки — интеракторы, которым выбор задаётся
    // напрямую через XRInteractionManager. Проверяется хват двумя руками, затвор, кнопки
    // и спуск руки на Grip_Primary, выключение стика этой руки.
    public class PistolInteractionTests
    {
        private sealed class TestHand : XRBaseInteractor { }

        private GameObject _root, _pistol;
        private XRInteractionManager _manager;
        private ExerciseRunner _runner;
        private WeaponController _weapon;
        private PistolInteraction _interaction;
        private SlideHandle _slide;
        private ShotResolver _resolver;
        private XRGrabInteractable _grab;
        private TestHand _left, _right;
        private InputActionAsset _actions;
        private InputAction _rightMove;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _manager = _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _left = Hand("Left", InteractorHandedness.Left, new Vector3(-0.3f, 1.2f, 0.3f));
            _right = Hand("Right", InteractorHandedness.Right, new Vector3(0.3f, 1.2f, 0.3f));

            _pistol = Object.Instantiate(LoadPrefab());
            _pistol.transform.position = new Vector3(0f, 1f, 0.5f);
            _pistol.GetComponent<Rigidbody>().isKinematic = true;
            _weapon = _pistol.GetComponent<WeaponController>();
            _resolver = _pistol.GetComponent<ShotResolver>();
            _resolver.Configure(_runner, _pistol.transform.Find("Muzzle"), null, 0, 0);
            _weapon.Configure(_runner, _resolver);
            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            _rightMove = _actions.AddActionMap("Right Hand").AddAction("Move", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}");
            _rightMove.Enable();
            _interaction = _pistol.GetComponent<PistolInteraction>();
            _interaction.Configure(null, null, null, null, new[] { InputActionReference.Create(_rightMove) });
            _grab = _pistol.GetComponent<XRGrabInteractable>();
            _grab.interactionManager = _manager;
            _grab.attachEaseInTime = 0f;   // в тестах пистолет сразу в руке, без плавного подхвата
            _slide = _pistol.GetComponentInChildren<SlideHandle>();
            _slide.Configure(_weapon, null);
            var slideInteractable = _slide.GetComponent<XRSimpleInteractable>();
            slideInteractable.interactionManager = _manager;

            yield return null;
        }

        private static GameObject LoadPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/Pistol_Interactive.prefab");
#else
            return null;
#endif
        }

        private TestHand Hand(string name, InteractorHandedness handedness, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;
            go.SetActive(false);
            var hand = go.AddComponent<TestHand>();
            hand.handedness = handedness;
            hand.interactionManager = _manager;
            go.SetActive(true);
            return hand;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rightMove.Disable();
            Object.Destroy(_actions);
            Object.Destroy(_pistol);
            Object.Destroy(_root);
            yield return null;
        }

        [Test]
        public void Prefab_IsVariantOfArtPistol_WithRequiredSetup()
        {
#if UNITY_EDITOR
            var prefab = LoadPrefab();
            var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            Assert.IsNotNull(source, "Prefab Variant");
            Assert.AreEqual("Pistol", source.name);
#endif
            Assert.AreEqual(InteractableSelectMode.Multiple, _grab.selectMode, "без Multiple вторая рука не возьмёт");
            Assert.AreEqual("Grip_Primary", _grab.attachTransform.name);
            Assert.AreEqual("Grip_Secondary", _grab.secondaryAttachTransform.name);
            Assert.IsNotNull(_pistol.GetComponent<XRGeneralGrabTransformer>());
            CollectionAssert.DoesNotContain(_grab.colliders, _slide.GetComponent<Collider>(), "коллайдер затвора — у затвора");
            var slideBounds = _slide.GetComponent<Collider>().bounds;
            var secondary = _grab.secondaryAttachTransform.GetComponent<Collider>();
            Assert.IsNotNull(secondary);
            Assert.IsFalse(slideBounds.Intersects(secondary.bounds), "коллайдеры Slide и Grip_Secondary не перекрываются");
        }

        [UnityTest]
        public IEnumerator Grab_PrimaryHand_IsHeldWithHand()
        {
            _manager.SelectEnter((IXRSelectInteractor)_left, _grab);
            yield return null;
            Assert.IsTrue(_weapon.IsHeld);
            Assert.AreEqual(Rubezh.Gameplay.Hand.Left, _weapon.PrimaryHand);
            _manager.SelectExit((IXRSelectInteractor)_left, _grab);
            yield return null;
            Assert.IsFalse(_weapon.IsHeld);
        }

        [UnityTest]
        public IEnumerator TwoHands_BothSelect_FirstStaysPrimary()
        {
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            _manager.SelectEnter((IXRSelectInteractor)_left, _grab);
            yield return null;
            Assert.AreEqual(2, _grab.interactorsSelecting.Count, "второй хват");
            Assert.AreEqual(Rubezh.Gameplay.Hand.Right, _weapon.PrimaryHand);
            _manager.SelectExit((IXRSelectInteractor)_right, _grab);
            yield return null;
            Assert.IsTrue(_weapon.IsHeld, "держит вторая рука");
            Assert.AreEqual(Rubezh.Gameplay.Hand.Left, _weapon.PrimaryHand, "рука на Grip_Primary сменилась");
        }

        [UnityTest]
        public IEnumerator Slide_NotGrabbableUntilPistolHeld()
        {
            var slide = _slide.GetComponent<XRSimpleInteractable>();
            Assert.IsFalse(slide.enabled);
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            Assert.IsTrue(slide.enabled);
        }

        [UnityTest]
        public IEnumerator Slide_PullAndRelease_ChambersRound()
        {
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _weapon.InsertMagazine(5);
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            var slide = _slide.GetComponent<XRSimpleInteractable>();
            _left.transform.position = _slide.transform.position;
            _manager.SelectEnter((IXRSelectInteractor)_left, slide);
            yield return null;
            Assert.IsTrue(_slide.IsGrabbed);
            _left.transform.position -= _pistol.transform.forward * 0.035f;
            yield return null;
            Assert.AreEqual(0.03f, _slide.Offset, 0.001f, "затвор за рукой до упора");
            Assert.AreEqual(Chamber.Empty, _weapon.State.Chamber, "удерживается сзади");
            _manager.SelectExit((IXRSelectInteractor)_left, slide);
            yield return null;
            Assert.AreEqual(Chamber.Loaded, _weapon.State.Chamber);
            Assert.AreEqual(4, _weapon.State.RoundsInMagazine);
            Assert.AreEqual(0f, _slide.Offset);
        }

        [UnityTest]
        public IEnumerator Slide_EmptyGun_LocksBack_AndStaysBack()
        {
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            var slide = _slide.GetComponent<XRSimpleInteractable>();
            _left.transform.position = _slide.transform.position;
            _manager.SelectEnter((IXRSelectInteractor)_left, slide);
            yield return null;
            _left.transform.position -= _pistol.transform.forward * 0.035f;
            yield return null;
            _manager.SelectExit((IXRSelectInteractor)_left, slide);
            yield return null;
            Assert.IsTrue(_weapon.State.IsClear, "без магазина затвор на задержке — разряжен для сдачи");
            Assert.AreEqual(0.024f, _slide.Offset, 0.001f, "на задержке затвор стоит сзади");
            Assert.IsTrue(_interaction.ReleaseSlideStop(), "стик — снять с задержки");
            Assert.IsFalse(_weapon.State.SlideLocked);
        }

        [UnityTest]
        public IEnumerator ReleasePistol_ReleasesSlide()
        {
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            var slide = _slide.GetComponent<XRSimpleInteractable>();
            _manager.SelectEnter((IXRSelectInteractor)_left, slide);
            yield return null;
            _manager.SelectExit((IXRSelectInteractor)_right, _grab);
            yield return null;
            Assert.IsFalse(_slide.IsGrabbed);
            Assert.IsFalse(slide.isSelected);
        }

        [UnityTest]
        public IEnumerator Buttons_OnlyWhileHeld_ReachWeapon()
        {
            Assert.IsFalse(_interaction.ToggleSafety(), "пистолет не в руке");
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            Assert.IsTrue(_weapon.State.SafetyOn);
            Assert.IsTrue(_interaction.ToggleSafety());
            Assert.IsFalse(_weapon.State.SafetyOn, "предохранитель снят");
            Assert.IsTrue(_interaction.Eject());
            Assert.IsFalse(_weapon.State.MagazineInserted);
        }

        [UnityTest]
        public IEnumerator Trigger_FiresOncePerSqueeze_ShotsReachResolver()
        {
            int fired = 0;
            _weapon.Fired += () => fired++;
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _weapon.InsertMagazine(5);
            _weapon.Apply(WeaponCommand.SlideBack);
            _weapon.Apply(WeaponCommand.SlideForward);
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            _interaction.ToggleSafety();
            _interaction.Squeeze(1f);
            _interaction.Squeeze(1f);
            Assert.AreEqual(1, fired);
            _interaction.Squeeze(0.2f);
            _interaction.Squeeze(0.9f);
            Assert.AreEqual(2, fired);
            Assert.AreEqual(2, _resolver.Shots, "ShotResolver подписан на выстрелы контроллера");
        }

        [UnityTest]
        public IEnumerator Release_WithTriggerPulled_ReleasesTrigger()
        {
            int fired = 0;
            _weapon.Fired += () => fired++;
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _weapon.InsertMagazine(5);
            _weapon.Apply(WeaponCommand.SlideBack);
            _weapon.Apply(WeaponCommand.SlideForward);
            _weapon.Apply(WeaponCommand.SafetyOff);
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            _interaction.Squeeze(1f);
            _manager.SelectExit((IXRSelectInteractor)_right, _grab);
            yield return null;
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            _interaction.Squeeze(1f);
            Assert.AreEqual(2, fired, "спуск отпущен вместе с пистолетом");
        }

        [UnityTest]
        public IEnumerator StickOfHoldingHand_DisabledWhileHeld()
        {
            Assert.IsTrue(_rightMove.enabled);
            _manager.SelectEnter((IXRSelectInteractor)_right, _grab);
            yield return null;
            Assert.IsFalse(_rightMove.enabled, "стик руки с пистолетом выключен");
            _manager.SelectExit((IXRSelectInteractor)_right, _grab);
            yield return null;
            Assert.IsTrue(_rightMove.enabled);
        }

        [UnityTest]
        public IEnumerator Restart_ReturnsCabinetState_KeepsSubscribers()
        {
            int changes = 0;
            _weapon.Changed += _ => changes++;
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _runner.Restart();
            yield return null;
            Assert.IsTrue(_weapon.State.MagazineInserted, "в шкафу пистолет с пустым магазином");
            Assert.IsTrue(_weapon.State.SafetyOn);
            Assert.AreEqual(2, changes);
            Assert.That(_weapon.MisfireShot, Is.InRange(3, 4), "осечка назначена заново");
        }
    }
}
