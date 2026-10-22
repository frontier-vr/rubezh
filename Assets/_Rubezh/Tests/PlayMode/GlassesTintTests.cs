using System;
using System.Collections;
using NUnit.Framework;
using Rubezh.Gameplay;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Оттенок очков (NG-30): Volume с Color Filter включается, когда очки надеты, и гаснет, когда
    // сняты; RulesConfig выключает оттенок целиком. Рендерер: встроенный постпроцессинг выключен,
    // On Tile Post Processing добавлен, текстуры глубины и непрозрачного не запрашиваются.
    public class GlassesTintTests
    {
        private sealed class FakeWearables : MonoBehaviour, IWearables
        {
            public bool Glasses;
            public bool EarmuffsOn => false;
            public bool GlassesOn => Glasses;
            public event Action Changed;
            public void Set(bool on) { Glasses = on; Changed?.Invoke(); }
        }

        private GameObject _go;
        private FakeWearables _wear;
        private Volume _volume;
        private GlassesTint _tint;
        private RulesConfig _rules;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("GlassesTint");
            _wear = _go.AddComponent<FakeWearables>();
            _volume = _go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.glassesTintSeconds = 0.1f;
            _tint = _go.AddComponent<GlassesTint>();
            _tint.Configure(_wear, _volume, _rules);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(_go);
            UnityEngine.Object.Destroy(_rules);
            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator NoGlasses_NoTint_VolumeOff()
        {
            yield return Wait(0.2f);
            Assert.AreEqual(0f, _tint.Weight);
            Assert.IsFalse(_volume.enabled, "выключенный Volume ничего не стоит");
        }

        [UnityTest]
        public IEnumerator GlassesOn_TintFadesIn_OffFadesOut()
        {
            _wear.Set(true);
            yield return null;
            Assert.Less(_tint.Weight, 1f, "плавно, не рывком");
            yield return Wait(0.2f);
            Assert.AreEqual(1f, _tint.Weight);
            Assert.IsTrue(_volume.enabled);
            _wear.Set(false);
            yield return Wait(0.2f);
            Assert.AreEqual(0f, _tint.Weight);
            Assert.IsFalse(_volume.enabled);
        }

        [UnityTest]
        public IEnumerator DisabledInRules_NeverTints()
        {
            _rules.glassesTintEnabled = false;
            _wear.Set(true);
            yield return Wait(0.2f);
            Assert.AreEqual(0f, _tint.Weight, "выключатель на случай, если 72 Гц не держатся");
        }

#if UNITY_EDITOR
        [Test]
        public void Assets_ProfileHasColorFilter_RendererUsesOnTile()
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Rubezh/Settings/GlassesTint_Profile.asset");
            Assert.IsNotNull(profile);
            Assert.IsTrue(profile.TryGet(out ColorAdjustments ca));
            Assert.IsTrue(ca.colorFilter.overrideState);
            Assert.Less(ca.colorFilter.value.r, 1f, "оттенок, а не белый фильтр");

            var renderer = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Rubezh/Settings/URP_Renderer.asset");
            Assert.IsNull(renderer.postProcessData, "встроенный постпроцессинг рендерера выключен");
            Assert.IsTrue(renderer.rendererFeatures.Exists(f => f != null && f.GetType().Name == "OnTilePostProcessFeature"));

            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Rubezh/Settings/URP_Asset.asset");
            Assert.IsFalse(asset.supportsCameraDepthTexture, "лишняя копия текстуры глубины");
            Assert.IsFalse(asset.supportsCameraOpaqueTexture, "лишняя копия непрозрачного");
        }
#endif
    }
}
