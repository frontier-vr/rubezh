using System;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Подсказки в сцене (VP-12): кормит HintEngine снимком мира, якорем и действиями пользователя,
    // отдаёт событие «подсветить объект + фраза». Подсветку делает VP-21, фразу у контроллера —
    // NG-32. Захват, отпускание и сокеты сообщает слой XR (Activity), команды оружия, смену фазы
    // и телепорт (смену якоря) директор ловит сам.
    public sealed class HintDirector : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private TextAsset hintRules;
        [SerializeField] private TextAsset script;
        [SerializeField] private ExerciseFactsCollector facts;
        [SerializeField] private AnchorLocator anchors;
        [SerializeField] private WeaponController weapon;

        private HintEngine _engine;
        private InstructorScript _script;
        private Anchor _lastAnchor;
        private bool _jammed;
        private double _jammedAt;
        private int _shots;

        // Ключ объекта (docs/hints.md §3) и готовая фраза с кнопками по руке.
        public event Action<string, string> HintShown;
        public event Action HintCleared;

        public string CurrentTarget { get; private set; }
        public string CurrentPhrase { get; private set; }

        // Рука держит патрон (условие правила «патрон в магазин»); ставит слой XR.
        public bool RoundInHand { get; set; }

        private double Now => Time.timeAsDouble;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, TextAsset rulesAsset, TextAsset scriptAsset,
                              ExerciseFactsCollector collector, AnchorLocator locator, WeaponController weaponController)
        {
            Unhook();
            runner = exerciseRunner; rules = rulesConfig; hintRules = rulesAsset; script = scriptAsset;
            facts = collector; anchors = locator; weapon = weaponController;
            Build();
            Hook();
        }

        private void Awake() => Build();
        private void OnEnable() => Hook();
        private void OnDisable() => Unhook();

        private void Build()
        {
            var parsed = HintRules.From(hintRules);
            _script = InstructorScript.From(script);
            if (_engine != null)
            {
                _engine.Shown -= OnShown;
                _engine.Cleared -= OnCleared;
            }
            _engine = parsed != null
                ? new HintEngine(parsed.Rules, rules != null ? rules.hintIdleSeconds : 5f, rules != null ? rules.misfireWaitSeconds : 5f)
                : null;
            if (_engine == null) return;
            _engine.Shown += OnShown;
            _engine.Cleared += OnCleared;
            _engine.Activity(Now);
        }

        private void Hook()
        {
            Unhook();
            if (runner != null)
            {
                runner.PhaseChanged += OnPhaseChanged;
                runner.Restarted += OnRestarted;
            }
            if (weapon != null)
            {
                weapon.Applied += OnWeaponCommand;
                weapon.MagazineInserted += OnMagazineInserted;
                weapon.Jammed += OnJammed;
                weapon.Fired += OnFired;
            }
        }

        private void Unhook()
        {
            if (runner != null)
            {
                runner.PhaseChanged -= OnPhaseChanged;
                runner.Restarted -= OnRestarted;
            }
            if (weapon != null)
            {
                weapon.Applied -= OnWeaponCommand;
                weapon.MagazineInserted -= OnMagazineInserted;
                weapon.Jammed -= OnJammed;
                weapon.Fired -= OnFired;
            }
        }

        public void Activity() => _engine?.Activity(Now);

        private void OnPhaseChanged(Phase from, Phase to) => Activity();
        private void OnWeaponCommand(WeaponCommand command)
        {
            // Перезарядка затвором после осечки снимает задержку и её подсказку.
            if (command == WeaponCommand.SlideBack) _jammed = false;
            Activity();
        }
        private void OnMagazineInserted(int rounds) => Activity();
        private void OnFired() => _shots++;

        private void OnJammed(Jam jam)
        {
            _jammed = true;
            _jammedAt = Now;
        }

        private void OnRestarted()
        {
            _shots = 0;
            _jammed = false;
            Activity();
        }

        private void Update()
        {
            if (_engine == null || runner == null || runner.Director == null) return;
            var anchor = anchors != null ? anchors.Current : Anchor.None;
            if (anchor != _lastAnchor)
            {
                _lastAnchor = anchor;
                Activity();                     // телепорт
            }
            var context = new HintEngine.Context
            {
                Phase = runner.Phase,
                Facts = facts != null ? facts.Collect() : default,
                Anchor = anchor,
                RoundInHand = RoundInHand,
                Shots = _shots,
                Jammed = _jammed,
                JammedAt = _jammedAt
            };
            _engine.Tick(Now, context);
        }

        private void OnShown(string target, string hintId)
        {
            var hand = weapon != null ? weapon.PrimaryHand : Hand.Right;
            CurrentTarget = target;
            CurrentPhrase = _script != null ? _script.Hint(hintId, hand) : hintId;
            HintShown?.Invoke(CurrentTarget, CurrentPhrase);
        }

        private void OnCleared()
        {
            CurrentTarget = null;
            CurrentPhrase = null;
            HintCleared?.Invoke();
        }
    }
}
