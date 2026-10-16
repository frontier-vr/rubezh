using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Звуки пистолета (MM-09) на настоящем префабе: клипы назначены, каждое действие с оружием
    // звучит, недопустимая команда — нет.
    public class WeaponAudioTests
    {
        private GameObject _root, _pistol;
        private WeaponController _weapon;
        private WeaponAudio _audio;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            // Свой менеджер: иначе пистолет создаст менеджер в DontDestroyOnLoad, и он помешает другим тестам.
            _root.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            var runner = _root.AddComponent<ExerciseRunner>();
#if UNITY_EDITOR
            _pistol = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/Pistol_Interactive.prefab"));
#endif
            _pistol.GetComponent<Rigidbody>().isKinematic = true;
            _weapon = _pistol.GetComponent<WeaponController>();
            _weapon.Configure(runner, null);
            _audio = _pistol.GetComponent<WeaponAudio>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_pistol);
            Object.Destroy(_root);
            yield return null;
        }

        [Test]
        public void SfxGroup_AndClipsAssigned()
        {
            Assert.AreEqual("SFX", _audio.Source.outputAudioMixerGroup.name);
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(_audio);
            foreach (var field in new[] { "shot", "dryFire", "slideBack", "slideForward", "safety", "magazineInsert", "magazineDrop" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"нет клипа {field}");
#endif
        }

        [UnityTest]
        public IEnumerator EveryAction_PlaysSound()
        {
            int Count(System.Action act) { int before = _audio.Played; act(); return _audio.Played - before; }

            Assert.AreEqual(1, Count(() => _weapon.Apply(WeaponCommand.EjectMagazine)), "выпадение магазина");
            Assert.AreEqual(1, Count(() => _weapon.InsertMagazine(5)), "вставка магазина");
            Assert.AreEqual(1, Count(() => _weapon.Apply(WeaponCommand.SlideBack)), "отвод затвора");
            Assert.AreEqual(1, Count(() => _weapon.Apply(WeaponCommand.SlideForward)), "возврат затвора");
            Assert.AreEqual(1, Count(() => _weapon.Apply(WeaponCommand.SafetyOff)), "предохранитель");
            Assert.AreEqual(0, Count(() => _weapon.Apply(WeaponCommand.SafetyOff)), "недопустимая команда без звука");
            Assert.AreEqual(1, Count(() => _weapon.Apply(WeaponCommand.TriggerPull)), "выстрел");
            yield return null;
        }
    }
}
