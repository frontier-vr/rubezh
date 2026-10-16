using System;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Предмет с местами (NG-09): знает, где он (IPlaced), и возвращается сам. Упал — не в руке и ниже
    // столешницы dropReturnSeconds подряд — встаёт на своё место у текущего якоря, без штрафа
    // (ошибка управления, objects.md). На Restart — на стартовое место.
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ItemReturn : MonoBehaviour, IPlaced
    {
        [Serializable]
        private struct Home
        {
            public Anchor anchor;
            public PlaceSocket place;
        }

        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private AnchorLocator anchors;
        [Tooltip("Стартовое место: пистолет — шкаф O-05, магазин — шахта пистолета")]
        [SerializeField] private PlaceSocket start;
        [SerializeField] private Home[] homes = Array.Empty<Home>();

        private XRGrabInteractable _grab;
        private DropWatch _drop;
        private Rigidbody _body;

        public int Returns { get; private set; }

        // Предмет ставится на место системой (возврат, Restart), а не рукой: шахта не сообщает
        // директору InsertMagazine, иначе Restart дал бы StepOrder.
        public static bool IsPlacing { get; private set; }

        private float DropHeight => rules != null ? rules.dropHeight : 0.85f;

        public Place Place
        {
            get
            {
                if (_grab == null || !_grab.isSelected)
                    return transform.position.y < DropHeight ? Place.Floor : Place.Unknown;
                var by = _grab.interactorsSelecting[0];
                if (by is XRSocketInteractor socket)
                {
                    var place = socket.GetComponent<PlaceSocket>();
                    return place != null ? place.Place : Place.Unknown;
                }
                return Place.Hand;
            }
        }

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, AnchorLocator locator, PlaceSocket startPlace)
        {
            if (runner != null) runner.Restarted -= ToStart;
            runner = exerciseRunner;
            rules = rulesConfig;
            anchors = locator;
            start = startPlace;
            _drop = NewWatch();
            if (runner != null && isActiveAndEnabled) runner.Restarted += ToStart;
        }

        public void SetHome(Anchor anchor, PlaceSocket place)
        {
            for (int i = 0; i < homes.Length; i++)
                if (homes[i].anchor == anchor) { homes[i].place = place; return; }
            Array.Resize(ref homes, homes.Length + 1);
            homes[homes.Length - 1] = new Home { anchor = anchor, place = place };
        }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _body = GetComponent<Rigidbody>();
            _drop = NewWatch();
        }

        private void OnEnable()
        {
            if (runner != null) runner.Restarted += ToStart;
        }

        private void OnDisable()
        {
            if (runner != null) runner.Restarted -= ToStart;
        }

        private void Update()
        {
            bool loose = !_grab.isSelected && transform.position.y < DropHeight;
            if (_drop.Tick(Time.timeAsDouble, loose)) ReturnHome();
        }

        public void ReturnHome()
        {
            var anchor = anchors != null ? anchors.Current : Anchor.A1;
            PlaceSocket target = start;
            foreach (var home in homes)
                if (home.anchor == anchor && home.place != null) { target = home.place; break; }
            if (PutInto(target)) Returns++;
        }

        public void ToStart() => PutInto(start);

        // Поставить в сокет места: отпустить из рук и других сокетов, выровнять по точке крепления.
        public bool PutInto(PlaceSocket place)
        {
            if (place == null) return false;
            var socket = place.Socket;
            var manager = _grab.interactionManager;
            if (manager == null) return false;
            if (socket.hasSelection)
            {
                if (socket.IsSelecting(_grab)) return true;
                return false;
            }
            for (int i = _grab.interactorsSelecting.Count - 1; i >= 0; i--)
                manager.SelectExit(_grab.interactorsSelecting[i], _grab);

            var socketAttach = socket.GetAttachTransform(_grab);
            var mine = _grab.GetAttachTransform(socket);
            var localRot = Quaternion.Inverse(transform.rotation) * mine.rotation;
            var localPos = transform.InverseTransformPoint(mine.position);
            var rot = socketAttach.rotation * Quaternion.Inverse(localRot);
            var pos = socketAttach.position - rot * Vector3.Scale(localPos, transform.lossyScale);
            if (_body != null)
            {
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }
                _body.position = pos;
                _body.rotation = rot;
            }
            transform.SetPositionAndRotation(pos, rot);
            Physics.SyncTransforms();
            socket.socketActive = true;   // система ставит на место и в запертую шахту (Restart сразу после выпуска)
            IsPlacing = true;
            try { manager.SelectEnter((IXRSelectInteractor)socket, _grab); }
            finally { IsPlacing = false; }
            return socket.IsSelecting(_grab);
        }

        private DropWatch NewWatch() => new DropWatch(rules != null ? rules.dropReturnSeconds : 1f);
    }
}
