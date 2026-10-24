using System;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // СИЗ на голове (NG-10): слоты у головы — сокеты на камере, предмет садится по своей пустышке
    // HeadAnchor. Наушники надеты — переход на снапшот микшера «Наушники» (SFX и Ambience глуше,
    // Voice не меняется), сняты — «Уши открыты»; только переходы снапшотов, без exposed-параметров
    // из кода. Очки надеты — стёкла прячутся, в поле зрения остаётся оправа. Снять — Grip у головы.
    public sealed class Wearables : MonoBehaviour, IWearables
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private XRSocketInteractor earmuffsSlot;
        [SerializeField] private XRSocketInteractor glassesSlot;
        [SerializeField] private AudioMixerSnapshot earsOpen;
        [SerializeField] private AudioMixerSnapshot earmuffs;

        public bool EarmuffsOn => earmuffsSlot != null && earmuffsSlot.hasSelection;
        public bool GlassesOn => glassesSlot != null && glassesSlot.hasSelection;
        public AudioMixerSnapshot CurrentSnapshot { get; private set; }

        public event Action Changed;

        private float TransitionSeconds => rules != null ? rules.earmuffsTransitionSeconds : 0.4f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, XRSocketInteractor earmuffsSocket,
                              XRSocketInteractor glassesSocket, AudioMixerSnapshot open, AudioMixerSnapshot muffled)
        {
            Unsubscribe();
            runner = exerciseRunner;
            rules = rulesConfig;
            earmuffsSlot = earmuffsSocket;
            glassesSlot = glassesSocket;
            earsOpen = open;
            earmuffs = muffled;
            Subscribe();
            ApplySnapshot(0f);
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Start() => ApplySnapshot(0f);

        private void Subscribe()
        {
            Unsubscribe();
            if (earmuffsSlot != null)
            {
                earmuffsSlot.selectEntered.AddListener(OnEarmuffsOn);
                earmuffsSlot.selectExited.AddListener(OnEarmuffsOff);
            }
            if (glassesSlot != null)
            {
                glassesSlot.selectEntered.AddListener(OnGlassesOn);
                glassesSlot.selectExited.AddListener(OnGlassesOff);
            }
        }

        private void Unsubscribe()
        {
            if (earmuffsSlot != null)
            {
                earmuffsSlot.selectEntered.RemoveListener(OnEarmuffsOn);
                earmuffsSlot.selectExited.RemoveListener(OnEarmuffsOff);
            }
            if (glassesSlot != null)
            {
                glassesSlot.selectEntered.RemoveListener(OnGlassesOn);
                glassesSlot.selectExited.RemoveListener(OnGlassesOff);
            }
        }

        private void OnEarmuffsOn(SelectEnterEventArgs args)
        {
            // Чашки у ушей камера не видит; прячем, чтобы не резались ближней плоскостью.
            SetVisible(args.interactableObject.transform, false, lensesOnly: false);
            Report(ExerciseAction.PutOnEarmuffs);
            ApplySnapshot(TransitionSeconds);
            Changed?.Invoke();
        }

        private void OnEarmuffsOff(SelectExitEventArgs args)
        {
            SetVisible(args.interactableObject.transform, true, lensesOnly: false);
            ApplySnapshot(TransitionSeconds);
            Changed?.Invoke();
        }

        private void OnGlassesOn(SelectEnterEventArgs args)
        {
            SetVisible(args.interactableObject.transform, false, lensesOnly: true);
            Report(ExerciseAction.PutOnGlasses);
            Changed?.Invoke();
        }

        private void OnGlassesOff(SelectExitEventArgs args)
        {
            SetVisible(args.interactableObject.transform, true, lensesOnly: true);
            Changed?.Invoke();
        }

        private void Report(ExerciseAction action)
        {
            if (runner != null && !ItemReturn.IsPlacing) runner.ReportAction(action);
        }

        private void ApplySnapshot(float seconds)
        {
            var target = EarmuffsOn ? earmuffs : earsOpen;
            if (target == null || target == CurrentSnapshot) return;
            CurrentSnapshot = target;
            target.TransitionTo(seconds);
        }

        // Стёкла непрозрачные: на голове их не видно, видна оправа (objects.md §5).
        private static void SetVisible(Transform item, bool visible, bool lensesOnly)
        {
            foreach (var r in item.GetComponentsInChildren<Renderer>(true))
                if (!lensesOnly || r.name.Contains("Lens")) r.enabled = visible;
        }
    }
}
