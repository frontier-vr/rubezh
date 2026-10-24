using Rubezh.Core;
using Rubezh.Gameplay;
using TMPro;
using UnityEngine;

namespace Rubezh.UI
{
    // Табло команд у рубежа O-31 — запасной вариант голоса (FR-25): текст команды текущей фазы
    // держится на табло всю фазу (заменяет «переспросить», design-doc §3.1), смена команды — с
    // коротким сигналом. Тексты — из сценария Data/Hints/script.ru.json. Внизу табло — место под
    // шкалу прогресса O-43 (NG-31).
    public sealed class CommandBoard : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private TextAsset script;
        [SerializeField] private TMP_Text commandText;
        [SerializeField] private AudioSource beepSource;

        private InstructorScript _script;
        private AudioClip _beep;

        public string Current => commandText != null ? commandText.text : string.Empty;
        public int Beeps { get; private set; }

        public void Configure(ExerciseRunner exerciseRunner, TextAsset scriptAsset, TMP_Text text, AudioSource source)
        {
            Unsubscribe();
            runner = exerciseRunner; script = scriptAsset; commandText = text; beepSource = source;
            _script = null;
            Subscribe();
            Show(runner.Phase, beep: false);
        }

        private void OnEnable()
        {
            Subscribe();
            if (runner != null && runner.Director != null) Show(runner.Phase, beep: false);
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (runner == null) return;
            runner.PhaseChanged -= OnPhaseChanged;
            runner.PhaseChanged += OnPhaseChanged;
        }

        private void Unsubscribe()
        {
            if (runner != null) runner.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(Phase from, Phase to) => Show(to, beep: true);

        private void Show(Phase phase, bool beep)
        {
            _script ??= InstructorScript.From(script);
            if (commandText != null) commandText.text = _script != null ? _script.Command(phase) : phase.ToString();
            if (!beep) return;
            Beeps++;
            if (beepSource == null) return;
            if (_beep == null) _beep = BuildBeep();
            beepSource.PlayOneShot(_beep);
        }

        // Короткий сигнал 880 Гц, 0,18 с, с плавным концом — свой, без заимствованных звуков.
        private static AudioClip BuildBeep()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.18f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(1f - i / (float)n) * Mathf.Clamp01(i / (rate * 0.005f));
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.35f * env;
            }
            var clip = AudioClip.Create("CommandBeep", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
