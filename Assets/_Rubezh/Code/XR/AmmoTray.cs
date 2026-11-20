using System.Collections.Generic;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Лоток O-18 (NG-09): вся серия патронов лежит в лотке сразу — seriesRounds гнёзд в ряд, брать
    // можно любой и не ждать следующего. Гнёзда — копии сокета spawnSocket, ряд идёт через его место
    // (пустышка Spawn) с шагом roundSlotSpacing. Счёт серии ведёт RoundDispenser: вставленный в
    // магазин патрон уходит в пул и не возвращается, упавший уходит в пул и появляется в свободном
    // гнезде заново. Restart — все патроны в пул, лоток снова полный.
    public sealed class AmmoTray : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [Tooltip("Сокет в середине ряда; остальные гнёзда лоток делает его копиями")]
        [SerializeField] private XRSocketInteractor spawnSocket;
        [SerializeField] private GameObject roundPrefab;
        [Tooltip("Куда идёт ряд гнёзд, в осях родителя сокета: в Main.unity лоток вытянут вдоль Z")]
        [SerializeField] private Vector3 rowDirection = Vector3.forward;

        private readonly List<XRGrabInteractable> _active = new List<XRGrabInteractable>();
        private readonly Stack<XRGrabInteractable> _pool = new Stack<XRGrabInteractable>();
        private readonly List<XRSocketInteractor> _slots = new List<XRSocketInteractor>();
        private RoundDispenser _dispenser;

        public int Seated => _dispenser.Seated;
        public int Out => _dispenser.Out;
        public int Pooled => _pool.Count;
        public IReadOnlyList<XRGrabInteractable> Active => _active;
        public IReadOnlyList<XRSocketInteractor> Slots => _slots;

        // Патрон, который лежит в лотке (первый по ряду); null — лоток пуст.
        public XRGrabInteractable Waiting
        {
            get
            {
                foreach (var slot in _slots)
                    if (slot != null && slot.hasSelection) return slot.firstInteractableSelected as XRGrabInteractable;
                return null;
            }
        }

        // Сколько патронов сейчас лежит в лотке.
        public int WaitingCount
        {
            get
            {
                int n = 0;
                foreach (var slot in _slots)
                    if (slot != null && slot.hasSelection) n++;
                return n;
            }
        }

        private int Series => rules != null ? rules.seriesRounds : 5;
        private float DropHeight => rules != null ? rules.dropHeight : 0.85f;
        private float Spacing => rules != null ? rules.roundSlotSpacing : 0.06f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, XRSocketInteractor socket, GameObject prefab)
        {
            if (runner != null) runner.Restarted -= ResetTray;
            runner = exerciseRunner;
            rules = rulesConfig;
            spawnSocket = socket;
            roundPrefab = prefab;
            _dispenser = NewDispenser();
            _slots.Clear();
            if (runner != null) runner.Restarted += ResetTray;
        }

        private void Awake() => _dispenser = NewDispenser();

        private void OnEnable()
        {
            if (runner != null) runner.Restarted += ResetTray;
        }

        private void OnDisable()
        {
            if (runner != null) runner.Restarted -= ResetTray;
        }

        private void Update()
        {
            if (spawnSocket == null || roundPrefab == null || spawnSocket.interactionManager == null) return;
            if (_slots.Count == 0) BuildSlots();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var round = _active[i];
                if (!round.isSelected && round.transform.position.y < DropHeight)
                {
                    Despawn(round);
                    _dispenser.Lost();
                }
            }
            int spawn = _dispenser.ToSpawn(_slots.Count - WaitingCount);
            foreach (var slot in _slots)
            {
                if (spawn == 0) break;
                if (slot.hasSelection) continue;
                Spawn(slot);
                spawn--;
            }
        }

        // Патрон вставлен в магазин (MagazineItem).
        public void Seat(GameObject round)
        {
            var grab = round.GetComponent<XRGrabInteractable>();
            if (grab == null || !_active.Contains(grab)) { round.SetActive(false); return; }
            Despawn(grab);
            _dispenser.Seat();
        }

        // Ряд гнёзд: сокет из сцены встаёт на своё место в ряду, остальные — его копии рядом.
        private void BuildSlots()
        {
            int count = Mathf.Max(1, Series);
            var center = spawnSocket.transform.localPosition;
            var step = rowDirection.sqrMagnitude > 1e-6f ? rowDirection.normalized * Spacing : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var slot = spawnSocket;
                if (i > 0)
                {
                    slot = Instantiate(spawnSocket, spawnSocket.transform.parent);
                    slot.name = spawnSocket.name + " " + (i + 1);
                }
                slot.transform.localPosition = center + step * (i - (count - 1) * 0.5f);
                _slots.Add(slot);
            }
            Physics.SyncTransforms();
        }

        private void Spawn(XRSocketInteractor slot)
        {
            var round = _pool.Count > 0 ? _pool.Pop() : Instantiate(roundPrefab).GetComponent<XRGrabInteractable>();
            round.interactionManager = slot.interactionManager;
            var attach = slot.GetAttachTransform(round);
            round.transform.SetPositionAndRotation(attach.position, attach.rotation);
            round.gameObject.SetActive(true);
            Physics.SyncTransforms();
            _active.Add(round);
            _dispenser.Spawned();
            slot.interactionManager.SelectEnter((IXRSelectInteractor)slot, round);
        }

        private void Despawn(XRGrabInteractable round)
        {
            var manager = round.interactionManager;
            for (int i = round.interactorsSelecting.Count - 1; i >= 0 && manager != null; i--)
                manager.SelectExit(round.interactorsSelecting[i], round);
            var body = round.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            round.gameObject.SetActive(false);
            _active.Remove(round);
            _pool.Push(round);
        }

        private void ResetTray()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Despawn(_active[i]);
            _dispenser.Reset();
        }

        private RoundDispenser NewDispenser() => new RoundDispenser(Series);
    }
}
