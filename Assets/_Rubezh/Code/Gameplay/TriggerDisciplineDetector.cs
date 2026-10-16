using UnityEngine;

namespace Rubezh.Gameplay
{
    // Детектор пальца на спуске: читает ITriggerInput руки, которая держит Grip_Primary, и сверяет
    // с фазой директора. Работает параллельно сектору на всех шагам.
    public sealed class TriggerDisciplineDetector : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [Tooltip("Компонент с IHeldWeapon: пистолет в руке и какая рука на Grip_Primary")]
        [SerializeField] private MonoBehaviour heldWeapon;
        [Tooltip("ITriggerInput левой руки")]
        [SerializeField] private MonoBehaviour leftTrigger;
        [Tooltip("ITriggerInput правой руки")]
        [SerializeField] private MonoBehaviour rightTrigger;

        private IHeldWeapon _held;
        private ITriggerInput _left, _right;
        private Core.TriggerDisciplineRule _rule;

        public Core.TriggerDisciplineRule Rule => _rule;

        public void Configure(ExerciseRunner exerciseRunner, IHeldWeapon held, ITriggerInput left, ITriggerInput right)
        {
            runner = exerciseRunner;
            _held = held; _left = left; _right = right;
            Init();
        }

        private void Start() => Init();

        private void Init()
        {
            _held ??= heldWeapon as IHeldWeapon;
            _left ??= leftTrigger as ITriggerInput;
            _right ??= rightTrigger as ITriggerInput;
            if (_rule != null || runner == null || runner.Director == null) return;
            _rule = new Core.TriggerDisciplineRule(runner.Director, new UnityClock());
            runner.Restarted += _rule.Reset;
        }

        private void OnDestroy()
        {
            if (runner != null && _rule != null) runner.Restarted -= _rule.Reset;
        }

        private void Update()
        {
            if (_rule == null || _held == null) return;
            var input = _held.PrimaryHand == Hand.Left ? _left : _right;
            _rule.Tick(_held.IsHeld, input != null && input.IsTouched);
        }
    }
}
