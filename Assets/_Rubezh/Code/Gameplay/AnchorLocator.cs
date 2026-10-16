using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Текущий якорь телепорта — ближайший к XR Origin по полу (layout.md §4).
    public sealed class AnchorLocator : MonoBehaviour
    {
        [SerializeField] private Transform rig;
        [SerializeField] private Transform a1;
        [SerializeField] private Transform a2;
        [SerializeField] private Transform a3;

        public void Configure(Transform rigRoot, Transform anchor1, Transform anchor2, Transform anchor3)
        {
            rig = rigRoot; a1 = anchor1; a2 = anchor2; a3 = anchor3;
        }

        public Anchor Current
        {
            get
            {
                if (rig == null) return Anchor.A1;
                var best = Anchor.A1;
                float bestDist = float.MaxValue;
                Check(a1, Anchor.A1, ref best, ref bestDist);
                Check(a2, Anchor.A2, ref best, ref bestDist);
                Check(a3, Anchor.A3, ref best, ref bestDist);
                return best;
            }
        }

        private void Check(Transform anchor, Anchor id, ref Anchor best, ref float bestDist)
        {
            if (anchor == null) return;
            var d = anchor.position - rig.position;
            float dist = d.x * d.x + d.z * d.z;
            if (dist < bestDist) { bestDist = dist; best = id; }
        }
    }
}
