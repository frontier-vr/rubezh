using System.Collections.Generic;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rubezh.UI
{
    // Подсветка следующего объекта FR-26 (VP-21): по событию подсказки VP-12 (ключ объекта)
    // включает пульсирующую подсветку цветом #1FB6C9 (docs/palette.md), действие пользователя её
    // снимает. Материал — непрозрачный Unlit из Shader Graph (Art/Shaders/Highlight). Объёмные
    // предметы получают контур — чуть увеличенную копию меша с задними гранями; плоские метки
    // (места, якоря) — копию поверх себя. Копии — дочерние объекты, поэтому едут вместе с предметом.
    public sealed class Highlighter : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Cull = Shader.PropertyToID("_Cull");

        [SerializeField] private HintDirector hints;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private Material highlight;
        [SerializeField] private Color color = new Color(0.1215686f, 0.7137255f, 0.7882353f);   // #1FB6C9

        private readonly Dictionary<string, List<HighlightTarget>> _targets = new Dictionary<string, List<HighlightTarget>>();
        private readonly Dictionary<Renderer, GameObject> _shells = new Dictionary<Renderer, GameObject>();
        private readonly List<GameObject> _active = new List<GameObject>();
        private Material _outline, _fill;

        public string Current { get; private set; }
        public IReadOnlyList<GameObject> ActiveShells => _active;
        public Material OutlineMaterial => _outline;
        public Material FillMaterial => _fill;

        private float PulseHz => rules != null ? rules.highlightPulseHz : 1.2f;
        private float Width => rules != null ? rules.highlightOutlineMeters : 0.007f;
        private float MinBrightness => rules != null ? rules.highlightMinBrightness : 0.45f;

        public void Configure(HintDirector hintDirector, RulesConfig rulesConfig, Material material)
        {
            OnDisable();
            hints = hintDirector; rules = rulesConfig; highlight = material;
            Build();
            OnEnable();
        }

        private void Awake() => Build();

        private void Build()
        {
            if (highlight == null) return;
            if (_outline == null)
            {
                _outline = new Material(highlight) { name = "Highlight_Outline" };
                _outline.SetFloat(Cull, (float)CullMode.Front);
                _outline.renderQueue = (int)RenderQueue.Geometry + 1;
                _fill = new Material(highlight) { name = "Highlight_Fill" };
                _fill.SetFloat(Cull, (float)CullMode.Back);
                _fill.renderQueue = (int)RenderQueue.Geometry + 1;     // поверх метки той же глубины
            }
            Refresh();
        }

        // Пересобрать список целей: ключи висят на объектах сцены компонентом HighlightTarget.
        public void Refresh()
        {
            _targets.Clear();
            foreach (var t in FindObjectsByType<HighlightTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(t.Key)) continue;
                if (!_targets.TryGetValue(t.Key, out var list)) _targets[t.Key] = list = new List<HighlightTarget>();
                list.Add(t);
            }
        }

        public bool Has(string key) => key != null && _targets.ContainsKey(key);

        private void OnEnable()
        {
            if (hints == null) return;
            hints.HintShown += OnHintShown;
            hints.HintCleared += Clear;
        }

        private void OnDisable()
        {
            if (hints == null) return;
            hints.HintShown -= OnHintShown;
            hints.HintCleared -= Clear;
        }

        private void OnDestroy()
        {
            if (_outline != null) Destroy(_outline);
            if (_fill != null) Destroy(_fill);
        }

        private void OnHintShown(string key, string phrase) => Show(key);

        public void Show(string key)
        {
            Clear();
            if (key == null || !_targets.TryGetValue(key, out var list)) return;
            Current = key;
            foreach (var target in list)
            {
                if (target == null) continue;
                foreach (var r in target.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.GetComponent<HighlightShell>() != null) continue;
                    var shell = Shell(r, target.Style);
                    if (shell == null) continue;
                    shell.SetActive(true);
                    _active.Add(shell);
                }
            }
            Pulse();
        }

        public void Clear()
        {
            foreach (var s in _active)
                if (s != null) s.SetActive(false);
            _active.Clear();
            Current = null;
        }

        private void Update()
        {
            if (_active.Count > 0) Pulse();
        }

        private void Pulse()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * PulseHz * Time.time);
            var c = color * Mathf.Lerp(MinBrightness, 1f, k);
            c.a = 1f;
            if (_outline != null) _outline.SetColor(BaseColor, c);
            if (_fill != null) _fill.SetColor(BaseColor, c);
        }

        private GameObject Shell(MeshRenderer source, HighlightTarget.Mode mode)
        {
            if (_shells.TryGetValue(source, out var cached) && cached != null) return cached;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return null;
            var mesh = filter.sharedMesh;
            var go = new GameObject("Highlight", typeof(MeshFilter), typeof(MeshRenderer), typeof(HighlightShell));
            go.transform.SetParent(source.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mode == HighlightTarget.Mode.Outline ? _outline : _fill;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            if (mode == HighlightTarget.Mode.Outline)
            {
                // Оболочка шире меша на Width с каждой стороны: масштаб вокруг центра границ меша.
                var b = mesh.bounds;
                var lossy = source.transform.lossyScale;
                var s = new Vector3(Grow(b.size.x * Mathf.Abs(lossy.x)), Grow(b.size.y * Mathf.Abs(lossy.y)), Grow(b.size.z * Mathf.Abs(lossy.z)));
                go.transform.localScale = s;
                go.transform.localPosition = b.center - Vector3.Scale(s, b.center);
            }
            go.SetActive(false);
            _shells[source] = go;
            return go;
        }

        private float Grow(float size) => size > 1e-4f ? 1f + 2f * Width / size : 1f;
    }
}
