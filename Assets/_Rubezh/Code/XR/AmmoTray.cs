using System.Collections.Generic;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Rubezh.XR
{
    // Лоток O-18 (NG-09): патрон лежит в сокете на пустышке Spawn; взяли — появляется следующий,
    // всего seriesRounds за цикл (RoundDispenser). Вставленный в магазин и упавший патрон уходят
    // в пул; упавший лоток выдаёт заново. Restart — все патроны в пул, счёт с нуля.
    public sealed class AmmoTray : MonoBehaviour
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private XRSocketInteractor spawnSocket;
        [SerializeField] private GameObject roundPrefab;

        private readonly List<XRGrabInteractable> _active = new List<XRGrabInteractable>();
        private readonly Stack<XRGrabInteractable> _pool = new Stack<XRGrabInteractable>();
        private RoundDispenser _dispenser;

        public int Seated => _dispenser.Seated;
        public int Out => _dispenser.Out;
        public int Pooled => _pool.Count;
        public IReadOnlyList<XRGrabInteractable> Active => _active;
        public XRGrabInteractable Waiting => spawnSocket != null && spawnSocket.hasSelection
            ? spawnSocket.firstInteractableSelected as XRGrabInteractable : null;

        private float DropHeight => rules != null ? rules.dropHeight : 0.85f;

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, XRSocketInteractor socket, GameObject prefab)
        {
            if (runner != null) runner.Restarted -= ResetTray;
            runner = exerciseRunner;
            rules = rulesConfig;
            spawnSocket = socket;
            roundPrefab = prefab;
            _dispenser = NewDispenser();
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
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var round = _active[i];
                if (!round.isSelected && round.transform.position.y < DropHeight)
                {
                    Despawn(round);
                    _dispenser.Lost();
                }
            }
            if (_dispenser.ShouldSpawn(spawnSocket.hasSelection)) Spawn();
        }

        // Патрон вставлен в магазин (MagazineItem).
        public void Seat(GameObject round)
        {
            var grab = round.GetComponent<XRGrabInteractable>();
            if (grab == null || !_active.Contains(grab)) { round.SetActive(false); return; }
            Despawn(grab);
            _dispenser.Seat();
        }

        private void Spawn()
        {
            var round = _pool.Count > 0 ? _pool.Pop() : Instantiate(roundPrefab).GetComponent<XRGrabInteractable>();
            round.interactionManager = spawnSocket.interactionManager;
            var attach = spawnSocket.GetAttachTransform(round);
            round.transform.SetPositionAndRotation(attach.position, attach.rotation);
            round.gameObject.SetActive(true);
            Physics.SyncTransforms();
            _active.Add(round);
            _dispenser.Spawned();
            spawnSocket.interactionManager.SelectEnter((IXRSelectInteractor)spawnSocket, round);
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

        private RoundDispenser NewDispenser() => new RoundDispenser(rules != null ? rules.seriesRounds : 5);
    }
}
