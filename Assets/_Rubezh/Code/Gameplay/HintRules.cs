using System;
using System.Collections.Generic;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Правила подсказок и названия нарушений из Data/Hints/hint_rules.ru.json (генерируется из
    // docs/hints.md, YG-08). Названия нарушений — короткие, нейтрально-технические: экран
    // результата (VP-20), замечания у контроллера (NG-32).
    public sealed class HintRules
    {
        [Serializable] public sealed class Rule { public string step; public string phase; public string condition; public string target; public string hint; }
        [Serializable] public sealed class Onboarding { public string id; public string when; public string text; }
        [Serializable] private sealed class NameLine { public string violation; public string name; }
        [Serializable] private sealed class Data
        {
            public string highlightColor;
            public Rule[] rules;
            public Onboarding[] onboarding;
            public NameLine[] violationNames;
        }

        private readonly Dictionary<Violation, string> _names = new Dictionary<Violation, string>();

        public HintRules(string json)
        {
            var data = JsonUtility.FromJson<Data>(json) ?? throw new ArgumentException("пустые правила подсказок", nameof(json));
            Rules = data.rules ?? new Rule[0];
            OnboardingLines = data.onboarding ?? new Onboarding[0];
            HighlightColor = data.highlightColor;
            if (data.violationNames != null)
                foreach (var n in data.violationNames)
                    if (Enum.TryParse(n.violation, out Violation v)) _names[v] = n.name;
        }

        public static HintRules From(TextAsset asset) => asset != null ? new HintRules(asset.text) : null;

        public IReadOnlyList<Rule> Rules { get; }
        public IReadOnlyList<Onboarding> OnboardingLines { get; }
        public string HighlightColor { get; }

        public bool HasName(Violation v) => _names.ContainsKey(v);
        public string ViolationName(Violation v) => _names.TryGetValue(v, out var n) ? n : v.ToString();
    }
}
