using System.Collections;
using System.Collections.Generic;
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
    // Вибрация и вспышка (NG-15): отдача и вспышка на каждый выстрел, на осечке — ничего; щелчки
    // магазина и задержки; импульс только рукам, не сокетам.
    public class WeaponFeedbackTests
    {
        private sealed class TestHand : XRBaseInteractor { }

        private GameObject _root, _pistol;
        private XRInteractionManager _manager;
        private WeaponController _weapon;
        private WeaponFeedback _feedback;
        private XRGrabInteractable _grab;
        private GameObject _flash;
        private TestHand _hand;
        private readonly List<float> _pulses = new List<float>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _manager = _root.AddComponent<XRInteractionManager>();
            _root.AddComponent<AudioListener>();
            var runner = _root.AddComponent<ExerciseRunner>();
#if UNITY_EDITOR
            _pistol = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/Pistol_Interactive.prefab"));
#endif
            _pistol.GetComponent<Rigidbody>().isKinematic = true;
            _weapon = _pistol.GetComponent<WeaponController>();
            _weapon.Configure(runner, null);
            _grab = _pistol.GetComponent<XRGrabInteractable>();
            _feedback = _pistol.GetComponent<WeaponFeedback>();
            _flash = _pistol.transform.Find("Muzzle/MuzzleFlash").gameObject;
            var h = new GameObject("Hand");
            h.SetActive(false);
            h.transform.SetParent(_root.transform, false);
            _hand = h.AddComponent<TestHand>();
            h.SetActive(true);
            Haptics.Sent += OnSent;
            yield return null;
            _manager.SelectEnter((IXRSelectInteractor)_hand, _grab);
            yield return null;
            _pulses.Clear();
        }

        private void OnSent(IXRInteractor who, float amplitude, float duration) => _pulses.Add(amplitude);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Haptics.Sent -= OnSent;
            Object.Destroy(_pistol);
            Object.Destroy(_root);
            yield return null;
        }

        private void Load()
        {
            _weapon.Apply(WeaponCommand.EjectMagazine);
            _weapon.InsertMagazine(5);
            _weapon.Apply(WeaponCommand.SlideBack);
            _weapon.Apply(WeaponCommand.SlideForward);
            _weapon.Apply(WeaponCommand.SafetyOff);
            _pulses.Clear();
        }

        [Test]
        public void Prefab_HasFlashOnMuzzle_OffByDefault()
        {
            Assert.IsNotNull(_feedback);
            Assert.IsFalse(_flash.activeSelf, "вспышка выключена до выстрела");
        }

        [UnityTest]
        public IEnumerator Shot_RecoilPulse_AndFlashForFewFrames()
        {
            Load();
            _weapon.Apply(WeaponCommand.TriggerPull);
            Assert.AreEqual(1, _pulses.Count, "отдача — импульс руке");
            Assert.Greater(_pulses[0], 0.5f);
            Assert.IsTrue(_flash.activeSelf, "вспышка включилась");
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Assert.IsFalse(_flash.activeSelf, "через 2–3 кадра погасла");
        }

        [UnityTest]
        public IEnumerator DryFire_NoRecoil_NoFlash()
        {
            _weapon.Apply(WeaponCommand.SafetyOff);              // пустой патронник
            _pulses.Clear();
            int flashes = _feedback.Flashes;
            _weapon.Apply(WeaponCommand.TriggerPull);
            yield return null;
            Assert.IsEmpty(_pulses, "сухой щелчок без отдачи");
            Assert.AreEqual(flashes, _feedback.Flashes);
            Assert.IsFalse(_flash.activeSelf);
        }

        [UnityTest]
        public IEnumerator Magazine_And_SlideLock_GiveClicks()
        {
            _weapon.Apply(WeaponCommand.EjectMagazine);
            Assert.AreEqual(1, _pulses.Count, "выпадение магазина");
            _weapon.Apply(WeaponCommand.SlideBack);              // без магазина — на задержку
            Assert.AreEqual(2, _pulses.Count, "затвор на задержке");
            _weapon.InsertMagazine(5);
            Assert.AreEqual(3, _pulses.Count, "щелчок магазина");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PistolInSocket_NoPulse()
        {
            _manager.SelectExit((IXRSelectInteractor)_hand, _grab);
            yield return null;
            Load();
            _weapon.Apply(WeaponCommand.TriggerPull);
            Assert.IsEmpty(_pulses, "в руке никого — импульс некому");
        }
    }
}
