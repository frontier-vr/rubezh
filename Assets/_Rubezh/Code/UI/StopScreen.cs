using System.Collections;
using Rubezh.Core;
using Rubezh.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rubezh.UI
{
    // Стоп-экран грубого нарушения (VP-22, FR-39) по макету YG-09 (docs/result-screen.md): у текущего
    // якоря (R1/R2/R3) красная полоса «СТОП» со значком, причина и пояснение из сценария Юли
    // (script.ru.json, failReasons) и стоп-кадр — что видел пользователь в момент нарушения.
    // Через failResultDelaySeconds (RulesConfig) стоп-экран уступает место экрану результата (VP-20)
    // на том же месте. Во время упражнения скрыт.
    public sealed class StopScreen : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private TextAsset script;
        [SerializeField] private AnchorLocator anchors;
        [Header("Где показывать: экраны R1–R3 и якоря A1–A3 (layout.md §4)")]
        [SerializeField] private Transform r1, r2, r3;
        [SerializeField] private Transform a1, a2, a3;
        [Header("Панель")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title, reason, frameCaption, footer;
        [SerializeField] private RawImage stopFrame;
        [Tooltip("Камера стоп-кадра: в момент нарушения встаёт на место глаз пользователя и снимает один кадр")]
        [SerializeField] private Camera frameCamera;
        [Tooltip("Глаза пользователя; пусто — Camera.main")]
        [SerializeField] private Transform eyes;

        private InstructorScript _script;
        private RenderTexture _frame;
        private Coroutine _hide;

        public bool Visible => panel != null && panel.activeSelf;
        public Violation? Reason { get; private set; }

        private float Delay => rules != null ? rules.failResultDelaySeconds : 4f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, AnchorLocator locator,
                              Transform screen1, Transform screen2, Transform screen3,
                              Transform anchor1, Transform anchor2, Transform anchor3, Transform eyesTransform)
        {
            OnDisable();
            runner = exerciseRunner; rules = rulesConfig; anchors = locator;
            r1 = screen1; r2 = screen2; r3 = screen3; a1 = anchor1; a2 = anchor2; a3 = anchor3; eyes = eyesTransform;
            OnEnable();
        }

        private void Awake()
        {
            _script = InstructorScript.From(script);
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (runner == null) return;
            runner.Finished += OnFinished;
            runner.Restarted += Hide;
        }

        private void OnDisable()
        {
            if (runner == null) return;
            runner.Finished -= OnFinished;
            runner.Restarted -= Hide;
        }

        private void OnDestroy()
        {
            if (_frame != null) _frame.Release();
        }

        private void OnFinished(ExerciseResult result)
        {
            if (!result.FailedCritical || !result.FailReason.HasValue) return;
            Show(result.FailReason.Value, CriticalTime(result), anchors != null ? anchors.Current : Anchor.A1);
            if (_hide != null) StopCoroutine(_hide);
            _hide = StartCoroutine(HideLater());
        }

        private IEnumerator HideLater()
        {
            yield return new WaitForSeconds(Delay);
            _hide = null;
            if (panel != null) panel.SetActive(false);
        }

        public void Show(Violation violation, double at, Anchor where)
        {
            Reason = violation;
            CaptureFrame();                        // до показа: в кадр не попадает сама панель
            Place(where);
            if (title != null) title.text = _script != null ? _script.FailTitle(violation) : violation.ToString();
            if (reason != null) reason.text = _script != null ? _script.FailText(violation) : string.Empty;
            if (frameCaption != null) frameCaption.text = "стоп-кадр · " + ResultScreen.FormatTime(at);
            if (footer != null) footer.text = "Упражнение прекращено. Результат — через " + Mathf.CeilToInt(Delay) + " с";
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (_hide != null) { StopCoroutine(_hide); _hide = null; }
            if (panel != null) panel.SetActive(false);
            Reason = null;
        }

        private static double CriticalTime(ExerciseResult r)
        {
            if (r.Violations != null)
                foreach (var v in r.Violations)
                    if (v.Severity == Severity.Critical) return v.Time;
            return r.TotalTime;
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

        // Стоп-кадр: отдельная камера встаёт на место глаз и рендерит один кадр в текстуру панели
        // (камеру шлема в текстуру не перенаправляем — она рисует оба глаза).
        private void CaptureFrame()
        {
            var eye = eyes != null ? eyes : Camera.main != null ? Camera.main.transform : null;
            if (frameCamera == null || stopFrame == null || eye == null) return;
            if (_frame == null)
            {
                _frame = new RenderTexture(640, 360, 16);
                stopFrame.texture = _frame;
            }
            frameCamera.transform.SetPositionAndRotation(eye.position, eye.rotation);
            frameCamera.targetTexture = _frame;
            frameCamera.Render();
        }
    }
}
