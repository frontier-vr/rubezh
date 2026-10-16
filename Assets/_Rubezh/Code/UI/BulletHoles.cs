using System.Collections.Generic;
using Rubezh.Gameplay;
using UnityEngine;

namespace Rubezh.UI
{
    // Пробоины на мишени: непрозрачные меши из пула по событию Hit от ShotResolver. Без Instantiate
    // в бою и без декалей с прозрачностью. Пробоина двухцветная: тёмный центр видно на бумаге,
    // светлый ободок — на чёрном яблоке. Пробоина становится дочерней плоскости мишени и едет с кареткой.
    public sealed class BulletHoles : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private ShotResolver shots;
        [SerializeField] private Material centerMaterial;
        [SerializeField] private Material rimMaterial;

        private readonly List<Transform> _pool = new List<Transform>();
        private Mesh _mesh;
        private int _next;

        public int Visible { get; private set; }
        public IReadOnlyList<Transform> Pool => _pool;

        public void Configure(ExerciseRunner exerciseRunner, ShotResolver resolver, Material center, Material rim)
        {
            Unsubscribe();
            runner = exerciseRunner; shots = resolver; centerMaterial = center; rimMaterial = rim;
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (shots != null) { shots.Hit -= Place; shots.Hit += Place; }
            if (runner != null) { runner.Restarted -= Clear; runner.Restarted += Clear; }
        }

        private void Unsubscribe()
        {
            if (shots != null) shots.Hit -= Place;
            if (runner != null) runner.Restarted -= Clear;
        }

        private void EnsurePool()
        {
            int size = runner != null && runner.Rules != null ? runner.Rules.bulletHolePoolSize : 10;
            float diameter = runner != null && runner.Rules != null ? runner.Rules.bulletHoleDiameter : 0.03f;
            if (_mesh == null) _mesh = BuildMesh(diameter * 0.5f, diameter * 0.5f * 0.55f);
            while (_pool.Count < size)
            {
                var go = new GameObject("BulletHole");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { centerMaterial, rimMaterial };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _pool.Add(go.transform);
            }
        }

        // Точка попадания в локальных координатах плоскости мишени (от ShotResolver). Лицевая сторона — −Z.
        public void Place(Vector3 localPoint, Transform plane)
        {
            EnsurePool();
            var hole = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            if (!hole.gameObject.activeSelf) Visible++;
            // Точка попадания лежит на поверхности: у Quad это z = 0, у модели мишени — её лицевая грань.
            Vector3 world = plane.TransformPoint(localPoint) - plane.forward * 0.0015f;   // чуть перед бумагой
            hole.SetParent(transform, false);                 // корень пула без масштаба
            hole.SetPositionAndRotation(world, plane.rotation);
            hole.localScale = Vector3.one;
            hole.SetParent(plane, true);                      // едет вместе с кареткой
            hole.gameObject.SetActive(true);
        }

        public void Clear()
        {
            foreach (var hole in _pool)
            {
                hole.gameObject.SetActive(false);
                hole.SetParent(transform, false);
            }
            Visible = 0;
            _next = 0;
        }

        // Восьмиугольник центра и кольцо ободка, плоскость XY, лицом к −Z — как у Quad
        // (обход по часовой, если смотреть со стороны стрелка).
        private static Mesh BuildMesh(float outer, float inner)
        {
            const int n = 8;
            var v = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < n; i++) v.Add(Ring(inner, i, n));
            for (int i = 0; i < n; i++) v.Add(Ring(outer, i, n));
            var center = new List<int>();
            var rim = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n, oa = 1 + n + i, ob = 1 + n + (i + 1) % n;
                center.AddRange(new[] { 0, b, a });
                rim.AddRange(new[] { a, b, ob, a, ob, oa });
            }
            var mesh = new Mesh { name = "BulletHole" };
            mesh.SetVertices(v);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(center, 0);
            mesh.SetTriangles(rim, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Ring(float r, int i, int n)
        {
            float a = Mathf.PI * 2f * i / n;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
    }
}
