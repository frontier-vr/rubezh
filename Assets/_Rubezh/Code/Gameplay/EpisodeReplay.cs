using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Повтор эпизода нарушения (VP-17, FR-45): призраки головы, рук, пистолета и магазина
    // проигрывают запись EpisodeRecorder по кругу в окне вокруг нарушения. Призраки на отдельном
    // слое: основная камера их не видит, их снимает камера повтора сбоку в текстуру экрана
    // результата. Луч из дульного среза призрака показывает, куда смотрел ствол.
    public sealed class EpisodeReplay : MonoBehaviour
    {
        [SerializeField] private EpisodeRecorder recorder;
        [SerializeField] private RulesConfig rules;
        [Tooltip("Призраки в порядке источников EpisodeRecorder: голова, левая рука, правая рука, пистолет, магазин")]
        [SerializeField] private Transform[] ghosts = new Transform[0];
        [SerializeField] private Camera replayCamera;
        [Tooltip("Дульный срез призрака пистолета: из него луч направления ствола")]
        [SerializeField] private Transform ghostMuzzle;
        [SerializeField] private LineRenderer muzzleRay;
        [Tooltip("Размер кадра повтора, пиксели: 16:9, как окно на экране результата")]
        [SerializeField] private Vector2Int textureSize = new Vector2Int(640, 360);

        private const int HeadTrack = 0;
        private const int PistolTrack = 3;

        private RenderTexture _texture;
        private float _startedAt;

        public bool Playing { get; private set; }
        public ViolationRecord Current { get; private set; }
        public ReplayWindow Window { get; private set; }
        public double Playhead { get; private set; }
        public RenderTexture Texture => _texture;

        private float Before => rules != null ? rules.replayBeforeSeconds : 2f;
        private float After => rules != null ? rules.replayAfterSeconds : 1.5f;
        private float MaxLength => rules != null ? rules.replayMaxSeconds : 8f;
        private float Pause => rules != null ? rules.replayLoopPauseSeconds : 1f;
        private float RayLength => rules != null ? rules.replayMuzzleRayMeters : 3f;

        public void Configure(EpisodeRecorder episodeRecorder, RulesConfig rulesConfig, Camera camera,
                              Transform muzzle, LineRenderer ray, params Transform[] ghostObjects)
        {
            recorder = episodeRecorder; rules = rulesConfig; replayCamera = camera;
            ghostMuzzle = muzzle; muzzleRay = ray; ghosts = ghostObjects ?? new Transform[0];
        }

        private void Awake() => SetGhostsActive(false);

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
        }

        // Есть ли что показать: запись этого эпизода не пуста.
        public bool CanPlay(in ViolationRecord v) =>
            recorder != null && recorder.TrackCount > 0 && recorder.RecordedUntil > 0 && v.Start <= recorder.RecordedUntil;

        public bool Play(in ViolationRecord v)
        {
            if (!CanPlay(v)) { Stop(); return false; }
            Current = v;
            Window = ReplayWindow.For(v, Before, After, MaxLength, recorder.RecordedUntil);
            _startedAt = Time.unscaledTime;
            Playing = true;
            EnsureTexture();
            SetGhostsActive(true);
            Frame(v);
            Apply(Window.From);
            return true;
        }

        public void Stop()
        {
            Playing = false;
            SetGhostsActive(false);
            if (replayCamera != null) replayCamera.enabled = false;
        }

        private void LateUpdate()
        {
            if (!Playing) return;
            Apply(Window.Loop(Time.unscaledTime - _startedAt, Pause));
        }

        // Положить призраков в момент time и снять кадр.
        public void Apply(double time)
        {
            Playhead = time;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] == null) continue;
                bool ok = recorder.TrySample(i, time, out var pose, out bool visible);
                ghosts[i].gameObject.SetActive(ok && visible);
                if (ok) ghosts[i].SetPositionAndRotation(pose.position, pose.rotation);
            }
            if (muzzleRay != null && ghostMuzzle != null)
            {
                muzzleRay.enabled = ghostMuzzle.gameObject.activeInHierarchy;
                muzzleRay.SetPosition(0, ghostMuzzle.position);
                muzzleRay.SetPosition(1, ghostMuzzle.position + ghostMuzzle.forward * RayLength);
            }
            if (replayCamera != null && _texture != null)
            {
                replayCamera.targetTexture = _texture;
                replayCamera.Render();
            }
        }

        // Камера из-за правого плеча стрелка и чуть сверху: видно, куда он смотрит и куда ушёл ствол
        // относительно направления на мишень. Положение — в момент, когда нарушение засчитано.
        private void Frame(in ViolationRecord v)
        {
            if (replayCamera == null) return;
            if (!recorder.TrySample(HeadTrack, v.Time, out var head, out _)) return;
            var focus = head.position;
            if (recorder.TrySample(PistolTrack, v.Time, out var pistol, out _)) focus = (head.position + pistol.position) * 0.5f;
            var forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            var side = Vector3.Cross(Vector3.up, forward);           // вправо от стрелка
            var eye = focus - forward * 0.85f + side * 0.35f + Vector3.up * 0.35f;
            var look = focus + forward * 0.5f;
            replayCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        }

        private void EnsureTexture()
        {
            if (_texture != null) return;
            _texture = new RenderTexture(Mathf.Max(64, textureSize.x), Mathf.Max(64, textureSize.y), 16) { name = "EpisodeReplay" };
        }

        private void SetGhostsActive(bool on)
        {
            foreach (var g in ghosts) if (g != null) g.gameObject.SetActive(on);
            if (muzzleRay != null) muzzleRay.enabled = on;
        }
    }
}
