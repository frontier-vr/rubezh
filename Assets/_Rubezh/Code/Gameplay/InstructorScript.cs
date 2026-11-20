using System;
using System.Collections.Generic;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Место, над которым висит табло с порядком действий (docs/script.md §6, layout.md §4.1).
    // Any — текст для всех табло сразу (конец упражнения).
    public enum Station { Any, Cabinet, PpeShelf, PrepTable, LineStand }

    // Тексты инструктора из Data/Hints/script.ru.json (генерируется из docs/script.md, YG-07):
    // команды по фазам, подсказки по ID, причины грубых нарушений, табло у столов и стоек.
    // В коде строк нет.
    public sealed class InstructorScript
    {
        [Serializable] private class CommandLine { public string phase; public string text; public string voice; }
        [Serializable] private class HintLine { public string id; public string text; }
        [Serializable] private class FailReason { public string violation; public string title; public string text; }
        [Serializable] private class StationName { public string station; public string where; }
        [Serializable] private class StationLine { public string phase; public string station; public string title; public string[] steps; }
        [Serializable]
        private class Data
        {
            public CommandLine[] commands; public HintLine[] hints; public FailReason[] failReasons;
            public StationName[] stationNames; public StationLine[] stations;
            public string boardNow; public string boardElsewhere;
        }

        // Табло одной фазы: у какого места идёт шаг, заголовок и пункты «что делать и как».
        public sealed class StationCard
        {
            public Station Station;
            public string Title;
            public IReadOnlyList<string> Steps;
        }

        private readonly Dictionary<Phase, (string text, string voice)> _commands = new Dictionary<Phase, (string, string)>();
        private readonly Dictionary<string, string> _hints = new Dictionary<string, string>();
        private readonly Dictionary<Violation, (string title, string text)> _fails = new Dictionary<Violation, (string, string)>();
        private readonly Dictionary<Phase, StationCard> _stations = new Dictionary<Phase, StationCard>();
        private readonly Dictionary<Station, string> _where = new Dictionary<Station, string>();
        private readonly string _boardNow;
        private readonly string _boardElsewhere;

        public InstructorScript(string json)
        {
            var data = JsonUtility.FromJson<Data>(json) ?? throw new ArgumentException("пустой сценарий", nameof(json));
            foreach (var c in data.commands)
                if (Enum.TryParse(c.phase, out Phase phase)) _commands[phase] = (c.text, c.voice);
            foreach (var h in data.hints) _hints[h.id] = h.text;
            foreach (var f in data.failReasons)
                if (Enum.TryParse(f.violation, out Violation v)) _fails[v] = (f.title, f.text);
            if (data.stationNames != null)
                foreach (var n in data.stationNames)
                    if (Enum.TryParse(n.station, out Station s)) _where[s] = n.where;
            if (data.stations != null)
                foreach (var line in data.stations)
                    if (Enum.TryParse(line.phase, out Phase phase) && Enum.TryParse(line.station, out Station s))
                        _stations[phase] = new StationCard { Station = s, Title = line.title, Steps = line.steps ?? new string[0] };
            _boardNow = data.boardNow ?? "{title}";
            _boardElsewhere = data.boardElsewhere ?? "{where}";
        }

        public static InstructorScript From(TextAsset asset) => asset != null ? new InstructorScript(asset.text) : null;

        public bool HasCommand(Phase phase) => _commands.ContainsKey(phase);
        public string Command(Phase phase) => _commands.TryGetValue(phase, out var c) ? c.text : string.Empty;
        public string VoiceId(Phase phase) => _commands.TryGetValue(phase, out var c) ? c.voice : string.Empty;
        public bool HasHint(string id) => _hints.ContainsKey(id);

        // Фраза подсказки с кнопками по руке: {Eject} — A/X, {Safety} — B/Y.
        public string Hint(string id, Hand hand) => _hints.TryGetValue(id, out var text) ? WithButtons(text, hand) : string.Empty;

        // Кнопки по руке в любой фразе (подсказки, онбординг NG-32, табло): {Eject} — A/X, {Safety} — B/Y.
        public static string WithButtons(string text, Hand hand) =>
            text == null ? string.Empty
                         : text.Replace("{Eject}", hand == Hand.Left ? "X" : "A")
                               .Replace("{Safety}", hand == Hand.Left ? "Y" : "B");

        public bool HasFailReason(Violation v) => _fails.ContainsKey(v);
        public string FailTitle(Violation v) => _fails.TryGetValue(v, out var f) ? f.title : v.ToString();
        public string FailText(Violation v) => _fails.TryGetValue(v, out var f) ? f.text : string.Empty;

        // Табло у столов и стоек (docs/script.md §6). null — для фазы табло не написано.
        public StationCard StationFor(Phase phase) => _stations.TryGetValue(phase, out var card) ? card : null;

        // «у стола снаряжения», «на огневом рубеже» — куда идти к месту.
        public string Where(Station station) => _where.TryGetValue(station, out var text) ? text : string.Empty;

        // Строки табло не своего места: какой шаг идёт и куда за ним идти.
        public string BoardNow(string title) => _boardNow.Replace("{title}", title);
        public string BoardElsewhere(Station station) => _boardElsewhere.Replace("{where}", Where(station));
    }
}
