using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Снимок мира для директора (user-flow.md §4.1) раз в кадр, после ввода и интеракций:
    // пистолет в руке и его состояние, магазин и его место, СИЗ (NG-10), мишень (NG-12), гильзы (VP-19).
    // Источник, которого ещё нет в сцене, даёт «нет».
    [DefaultExecutionOrder(100)]
    public sealed class ExerciseFactsCollector : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private WeaponController weapon;
        [Tooltip("IPlaced пистолета")]
        [SerializeField] private MonoBehaviour pistolPlace;
        [Tooltip("IMagazine: патронов в магазине")]
        [SerializeField] private MonoBehaviour magazine;
        [Tooltip("IPlaced магазина")]
        [SerializeField] private MonoBehaviour magazinePlace;
        [Tooltip("IWearables (NG-10)")]
        [SerializeField] private MonoBehaviour wearables;
        [Tooltip("ITargetSupply (NG-12)")]
        [SerializeField] private MonoBehaviour target;
        [Tooltip("ICasingCounter (VP-19)")]
        [SerializeField] private MonoBehaviour casings;

        public ExerciseFacts Last { get; private set; }

        public void Configure(ExerciseRunner exerciseRunner, WeaponController weaponController, IPlaced pistol,
                              IMagazine magazineItem, IPlaced magazineAt, IWearables wear = null, ITargetSupply targetSupply = null)
        {
            runner = exerciseRunner;
            weapon = weaponController;
            pistolPlace = pistol as MonoBehaviour;
            magazine = magazineItem as MonoBehaviour;
            magazinePlace = magazineAt as MonoBehaviour;
            wearables = wear as MonoBehaviour;
            target = targetSupply as MonoBehaviour;
        }

        public void SetCasings(ICasingCounter counter) => casings = counter as MonoBehaviour;

        private void LateUpdate()
        {
            if (runner == null || runner.Director == null || weapon == null) return;
            Last = Collect();
            runner.UpdateFacts(Last);
        }

        public ExerciseFacts Collect()
        {
            var mag = magazine as IMagazine;
            var wear = wearables as IWearables;
            return new ExerciseFacts(
                weapon.IsHeld,
                weapon.State,
                mag != null ? mag.Rounds : 0,
                pistolPlace is IPlaced p ? p.Place : Place.Unknown,
                magazinePlace is IPlaced m ? m.Place : Place.Unknown,
                wear != null && wear.EarmuffsOn,
                wear != null && wear.GlassesOn,
                target is ITargetSupply t && t.Supplied,
                casings is ICasingCounter c ? c.CasingsOutsideCatcher : 0);
        }
    }
}
