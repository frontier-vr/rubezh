using System.Collections.Generic;
using Rubezh.Core;
using Rubezh.Gameplay;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Rubezh.XR
{
    // Гильзы (VP-19): пул без Instantiate в бою, вылет из EjectionPort по CasingEjected. У рубежа
    // гильза летит по дуге в гильзоуловитель O-22 (CatcherZone), дальше от него — просто вправо.
    // Упавшая на пол гильза через casingReturnSeconds появляется на полочке O-21 (ошибка
    // управления прощается), оттуда её можно взять и бросить в уловитель. ICasingCounter считает
    // гильзы вне уловителя для CasingLeft при сдаче (VP-11). При прихвате (VP-13) гильза торчит из
    // окна выброса до перезарядки и вылетает вместе с ней.
    public sealed class CasingEjector : MonoBehaviour, ICasingCounter
    {
        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private RulesConfig rules;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Transform ejectionPort;
        [SerializeField] private GameObject casingPrefab;
        [Tooltip("Пустышка CatcherZone гильзоуловителя O-22: центр объёма")]
        [SerializeField] private Transform catcherZone;
        [SerializeField] private Vector3 catcherSize = new Vector3(0.4f, 0.8f, 0.5f);
        [Tooltip("Полочка поднятых гильз O-21")]
        [SerializeField] private Transform floorShelf;

        private static readonly Vector3 StuckOffset = new Vector3(0.006f, 0.009f, 0f);

        private readonly List<Casing> _active = new List<Casing>();
        private Casing _stuck;
        private readonly Stack<Casing> _free = new Stack<Casing>();
        private int _created;

        private sealed class Casing
        {
            public GameObject go;
            public Rigidbody body;
            public XRGrabInteractable grab;
            public float looseSince = -1f;
        }

        public int CasingsOutsideCatcher
        {
            get
            {
                int n = 0;
                foreach (var c in _active) if (!InCatcher(c.go.transform.position)) n++;
                return n;
            }
        }

        public int Active => _active.Count;
        public int Created => _created;

        private int PoolSize => rules != null ? rules.casingPoolSize : 10;
        private float FlightSeconds => rules != null ? rules.casingFlightSeconds : 0.45f;
        private float AimMaxDistance => rules != null ? rules.casingAimMaxDistance : 2f;
        private float ReturnSeconds => rules != null ? rules.casingReturnSeconds : 1f;
        private float DropHeight => rules != null ? rules.dropHeight : 0.85f;
        private const float SettledSpeedSqr = 0.0025f;   // 5 см/с: гильза улеглась на поддон
        private const float CatcherNetHeight = 0.2f;     // сетка над коробом — от центра CatcherZone, м

        public void Configure(ExerciseRunner exerciseRunner, RulesConfig rulesConfig, WeaponController weaponController,
                              Transform port, GameObject prefab, Transform catcher, Transform shelf)
        {
            Unhook();
            runner = exerciseRunner; rules = rulesConfig; weapon = weaponController;
            ejectionPort = port; casingPrefab = prefab; catcherZone = catcher; floorShelf = shelf;
            Hook();
            Prewarm();
        }

        private void Start() => Prewarm();
        private void OnEnable() => Hook();

        // Все гильзы пула создаются заранее: во время стрельбы Instantiate нет.
        private void Prewarm()
        {
            if (casingPrefab == null) return;
            while (_created < PoolSize)
            {
                var go = Instantiate(casingPrefab);
                go.name = "Casing_" + _created;
                go.SetActive(false);
                _free.Push(new Casing { go = go, body = go.GetComponent<Rigidbody>(), grab = go.GetComponent<XRGrabInteractable>() });
                _created++;
            }
        }
        private void OnDisable() => Unhook();

        private void Hook()
        {
            Unhook();
            if (weapon != null)
            {
                weapon.CasingEjected += Eject;
                weapon.Jammed += OnJammed;
            }
            if (runner != null) runner.Restarted += ResetAll;
        }

        private void Unhook()
        {
            if (weapon != null)
            {
                weapon.CasingEjected -= Eject;
                weapon.Jammed -= OnJammed;
            }
            if (runner != null) runner.Restarted -= ResetAll;
        }

        // Прихват гильзы (VP-13): гильза торчит из окна выброса, пока затвор не перезарядят.
        private void OnJammed(Jam jam)
        {
            if (jam != Jam.StovePipe || casingPrefab == null || ejectionPort == null || _stuck != null) return;
            var c = Take();
            c.body.isKinematic = true;
            if (c.grab != null) c.grab.enabled = false;
            c.go.transform.SetParent(ejectionPort, false);
            c.go.transform.localPosition = StuckOffset;
            // Ось гильзы — локальная Z: ставим её торчком из окна с наклоном наружу, вправо.
            c.go.transform.localRotation = Quaternion.AngleAxis(-25f, Vector3.forward) * Quaternion.AngleAxis(-90f, Vector3.right);
            c.go.SetActive(true);
            _stuck = c;
        }

        public bool HasStuckCasing => _stuck != null;
        public Transform StuckCasing => _stuck != null ? _stuck.go.transform : null;

        public bool InCatcher(Vector3 position) =>
            catcherZone != null && new Bounds(catcherZone.position, catcherSize).Contains(position);

        // Начальная скорость гильзы: по дуге в уловитель, если он рядом, иначе вправо-вверх.
        public Vector3 LaunchVelocity(Vector3 from)
        {
            if (catcherZone != null)
            {
                var target = catcherZone.position + Vector3.up * (catcherSize.y * 0.25f);
                var d = target - from;
                if (d.magnitude <= AimMaxDistance)
                {
                    float t = FlightSeconds;
                    return d / t - 0.5f * Physics.gravity * t;
                }
            }
            var right = ejectionPort != null ? ejectionPort.right : Vector3.right;
            return right * 1.6f + Vector3.up * 1.2f;
        }

        public void Eject()
        {
            if (casingPrefab == null || ejectionPort == null) return;
            // При прихвате вылетает та самая гильза, что торчала из окна.
            var c = _stuck ?? Take();
            Unstick();
            c.go.transform.SetPositionAndRotation(ejectionPort.position, ejectionPort.rotation);
            c.body.position = ejectionPort.position;
            c.body.rotation = ejectionPort.rotation;
            c.body.isKinematic = false;
            c.go.SetActive(true);
            c.body.linearVelocity = LaunchVelocity(ejectionPort.position);
            c.body.angularVelocity = Random.insideUnitSphere * 20f;
            c.looseSince = -1f;
        }

        private Casing Take()
        {
            if (_free.Count > 0)
            {
                var f = _free.Pop();
                _active.Add(f);
                return f;
            }
            // Пул кончился: самая старая гильза переезжает.
            var oldest = _active[0];
            _active.RemoveAt(0);
            Release(oldest);
            _active.Add(oldest);
            return oldest;
        }

        private void Release(Casing c)
        {
            if (c.grab != null && c.grab.interactionManager != null)
                for (int i = c.grab.interactorsSelecting.Count - 1; i >= 0; i--)
                    c.grab.interactionManager.SelectExit(c.grab.interactorsSelecting[i], c.grab);
            c.go.SetActive(false);
        }

        // Зажатую гильзу отпустить из окна (на перезарядке или Restart).
        private void Unstick()
        {
            if (_stuck == null) return;
            _stuck.go.transform.SetParent(null, true);
            if (_stuck.grab != null) _stuck.grab.enabled = true;
            _stuck = null;
        }

        private void Update()
        {
            float now = Time.time;
            foreach (var c in _active)
            {
                if (c == _stuck) continue;
                var p = c.go.transform.position;
                bool held = c.grab != null && c.grab.isSelected;
                bool inCatcher = InCatcher(p);
                if (!held)
                {
                    // Влетела в уловитель — падает на его поддон (YG-10) и, улёгшись, остаётся там;
                    // вне уловителя снова падает.
                    // Над коробом гильзу гасит сетка: дальше она падает отвесно, не отскакивая от отбойника.
                    if (inCatcher && !c.body.isKinematic && p.y < catcherZone.position.y + CatcherNetHeight)
                    {
                        var v = c.body.linearVelocity;
                        if (v.x != 0f || v.z != 0f) c.body.linearVelocity = new Vector3(0f, Mathf.Min(v.y, 0f), 0f);
                    }
                    bool landed = inCatcher && p.y < catcherZone.position.y && c.body.linearVelocity.sqrMagnitude < SettledSpeedSqr;
                    if (landed && !c.body.isKinematic) { c.body.linearVelocity = Vector3.zero; c.body.angularVelocity = Vector3.zero; c.body.isKinematic = true; }
                    else if (!inCatcher && c.body.isKinematic) c.body.isKinematic = false;
                }
                bool onFloor = !held && p.y < DropHeight && !inCatcher;
                if (!onFloor) { c.looseSince = -1f; continue; }
                if (c.looseSince < 0f) c.looseSince = now;
                if (now - c.looseSince < ReturnSeconds) continue;
                c.looseSince = -1f;
                ToShelf(c);
            }
        }

        private void ToShelf(Casing c)
        {
            if (floorShelf == null) return;
            c.body.isKinematic = false;
            var offset = new Vector3(Random.Range(-0.05f, 0.05f), 0.01f, Random.Range(-0.03f, 0.03f));
            var pos = floorShelf.position + offset;
            c.body.linearVelocity = Vector3.zero;
            c.body.angularVelocity = Vector3.zero;
            c.body.position = pos;
            c.go.transform.position = pos;
        }

        private void ResetAll()
        {
            Unstick();
            foreach (var c in _active)
            {
                Release(c);
                _free.Push(c);
            }
            _active.Clear();
        }

        private void OnDestroy()
        {
            Unhook();
            foreach (var c in _active) if (c.go != null) Destroy(c.go);
            foreach (var c in _free) if (c.go != null) Destroy(c.go);
        }
    }
}
