using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rubezh.Tests.EditMode
{
    // Атлас окружения (YG-12): все модели окружения на одном материале и атласе 2048 × 2048,
    // UV внутри атласа, второй UV для lightmap, прозрачности нет.
    public class EnvironmentAtlasTests
    {
        private static readonly string[] Models = { "Room", "Cabinet", "PpeShelf", "PrepTable", "AmmoTray", "LineStand", "TargetRemote" };

        [Test]
        public void AllEnvironmentModels_OnOneAtlasMaterial()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rubezh/Art/Atlas/ENV_Atlas.mat");
            Assert.IsNotNull(atlas);
            Assert.AreEqual("Universal Render Pipeline/Simple Lit", atlas.shader.name);
            Assert.AreEqual(0f, atlas.GetFloat("_Surface"), "без прозрачности");
            var tex = atlas.GetTexture("_BaseMap");
            Assert.AreEqual(2048, tex.width);
            Assert.AreEqual(2048, tex.height);
            foreach (var m in Models)
            {
                var path = "Assets/_Rubezh/Art/Environment/Env_" + m + ".fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(go, path);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var mat in r.sharedMaterials)
                        Assert.AreSame(atlas, mat, m + "/" + r.name);
            }
        }

        [Test]
        public void Uv0InsideAtlas_Uv2Generated()
        {
            foreach (var m in Models)
            {
                var path = "Assets/_Rubezh/Art/Environment/Env_" + m + ".fbx";
                Assert.IsTrue(((ModelImporter)AssetImporter.GetAtPath(path)).generateSecondaryUV, path + ": UV2 для lightmap");
                foreach (var f in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = f.sharedMesh;
                    Assert.AreEqual(1, mesh.subMeshCount, m + "/" + f.name + ": одна подсетка");
                    var uv2 = new System.Collections.Generic.List<Vector2>();
                    mesh.GetUVs(1, uv2);
                    Assert.AreEqual(mesh.vertexCount, uv2.Count, m + "/" + f.name + ": есть UV2");
                    foreach (var uv in mesh.uv)
                    {
                        Assert.That(uv.x, Is.InRange(0f, 1f), m + "/" + f.name);
                        Assert.That(uv.y, Is.InRange(0f, 1f), m + "/" + f.name);
                    }
                }
            }
        }
    }
}
