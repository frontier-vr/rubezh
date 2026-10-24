using System;
using System.Collections.Generic;
using Rubezh.Core;

namespace Rubezh.Gameplay
{
    // Логика подсказок FR-26 (VP-12) по правилам Юли (docs/hints.md, hint_rules.ru.json).
    // Подсказка включается только после паузы idleSeconds без действий: таймер сбрасывают захват,
    // отпускание, событие сокета, любая команда оружия, смена фазы и телепорт (Activity). В фазе
    // берётся первая сверху строка, чьё условие выполнено. В Fire подсказки — до первого выстрела,
    // дальше только «перезарядить» после осечки. В Done и Failed подсказок нет.
    // Результат — событие (ключ объекта для подсветки VP-21, ID фразы для NG-32).
    public sealed class HintEngine
    {
        public struct Context
        {
            public Phase Phase;
            public ExerciseFacts Facts;
            public Anchor Anchor;
            public bool RoundInHand;
            public int Shots;
            public bool Jammed;
            public double JammedAt;
        }

        private readonly IReadOnlyList<HintRules.Rule> _rules;
        private readonly double _idleSeconds;
        private readonly double _jamPause;
        private double _lastActivity;
        private HintRules.Rule _current;

        public HintEngine(IReadOnlyList<HintRules.Rule> rules, double idleSeconds, double jamPauseSeconds)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _idleSeconds = idleSeconds;
            _jamPause = jamPauseSeconds;
        }

        public event Action<string, string> Shown;     // (ключ объекта, ID фразы)
        public event Action Cleared;

        public HintRules.Rule Current => _current;

        // Действие пользователя: таймер с нуля, подсказка снимается.
        public void Activity(double now)
        {
            _lastActivity = now;
            if (_current == null) return;
            _current = null;
            Cleared?.Invoke();
        }

        public void Tick(double now, in Context c)
        {
            if (c.Phase == Phase.Done || c.Phase == Phase.Failed)
            {
                if (_current != null) { _current = null; Cleared?.Invoke(); }
                return;
            }
            if (now - _lastActivity < _idleSeconds) return;
            var rule = Pick(now, c);
            if (rule == _current) return;
            _current = rule;
            if (rule == null) Cleared?.Invoke();
            else Shown?.Invoke(rule.target, rule.hint);
        }

        public HintRules.Rule Pick(double now, in Context c)
        {
            string phase = c.Phase.ToString();
            foreach (var r in _rules)
            {
                if (r.phase != phase) continue;
                // После первого выстрела в «Огонь» подсказываем только устранение осечки.
                if (c.Phase == Phase.Fire && c.Shots > 0 && r.condition != "jam_wait_elapsed") continue;
                if (Holds(r.condition, now, c)) return r;
            }
            return null;
        }

        public bool Holds(string condition, double now, in Context c)
        {
            var f = c.Facts;
            var w = f.Weapon;
            int colon = condition.IndexOf(':');
            string key = colon < 0 ? condition : condition.Substring(0, colon);
            string arg = colon < 0 ? null : condition.Substring(colon + 1);
            switch (key)
            {
                case "at": return c.Anchor.ToString() == arg;
                case "not_at": return c.Anchor.ToString() != arg;
                case "mag_in_weapon": return w.MagazineInserted;
                case "mag_not_in_weapon": return !w.MagazineInserted;
                case "mag_not_on": return f.MagazinePlace != ParsePlace(arg);
                case "pistol_not_on": return f.PistolPlace != ParsePlace(arg);
                case "pistol_or_mag_not_on": return f.PistolPlace != ParsePlace(arg) || f.MagazinePlace != ParsePlace(arg);
                case "slide_not_locked": return !w.SlideLocked;
                case "no_earmuffs": return !f.EarmuffsOn;
                case "no_glasses": return !f.GlassesOn;
                case "round_in_hand": return c.RoundInHand;
                case "no_round_in_hand": return !c.RoundInHand;
                case "target_not_supplied": return !f.TargetSupplied;
                case "chamber_empty_no_jam": return w.Chamber == Chamber.Empty && w.Jam == Jam.None && !w.SlideLocked;
                case "safety_on": return w.SafetyOn;
                case "safety_off": return !w.SafetyOn;
                case "no_shots_yet": return c.Shots == 0;
                case "jam_wait_elapsed": return c.Jammed && now - c.JammedAt >= _jamPause;
                case "not_clear": return !w.IsClear;
                default: return false;
            }
        }

        // Все коды условий, которые понимает движок (тест: каждая строка правил Юли распознана).
        public static readonly string[] KnownConditions =
        {
            "at", "not_at", "mag_in_weapon", "mag_not_in_weapon", "mag_not_on", "pistol_not_on", "pistol_or_mag_not_on",
            "slide_not_locked", "no_earmuffs", "no_glasses", "round_in_hand", "no_round_in_hand", "target_not_supplied",
            "chamber_empty_no_jam", "safety_on", "safety_off", "no_shots_yet", "jam_wait_elapsed", "not_clear"
        };

        private static Place ParsePlace(string s) => Enum.TryParse(s, out Place p) ? p : Place.Unknown;
    }
}
