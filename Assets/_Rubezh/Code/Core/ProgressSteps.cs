using System;

namespace Rubezh.Core
{
    // Шкала прогресса O-43 (NG-31, docs/user-flow.md §6): девять шагов, каждый загорается, когда шаг
    // закрыт. Чистая таблица «событие директора → сегмент»; рисует её ProgressBar в UI.
    // На Failed шкала замирает, на Restart обнуляется.
    public sealed class ProgressSteps
    {
        public const int Count = 9;

        public static readonly string[] Names =
            { "Получение", "Осмотр", "СИЗ", "Снаряжение", "Рубеж", "Стрельба", "Задержка", "Разряжание", "Сдача" };

        private readonly bool[] _lit = new bool[Count];
        private bool _frozen;
        private bool _jammed;

        public event Action<int> Lit;          // номер сегмента 0..8
        public event Action Reset;

        public bool IsLit(int index) => _lit[index];
        public bool Frozen => _frozen;

        public int LitCount
        {
            get
            {
                int n = 0;
                foreach (var l in _lit) if (l) n++;
                return n;
            }
        }

        public void OnPhaseChanged(Phase from, Phase to)
        {
            if (_frozen) return;
            if (to == Phase.Failed)
            {
                _frozen = true;
                return;
            }
            switch (from)
            {
                case Phase.Receive: Light(0); break;
                case Phase.Inspect: Light(1); break;
                case Phase.Ppe: Light(2); break;
                case Phase.LoadMagazine: Light(3); break;
                case Phase.ToLine: Light(4); break;
                case Phase.Fire when to == Phase.Cease:
                    Light(5);                  // без осечки «Стрельба» и «Задержка» — на переходе в «Стой»
                    Light(6);
                    break;
                case Phase.ShowClear: Light(7); break;
                case Phase.Return: Light(8); break;
            }
        }

        // Задержка в стрельбе: сегмент 6 «Стрельба» закрыт, ждём устранения.
        public void OnJammed()
        {
            if (_frozen) return;
            _jammed = true;
            Light(5);
        }

        // Задержка устранена: сегмент 7 «Задержка».
        public void OnJamCleared()
        {
            if (_frozen || !_jammed) return;
            _jammed = false;
            Light(6);
        }

        public void OnRestart()
        {
            Array.Clear(_lit, 0, Count);
            _frozen = false;
            _jammed = false;
            Reset?.Invoke();
        }

        private void Light(int index)
        {
            if (_lit[index]) return;
            _lit[index] = true;
            Lit?.Invoke(index);
        }
    }
}
