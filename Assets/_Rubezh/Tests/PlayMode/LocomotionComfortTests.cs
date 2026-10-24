using System.Collections;
using NUnit.Framework;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Комфорт перемещения (YG-16): звук на телепорт и поворот щелчками в режиме «звук» (по умолчанию),
    // виньетка включается и выключается целым объектом только в своих режимах.
    public class LocomotionComfortTests
    {
        private GameObject _go, _vignette;
        private LocomotionComfort _comfort;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _go = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Art/LocomotionComfort.prefab"));
#endif
            _go.AddComponent<AudioListener>();
            _vignette = new GameObject("Vignette");
            _comfort = _go.GetComponent<LocomotionComfort>();
            var source = _go.GetComponent<AudioSource>();
            AudioClip t = null, s = null;
#if UNITY_EDITOR
            t = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Rubezh/Audio/SFX/Locomotion_Teleport.wav");
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Rubezh/Audio/SFX/Locomotion_SnapTurn.wav");
#endif
            _comfort.Configure(null, null, source, t, s, _vignette);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_go);
            Object.Destroy(_vignette);
            yield return null;
        }

        [Test]
        public void DefaultIsSound_NoVignette()
        {
            Assert.AreEqual(LocomotionComfort.Mode.Sound, _comfort.Current);
            Assert.IsFalse(_vignette.activeSelf, "виньетка выключена целым объектом");
            _comfort.SimulateTeleport();
            _comfort.SimulateTurn();
            Assert.AreEqual(2, _comfort.Sounds, "звук на телепорт и на поворот");
        }

        [Test]
        public void Sounds_AreShortAndQuiet()
        {
#if UNITY_EDITOR
            var t = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Rubezh/Audio/SFX/Locomotion_Teleport.wav");
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Rubezh/Audio/SFX/Locomotion_SnapTurn.wav");
            Assert.Less(t.length, 0.5f, "телепорт — короткий «вжух»");
            Assert.Less(s.length, 0.1f, "поворот — щелчок");
#endif
            Assert.Less(_go.GetComponent<AudioSource>().volume, 1f);
        }

        [Test]
        public void Modes_SwitchVignetteAndSound()
        {
            _comfort.SetMode(LocomotionComfort.Mode.Vignette);
            Assert.IsTrue(_vignette.activeSelf);
            _comfort.SimulateTeleport();
            Assert.AreEqual(0, _comfort.Sounds, "в режиме «виньетка» звука нет");
            _comfort.SetMode(LocomotionComfort.Mode.SoundAndVignette);
            _comfort.SimulateTurn();
            Assert.AreEqual(1, _comfort.Sounds);
            _comfort.SetMode(LocomotionComfort.Mode.None);
            Assert.IsFalse(_vignette.activeSelf);
            _comfort.SimulateTeleport();
            Assert.AreEqual(1, _comfort.Sounds, "без средств — тишина");
        }
    }
}
