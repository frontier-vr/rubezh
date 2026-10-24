using Rubezh.Core;
using Rubezh.Gameplay;
using TMPro;
using UnityEngine;

namespace Rubezh.UI
{
    // Шкала прогресса O-43 внизу табло O-31 (NG-31): 9 подписанных сегментов. Какие гореть — решает
    // ProgressSteps по событиям директора (PhaseChanged, Finished, Restarted) и оружия (Jammed,
    // устранение задержки), логики фаз здесь нет. Горящий сегмент залит светлым и подписан тёмным,
    // негорящий — тёмный с тусклой подписью: отличие яркостью и заливкой, не только цветом.
    // Материалы непрозрачные Unlit.
    public sealed class ProgressBar : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private WeaponController weapon;
        [Tooltip("Заливка сегментов слева направо, 9 шт.")]
        [SerializeField] private Renderer[] segments;
        [SerializeField] private TMP_Text[] labels;
        [SerializeField] private Material litMaterial;
        [SerializeField] private Material offMaterial;
        [SerializeField] private Color litLabel = new Color(0.063f, 0.078f, 0.094f);     // #101418
        [SerializeField] private Color offLabel = new Color(0.55f, 0.58f, 0.62f);

        private readonly ProgressSteps _steps = new ProgressSteps();
        private bool _jammed;

        public ProgressSteps Steps => _steps;
        public bool IsLit(int index) => _steps.IsLit(index);

        public void Configure(ExerciseRunner exerciseRunner, WeaponController weaponController)
        {
            Unhook();
            runner = exerciseRunner; weapon = weaponController;
            Hook();
        }

        private void Awake()
        {
            _steps.Lit += Paint;
            _steps.Reset += PaintAll;
            for (int i = 0; i < ProgressSteps.Count && labels != null && i < labels.Length; i++)
                if (labels[i] != null) labels[i].text = ProgressSteps.Names[i];
            PaintAll();
        }

        private void OnEnable() => Hook();
        private void OnDisable() => Unhook();

        private void Hook()
        {
            Unhook();
            if (runner != null)
            {
                runner.PhaseChanged += _steps.OnPhaseChanged;
                runner.Restarted += OnRestarted;
            }
            if (weapon != null)
            {
                weapon.Jammed += OnJammed;
                weapon.Changed += OnWeaponChanged;
            }
        }

        private void Unhook()
        {
            if (runner != null)
            {
                runner.PhaseChanged -= _steps.OnPhaseChanged;
                runner.Restarted -= OnRestarted;
            }
            if (weapon != null)
            {
                weapon.Jammed -= OnJammed;
                weapon.Changed -= OnWeaponChanged;
            }
        }

        private void OnRestarted()
        {
            _jammed = false;
            _steps.OnRestart();
        }

        private void OnJammed(Jam jam)
        {
            if (runner != null && runner.Director != null && runner.Phase != Phase.Fire) return;
            _jammed = true;
            _steps.OnJammed();
        }

        private void OnWeaponChanged(WeaponState state)
        {
            if (!_jammed || state.Jam != Jam.None) return;
            _jammed = false;
            _steps.OnJamCleared();
        }

        private void PaintAll()
        {
            for (int i = 0; i < ProgressSteps.Count; i++) Paint(i);
        }

        private void Paint(int index)
        {
            bool lit = _steps.IsLit(index);
            if (segments != null && index < segments.Length && segments[index] != null)
                segments[index].sharedMaterial = lit ? litMaterial : offMaterial;
            if (labels != null && index < labels.Length && labels[index] != null)
            {
                labels[index].color = lit ? litLabel : offLabel;
                labels[index].fontStyle = lit ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
