using System;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Мишенный механизм O-26 (NG-12, FR-15, FR-16): регулятор выбирает дистанцию 3, 5, 7 или 9 м от
    // линии огня, кнопка пульта подаёт мишень — каретка едет вдоль направляющей, рамка разворачивается
    // к стрелку. «Мишень подана» (ITargetSupply) — каретка на месте и рамка развёрнута. В начале и после
    // Restart каретка у ближнего края, рамка ребром (layout.md §6).
    public sealed class TargetCarriage : MonoBehaviour, ITargetSupply
    {
        public enum State { AtStart, Moving, Supplied }

        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private Transform carriage;
        [SerializeField] private Transform frame;
        [Tooltip("Z линии огня O-29, от неё считаются дистанции")]
        [SerializeField] private float fireLineZ = 4.2f;
        [SerializeField] private float startZ = 4.8f;
        [SerializeField] private float[] distances = { 3f, 5f, 7f, 9f };
        [SerializeField] private int defaultIndex = 2;

        private const float FacingYaw = 180f;   // лицом к стрелку (модель MM-10 в файле смотрит в +Z)
        private const float EdgeYaw = 90f;      // ребром к стрелку

        private int _index;
        private float _targetZ;
        private float _turn;                    // 0 — ребром, 1 — лицом к стрелку

        public State Current { get; private set; } = State.AtStart;
        public bool Supplied => Current == State.Supplied;
        public float Distance => distances[_index];
        public int DistanceIndex => _index;
        public float TargetZ => fireLineZ + Distance;

        public event Action<float> DistanceChanged;
        public event Action Supplying;
        public event Action Arrived;

        private float Speed => rules != null ? rules.targetCarriageSpeed : 1.5f;
        private float TurnSeconds => rules != null ? rules.targetTurnSeconds : 0.6f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, Transform carriageTransform, Transform frameTransform)
        {
            if (runner != null) runner.Restarted -= ResetToStart;
            runner = exerciseRunner; rules = rulesConfig; carriage = carriageTransform; frame = frameTransform;
            if (runner != null) runner.Restarted += ResetToStart;
            ResetToStart();
        }

        private void Awake()
        {
            _index = Mathf.Clamp(defaultIndex, 0, distances.Length - 1);
        }

        private void OnEnable()
        {
            if (runner != null) runner.Restarted += ResetToStart;
        }

        private void OnDisable()
        {
            if (runner != null) runner.Restarted -= ResetToStart;
        }

        private void Start() => ResetToStart();

        // Регулятор: следующая дистанция по кругу 3 → 5 → 7 → 9 → 3.
        public void NextDistance() => SelectDistance((_index + 1) % distances.Length);

        public void SelectDistance(int index)
        {
            _index = Mathf.Clamp(index, 0, distances.Length - 1);
            DistanceChanged?.Invoke(Distance);
        }

        // Кнопка пульта: подать мишень на выбранную дистанцию.
        public void Supply()
        {
            if (runner != null && runner.Director != null) runner.ReportAction(ExerciseAction.PressTargetButton);
            _targetZ = TargetZ;
            Current = State.Moving;
            Supplying?.Invoke();
        }

        public void ResetToStart()
        {
            Current = State.AtStart;
            _index = Mathf.Clamp(defaultIndex, 0, distances.Length - 1);
            _turn = 0f;
            if (carriage != null)
            {
                var p = carriage.position;
                carriage.position = new Vector3(p.x, p.y, startZ);
            }
            ApplyTurn();
        }

        private void Update()
        {
            if (Current != State.Moving || carriage == null) return;
            var p = carriage.position;
            float z = Mathf.MoveTowards(p.z, _targetZ, Speed * Time.deltaTime);
            carriage.position = new Vector3(p.x, p.y, z);
            bool there = Mathf.Approximately(z, _targetZ);
            // Рамка разворачивается, когда каретка подъезжает (последние полметра) или стоит на месте.
            if (there || Mathf.Abs(_targetZ - z) < 0.5f)
                _turn = Mathf.MoveTowards(_turn, 1f, Time.deltaTime / Mathf.Max(0.01f, TurnSeconds));
            ApplyTurn();
            if (there && _turn >= 1f)
            {
                Current = State.Supplied;
                Arrived?.Invoke();
            }
        }

        private void ApplyTurn()
        {
            if (frame != null) frame.localRotation = Quaternion.Euler(0f, Mathf.Lerp(EdgeYaw, FacingYaw, _turn), 0f);
        }
    }
}
