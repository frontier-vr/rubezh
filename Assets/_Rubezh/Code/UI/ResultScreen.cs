using System.Collections;
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
    // Во время упражнения скрыт. Done — сразу у шкафа (R1), лицом к A1. Failed — через N с
    // (RulesConfig) у текущего якоря, на месте стоп-экрана VP-22, с баллом 0.
    public sealed class ResultScreen : MonoBehaviour
    {
        private const int MaxRows = 8;

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

        private HintRules _names;
        private RenderTexture _targetTexture;
        private Coroutine _delayed;

        public bool Visible => panel != null && panel.activeSelf;
        public ExerciseResult Shown { get; private set; }

        private float FailDelay => rules != null ? rules.failResultDelaySeconds : 4f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, AnchorLocator locator,
                              Transform screen1, Transform screen2, Transform screen3,
                              Transform anchor1, Transform anchor2, Transform anchor3, Transform plane)
        {
            OnDisable();
            runner = exerciseRunner; rules = rulesConfig; anchors = locator;
            r1 = screen1; r2 = screen2; r3 = screen3; a1 = anchor1; a2 = anchor2; a3 = anchor3; targetPlane = plane;
            OnEnable();
        }

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
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.Finished -= OnFinished;
                runner.Restarted -= Hide;
            }
            if (repeat != null) repeat.onClick.RemoveListener(Repeat);
        }

        private void OnDestroy()
        {
            if (_targetTexture != null) _targetTexture.Release();
        }

        private void OnFinished(ExerciseResult result)
        {
            if (_delayed != null) StopCoroutine(_delayed);
            if (result.FailedCritical) _delayed = StartCoroutine(ShowLater(result));
            else Show(result, Anchor.A1);
        }

        private IEnumerator ShowLater(ExerciseResult result)
        {
            yield return new WaitForSeconds(FailDelay);
            _delayed = null;
            Show(result, anchors != null ? anchors.Current : Anchor.A1);
        }

        public void Show(ExerciseResult result, Anchor at)
        {
            Shown = result;
            Place(at);
            Fill(result);
            CaptureTarget();
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (_delayed != null) { StopCoroutine(_delayed); _delayed = null; }
            if (panel != null) panel.SetActive(false);
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
            if (r.FailedCritical && r.FailReason.HasValue)
            {
                if (violationsCaption != null) violationsCaption.text = "причина";
                if (violationsValue != null) { violationsValue.text = Name(r.FailReason.Value); violationsValue.color = Hex("#D23A2A"); }
            }
            else
            {
                if (violationsCaption != null) violationsCaption.text = "нарушения";
                if (violationsValue != null) { violationsValue.text = count.ToString(); violationsValue.color = Hex("#F2F0EA"); }
            }
            if (table != null) table.text = Table(r);
        }

        public string Table(ExerciseResult r)
        {
            if (r.Violations == null || r.Violations.Count == 0) return "Нарушений нет";
            var rules = runner != null && runner.Director != null ? runner.Director.Rules : new ExerciseRules();
            var sb = new StringBuilder();
            int shown = Mathf.Min(MaxRows, r.Violations.Count);
            for (int i = 0; i < shown; i++)
            {
                var v = r.Violations[i];
                string t = FormatTime(v.Time);
                if (v.Severity == Severity.Critical)
                    sb.Append("<color=#D23A2A>× грубое · ").Append(t).Append(" · ").Append(Name(v.Type)).Append(" · стоп</color>");
                else
                {
                    sb.Append(t).Append(" · ").Append(Name(v.Type));
                    if (v.Duration > 0.05) sb.Append($" ({v.Duration:0.0} с)");
                    sb.Append(" · −").Append(ExerciseScoring.Penalty(rules, v));
                }
                if (i < shown - 1) sb.Append('\n');
            }
            if (r.Violations.Count > MaxRows) sb.Append("\nещё ").Append(r.Violations.Count - MaxRows);
            return sb.ToString();
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
