using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Детектор сектора: раз в кадр один луч из пустышки Muzzle вдоль её +Z.
    // Ствол в секторе, если первым луч встречает поверхность сектора (слой Sector: лицевая
    // плоскость пулеулавливателя и пол коридора, docs/layout.md §5) или мишень, а не щиты,
    // потолок, стены и пол зала (слой Environment). Вне рубежа (не на A3, #164) безопасно и «вниз»:
    // ствол опущен ниже горизонта больше чем на RulesConfig.safeDownDegrees.
    public sealed class SectorDetector : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private Transform muzzle;
        [Tooltip("Компонент с IHeldWeapon: пистолет в руке и состояние оружия")]
        [SerializeField] private MonoBehaviour heldWeapon;
        [SerializeField] private LayerMask sectorLayers;
        [SerializeField] private LayerMask blockerLayers;
        [SerializeField, Min(1f)] private float maxDistance = 20f;
        [Tooltip("Где пользователь: на рубеже (A3) — только сектор; пусто — всегда как на рубеже")]
        [SerializeField] private AnchorLocator anchors;
        [SerializeField] private RulesConfig rules;

        private IHeldWeapon _held;
        private SectorRule _rule;

        public bool InSector { get; private set; }
        public SectorRule Rule => _rule;

        public void Configure(ExerciseRunner exerciseRunner, Transform muzzlePoint, IHeldWeapon held,
                              LayerMask sector, LayerMask blockers)
        {
            runner = exerciseRunner;
            muzzle = muzzlePoint;
            heldWeapon = held as MonoBehaviour;
            _held = held;
            sectorLayers = sector;
            blockerLayers = blockers;
            Init();
        }

        private void Start() => Init();

        private void Init()
        {
            if (_held == null) _held = heldWeapon as IHeldWeapon;
            if (_rule != null || runner == null || runner.Director == null) return;
            _rule = new SectorRule(runner.Director, new UnityClock());
            runner.Restarted += _rule.Reset;
        }

        private void OnDestroy()
        {
            if (runner != null && _rule != null) runner.Restarted -= _rule.Reset;
        }

        private void Update()
        {
            if (_rule == null || muzzle == null || _held == null) return;

            bool held = _held.IsHeld;
            bool onLine = anchors == null || anchors.Current == Anchor.A3;
            InSector = !held || Cast(out _) || (!onLine && PointsDown);
            _rule.Tick(held, InSector, _held.State.IsLoaded, onLine);
        }

        public void SetAnchors(AnchorLocator locator, RulesConfig rulesConfig)
        {
            anchors = locator;
            rules = rulesConfig;
        }

        // Ствол опущен вниз — безопасное направление вне рубежа.
        public bool PointsDown =>
            muzzle != null && muzzle.forward.y <= -Mathf.Sin((rules != null ? rules.safeDownDegrees : 30f) * Mathf.Deg2Rad);

        // Проверка сектора сейчас, вне Update: ShotResolver спрашивает в момент выстрела.
        public bool CheckNow() => muzzle != null && Cast(out _);

        private bool Cast(out RaycastHit hit)
        {
            int mask = sectorLayers | blockerLayers;
            if (!Physics.Raycast(muzzle.position, muzzle.forward, out hit, maxDistance, mask, QueryTriggerInteraction.Collide))
                return false;
            return (sectorLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (muzzle == null) return;
            Gizmos.color = InSector ? Color.green : Color.red;
            Gizmos.DrawRay(muzzle.position, muzzle.forward * maxDistance);
        }
    }
}
