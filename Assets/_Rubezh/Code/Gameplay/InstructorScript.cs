using System;
using System.Collections.Generic;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Тексты инструктора из Data/Hints/script.ru.json (генерируется из docs/script.md, YG-07):
    // команды по фазам, подсказки по ID, причины грубых нарушений для стоп-экрана. В коде строк нет.
    public sealed class InstructorScript
    {
        [Serializable] private class CommandLine { public string phase; public string text; public string voice; }
        [Serializable] private class HintLine { public string id; public string text; }
        [Serializable] private class FailReason { public string violation; public string title; public string text; }
        [Serializable] private class Data { public CommandLine[] commands; public HintLine[] hints; public FailReason[] failReasons; }

        private readonly Dictionary<Phase, (string text, string voice)> _commands = new Dictionary<Phase, (string, string)>();
        private readonly Dictionary<string, string> _hints = new Dictionary<string, string>();
        private readonly Dictionary<Violation, (string title, string text)> _fails = new Dictionary<Violation, (string, string)>();

        public InstructorScript(string json)
        {
            var data = JsonUtility.FromJson<Data>(json) ?? throw new ArgumentException("пустой сценарий", nameof(json));
            foreach (var c in data.commands)
                if (Enum.TryParse(c.phase, out Phase phase)) _commands[phase] = (c.text, c.voice);
            foreach (var h in data.hints) _hints[h.id] = h.text;
            foreach (var f in data.failReasons)
                if (Enum.TryParse(f.violation, out Violation v)) _fails[v] = (f.title, f.text);
        }

        public static InstructorScript From(TextAsset asset) => asset != null ? new InstructorScript(asset.text) : null;

        public bool HasCommand(Phase phase) => _commands.ContainsKey(phase);
        public string Command(Phase phase) => _commands.TryGetValue(phase, out var c) ? c.text : string.Empty;
        public string VoiceId(Phase phase) => _commands.TryGetValue(phase, out var c) ? c.voice : string.Empty;
        public bool HasHint(string id) => _hints.ContainsKey(id);

        // Фраза подсказки с кнопками по руке: {Eject} — A/X, {Safety} — B/Y.
        public string Hint(string id, Hand hand)
        {
            if (!_hints.TryGetValue(id, out var text)) return string.Empty;
            return text.Replace("{Eject}", hand == Hand.Left ? "X" : "A")
                       .Replace("{Safety}", hand == Hand.Left ? "Y" : "B");
        }

        public bool HasFailReason(Violation v) => _fails.ContainsKey(v);
        public string FailTitle(Violation v) => _fails.TryGetValue(v, out var f) ? f.title : v.ToString();
        public string FailText(Violation v) => _fails.TryGetValue(v, out var f) ? f.text : string.Empty;
    }
}
