using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Импульсы вибрации рукам (NG-15): в контроллер руки, которая держит предмет (сокеты не в счёт).
    // HapticImpulsePlayer контроллера задаёт путь к устройству; без шлема импульс уходит в пустоту,
    // а событие Sent позволяет проверить его в тестах.
    public static class Haptics
    {
        public static event Action<IXRInteractor, float, float> Sent;

        // Всем рукам, которые держат предмет.
        public static int ToHolders(IXRSelectInteractable item, float amplitude, float duration)
        {
            if (item == null) return 0;
            int n = 0;
            foreach (var interactor in item.interactorsSelecting)
            {
                if (interactor is XRSocketInteractor) continue;
                To(interactor, amplitude, duration);
                n++;
            }
            return n;
        }

        public static void To(IXRInteractor interactor, float amplitude, float duration)
        {
            if (interactor == null) return;
            var player = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null) player.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
            Sent?.Invoke(interactor, amplitude, duration);
        }
    }
}
