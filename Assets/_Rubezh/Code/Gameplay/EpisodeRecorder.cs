using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Запись упражнения для повтора эпизода нарушения (VP-17): голова, руки, пистолет и магазин
    // по времени упражнения (Director.Elapsed — то же время, что у ViolationRecord). Пишет с
    // частотой из RulesConfig, пока упражнение идёт; «Повторить» начинает запись заново.
    [DefaultExecutionOrder(100)]
    public sealed class EpisodeRecorder : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [Tooltip("Что записывать, порядок = порядок призраков в EpisodeReplay: голова, левая рука, правая рука, пистолет, магазин")]
        [SerializeField] private Transform[] sources = new Transform[0];

        private PoseTrack[] _tracks = new PoseTrack[0];
        private double _lastSample = double.NegativeInfinity;

        public int TrackCount => _tracks.Length;
        public double RecordedUntil { get; private set; }
        public Transform Source(int i) => i >= 0 && i < sources.Length ? sources[i] : null;

        private float Rate => rules != null ? rules.replaySampleRate : 30f;
        private float BufferSeconds => rules != null ? rules.replayBufferSeconds : 720f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, params Transform[] tracked)
        {
            OnDisable();
            runner = exerciseRunner; rules = rulesConfig; sources = tracked ?? new Transform[0];
            Allocate();
            OnEnable();
        }

        private void Awake() => Allocate();

        private void OnEnable()
        {
            if (runner != null) runner.Restarted += Clear;
        }

        private void OnDisable()
        {
            if (runner != null) runner.Restarted -= Clear;
        }

        private void Allocate()
        {
            int capacity = Mathf.CeilToInt(Mathf.Max(1f, BufferSeconds) * Mathf.Max(1f, Rate)) + 1;
            _tracks = new PoseTrack[sources.Length];
            for (int i = 0; i < _tracks.Length; i++) _tracks[i] = new PoseTrack(capacity);
            Clear();
        }

        public void Clear()
        {
            foreach (var t in _tracks) t.Clear();
            _lastSample = double.NegativeInfinity;
            RecordedUntil = 0;
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Director == null) return;
            var director = runner.Director;
            double now = director.Elapsed;
            // Последний кадр пишется и в момент остановки: грубое нарушение засчитано именно тогда.
            bool finishedNow = !director.IsRunning && now > RecordedUntil;
            if (!director.IsRunning && !finishedNow) return;
            if (!finishedNow && now - _lastSample < 1.0 / Mathf.Max(1f, Rate)) return;
            Sample(now);
        }

        public void Sample(double time)
        {
            for (int i = 0; i < _tracks.Length; i++)
            {
                var s = sources[i];
                if (s == null) continue;
                _tracks[i].Add(time, new Pose(s.position, s.rotation), s.gameObject.activeInHierarchy);
            }
            _lastSample = time;
            if (time > RecordedUntil) RecordedUntil = time;
        }

        public bool TrySample(int track, double time, out Pose pose, out bool visible)
        {
            pose = Pose.identity; visible = false;
            return track >= 0 && track < _tracks.Length && _tracks[track].TrySample(time, out pose, out visible);
        }
    }
}
