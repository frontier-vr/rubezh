using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rubezh.Tests.EditMode
{
    // Атлас оружия и снаряжения (MM-13): пистолет, магазин, патрон, гильза, СИЗ и мишенный механизм —
    // на одном материале и атласе 1024 × 1024, без прозрачности. Мишенный лист — своя печать (MM-12).
    public class WeaponAtlasTests
    {
        private static readonly string[] Models =
        {
            "Weapon/Pistol", "Weapon/Magazine", "Weapon/Round", "Weapon/Casing",
            "Equipment/Earmuffs", "Equipment/Glasses", "Target/TargetMechanism"
        };

        [Test]
        public void AllModels_OnOneAtlas1024()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rubezh/Art/Atlas/MM_Atlas.mat");
            Assert.IsNotNull(atlas);
            Assert.AreEqual("Universal Render Pipeline/Simple Lit", atlas.shader.name);
            Assert.AreEqual(0f, atlas.GetFloat("_Surface"), "без прозрачности");
            var tex = atlas.GetTexture("_BaseMap");
            Assert.AreEqual(1024, tex.width);
            Assert.AreEqual(1024, tex.height);
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Rubezh/Art/Target/Target_Paper.mat");
            foreach (var m in Models)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Rubezh/Art/" + m + ".fbx");
                Assert.IsNotNull(go, m);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (r.name == "TargetPlane") { Assert.AreSame(paper, mat, "мишенный лист — своя печать"); continue; }
                        Assert.AreSame(atlas, mat, m + "/" + r.name);
                    }
            }
        }

        [Test]
        public void Prefabs_HaveNoMissingMaterials()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Rubezh/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var r in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var mat in r.sharedMaterials)
                        Assert.IsNotNull(mat, path + "/" + r.name);
            }
        }
    }
}
