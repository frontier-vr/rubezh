using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rubezh.XR
{
    // Оттенок стёкол очков (NG-30, дизайн-документ §7, объект 5): глобальный Volume с Color
    // Adjustments → Color Filter. Вес 1, пока очки надеты (IWearables.GlassesOn), 0 — сняты;
    // переход за RulesConfig.glassesTintSeconds. Рисует его On Tile Post Processing (URP_Renderer),
    // встроенный постпроцессинг рендерера выключен. RulesConfig.glassesTintEnabled выключает оттенок
    // целиком, если на Quest 2 он не держит 72 Гц.
    public sealed class GlassesTint : MonoBehaviour
    {
        [Tooltip("Компонент с IWearables")]
        [SerializeField] private MonoBehaviour wearables;
        [SerializeField] private Volume volume;
        [SerializeField] private RulesConfig rules;

        private IWearables _wearables;
        private float _target;

        public float Weight => volume != null ? volume.weight : 0f;

        private bool Enabled => rules == null || rules.glassesTintEnabled;
        private float Seconds => rules != null ? rules.glassesTintSeconds : 0.2f;

        public void Configure(IWearables source, Volume tintVolume, RulesConfig rulesConfig)
        {
            OnDisable();
            wearables = source as MonoBehaviour;
            _wearables = source; volume = tintVolume; rules = rulesConfig;
            OnEnable();
        }

        private void OnEnable()
        {
            _wearables ??= wearables as IWearables;
            if (_wearables != null) _wearables.Changed += Refresh;
            Refresh();
            if (volume != null) volume.weight = _target;
        }

        private void OnDisable()
        {
            if (_wearables != null) _wearables.Changed -= Refresh;
        }

        private void Refresh() => _target = Enabled && _wearables != null && _wearables.GlassesOn ? 1f : 0f;

        private void Update()
        {
            if (volume == null) return;
            float step = Seconds > 0f ? Time.deltaTime / Seconds : 1f;
            volume.weight = Mathf.MoveTowards(volume.weight, _target, step);
            volume.enabled = volume.weight > 0f;   // выключенный Volume не стоит ничего
        }
    }
}
