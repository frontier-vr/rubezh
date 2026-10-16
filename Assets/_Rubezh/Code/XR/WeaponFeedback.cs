using System.Collections;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Rubezh.XR
{
    // Отклик пистолета в руке (NG-15, FR-21): отдача вибрацией и вспышка MM-17 на 2–3 кадра по
    // Fired; на осечке и сухом щелчке (DryFire) отдачи и вспышки нет. Щелчок магазина в шахте,
    // выпадение магазина, постановка затвора на задержку — короткие импульсы. Захват даёт
    // SimpleHapticFeedback на интеракторе (select entered).
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class WeaponFeedback : MonoBehaviour
    {
        [SerializeField] private WeaponController weapon;
        [Tooltip("Вспышка MM-17 на пустышке Muzzle; по умолчанию выключена")]
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Min(1)] private int flashFrames = 2;
        [SerializeField, Range(0f, 1f)] private float recoilAmplitude = 0.9f;
        [SerializeField, Min(0f)] private float recoilSeconds = 0.08f;
        [SerializeField, Range(0f, 1f)] private float clickAmplitude = 0.45f;
        [SerializeField, Min(0f)] private float clickSeconds = 0.04f;

        private XRGrabInteractable _grab;
        private Coroutine _flash;
        private bool _wasLocked;

        public int Flashes { get; private set; }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        private void OnEnable()
        {
            if (weapon == null) return;
            weapon.Fired += OnFired;
            weapon.MagazineInserted += OnMagazineInserted;
            weapon.Applied += OnApplied;
            weapon.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (weapon == null) return;
            weapon.Fired -= OnFired;
            weapon.MagazineInserted -= OnMagazineInserted;
            weapon.Applied -= OnApplied;
            weapon.Changed -= OnChanged;
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
        }

        private void OnFired()
        {
            Haptics.ToHolders(_grab, recoilAmplitude, recoilSeconds);
            Flashes++;
            if (muzzleFlash == null) return;
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            muzzleFlash.SetActive(true);
            for (int i = 0; i < flashFrames; i++) yield return null;
            muzzleFlash.SetActive(false);
            _flash = null;
        }

        private void OnMagazineInserted(int rounds) => Haptics.ToHolders(_grab, clickAmplitude, clickSeconds);

        private void OnApplied(WeaponCommand command)
        {
            if (command == WeaponCommand.EjectMagazine) Haptics.ToHolders(_grab, clickAmplitude * 0.7f, clickSeconds);
        }

        private void OnChanged(WeaponState state)
        {
            // Затвор встал на задержку (последний выстрел, отвод без патронов) — щелчок в ладонь.
            if (state.SlideLocked && !_wasLocked) Haptics.ToHolders(_grab, clickAmplitude, clickSeconds);
            _wasLocked = state.SlideLocked;
        }
    }
}
