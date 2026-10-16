using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rubezh.Sandbox
{
    // Заглушка пистолета в руке для песочниц без шлема: клавиши вместо XR.
    // H — взять/положить, L — снарядить/разрядить, Q/E — развернуть ствол, R — Restart;
    // палец на спуске — T (TriggerInputStub), M — камера перед дульным срезом.
    public sealed class HeldWeaponStub : MonoBehaviour, IHeldWeapon
    {
        [SerializeField] private bool held = true;
        [SerializeField] private bool loaded = true;
        [SerializeField] private float yawSpeed = 90f;
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private SectorDetector sector;
        [Tooltip("Камера песочницы и пустышка Muzzle: M ставит камеру перед дульным срезом")]
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform muzzle;

        private Vector3 _camPos;
        private Quaternion _camRot;
        private bool _lookingIn;

        [SerializeField] private Hand primaryHand = Hand.Right;

        public bool IsHeld => held;
        public Hand PrimaryHand => primaryHand;
        public WeaponState State => loaded
            ? new WeaponState(true, 4, Chamber.Loaded, false, false, Jam.None)
            : new WeaponState(false, 0, Chamber.Empty, true, true, Jam.None);

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.hKey.wasPressedThisFrame) held = !held;
            if (kb.lKey.wasPressedThisFrame) loaded = !loaded;
            if (kb.rKey.wasPressedThisFrame && runner != null) runner.Restart();
            if (kb.mKey.wasPressedThisFrame) ToggleLookIntoMuzzle();
            float yaw = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            if (yaw != 0f) transform.Rotate(0f, yaw * yawSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void ToggleLookIntoMuzzle()
        {
            if (headCamera == null || muzzle == null) return;
            if (!_lookingIn)
            {
                _camPos = headCamera.position; _camRot = headCamera.rotation;
                headCamera.position = muzzle.position + muzzle.forward * 0.3f + Vector3.up * 0.03f;
                headCamera.rotation = Quaternion.LookRotation(muzzle.position - headCamera.position);
            }
            else
            {
                headCamera.position = _camPos; headCamera.rotation = _camRot;
            }
            _lookingIn = !_lookingIn;
        }

        private void OnGUI()
        {
            if (runner == null || runner.Director == null) return;
            var d = runner.Director;
            var text = $"Фаза: {d.Phase}\nВ руке (H): {held}   Снаряжён (L): {loaded}\n";
            if (sector != null)
                text += $"В секторе: {sector.InSector}   вне сектора: {sector.Rule?.OutDuration:F2} с\n";
            text += "Q/E — развернуть, T — палец на спуске, M — заглянуть в ствол, R — заново\nНарушения:\n";
            foreach (var v in d.Violations) text += $"  {v.Time:F1} с  {v.Type} {v.Severity} {v.Duration:F2} с\n";
            GUI.Box(new Rect(10, 10, 420, 220), text);
        }
    }
}
