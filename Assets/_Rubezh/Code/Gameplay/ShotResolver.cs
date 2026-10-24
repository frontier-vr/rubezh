using System;
using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Выстрел: на событие Fired оружия — один луч из Muzzle вдоль её +Z. Первое попадание в слой
    // Target — Hit(точка в локальных координатах плоскости мишени), по нему пробоины (VP-23) и точность.
    // Выстрел до «Огонь», после «Стой» и вне сектора — грубые нарушения (FR-35, FR-36).
    // Иерархия мишени: TargetMechanism/Carriage/TargetFrame/TargetPlane.
    public sealed class ShotResolver : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private Transform muzzle;
        [SerializeField] private SectorDetector sector;
        [SerializeField] private LayerMask targetLayers;
        [Tooltip("Что останавливает пулю раньше мишени: щиты, пол, стойка, пулеулавливатель")]
        [SerializeField] private LayerMask blockerLayers;
        [SerializeField, Min(1f)] private float maxDistance = 30f;

        private IWeapon _weapon;

        // Точка попадания в локальных координатах TargetPlane и сама плоскость.
        public event Action<Vector3, Transform> Hit;
        public event Action<Violation> GrossShot;

        public int Shots { get; private set; }
        public int Hits { get; private set; }

        public void Configure(ExerciseRunner exerciseRunner, Transform muzzlePoint, SectorDetector sectorDetector,
                              LayerMask targets, LayerMask blockers)
        {
            runner = exerciseRunner;
            muzzle = muzzlePoint;
            sector = sectorDetector;
            targetLayers = targets;
            blockerLayers = blockers;
        }

        // Подписаться на выстрелы оружия. Вызывает интерактивный пистолет (NG-08) при создании Weapon.
        public void Bind(IWeapon weapon)
        {
            if (_weapon != null) _weapon.Fired -= OnFired;
            _weapon = weapon;
            if (_weapon != null) _weapon.Fired += OnFired;
        }

        private void Awake()
        {
            if (runner != null) runner.Restarted += ResetCounters;
        }

        private void OnDestroy()
        {
            Bind(null);
            if (runner != null) runner.Restarted -= ResetCounters;
        }

        private void ResetCounters() => Shots = Hits = 0;

        private void OnFired()
        {
            if (runner == null || muzzle == null) return;
            var director = runner.Director;
            Shots++;

            bool hit = Physics.Raycast(muzzle.position, muzzle.forward, out var info, maxDistance,
                                       targetLayers | blockerLayers, QueryTriggerInteraction.Ignore)
                       && (targetLayers.value & (1 << info.collider.gameObject.layer)) != 0;
            if (hit)
            {
                Hits++;
                Hit?.Invoke(info.transform.InverseTransformPoint(info.point), info.transform);
            }
            director.RegisterShot(hit);

            bool inSector = sector == null || sector.CheckNow();
            var gross = ShotRule.Classify(director.Phase, inSector);
            if (gross.HasValue)
            {
                GrossShot?.Invoke(gross.Value);
                director.Raise(gross.Value, Severity.Critical);
            }
        }
    }
}
