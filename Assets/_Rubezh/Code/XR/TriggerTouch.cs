namespace Rubezh.XR
{
    public enum TouchSource
    {
        Auto,       // ёмкостный датчик, если он есть на устройстве, иначе порог
        Sensor,     // только датчик triggerTouched
        Threshold   // только порог нажатия
    }

    // Правило «палец на спуске» без движка: отдельно, чтобы покрыть тестами.
    public static class TriggerTouch
    {
        public static bool IsTouched(TouchSource source, bool sensorAvailable, bool sensorTouched,
                                     float pressValue, float threshold)
        {
            bool byThreshold = pressValue > threshold;
            switch (source)
            {
                case TouchSource.Sensor: return sensorTouched;
                case TouchSource.Threshold: return byThreshold;
                default: return sensorAvailable ? sensorTouched || byThreshold : byThreshold;
            }
        }
    }
}
