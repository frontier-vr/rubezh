using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace Rubezh.XR
{
    // Комфорт перемещения (YG-16, docs/comfort.md): после телепорта и поворота щелчками пользователь
    // не должен теряться. Самое дешёвое средство — короткий звук перемещения (по умолчанию);
    // тоннельная виньетка из Starter Assets — только если звука мало: она рисует полупрозрачную
    // полусферу, нужен замер и исключение в ТЗ §7.3. Виньетку включаем и выключаем целым объектом
    // (SetActive), иначе по документации XRI остаётся Mesh Renderer и она может «залипнуть».
    public sealed class LocomotionComfort : MonoBehaviour
    {
        public enum Mode { None, Sound, Vignette, SoundAndVignette }

        [SerializeField] private Mode mode = Mode.Sound;
        [SerializeField] private LocomotionProvider teleport;
        [SerializeField] private LocomotionProvider snapTurn;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip teleportClip;
        [SerializeField] private AudioClip snapTurnClip;
        [Tooltip("TunnelingVignette из Starter Assets — дочерний объект Main Camera")]
        [SerializeField] private GameObject vignette;

        public Mode Current => mode;
        public int Sounds { get; private set; }

        public bool SoundOn => mode == Mode.Sound || mode == Mode.SoundAndVignette;
        public bool VignetteOn => mode == Mode.Vignette || mode == Mode.SoundAndVignette;

        public void Configure(LocomotionProvider teleportProvider, LocomotionProvider turnProvider, AudioSource audioSource,
                              AudioClip teleportSound, AudioClip turnSound, GameObject vignetteObject)
        {
            OnDisable();
            teleport = teleportProvider; snapTurn = turnProvider; source = audioSource;
            teleportClip = teleportSound; snapTurnClip = turnSound; vignette = vignetteObject;
            OnEnable();
            Apply();
        }

        public void SetMode(Mode value)
        {
            mode = value;
            Apply();
        }

        private void Start() => Apply();

        private void OnEnable()
        {
            if (teleport != null) teleport.locomotionEnded += OnTeleported;
            if (snapTurn != null) snapTurn.locomotionEnded += OnTurned;
        }

        private void OnDisable()
        {
            if (teleport != null) teleport.locomotionEnded -= OnTeleported;
            if (snapTurn != null) snapTurn.locomotionEnded -= OnTurned;
        }

        private void Apply()
        {
            if (vignette != null && vignette.activeSelf != VignetteOn) vignette.SetActive(VignetteOn);
        }

        private void OnTeleported(LocomotionProvider provider) => Play(teleportClip);
        private void OnTurned(LocomotionProvider provider) => Play(snapTurnClip);

        public void Play(AudioClip clip)
        {
            if (!SoundOn || clip == null || source == null) return;
            source.PlayOneShot(clip);
            Sounds++;
        }

        // Для песочницы и тестов: звук, как будто перемещение закончилось.
        public void SimulateTeleport() => OnTeleported(teleport);
        public void SimulateTurn() => OnTurned(snapTurn);
    }
}
