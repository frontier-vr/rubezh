using System.Collections;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rubezh.Tests.PlayMode
{
    // Подсветка следующего объекта (VP-21): по событию подсказки VP-12 подсвечивается объект с нужным
    // ключом, пульсирует одним цветом #1FB6C9, материал непрозрачный Unlit; действие снимает подсветку.
    public class HighlighterTests
    {
        private GameObject _root, _box, _mark, _anchor;
        private RulesConfig _rules;
        private Highlighter _highlighter;
        private Material _material;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("Root");
            _rules = ScriptableObject.CreateInstance<RulesConfig>();
            _rules.hintIdleSeconds = 0.1f;
            _box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _box.name = "Pistol";
            _box.transform.SetParent(_root.transform, false);
            _box.transform.localScale = new Vector3(0.2f, 0.12f, 0.03f);
            _box.AddComponent<HighlightTarget>().Configure("Pistol", HighlightTarget.Mode.Outline);
            _mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _mark.transform.SetParent(_root.transform, false);
            _mark.AddComponent<HighlightTarget>().Configure("InspectShelf", HighlightTarget.Mode.Fill);
            _anchor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _anchor.transform.SetParent(_root.transform, false);
            _anchor.AddComponent<HighlightTarget>().Configure("Anchor_A1", HighlightTarget.Mode.Fill);
#if UNITY_EDITOR
            _material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rubezh/Art/Shaders/Highlight.mat");
#endif
            _highlighter = _root.AddComponent<Highlighter>();
            _highlighter.Configure(null, _rules, _material);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_rules);
            yield return null;
        }

        [Test]
        public void Material_IsShaderGraphUnlit_Opaque()
        {
            Assert.AreEqual("Rubezh/Highlight", _material.shader.name);
#if UNITY_EDITOR
            StringAssert.EndsWith(".shadergraph", UnityEditor.AssetDatabase.GetAssetPath(_material.shader));
#endif
            Assert.AreEqual(0f, _material.GetFloat("_Surface"), "без прозрачности");
            Assert.AreEqual("#1FB6C9", "#" + ColorUtility.ToHtmlStringRGB(_material.GetColor("_BaseColor")));
        }

        [Test]
        public void Color_DiffersFromMarkingStopContoursAndFinish()
        {
            var c = _material.GetColor("_BaseColor");
            foreach (var other in new[] { "#F2C200", "#D23A2A", "#ECEAE4", "#3E9E4F" })
            {
                ColorUtility.TryParseHtmlString(other, out var o);
                Assert.Greater(Mathf.Abs(c.r - o.r) + Mathf.Abs(c.g - o.g) + Mathf.Abs(c.b - o.b), 0.5f, other);
            }
        }

        [Test]
        public void Show_OutlinesObject_ClearRemoves()
        {
            _highlighter.Show("Pistol");
            Assert.AreEqual("Pistol", _highlighter.Current);
            Assert.AreEqual(1, _highlighter.ActiveShells.Count);
            var shell = _highlighter.ActiveShells[0];
            Assert.IsTrue(shell.activeInHierarchy);
            Assert.AreEqual(_box.transform, shell.transform.parent, "контур едет вместе с предметом");
            var src = _box.GetComponent<Renderer>().bounds;
            var hl = shell.GetComponent<Renderer>().bounds;
            Assert.Greater(hl.size.z, src.size.z + 0.01f, "контур шире предмета на ~7 мм с каждой стороны");
            Assert.AreEqual(src.center.x, hl.center.x, 1e-4f);
            Assert.AreSame(_highlighter.OutlineMaterial, shell.GetComponent<Renderer>().sharedMaterial);
            Assert.AreEqual((float)UnityEngine.Rendering.CullMode.Front, _highlighter.OutlineMaterial.GetFloat("_Cull"));
            _highlighter.Clear();
            Assert.IsFalse(shell.activeSelf);
            Assert.IsNull(_highlighter.Current);
        }

        [Test]
        public void FlatMark_IsFilledOnTop()
        {
            _highlighter.Show("InspectShelf");
            var shell = _highlighter.ActiveShells[0];
            Assert.AreEqual(Vector3.one, shell.transform.localScale);
            Assert.AreSame(_highlighter.FillMaterial, shell.GetComponent<Renderer>().sharedMaterial);
            Assert.Greater(_highlighter.FillMaterial.renderQueue, 2000, "поверх метки той же глубины");
        }

        [UnityTest]
        public IEnumerator Highlight_Pulses()
        {
            _highlighter.Show("Pistol");
            float a = _highlighter.OutlineMaterial.GetColor("_BaseColor").g;
            float min = a, max = a;
            for (float t = 0; t < 1f; t += Time.deltaTime)
            {
                yield return null;
                float g = _highlighter.OutlineMaterial.GetColor("_BaseColor").g;
                min = Mathf.Min(min, g); max = Mathf.Max(max, g);
            }
            Assert.Greater(max - min, 0.15f, "подсветка пульсирует");
        }

        // Вся цепочка: пауза бездействия VP-12 → событие подсказки → подсветка; действие снимает её.
        [UnityTest]
        public IEnumerator HintEvent_HighlightsTarget_ActivityClears()
        {
            var runner = _root.AddComponent<ExerciseRunner>();
            var rig = new GameObject("Rig").transform; rig.SetParent(_root.transform, false); rig.position = new Vector3(0, 0, 3.25f);
            var a1 = new GameObject("A1").transform; a1.SetParent(_root.transform, false); a1.position = new Vector3(-2.4f, 0, 1.7f);
            var a3 = new GameObject("A3").transform; a3.SetParent(_root.transform, false); a3.position = new Vector3(0, 0, 3.25f);
            var anchors = _root.AddComponent<AnchorLocator>();
            anchors.Configure(rig, a1, null, a3);
            var hints = _root.AddComponent<HintDirector>();
#if UNITY_EDITOR
            hints.Configure(runner, _rules,
                UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Rubezh/Data/Hints/hint_rules.ru.json"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Rubezh/Data/Hints/script.ru.json"),
                null, anchors, null);
#endif
            _highlighter.Configure(hints, _rules, _material);
            Assert.AreEqual(Phase.Receive, runner.Phase);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual("Anchor_A1", _highlighter.Current, "пользователь не у шкафа — подсвечена метка A1");
            Assert.IsTrue(_highlighter.ActiveShells[0].activeInHierarchy);
            hints.Activity();
            yield return null;
            Assert.IsNull(_highlighter.Current, "действие снимает подсветку");
        }
    }
}
