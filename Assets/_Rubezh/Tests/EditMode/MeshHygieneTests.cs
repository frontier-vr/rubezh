using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rubezh.Tests.EditMode
{
    // Чистота сеток в FBX (MM-14): у замкнутых кусков нормали наружу (объём куска положительный),
    // вырожденных треугольников нет. Ловит вывернутый казённик (#169) и остатки булевых вырезов (#170).
    public class MeshHygieneTests
    {
        private static readonly string[] Folders =
        {
            "Assets/_Rubezh/Art/Weapon", "Assets/_Rubezh/Art/Equipment", "Assets/_Rubezh/Art/Target",
            "Assets/_Rubezh/Art/Environment", "Assets/_Rubezh/Art/Replay"
        };

        private static IEnumerable<Mesh> Meshes()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", Folders))
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (o is Mesh m) yield return m;
        }

        // Unity делит вершины по нормалям и UV: сварить по положению, чтобы найти связные куски.
        private static int[] Weld(Vector3[] v)
        {
            var map = new Dictionary<Vector3Int, int>();
            var id = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var key = Vector3Int.RoundToInt(v[i] * 100000f);   // 0,01 мм
                if (!map.TryGetValue(key, out int k)) { k = map.Count; map[key] = k; }
                id[i] = k;
            }
            return id;
        }

        [Test]
        public void ClosedPieces_FaceOutward()
        {
            var bad = new List<string>();
            int checkedPieces = 0;
            foreach (var mesh in Meshes())
            {
                var v = mesh.vertices;
                var t = mesh.triangles;
                var w = Weld(v);
                int n = t.Length / 3;
                // Куски: треугольники, связные через общие сваренные вершины.
                var parent = new int[n];
                for (int i = 0; i < n; i++) parent[i] = i;
                int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
                var owner = new Dictionary<int, int>();
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < 3; k++)
                    {
                        int vid = w[t[i * 3 + k]];
                        if (owner.TryGetValue(vid, out int other)) parent[Find(i)] = Find(other);
                        else owner[vid] = i;
                    }
                // Замкнутость куска: каждое ребро встречается дважды в разных направлениях.
                var edges = new Dictionary<long, int>();
                var volume = new Dictionary<int, double>();
                for (int i = 0; i < n; i++)
                {
                    int a = w[t[i * 3]], b = w[t[i * 3 + 1]], c = w[t[i * 3 + 2]];
                    foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                    {
                        long key = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p;
                        edges[key] = edges.TryGetValue(key, out int c0) ? c0 + 1 : 1;
                    }
                    Vector3 A = v[t[i * 3]], B = v[t[i * 3 + 1]], C = v[t[i * 3 + 2]];
                    int r = Find(i);
                    volume[r] = (volume.TryGetValue(r, out double s) ? s : 0) + Vector3.Dot(A, Vector3.Cross(B, C)) / 6.0;
                }
                var open = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    int a = w[t[i * 3]], b = w[t[i * 3 + 1]], c = w[t[i * 3 + 2]];
                    foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                    {
                        long key = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p;
                        if (edges[key] != 2) open.Add(Find(i));
                    }
                }
                foreach (var kv in volume)
                {
                    if (open.Contains(kv.Key)) continue;          // плоскости и открытые куски не судим
                    checkedPieces++;
                    // Ориентированный объём куска с нормалями наружу положительный (так у всех
                    // заведомо целых моделей), у вывернутого — отрицательный.
                    if (kv.Value < -1e-12) bad.Add($"{mesh.name}: кусок вывернут, объём {kv.Value:E2} м³");
                }
            }
            Assert.Greater(checkedPieces, 50, "проверены замкнутые куски всех моделей");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void NoDegenerateTriangles()
        {
            var bad = new List<string>();
            foreach (var mesh in Meshes())
            {
                var v = mesh.vertices;
                var t = mesh.triangles;
                int degenerate = 0;
                for (int i = 0; i < t.Length; i += 3)
                    if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f < 1e-10f) degenerate++;
                if (degenerate > 0) bad.Add($"{mesh.name}: {degenerate} вырожденных треугольников");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
