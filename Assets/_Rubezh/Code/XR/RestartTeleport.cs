using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Rubezh.XR
{
    // «Повторить» (VP-20): после Restart пользователь телепортируется на A1 лицом к шкафу.
    // Предметы и СИЗ возвращают на места их ItemReturn, шкалу O-43 обнуляет NG-31.
    public sealed class RestartTeleport : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private TeleportationProvider teleportation;
        [Tooltip("Якорь A1: позиция и направление взгляда (−X, к шкафу)")]
        [SerializeField] private Transform anchorA1;

        public int Requests { get; private set; }

        public void Configure(ExerciseRunner exerciseRunner, TeleportationProvider provider, Transform a1)
        {
            if (runner != null) runner.Restarted -= OnRestarted;
            runner = exerciseRunner; teleportation = provider; anchorA1 = a1;
            if (runner != null && isActiveAndEnabled) runner.Restarted += OnRestarted;
        }

        private void OnEnable()
        {
            if (runner != null) runner.Restarted += OnRestarted;
        }

        private void OnDisable()
        {
            if (runner != null) runner.Restarted -= OnRestarted;
        }

        private void OnRestarted()
        {
            if (anchorA1 == null) return;
            Requests++;
            if (teleportation == null) return;
            teleportation.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = anchorA1.position,
                destinationRotation = anchorA1.rotation,
                matchOrientation = MatchOrientation.TargetUpAndForward
            });
        }
    }
}
