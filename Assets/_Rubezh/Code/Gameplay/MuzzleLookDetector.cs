using UnityEngine;

namespace Rubezh.Gameplay
{
    // Детектор взгляда в ствол: раз в кадр один луч из камеры шлема вперёд против коллайдера
    // дульного среза (слой Muzzle). Взгляд «в ствол», если луч попал в коллайдер и камера стоит
    // перед дульным срезом — в конусе вокруг оси ствола (+Z пустышки Muzzle).
    // Дальность луча и угол конуса — в RulesConfig.
    // Сбоку на пистолет смотреть можно.
    public sealed class MuzzleLookDetector : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform muzzle;
        [Tooltip("Коллайдер-триггер на дульном срезе, слой Muzzle")]
        [SerializeField] private Collider muzzleCollider;
        [SerializeField] private LayerMask muzzleLayers;

        private Core.MuzzleLookRule _rule;

        public bool IsLooking { get; private set; }
        public Core.MuzzleLookRule Rule => _rule;

        public void Configure(ExerciseRunner exerciseRunner, Transform head, Transform muzzlePoint,
                              Collider probe, LayerMask layers)
        {
            runner = exerciseRunner;
            headCamera = head;
            muzzle = muzzlePoint;
            muzzleCollider = probe;
            muzzleLayers = layers;
            Init();
        }

        private void Start() => Init();

        private void Init()
        {
            if (_rule != null || runner == null || runner.Director == null) return;
            _rule = new Core.MuzzleLookRule(runner.Director, new UnityClock());
            runner.Restarted += _rule.Reset;
        }

        private void OnDestroy()
        {
            if (runner != null && _rule != null) runner.Restarted -= _rule.Reset;
        }

        private void Update()
        {
            if (_rule == null || headCamera == null || muzzle == null || muzzleCollider == null) return;
            IsLooking = Check();
            _rule.Tick(IsLooking);
        }

        private bool Check()
        {
            var rules = runner.Rules;
            float maxDistance = rules != null ? rules.muzzleLookMaxDistance : 0.7f;
            float coneAngle = rules != null ? rules.muzzleLookConeDegrees : 30f;
            if (!Physics.Raycast(headCamera.position, headCamera.forward, out var hit, maxDistance,
                                 muzzleLayers, QueryTriggerInteraction.Collide))
                return false;
            if (hit.collider != muzzleCollider) return false;
            Vector3 toHead = headCamera.position - muzzle.position;
            return Vector3.Angle(muzzle.forward, toHead) <= coneAngle;
        }
    }
}
