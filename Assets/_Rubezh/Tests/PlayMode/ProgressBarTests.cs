using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Шкала O-43 в префабе (NG-31): 9 подписанных сегментов, горящий отличается заливкой и подписью,
    // материалы непрозрачные Unlit; грубое замораживает шкалу, «Повторить» обнуляет.
    public class ProgressBarTests
    {
        private GameObject _root, _barGo;
        private ExerciseRunner _runner;
        private ProgressBar _bar;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _runner = _root.AddComponent<ExerciseRunner>();
#if UNITY_EDITOR
            _barGo = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Prefabs/Interactive/ProgressBar.prefab"));
#endif
            _bar = _barGo.GetComponent<ProgressBar>();
            _bar.Configure(_runner, null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_barGo);
            Object.Destroy(_root);
            yield return null;
        }

        private Renderer Segment(int i) => _barGo.transform.Find("Segment_" + (i + 1)).GetComponent<Renderer>();
        private TMPro.TMP_Text Label(int i) => _barGo.transform.Find("Label_" + (i + 1)).GetComponent<TMPro.TMP_Text>();

        [Test]
        public void NineLabeledSegments_SizeFromLayout_OpaqueUnlit()
        {
            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(ProgressSteps.Names[i], Label(i).text);
                var m = Segment(i).sharedMaterial;
                Assert.AreEqual("Universal Render Pipeline/Unlit", m.shader.name);
                Assert.AreEqual(0f, m.GetFloat("_Surface"), "без прозрачности");
            }
            float left = Segment(0).bounds.min.x, right = Segment(8).bounds.max.x;
            Assert.AreEqual(1.1f, right - left, 0.002f, "ширина 1,1 м (layout.md §3)");
            Assert.AreEqual(0.08f, Segment(0).bounds.size.y, 0.011f);
        }

        [Test]
        public void LitSegment_DiffersByFillAndLabel()
        {
            _bar.Steps.OnPhaseChanged(Phase.Receive, Phase.Inspect);
            Assert.IsTrue(_bar.IsLit(0));
            Assert.AreNotSame(Segment(0).sharedMaterial, Segment(1).sharedMaterial);
            float lit = Segment(0).sharedMaterial.GetColor("_BaseColor").grayscale;
            float off = Segment(1).sharedMaterial.GetColor("_BaseColor").grayscale;
            Assert.Greater(lit - off, 0.5f, "горящий — светлая заливка");
            Assert.AreEqual(TMPro.FontStyles.Bold, Label(0).fontStyle);
            Assert.AreEqual(TMPro.FontStyles.Normal, Label(1).fontStyle);
            Assert.Less(Label(0).color.grayscale, Label(1).color.grayscale, "подпись горящего — тёмная на светлом");
        }

        [UnityTest]
        public IEnumerator Gross_Freezes_Repeat_Resets()
        {
            _bar.Steps.OnPhaseChanged(Phase.Receive, Phase.Inspect);
            _runner.Raise(Violation.LookIntoMuzzle, Severity.Critical);
            yield return null;
            Assert.IsTrue(_bar.Steps.Frozen, "грубое — шкала замерла");
            Assert.IsTrue(_bar.IsLit(0));
            _runner.Restart();
            yield return null;
            Assert.IsFalse(_bar.IsLit(0), "«Повторить» обнуляет");
            Assert.AreSame(Segment(1).sharedMaterial, Segment(0).sharedMaterial);
        }
    }
}
