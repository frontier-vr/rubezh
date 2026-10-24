using System.Collections;
using System.Collections.Generic;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Голос инструктора (VP-14, FR-25): директор упражнения говорит команду своей фазы, подсказки
    // VP-12 и причину стопа. Какая фраза — берётся из сценария Data/Hints/script.ru.json по ID
    // озвучки (cmd_*, hint_*, fail_<Violation>), сами WAV — из набора Audio/Voice (YG-13) по имени
    // клипа. AudioSource — в группе микшера Voice: наушники на неё не действуют.
    // Команда перебивает подсказку; подсказка ждёт, пока договорят команду. Без клипа фраза
    // остаётся только на табло команд VP-24.
    [RequireComponent(typeof(AudioSource))]
    public sealed class InstructorVoice : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private HintDirector hints;
        [SerializeField] private TextAsset script;
        [Tooltip("Набор озвучки Audio/Voice: имя клипа = ID фразы")]
        [SerializeField] private AudioClip[] clips;
        [Tooltip("Пауза после загрузки сцены до первой команды, с")]
        [SerializeField] private float startDelaySeconds = 1f;

        private readonly Dictionary<string, AudioClip> _byId = new Dictionary<string, AudioClip>();
        private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
        private InstructorScript _script;
        private AudioSource _source;
        private bool _speakingCommand;

        public AudioSource Source => _source;
        public string Speaking => _source != null && _source.isPlaying && _source.clip != null ? _source.clip.name : null;
        public IReadOnlyCollection<AudioClip> Queued => _queue;
        public string LastSaid { get; private set; }

        public void Configure(ExerciseRunner exerciseRunner, HintDirector hintDirector, TextAsset scriptAsset, AudioClip[] voiceClips)
        {
            OnDisable();
            runner = exerciseRunner; hints = hintDirector; script = scriptAsset; clips = voiceClips;
            Build();
            OnEnable();
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            Build();
        }

        private void Build()
        {
            _script = InstructorScript.From(script);
            _byId.Clear();
            if (clips == null) return;
            foreach (var c in clips)
                if (c != null) _byId[c.name] = c;
        }

        private void OnEnable()
        {
            if (runner != null)
            {
                runner.PhaseChanged += OnPhaseChanged;
                runner.Finished += OnFinished;
                runner.Restarted += OnRestarted;
            }
            if (hints != null) hints.HintShown += OnHintShown;
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.PhaseChanged -= OnPhaseChanged;
                runner.Finished -= OnFinished;
                runner.Restarted -= OnRestarted;
            }
            if (hints != null) hints.HintShown -= OnHintShown;
        }

        // Первую команду — не в кадре загрузки сцены: первые кадры идут секундами реального времени,
        // и фраза договаривается раньше, чем пользователь видит сцену.
        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            yield return new WaitForSeconds(startDelaySeconds);
            if (runner != null && runner.Director != null && LastSaid == null) Command(runner.Phase);
        }

        public bool Has(string id) => id != null && _byId.ContainsKey(id);

        public string CommandId(Phase phase) => _script != null ? _script.VoiceId(phase) : null;

        private void OnPhaseChanged(Phase from, Phase to)
        {
            // Failed озвучивает OnFinished: команда «Стоп» и сразу причина.
            if (to != Phase.Failed) Command(to);
        }

        private void OnFinished(ExerciseResult result)
        {
            if (!result.FailedCritical) return;
            Command(Phase.Failed);
            if (result.FailReason.HasValue) Enqueue("fail_" + result.FailReason.Value);
        }

        private void OnRestarted()
        {
            _queue.Clear();
            if (runner != null) Command(runner.Phase);
        }

        private void OnHintShown(string target, string phrase)
        {
            var id = hints != null ? hints.CurrentHintId : null;
            if (!Has(id)) return;
            if (_speakingCommand && _source.isPlaying) Enqueue(id);
            else Play(id, false);
        }

        // Команда фазы: обрывает подсказку и очередь.
        public void Command(Phase phase)
        {
            var id = CommandId(phase);
            if (!Has(id)) return;
            _queue.Clear();
            Play(id, true);
        }

        private void Enqueue(string id)
        {
            if (Has(id)) _queue.Enqueue(_byId[id]);
        }

        private void Play(string id, bool command)
        {
            if (_source == null) _source = GetComponent<AudioSource>();
            _source.Stop();
            _source.clip = _byId[id];
            _source.Play();
            _speakingCommand = command;
            LastSaid = id;
        }

        private void Update()
        {
            if (_queue.Count == 0 || _source == null || _source.isPlaying) return;
            var next = _queue.Dequeue();
            Play(next.name, next.name.StartsWith("fail_") || next.name.StartsWith("cmd_"));
        }
    }
}
