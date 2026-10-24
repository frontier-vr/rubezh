using System;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Обёртка директора упражнения для сцены: одна на сцену, остальные компоненты получают её
    // сериализованной ссылкой. Отдаёт события директора и Restart().
    [DefaultExecutionOrder(-100)]
    public sealed class ExerciseRunner : MonoBehaviour
    {
        [SerializeField] private RulesConfig rules;
        [Tooltip("Зерно генератора задержек; 0 — новое случайное зерно при каждом запуске")]
        [SerializeField] private int seed;

        public ExerciseDirector Director { get; private set; }
        public RulesConfig Rules => rules;
        public Phase Phase => Director.Phase;
        public IRandom Random { get; private set; }

        public event Action<Phase, Phase> PhaseChanged;
        public event Action<ViolationRecord> ViolationRaised;
        public event Action<ExerciseResult> Finished;
        public event Action Restarted;

        private void Awake()
        {
            Director = new ExerciseDirector(new UnityClock(), rules != null ? rules.ToRules() : new ExerciseRules());
            Random = new SeededRandom(seed != 0 ? seed : System.Environment.TickCount);
            Director.PhaseChanged += (from, to) => PhaseChanged?.Invoke(from, to);
            Director.ViolationRaised += record => ViolationRaised?.Invoke(record);
            Director.Finished += result => Finished?.Invoke(result);
        }

        public void UpdateFacts(in ExerciseFacts facts) => Director.UpdateFacts(facts);

        public void ReportAction(ExerciseAction action, Anchor anchor = Anchor.None) => Director.ReportAction(action, anchor);

        public void Raise(Violation type, Severity severity, double duration = 0) => Director.Raise(type, severity, duration);

        public void RegisterShot(bool hit) => Director.RegisterShot(hit);

        // «Повторить»: директор с начала; предметы и телепорт возвращают подписчики Restarted.
        public void Restart()
        {
            Director.Restart();
            Restarted?.Invoke();
        }
    }
}
