using UnityEngine;

namespace Rubezh.Gameplay
{
    // Звуки пистолета в группе SFX микшера: выстрел и сухой щелчок. Клипы — MM-09; пока их нет,
    // временные звуки собираются в коде (шум с быстрым спадом и короткий щелчок).
    [RequireComponent(typeof(AudioSource))]
    public sealed class WeaponAudio : MonoBehaviour
    {
        [SerializeField] private WeaponController weapon;
        [SerializeField] private AudioClip shot;
        [SerializeField] private AudioClip dryFire;

        private AudioSource _source;

        public AudioSource Source => _source;
        public int Played { get; private set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (shot == null) shot = Noise("TempShot", 0.25f, 0.9f, 40f);
            if (dryFire == null) dryFire = Noise("TempDryFire", 0.03f, 0.4f, 200f);
        }

        private void OnEnable()
        {
            if (weapon == null) return;
            weapon.Fired += OnFired;
            weapon.DryFire += OnDryFire;
        }

        private void OnDisable()
        {
            if (weapon == null) return;
            weapon.Fired -= OnFired;
            weapon.DryFire -= OnDryFire;
        }

        private void OnFired() => Play(shot);
        private void OnDryFire() => Play(dryFire);

        private void Play(AudioClip clip)
        {
            Played++;
            _source.PlayOneShot(clip);
        }

        private static AudioClip Noise(string name, float seconds, float gain, float decay)
        {
            const int rate = 44100;
            int n = (int)(rate * seconds);
            var data = new float[n];
            var random = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                data[i] = ((float)random.NextDouble() * 2f - 1f) * gain * Mathf.Exp(-decay * t);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
