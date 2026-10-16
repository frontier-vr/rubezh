using Rubezh.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Rubezh.XR
{
    // Пульт подачи мишени O-25 (NG-12). Кнопка и регулятор — XRSimpleInteractable с XRPokeFilter:
    // нажатие пальцем. Регулятор щёлкает дистанции 3 → 5 → 7 → 9 м по кругу и поворачивается на
    // свою риску, табличка у него показывает выбранную дистанцию. Кнопка подаёт мишень.
    // Видимая часть кнопки отдельно от коллайдера нажатия: её двигает XRPokeFollowAffordance.
    public sealed class TargetRemote : MonoBehaviour
    {
        [SerializeField] private TargetCarriage target;
        [SerializeField] private XRSimpleInteractable button;
        [SerializeField] private XRSimpleInteractable dial;
        [Tooltip("Видимая ручка регулятора: поворачивается на риску дистанции")]
        [SerializeField] private Transform dialVisual;
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private float degreesPerStep = 60f;

        public void Configure(TargetCarriage carriage, XRSimpleInteractable supplyButton, XRSimpleInteractable distanceDial,
                              Transform dialKnob, TMP_Text label)
        {
            Unhook();
            target = carriage; button = supplyButton; dial = distanceDial; dialVisual = dialKnob; distanceLabel = label;
            Hook();
            Refresh(target != null ? target.Distance : 0f);
        }

        private void OnEnable() => Hook();
        private void OnDisable() => Unhook();
        private void Start() => Refresh(target != null ? target.Distance : 0f);

        private void Hook()
        {
            Unhook();
            if (button != null) button.selectEntered.AddListener(OnButton);
            if (dial != null) dial.selectEntered.AddListener(OnDial);
            if (target != null) target.DistanceChanged += Refresh;
        }

        private void Unhook()
        {
            if (button != null) button.selectEntered.RemoveListener(OnButton);
            if (dial != null) dial.selectEntered.RemoveListener(OnDial);
            if (target != null) target.DistanceChanged -= Refresh;
        }

        private void OnButton(SelectEnterEventArgs _) => target?.Supply();

        private void OnDial(SelectEnterEventArgs _) => target?.NextDistance();

        private void Refresh(float distance)
        {
            if (distanceLabel != null) distanceLabel.text = $"{distance:0} м";
            if (dialVisual != null && target != null)
                dialVisual.localRotation = Quaternion.Euler(0f, target.DistanceIndex * degreesPerStep, 0f);
        }
    }
}
