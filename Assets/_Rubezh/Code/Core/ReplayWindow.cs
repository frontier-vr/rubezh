using System.Collections.Generic;

namespace Rubezh.Core
{
    // Окно повтора эпизода нарушения (VP-17, FR-45): немного до начала эпизода, сам эпизод и
    // немного после. Время — секунды от начала упражнения, как у ViolationRecord.
    public readonly struct ReplayWindow
    {
        public readonly double From;
        public readonly double To;

        public ReplayWindow(double from, double to)
        {
            From = from; To = to;
        }

        public double Length => To - From;

        // recordedUntil — до какого времени есть запись. Длинный эпизод обрезается до maxLength
        // с конца: момент, когда нарушение засчитано, остаётся в окне.
        public static ReplayWindow For(in ViolationRecord v, double before, double after, double maxLength, double recordedUntil)
        {
            double to = System.Math.Min(System.Math.Max(0, recordedUntil), v.Time + System.Math.Max(0, after));
            double from = System.Math.Max(0, v.Start - System.Math.Max(0, before));
            if (maxLength > 0 && to - from > maxLength) from = to - maxLength;
            if (from > to) from = to;
            return new ReplayWindow(from, to);
        }

        // Какой эпизод показать первым: грубое нарушение (из-за него упражнение остановлено),
        // иначе первое по времени. −1 — нарушений нет.
        public static int DefaultIndex(IReadOnlyList<ViolationRecord> violations)
        {
            if (violations == null || violations.Count == 0) return -1;
            for (int i = 0; i < violations.Count; i++)
                if (violations[i].Severity == Severity.Critical) return i;
            return 0;
        }

        // Время внутри окна для проигрывания по кругу: playTime — секунды с начала показа.
        public double Loop(double playTime, double pause)
        {
            double cycle = Length + System.Math.Max(0, pause);
            if (cycle <= 0) return From;
            double t = playTime % cycle;
            if (t < 0) t += cycle;
            return From + System.Math.Min(t, Length);
        }
    }
}
