using System.Collections.Generic;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Запись положений одного объекта по времени упражнения (VP-17): кольцевой буфер с ограничением
    // по числу кадров, выборка с интерполяцией между соседними кадрами.
    public sealed class PoseTrack
    {
        private readonly List<double> _times;
        private readonly List<Pose> _poses;
        private readonly List<bool> _visible;
        private readonly int _capacity;
        private int _first;   // индекс самого старого кадра, когда буфер заполнен по кругу

        public PoseTrack(int capacity)
        {
            _capacity = Mathf.Max(2, capacity);
            _times = new List<double>(_capacity);
            _poses = new List<Pose>(_capacity);
            _visible = new List<bool>(_capacity);
        }

        public int Count => _times.Count;
        public double FirstTime => Count > 0 ? _times[_first] : 0;
        public double LastTime => Count > 0 ? _times[(_first + Count - 1) % Count] : 0;

        public void Clear()
        {
            _times.Clear(); _poses.Clear(); _visible.Clear();
            _first = 0;
        }

        // Время только растёт; кадр с тем же или меньшим временем заменяет последний.
        public void Add(double time, Pose pose, bool visible = true)
        {
            if (Count > 0 && time <= LastTime)
            {
                int last = (_first + Count - 1) % Count;
                _poses[last] = pose; _visible[last] = visible;
                return;
            }
            if (Count < _capacity)
            {
                _times.Add(time); _poses.Add(pose); _visible.Add(visible);
                return;
            }
            _times[_first] = time; _poses[_first] = pose; _visible[_first] = visible;
            _first = (_first + 1) % _capacity;
        }

        // Положение в момент time: до записи — первый кадр, после — последний.
        public bool TrySample(double time, out Pose pose, out bool visible)
        {
            pose = Pose.identity; visible = false;
            int n = Count;
            if (n == 0) return false;
            if (time <= FirstTime) { pose = _poses[_first]; visible = _visible[_first]; return true; }
            if (time >= LastTime) { int l = (_first + n - 1) % n; pose = _poses[l]; visible = _visible[l]; return true; }

            int lo = 0, hi = n - 1;               // бинарный поиск по логическим индексам
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_times[(_first + mid) % n] <= time) lo = mid; else hi = mid;
            }
            int a = (_first + lo) % n, b = (_first + hi) % n;
            float k = (float)((time - _times[a]) / (_times[b] - _times[a]));
            pose = new Pose(Vector3.Lerp(_poses[a].position, _poses[b].position, k),
                            Quaternion.Slerp(_poses[a].rotation, _poses[b].rotation, k));
            visible = k < 0.5f ? _visible[a] : _visible[b];
            return true;
        }
    }
}
