using System.IO;
using UnityEngine;

namespace Rubezh.Tests.PlayMode
{
    // Запись видео сквозного прогона (NG-20): камера на высоте глаз над XR Origin смотрит на правую
    // руку, кадры 1280×720 в PNG с шагом 1/30 с игрового времени (Time.captureFramerate), так что
    // ролик идёт с настоящей скоростью, сколько бы ни длился кадр в редакторе. Включается, только
    // если в EditorPrefs задан каталог «Rubezh.VideoDir»; склейка — tools/video/encode.ps1.
    public sealed class VideoCapture : MonoBehaviour
    {
        public const string PrefsKey = "Rubezh.VideoDir";
        private const int Fps = 30;

        private string _dir;
        private Transform _rig, _hand, _focus;
        private Camera _camera;
        private RenderTexture _target;
        private Texture2D _frame;
        private Vector3 _look;
        private int _index;

        public static VideoCapture StartIfRequested(Transform rig, Transform hand)
        {
#if UNITY_EDITOR
            var dir = UnityEditor.EditorPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(dir)) return null;
            Directory.CreateDirectory(dir);
            var go = new GameObject("VideoCapture");
            var capture = go.AddComponent<VideoCapture>();
            capture._dir = dir; capture._rig = rig; capture._hand = hand;
            return capture;
#else
            return null;
#endif
        }

        private void Start()
        {
            Time.captureFramerate = Fps;
            _camera = gameObject.AddComponent<Camera>();
            _camera.fieldOfView = 75f;
            _camera.nearClipPlane = 0.03f;
            _camera.cullingMask &= ~(1 << LayerMask.NameToLayer("Replay"));
            _camera.enabled = false;
            _target = new RenderTexture(1280, 720, 24);
            _frame = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            _look = _rig.position + _rig.forward * 2f + Vector3.up * 1.3f;
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            var eye = _rig.position + Vector3.up * 1.65f - _rig.forward * 0.25f;
            var want = _focus != null ? _focus.position : _hand != null ? _hand.position + _rig.forward * 0.6f : _rig.position + _rig.forward * 2f + Vector3.up * 1.3f;
            _look = Vector3.Lerp(_look, want, 0.15f);                        // голова догоняет руку плавно
            transform.SetPositionAndRotation(eye, Quaternion.LookRotation(_look - eye, Vector3.up));
            _camera.targetTexture = _target;
            _camera.Render();
            RenderTexture.active = _target;
            _frame.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(_dir, $"frame_{_index++:D5}.png"), _frame.EncodeToPNG());
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Rubezh/Видео прогона/Записывать кадры в Temp/video")]
        private static void RecordOn() => UnityEditor.EditorPrefs.SetString(PrefsKey, Path.GetFullPath("Temp/video"));

        [UnityEditor.MenuItem("Rubezh/Видео прогона/Не записывать")]
        private static void RecordOff() => UnityEditor.EditorPrefs.DeleteKey(PrefsKey);
#endif

        // Смотреть на точку (экран результата в конце), а не на руку.
        public void Focus(Transform target) => _focus = target;

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
            if (_target != null) _target.Release();
        }
    }
}
