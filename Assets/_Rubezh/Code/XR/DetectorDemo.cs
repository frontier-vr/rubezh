using Rubezh.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rubezh.XR
{
    // Демо детекторов для финальной защиты (VP-18): то, что система «видит», становится видно
    // и пользователю. Луч сектора из дульного среза до первой поверхности — зелёный в секторе,
    // красный вне его; конус взгляда в ствол — белый, красный, когда камера в нём; табличка с
    // тремя состояниями над пистолетом; пистолет не в руке — луч белый, сектор не судится.
    // Включается кнопкой Menu левого контроллера (в редакторе — F2), в обычном упражнении
    // выключено. На правила и счёт не влияет: только читает детекторы.
    public sealed class DetectorDemo : MonoBehaviour
    {
        [SerializeField] private SectorDetector sector;
        [SerializeField] private TriggerDisciplineDetector trigger;
        [SerializeField] private MuzzleLookDetector look;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform head;
        [Tooltip("Над чем висит табличка состояний: пистолет — туда и так смотрят")]
        [SerializeField] private Transform labelAnchor;
        [SerializeField] private GameObject view;
        [SerializeField] private LineRenderer sectorRay;
        [SerializeField] private LineRenderer lookCone;
        [SerializeField] private TMP_Text label;
        [SerializeField] private bool visibleOnStart;

        private const int ConeSegments = 16;
        private static readonly Color Green = new Color(0.243f, 0.620f, 0.310f);   // #3E9E4F
        private static readonly Color Red = new Color(0.824f, 0.227f, 0.165f);     // #D23A2A
        private static readonly Color Light = new Color(0.949f, 0.941f, 0.918f);   // #F2F0EA

        private InputAction _toggle;

        public bool Visible => view != null && view.activeSelf;

        public void Configure(SectorDetector sectorDetector, TriggerDisciplineDetector triggerDetector,
                              MuzzleLookDetector lookDetector, RulesConfig rulesConfig, Transform muzzlePoint, Transform headCamera)
        {
            sector = sectorDetector; trigger = triggerDetector; look = lookDetector;
            rules = rulesConfig; muzzle = muzzlePoint; head = headCamera;
        }

        public void ConfigureView(GameObject root, LineRenderer ray, LineRenderer cone, TMP_Text text, Transform anchor)
        {
            view = root; sectorRay = ray; lookCone = cone; label = text; labelAnchor = anchor;
        }

        private void Awake() => SetVisible(visibleOnStart);

        private void OnEnable()
        {
            _toggle = new InputAction("DetectorDemo", InputActionType.Button);
            _toggle.AddBinding("<XRController>{LeftHand}/menuButton");
            _toggle.AddBinding("<Keyboard>/f2");
            _toggle.performed += OnToggle;
            _toggle.Enable();
        }

        private void OnDisable()
        {
            if (_toggle == null) return;
            _toggle.performed -= OnToggle;
            _toggle.Dispose();
            _toggle = null;
        }

        private void OnToggle(InputAction.CallbackContext _) => SetVisible(!Visible);

        public void SetVisible(bool on)
        {
            if (view != null) view.SetActive(on);
        }

        private void LateUpdate()
        {
            if (!Visible || muzzle == null) return;
            DrawSector();
            DrawCone();
            if (label != null) label.text = Text();
            PlaceLabel();
        }

        private void DrawSector()
        {
            if (sectorRay == null || sector == null) return;
            bool hit = sector.Probe(out var point);
            sectorRay.positionCount = 2;
            sectorRay.SetPosition(0, muzzle.position);
            sectorRay.SetPosition(1, hit ? point : muzzle.position + muzzle.forward * 20f);
            var c = !sector.IsHeld ? Light : sector.InSector ? Green : Red;
            sectorRay.startColor = sectorRay.endColor = c;
        }

        // Конус взгляда: окружность на дальности луча и четыре образующие от дульного среза.
        private void DrawCone()
        {
            if (lookCone == null) return;
            float distance = rules != null ? rules.muzzleLookMaxDistance : 0.7f;
            float angle = rules != null ? rules.muzzleLookConeDegrees : 30f;
            float radius = distance * Mathf.Tan(angle * Mathf.Deg2Rad);
            var center = muzzle.position + muzzle.forward * distance;
            // Обход: окружность, затем к вершине и обратно по четырём образующим.
            lookCone.positionCount = ConeSegments + 1 + 4 * 2;
            int k = 0;
            for (int i = 0; i <= ConeSegments; i++)
            {
                float a = 2f * Mathf.PI * i / ConeSegments;
                lookCone.SetPosition(k++, center + (muzzle.right * Mathf.Cos(a) + muzzle.up * Mathf.Sin(a)) * radius);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.5f * i;
                lookCone.SetPosition(k++, muzzle.position);
                lookCone.SetPosition(k++, center + (muzzle.right * Mathf.Cos(a) + muzzle.up * Mathf.Sin(a)) * radius);
            }
            var c = look != null && look.IsLooking ? Red : Light;
            lookCone.startColor = lookCone.endColor = c;
        }

        public string Text()
        {
            string s = sector == null ? "—" : !sector.IsHeld ? "не в руке"
                : sector.InSector ? "<color=#3E9E4F>в секторе</color>" : "<color=#D23A2A>вне сектора</color>";
            string t = trigger == null ? "—" : trigger.FingerOnTrigger ? "<color=#D23A2A>на спуске</color>" : "<color=#3E9E4F>вдоль рамки</color>";
            string l = look == null ? "—" : look.IsLooking ? "<color=#D23A2A>в ствол</color>" : "<color=#3E9E4F>нет</color>";
            return $"Ствол: {s}\nПалец: {t}\nВзгляд: {l}";
        }

        private void PlaceLabel()
        {
            if (label == null || labelAnchor == null) return;
            var t = label.transform;
            t.position = labelAnchor.position + Vector3.up * 0.12f;
            if (head != null)
            {
                var toLabel = t.position - head.position;
                if (toLabel.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(toLabel, Vector3.up);
            }
        }
    }
}
