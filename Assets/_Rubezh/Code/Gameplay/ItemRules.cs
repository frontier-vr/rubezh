using System;

namespace Rubezh.Gameplay
{
    // Упавший предмет (NG-09): не в руке и ниже столешницы дольше задержки — пора вернуть на место.
    public sealed class DropWatch
    {
        private readonly double _delay;
        private double _since = -1;

        public DropWatch(double delaySeconds)
        {
            if (delaySeconds < 0) throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            _delay = delaySeconds;
        }

        public bool IsWaiting => _since >= 0;

        // true один раз, когда предмет пролежал «упавшим» delay секунд подряд.
        public bool Tick(double now, bool loose)
        {
            if (!loose) { _since = -1; return false; }
            if (_since < 0) _since = now;
            if (now - _since < _delay) return false;
            _since = -1;
            return true;
        }
    }

    // Лоток O-18 (NG-09): счёт серии. Все патроны серии лежат в лотке сразу; выданный и ещё не
    // вставленный патрон (лежит в лотке или в руке) — «на руках»; вставленный в магазин — «вставлен»;
    // упавший уходит в пул, и лоток выдаёт его заново в свободное гнездо.
    public sealed class RoundDispenser
    {
        private readonly int _series;

        public RoundDispenser(int series)
        {
            if (series < 1) throw new ArgumentOutOfRangeException(nameof(series));
            _series = series;
        }

        public int Seated { get; private set; }
        public int Out { get; private set; }

        // Сколько патронов выдать сейчас: всё, чего не хватает до серии, но не больше свободных гнёзд.
        public int ToSpawn(int freeSlots) => Math.Max(0, Math.Min(freeSlots, _series - Seated - Out));

        public void Spawned() => Out++;

        public void Seat()
        {
            if (Out > 0) Out--;
            Seated++;
        }

        public void Lost()
        {
            if (Out > 0) Out--;
        }

        public void Reset() => Seated = Out = 0;
    }
}
