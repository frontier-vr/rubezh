using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.Tests.PlayMode
{
    // СИЗ (NG-10): слоты у головы, IWearables, снапшоты микшера, оправа очков, возврат на полку.
    public class WearablesTests
    {
        private sealed class TestHand : XRBaseInteractor { }

        private GameObject _root, _earmuffs, _glasses;
        private XRInteractionManager _manager;
        private ExerciseRunner _runner;
        private RulesConfig _rules;
        private AudioMixer _mixer;
        private Wearables _wear;
        private XRSocketInteractor _headE, _headG;
        private PlaceSocket _shelfE, _shelfG;
        private XRGrabInteractable _grabE, _grabG;
        private TestHand _hand;
        private int _changes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Rubezh/Audio/Mixer/Rubezh.mixer");
#endif
            _root = new GameObject("Root");
            _manager = _root.AddComponent<XRInteractionManager>();
            _runner = _root.AddComponent<ExerciseRunner>();
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.earmuffsTransitionSeconds = 0.1f;
            _rules.dropReturnSeconds = 0.2f;
            var head = new GameObject("Head").transform;
            head.SetParent(_root.transform, false);
            head.position = new Vector3(0f, 1.6f, 0f);
            _headE = Socket(head, "HeadSlot_Earmuffs", 4, new Vector3(0f, -0.03f, -0.09f));
            _headG = Socket(head, "HeadSlot_Glasses", 5, new Vector3(0f, 0f, 0.015f));
            _shelfE = Socket(_root.transform, "O-10", 4, new Vector3(-3.58f, 1.27f, 2.32f)).gameObject.AddComponent<PlaceSocket>();
            _shelfG = Socket(_root.transform, "O-12", 5, new Vector3(-3.52f, 1.245f, 2.7f)).gameObject.AddComponent<PlaceSocket>();

            _wear = _root.AddComponent<Wearables>();
            _wear.Configure(_runner, _rules, _headE, _headG, _mixer.FindSnapshot("EarsOpen"), _mixer.FindSnapshot("Earmuffs"));
            _wear.Changed += () => _changes++;

            _earmuffs = Item("Earmuffs_Interactive", _shelfE);
            _glasses = Item("Glasses_Interactive", _shelfG);
            _grabE = _earmuffs.GetComponent<XRGrabInteractable>();
            _grabG = _glasses.GetComponent<XRGrabInteractable>();

            var go = new GameObject("Hand");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(0.2f, 1.3f, 0.3f);
            go.SetActive(false);
            _hand = go.AddComponent<TestHand>();
            _hand.interactionLayers = new InteractionLayerMask { value = -1 };
            go.SetActive(true);
            yield return null;
            _changes = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_earmuffs);
            Object.Destroy(_glasses);
            Object.Destroy(_root);
            Object.Destroy(_rules);
            _mixer.FindSnapshot("EarsOpen").TransitionTo(0f);
            yield return null;
        }

        private static XRSocketInteractor Socket(Transform parent, string name, int layer, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.SetActive(false);
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.1f;
            var s = go.AddComponent<XRSocketInteractor>();
            s.interactionLayers = new InteractionLayerMask { value = 1 << layer };
            go.SetActive(true);
            return s;
        }

        private GameObject Item(string prefab, PlaceSocket shelf)
        {
#if UNITY_EDITOR
            var go = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Rubezh/Prefabs/Interactive/{prefab}.prefab"));
#else
            GameObject go = null;
#endif
            go.GetComponent<XRGrabInteractable>().attachEaseInTime = 0f;
            var ret = go.GetComponent<ItemReturn>();
            ret.Configure(_runner, _rules, null, shelf);
            ret.ToStart();
            return go;
        }

        private IEnumerator PutOn(XRGrabInteractable item, XRSocketInteractor slot)
        {
            _manager.SelectEnter((IXRSelectInteractor)_hand, item);
            yield return null;
            _manager.SelectExit((IXRSelectInteractor)_hand, item);
            _manager.SelectEnter((IXRSelectInteractor)slot, item);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Start_NothingWorn_EarsOpen()
        {
            yield return null;
            Assert.IsFalse(_wear.EarmuffsOn);
            Assert.IsFalse(_wear.GlassesOn);
            Assert.AreEqual("EarsOpen", _wear.CurrentSnapshot.name);
            Assert.IsTrue(_shelfE.Socket.IsSelecting(_grabE), "наушники на полке O-10");
        }

        [UnityTest]
        public IEnumerator Earmuffs_On_MuffleSfx_ReportAction()
        {
            yield return PutOn(_grabE, _headE);
            Assert.IsTrue(_wear.EarmuffsOn);
            Assert.AreEqual(1, _changes);
            Assert.AreEqual("Earmuffs", _wear.CurrentSnapshot.name);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(_mixer.GetFloat("SfxCutoff", out float cutoff));
            Assert.Less(cutoff, 2000f, "фильтр низких частот на SFX");
            Assert.IsTrue(_mixer.GetFloat("SfxVolume", out float volume));
            Assert.Less(volume, -6f, "SFX тише");
            Assert.IsFalse(_earmuffs.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "на голове наушники не рисуются");
        }

        [UnityTest]
        public IEnumerator Earmuffs_Off_EarsOpenAgain()
        {
            yield return PutOn(_grabE, _headE);
            _manager.SelectEnter((IXRSelectInteractor)_hand, _grabE);   // Grip у головы — снять
            yield return null;
            Assert.IsFalse(_wear.EarmuffsOn);
            Assert.AreEqual("EarsOpen", _wear.CurrentSnapshot.name);
            yield return new WaitForSeconds(0.3f);
            _mixer.GetFloat("SfxCutoff", out float cutoff);
            Assert.Greater(cutoff, 10000f);
            Assert.IsTrue(_earmuffs.GetComponentsInChildren<Renderer>().All(r => r.enabled));
        }

        [UnityTest]
        public IEnumerator Glasses_On_FrameVisible_LensesHidden()
        {
            yield return PutOn(_grabG, _headG);
            Assert.IsTrue(_wear.GlassesOn);
            var renderers = _glasses.GetComponentsInChildren<Renderer>();
            Assert.IsTrue(renderers.Where(r => r.name.Contains("Frame")).All(r => r.enabled), "оправа в поле зрения");
            Assert.IsTrue(renderers.Where(r => r.name.Contains("Lens")).All(r => !r.enabled), "непрозрачные стёкла не закрывают обзор");
            var anchor = _glasses.transform.Find("HeadAnchor");
            Assert.Less(Vector3.Distance(anchor.position, _headG.transform.position), 0.001f, "очки сели по HeadAnchor");
            Assert.AreEqual("EarsOpen", _wear.CurrentSnapshot.name, "очки звук не меняют");
        }

        [UnityTest]
        public IEnumerator PutOn_DuringInspect_IsIncompleteInspection()
        {
            var empty = new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None);
            _runner.UpdateFacts(new ExerciseFacts(true, empty, 0, Place.Hand, Place.InWeapon, false, false, false));
            Assert.AreEqual(Phase.Inspect, _runner.Phase);
            yield return PutOn(_grabE, _headE);
            Assert.IsTrue(_runner.Director.Violations.Any(v => v.Type == Violation.IncompleteInspection), "PutOnEarmuffs дошло до директора");
        }

        [UnityTest]
        public IEnumerator Dropped_ReturnsToShelf_AndRestartTakesOffHead()
        {
            _manager.SelectEnter((IXRSelectInteractor)_hand, _grabG);
            yield return null;
            _manager.SelectExit((IXRSelectInteractor)_hand, _grabG);
            _glasses.GetComponent<Rigidbody>().position = new Vector3(0f, 0.2f, 0.5f);
            _glasses.transform.position = new Vector3(0f, 0.2f, 0.5f);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_shelfG.Socket.IsSelecting(_grabG), "упавшие очки на полке");

            yield return PutOn(_grabE, _headE);
            _runner.Restart();
            yield return null;
            Assert.IsFalse(_wear.EarmuffsOn, "Restart снимает наушники");
            Assert.IsTrue(_shelfE.Socket.IsSelecting(_grabE));
            Assert.AreEqual("EarsOpen", _wear.CurrentSnapshot.name);
        }
    }
}
