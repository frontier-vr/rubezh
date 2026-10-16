using System;
using Rubezh.Core;

namespace Rubezh.Gameplay
{
    // Затвор рукой (NG-08): ход руки назад вдоль оси затвора двигает затвор; доведён до доли хода —
    // SlideBack, вернулся вперёд рукой или отпущен — SlideForward. Затвор с задержки снимается так же:
    // довести и отпустить (если патронов нет, автомат оставит его на задержке).
    public sealed class SlidePull
    {
        private readonly float _travel;
        private readonly float _backAt;
        private readonly float _forwardAt;
        private float _start;
        private bool _back;

        public SlidePull(float travel, float backFraction, float forwardFraction)
        {
            if (travel <= 0f) throw new ArgumentOutOfRangeException(nameof(travel));
            _travel = travel;
            _backAt = travel * backFraction;
            _forwardAt = travel * forwardFraction;
        }

        public float Offset { get; private set; }   // 0 — затвор впереди, travel — до упора назад
        public bool IsBack => _back;

        // Рука взялась за затвор; startOffset — где затвор сейчас (на задержке он уже сзади).
        public void Begin(float startOffset)
        {
            _start = Clamp(startOffset);
            Offset = _start;
            _back = false;
        }

        // pull — насколько рука ушла назад от точки захвата, м.
        public WeaponCommand? Drag(float pull)
        {
            Offset = Clamp(_start + pull);
            if (!_back && Offset >= _backAt)
            {
                _back = true;
                return WeaponCommand.SlideBack;
            }
            if (_back && Offset <= _forwardAt)
            {
                _back = false;
                return WeaponCommand.SlideForward;
            }
            return null;
        }

        public WeaponCommand? Release()
        {
            Offset = 0f;
            if (!_back) return null;
            _back = false;
            return WeaponCommand.SlideForward;
        }

        private float Clamp(float v) => Math.Max(0f, Math.Min(_travel, v));
    }

    // Спуск с гистерезисом: дожал выше порога — TriggerPull, отпустил ниже второго — TriggerRelease.
    public sealed class TriggerSqueeze
    {
        private readonly float _pullAt;
        private readonly float _releaseAt;

        public TriggerSqueeze(float pullThreshold, float releaseThreshold)
        {
            if (releaseThreshold >= pullThreshold) throw new ArgumentException("порог отпускания должен быть ниже порога выстрела");
            _pullAt = pullThreshold;
            _releaseAt = releaseThreshold;
        }

        public bool IsPulled { get; private set; }

        public WeaponCommand? Update(float press)
        {
            if (!IsPulled && press >= _pullAt) { IsPulled = true; return WeaponCommand.TriggerPull; }
            if (IsPulled && press <= _releaseAt) { IsPulled = false; return WeaponCommand.TriggerRelease; }
            return null;
        }

        // Рука отпустила пистолет с зажатым спуском.
        public WeaponCommand? Reset()
        {
            if (!IsPulled) return null;
            IsPulled = false;
            return WeaponCommand.TriggerRelease;
        }
    }
}
