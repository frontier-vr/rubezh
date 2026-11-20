using System.Text;
using Rubezh.Core;
using Rubezh.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rubezh.UI
{
    // Экран результата (VP-20, FR-42) по макету YG-09 (docs/result-screen.md): вердикт со значком и
    // словом, балл, время, точность, мишень с пробоинами, нарушения со временем и штрафом, «Повторить».
    // Грубые нарушения упражнение не останавливают (Р-09) и весь балл не обнуляют (Р-11): в таблице они
    // идут первыми, красным, с пометкой «0 баллов», в шапке — сколько пунктов с 0 баллов.
    // Под таблицей — повтор эпизода нарушения (VP-17): первым грубое, «Следующее» листает по кругу.
    // Во время упражнения скрыт, показывается после сдачи оружия у шкафа (R1), лицом к A1.
    public sealed class ResultScreen : MonoBehaviour
    {
        private const int MaxRows = 8;
        private const int MaxRowsWithReplay = 4;      // под таблицей — окно повтора (VP-17)
        private const float TableHeight = 460f;
        private const float TableHeightWithReplay = 210f;

        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private TextAsset hintRules;
        [SerializeField] private AnchorLocator anchors;
        [Header("Где показывать: экраны R1–R3 и якоря A1–A3 (layout.md §4)")]
        [SerializeField] private Transform r1, r2, r3;
        [SerializeField] private Transform a1, a2, a3;
        [Header("Панель")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Image verdictIcon;
        [SerializeField] private Sprite iconPassed, iconRemarks, iconFailed;
        [SerializeField] private TMP_Text verdict, score, time, accuracy, violationsValue, violationsCaption, table;
        [SerializeField] private RawImage targetView;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform targetPlane;
        [SerializeField] private Button repeat;
        [Header("Повтор эпизода нарушения (VP-17)")]
        [SerializeField] private EpisodeReplay replay;
        [SerializeField] private GameObject replayBlock;
        [SerializeField] private RawImage replayView;
        [SerializeField] private TMP_Text replayCaption;
        [SerializeField] private Button replayNext;

        private HintRules _names;
        private RenderTexture _targetTexture;
        private int _replayIndex = -1;

        public bool Visible => panel != null && panel.activeSelf;
        public ExerciseResult Shown { get; private set; }
        public int ReplayIndex => _replayIndex;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, AnchorLocator locator,
                              Transform screen1, Transform screen2, Transform screen3,
                              Transform anchor1, Transform anchor2, Transform anchor3, Transform plane)
        {
            OnDisable();
            runner = exerciseRunner; rules = rulesConfig; anchors = locator;
            r1 = screen1; r2 = screen2; r3 = screen3; a1 = anchor1; a2 = anchor2; a3 = anchor3; targetPlane = plane;
            OnEnable();
        }

        public void ConfigureReplay(EpisodeReplay episodeReplay) => replay = episodeReplay;

        private void Awake()
        {
            _names = HintRules.From(hintRules);
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (runner != null)
            {
                runner.Finished += OnFinished;
                runner.Restarted += Hide;
            }
            if (repeat != null) repeat.onClick.AddListener(Repeat);
            if (replayNext != null) replayNext.onClick.AddListener(NextReplay);
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.Finished -= OnFinished;
                runner.Restarted -= Hide;
            }
            if (repeat != null) repeat.onClick.RemoveListener(Repeat);
            if (replayNext != null) replayNext.onClick.RemoveListener(NextReplay);
        }

        private void OnDestroy()
        {
            if (_targetTexture != null) _targetTexture.Release();
        }

        // Упражнение всегда кончается сдачей в шкаф (Р-09), результат — у R1.
        private void OnFinished(ExerciseResult result) => Show(result, Anchor.A1);

        public void Show(ExerciseResult result, Anchor at)
        {
            Shown = result;
            Place(at);
            if (panel != null) panel.SetActive(true);
            ShowReplay(ReplayWindow.DefaultIndex(result.Violations));   // до Fill: от повтора зависит высота таблицы
            Fill(result);
            CaptureTarget();
        }

        public void Hide()
        {
            StopReplay();
            if (panel != null) panel.SetActive(false);
        }

        // Повтор нарушения index из показанного результата; −1 или нет записи — блок скрыт.
        public bool ShowReplay(int index)
        {
            var list = Shown != null ? Shown.Violations : null;
            bool ok = replay != null && list != null && index >= 0 && index < list.Count
                      && ReplayWindow.Shows(list[index].Type) && replay.Play(list[index]);
            _replayIndex = ok ? index : -1;
            if (replayBlock != null) replayBlock.SetActive(ok);
            if (!ok) return false;
            int count = ReplayWindow.Count(list, index, out int ordinal);
            if (replayView != null) replayView.texture = replay.Texture;
            if (replayCaption != null) replayCaption.text = ReplayCaption(list[index], ordinal - 1, count);
            if (replayNext != null) replayNext.gameObject.SetActive(count > 1);
            return true;
        }

        // «Следующее»: по кругу по нарушениям, которые видны в повторе (#176).
        public void NextReplay()
        {
            var list = Shown != null ? Shown.Violations : null;
            int next = ReplayWindow.NextIndex(list, _replayIndex);
            if (next >= 0) ShowReplay(next);
        }

        public string ReplayCaption(in ViolationRecord v, int index, int count)
        {
            var sb = new StringBuilder("Повтор нарушения");
            if (count > 1) sb.Append(' ').Append(index + 1).Append(" из ").Append(count);
            sb.Append('\n');
            if (v.Severity == Severity.Critical) sb.Append("<color=#D23A2A>");
            sb.Append(FormatTime(v.Start)).Append(" · ").Append(Name(v.Type));
            if (v.Severity == Severity.Critical) sb.Append("</color>");
            return sb.ToString();
        }

        private void StopReplay()
        {
            if (replay != null) replay.Stop();
            _replayIndex = -1;
            if (replayBlock != null) replayBlock.SetActive(false);
        }

        // «Повторить»: директор с начала; предметы, СИЗ и телепорт возвращают подписчики Restarted.
        public void Repeat()
        {
            if (runner != null) runner.Restart();
            Hide();
        }

        private void Place(Anchor at)
        {
            Transform screen = at == Anchor.A2 ? r2 : at == Anchor.A3 ? r3 : r1;
            Transform anchor = at == Anchor.A2 ? a2 : at == Anchor.A3 ? a3 : a1;
            if (screen == null) return;
            var forward = screen.position - (anchor != null ? anchor.position : screen.position - screen.forward);
            forward.y = 0f;
            transform.SetPositionAndRotation(screen.position, Quaternion.LookRotation(forward.sqrMagnitude > 1e-4f ? forward : screen.forward));
        }

        public void Fill(ExerciseResult r)
        {
            switch (r.Verdict)
            {
                case Verdict.Passed: SetVerdict("ЗАЧЁТ", iconPassed, "#3E9E4F"); break;
                case Verdict.PassedWithRemarks: SetVerdict("ЗАЧЁТ С ЗАМЕЧАНИЯМИ", iconRemarks, "#F2C200"); break;
                default: SetVerdict("НЕ ЗАЧЁТ", iconFailed, "#D23A2A"); break;
            }
            if (score != null) score.text = r.Score.ToString();
            if (time != null) time.text = FormatTime(r.TotalTime);
            if (accuracy != null) accuracy.text = r.Shots > 0 ? $"{r.Hits} из {r.Shots} · {Mathf.RoundToInt(100f * r.Hits / r.Shots)}%" : "нет выстрелов";
            int count = r.Violations != null ? r.Violations.Count : 0;
            int zeroed = r.ZeroedItems != null ? r.ZeroedItems.Count : 0;
            if (zeroed > 0)
            {
                if (violationsCaption != null) violationsCaption.text = "пункты с 0 баллов";
                if (violationsValue != null) { violationsValue.text = zeroed.ToString(); violationsValue.color = Hex("#D23A2A"); }
            }
            else
            {
                if (violationsCaption != null) violationsCaption.text = "нарушения";
                if (violationsValue != null) { violationsValue.text = count.ToString(); violationsValue.color = Hex("#F2F0EA"); }
            }
            if (table != null)
            {
                var size = table.rectTransform.sizeDelta;
                table.rectTransform.sizeDelta = new Vector2(size.x, _replayIndex >= 0 ? TableHeightWithReplay : TableHeight);
                table.text = Table(r);
            }
        }

        private int Rows => _replayIndex >= 0 ? MaxRowsWithReplay : MaxRows;

        // Строки таблицы: сначала грубые (пункты с 0 баллов), потом мелкие — те и другие по времени.
        public string Table(ExerciseResult r)
        {
            if (r.Violations == null || r.Violations.Count == 0) return "Нарушений нет";
            var rules = runner != null && runner.Director != null ? runner.Director.Rules : new ExerciseRules();
            var rows = new System.Collections.Generic.List<string>();
            var zeroed = new System.Collections.Generic.HashSet<Violation>();
            foreach (var v in r.Violations)
            {
                if (v.Severity != Severity.Critical) continue;
                // Пункт обнуляется один раз: повторное грубое того же вида балл больше не меняет.
                string mark = zeroed.Add(v.Type) ? "× 0 баллов · " : "× повтор · ";
                rows.Add("<color=#D23A2A>" + mark + FormatTime(v.Start) + " · " + Name(v.Type) + "</color>");
            }
            foreach (var v in r.Violations)
            {
                if (v.Severity == Severity.Critical) continue;
                var sb = new StringBuilder();
                sb.Append(FormatTime(v.Start)).Append(" · ").Append(Name(v.Type));   // начало эпизода (VP-15)
                if (v.Duration > 0.05) sb.Append($" ({v.Duration:0.0} с)");
                sb.Append(" · −").Append(ExerciseScoring.Penalty(rules, v));
                rows.Add(sb.ToString());
            }
            int shown = Mathf.Min(Rows, rows.Count);
            var table = new StringBuilder();
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) table.Append('\n');
                table.Append(rows[i]);
            }
            if (rows.Count > shown) table.Append("\nещё ").Append(rows.Count - shown);
            return table.ToString();
        }

        public static string FormatTime(double seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt((float)seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        private string Name(Violation v) => _names != null ? _names.ViolationName(v) : v.ToString();

        private void SetVerdict(string word, Sprite icon, string color)
        {
            if (verdict != null) verdict.text = word;
            if (verdictIcon != null)
            {
                verdictIcon.sprite = icon;
                verdictIcon.color = Hex(color);
            }
        }

        // Мишень с пробоинами: тот же лист, что в сцене, снятый камерой спереди в текстуру панели.
        private void CaptureTarget()
        {
            if (targetCamera == null || targetPlane == null || targetView == null) return;
            if (_targetTexture == null)
            {
                _targetTexture = new RenderTexture(512, 512, 16);
                targetView.texture = _targetTexture;
            }
            // Лицо листа — его локальная −Z (как у Quad), камера смотрит на него вдоль +forward.
            targetCamera.transform.SetPositionAndRotation(targetPlane.position - targetPlane.forward * 0.6f,
                                                          Quaternion.LookRotation(targetPlane.forward, targetPlane.up));
            targetCamera.targetTexture = _targetTexture;
            targetCamera.Render();
        }

        private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.white;
    }
}
