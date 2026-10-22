using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rubezh.Core;
using Rubezh.Gameplay;
using Rubezh.UI;
using Rubezh.XR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Rubezh.Tests.PlayMode
{
    // Сквозной прогон упражнения в Main.unity (NG-19, автоматическая часть протокола): все 11 шагов
    // руками XRI — захваты, сокеты, телепорты, кнопки оружия — от шкафа до сдачи, экран результата
    // и «Повторить». Руки и сокеты работают через XRInteractionManager, как у живого пользователя;
    // нажатие кнопки пульта пальцем заменено подачей мишени (пальцевое нажатие — в протоколе шлема).
    public class FullRunTests
    {
        private Scene _scene;
        private XRInteractionManager _manager;
        private ExerciseRunner _runner;
        private WeaponController _weapon;
        private IXRSelectInteractor _right, _left;
        private readonly List<ViolationRecord> _violations = new List<ViolationRecord>();
        private readonly List<string> _log = new List<string>();
        private VideoCapture _video;      // запись видео прогона (NG-20), только по EditorPrefs

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);
            while (!load.isDone) yield return null;
            _scene = SceneManager.GetSceneByName("Main");
            SceneManager.SetActiveScene(_scene);
            yield return null;
            yield return null;
            _manager = Object.FindFirstObjectByType<XRInteractionManager>();
            _runner = Object.FindFirstObjectByType<ExerciseRunner>();
            _weapon = Object.FindFirstObjectByType<WeaponController>();
            _right = Hand("Right Controller");
            _left = Hand("Left Controller");
            _violations.Clear();
            _log.Clear();
            _runner.ViolationRaised += OnViolation;
            _runner.PhaseChanged += OnPhase;
            _video = VideoCapture.StartIfRequested(GameObject.Find("XR Origin (XR Rig)").transform, ((Component)_right).transform);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null)
            {
                _runner.ViolationRaised -= OnViolation;
                _runner.PhaseChanged -= OnPhase;
            }
            Debug.Log("[FullRun]\n" + string.Join("\n", _log));
            if (_video != null) Object.Destroy(_video.gameObject);
            var unload = SceneManager.UnloadSceneAsync(_scene);
            while (unload != null && !unload.isDone) yield return null;
            yield return null;
        }

        private void OnViolation(ViolationRecord v)
        {
            _violations.Add(v);
            var loc = Object.FindFirstObjectByType<AnchorLocator>();
            _log.Add($"{Time.time:0.0} нарушение {v.Type}/{v.Severity} {v.Duration:0.00} с, фаза {_runner.Phase}, якорь {loc.Current}");
        }
        private void OnPhase(Phase from, Phase to) => _log.Add($"{Time.time:0.0} {from} → {to}");

        private static IXRSelectInteractor Hand(string controller)
        {
            foreach (var i in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (i.GetComponentsInParent<Transform>(true).Any(t => t.name == controller))
                {
                    // Без шлема менеджер модальностей держит контроллеры выключенными: включаем руку сами.
                    for (var t = i.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                    i.enabled = true;
                    return i;
                }
            Assert.Fail("нет руки " + controller);
            return null;
        }

        private static XRGrabInteractable Item(string name)
        {
            var g = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(g, name);
            return g;
        }

        // Место по типу и по слою предмета: на стойке два сокета LineStand — пистолета и магазина.
        private static XRSocketInteractor PlaceOf(Place place, XRGrabInteractable item)
        {
            var s = Object.FindObjectsByType<PlaceSocket>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Place == place && (p.Socket.interactionLayers.value & item.interactionLayers.value) != 0);
            Assert.IsNotNull(s, $"место {place} для {item.name}");
            return s.Socket;
        }

        // Отпустить предмет из всех, кто его держит (рука, сокет).
        private void Free(XRGrabInteractable item)
        {
            for (int i = item.interactorsSelecting.Count - 1; i >= 0; i--)
                _manager.SelectExit(item.interactorsSelecting[i], item);
        }

        private IEnumerator Take(IXRSelectInteractor hand, XRGrabInteractable item)
        {
            Free(item);
            _manager.SelectEnter(hand, item);
            yield return null;
            yield return null;
        }

        // Поднести предмет к сокету (он выбирает только то, что в его зоне) и отдать сокету.
        private IEnumerator Put(XRGrabInteractable item, IXRSelectInteractor socket, bool verify = true)
        {
            // Из сокета предмет сначала берёт рука (как у человека): иначе прежний сокет, в чьей зоне
            // он ещё лежит, сразу заберёт его обратно.
            if (item.interactorsSelecting.Any(i => i is XRSocketInteractor))
            {
                var hand = item == _weapon.GetComponent<XRGrabInteractable>() ? _right : _left;
                yield return Take(hand, item);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }
            Free(item);
            var attach = socket.GetAttachTransform(item);
            var body = item.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            // Совместить точку хвата предмета (HeadAnchor у СИЗ) с точкой сокета.
            item.transform.rotation = attach.rotation;
            var own = item.GetAttachTransform(socket);
            item.transform.position += attach.position - own.position;
            if (body != null) { body.position = item.transform.position; body.rotation = item.transform.rotation; }
            Physics.SyncTransforms();
            // В том же кадре, до шага физики: отпущенная рука — и сокет уже держит (как при отпускании
            // Grip в зоне сокета). Иначе физика успеет вытолкнуть предмет из стола.
            if (item.isActiveAndEnabled && !socket.IsSelecting(item)) _manager.SelectEnter(socket, item);
            yield return null;
            yield return null;
            if (!verify) yield break;
            Assert.IsTrue(socket.IsSelecting(item), $"{item.name} в {((Component)socket).name}: предмет держат " +
                string.Join(",", item.interactorsSelecting.Select(i => ((Component)i).name)) +
                $", сокет занят {socket.hasSelection}; {Facts()}");
        }

        private IEnumerator TeleportTo(Vector3 position, float yaw)
        {
            var tp = Object.FindFirstObjectByType<TeleportationProvider>();
            tp.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = position,
                destinationRotation = Quaternion.Euler(0, yaw, 0),
                matchOrientation = MatchOrientation.TargetUpAndForward
            });
            // Рука с пистолетом переезжает вместе с ригом и сразу целится по новому месту: при записи
            // видео кадр — 1/30 с, и четыре кадра без прицела дали бы занос вне сектора.
            for (int i = 0; i < 4; i++) { yield return null; if (_weapon.IsHeld) AimDownrange(); }
        }

        private IEnumerator WaitPhase(Phase phase, float seconds = 2f)
        {
            float t = 0;
            while (_runner.Phase != phase && t < seconds) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(phase, _runner.Phase, "ждали фазу " + phase + "; " + Report());
            if (_video != null) yield return new WaitForSeconds(1.5f);   // в ролике видно табло и шкалу
        }

        private string Report() =>
            "нарушения: " + string.Join(", ", _violations.Select(v => $"{v.Type}/{v.Severity}@{v.Time:0.0}+{v.Duration:0.00}")) +
            $"; факты: {Facts()}";

        private string Facts()
        {
            var f = Object.FindFirstObjectByType<ExerciseFactsCollector>().Collect();
            var w = f.Weapon;
            return $"inHand={f.PistolInHand} pistol={f.PistolPlace} mag={f.MagazinePlace} rounds={f.MagazineRounds} " +
                   $"magIn={w.MagazineInserted} inMag={w.RoundsInMagazine} ch={w.Chamber} locked={w.SlideLocked} safety={w.SafetyOn} jam={w.Jam} " +
                   $"ear={f.EarmuffsOn} gl={f.GlassesOn} target={f.TargetSupplied}";
        }

        private void Cmd(WeaponCommand c) => _weapon.Apply(c);

        // Правая рука с пистолетом — перед собой, ствол к мишеням (+Z зала), как на рубеже.
        // Прицеливание итерациями: пистолет догоняет кисть за пару кадров, после этого поворот ствола
        // относительно кисти постоянен и поправка точная.
        private IEnumerator Aim()
        {
            for (int i = 0; i < 4; i++)
            {
                AimDownrange();
                for (int f = 0; f < 3; f++) yield return null;
            }
        }

        private void AimDownrange()
        {
            var hand = ((Component)_right).transform;
            while (hand.parent != null && hand.name != "Right Controller") hand = hand.parent;
            var rig = GameObject.Find("XR Origin (XR Rig)").transform;
            hand.position = new Vector3(rig.position.x + 0.15f, 1.3f, rig.position.z + 0.35f);
            if (!_weapon.IsHeld)
            {
                hand.rotation = Quaternion.LookRotation(Vector3.forward);
                return;
            }
            // Рукоять под углом к кисти: поворот ствола относительно кисти постоянен, пока пистолет в руке.
            // Кисть ставим так, чтобы ствол смотрел в центр мишени (или вдоль коридора, если её нет).
            var muzzle = _weapon.GetComponentsInChildren<Transform>(true).First(x => x.name == "Muzzle");
            var plane = Object.FindFirstObjectByType<TargetCarriage>().GetComponentsInChildren<Transform>(true).First(x => x.name == "TargetPlane");
            // Мишень подана — в её центр; иначе в коридор: от шкафа (A1) сектор виден узкой щелью между
            // щитами, поэтому туда — вниз-вправо на пол коридора.
            bool supplied = Object.FindFirstObjectByType<ExerciseFactsCollector>().Collect().TargetSupplied;
            float side = Mathf.Sign(muzzle.position.x);
            // Вне рубежа — стволом вниз, в пол (безопасное направление, #164).
            var aim = supplied ? plane.position
                    : Mathf.Abs(muzzle.position.x) > 1.6f ? muzzle.position + new Vector3(0f, -1f, 0.4f) : new Vector3(0f, 1.5f, 13f);
            var rel = Quaternion.Inverse(hand.rotation) * muzzle.rotation;
            hand.rotation = Quaternion.LookRotation(aim - muzzle.position, Vector3.up) * Quaternion.Inverse(rel);
        }

        [UnityTest]
        public IEnumerator WholeExercise_FromCabinetToCabinet_Passes()
        {
            var pistol = _weapon.GetComponent<XRGrabInteractable>();
            var magazine = Object.FindFirstObjectByType<MagazineItem>();
            var magGrab = magazine.Grab;

            // 1. Получение — взять пистолет из шкафа.
            Assert.AreEqual(Phase.Receive, _runner.Phase);
            AimDownrange();
            yield return Take(_right, pistol);
            yield return Aim();                          // ствол — в сектор, и у шкафа тоже
            yield return WaitPhase(Phase.Inspect);

            // 2. Осмотр — магазин A/X, на полку осмотра, затвор на задержку.
            Cmd(WeaponCommand.EjectMagazine);
            yield return null;
            yield return Put(magGrab, PlaceOf(Place.InspectShelf, magGrab));
            Cmd(WeaponCommand.SlideBack);
            yield return WaitPhase(Phase.Ppe);

            // 3. СИЗ — наушники и очки к голове.
            var wear = Object.FindFirstObjectByType<Wearables>();
            yield return Put(Item("Earmuffs"), Slot(wear, "earmuffsSlot"));
            yield return Put(Item("Glasses"), Slot(wear, "glassesSlot"));
            yield return WaitPhase(Phase.LoadMagazine);

            // 4. Снаряжение — к столу A2, магазин на стол, 5 патронов из лотка.
            yield return TeleportTo(new Vector3(2.4f, 0, 1.9f), 90);
            yield return Aim();
            yield return Put(magGrab, PlaceOf(Place.PrepTable, magGrab));
            var tray = Object.FindFirstObjectByType<AmmoTray>();
            for (int i = 0, tries = 0; i < 5 && tries < 10; tries++)
            {
                float wait = 0;
                while (tray.Waiting == null && wait < 1f) { wait += Time.deltaTime; yield return null; }
                Assert.IsNotNull(tray.Waiting, $"патрон {i + 1} в лотке");
                var round = tray.Waiting;
                var open = magazine.GetComponentsInChildren<XRSocketInteractor>(true).First(s => s.socketActive && !s.hasSelection);
                yield return Take(_left, round);
                yield return Put(round, open, verify: false);     // патрон-предмет уходит в пул, считаем магазин
                for (int k = 0; k < 10 && magazine.Rounds < i + 1; k++) yield return null;
                if (magazine.Rounds == i + 1) i++;
                else _log.Add($"патрон {i + 1} не сел с первого раза, вернулся в лоток — берём снова");
            }
            Assert.AreEqual(5, magazine.Rounds, $"магазин снаряжён; {Facts()}");
            yield return WaitPhase(Phase.ToLine);

            // 5. На рубеж — A3, пистолет и магазин на стойку, подать мишень.
            yield return TeleportTo(new Vector3(0, 0, 3.25f), 0);
            yield return Aim();
            yield return Put(pistol, PlaceOf(Place.LineStand, pistol));
            yield return Put(magGrab, PlaceOf(Place.LineStand, magGrab));
            Object.FindFirstObjectByType<TargetCarriage>().Supply();
            yield return WaitPhase(Phase.Load, 6f);

            // 6. Заряжай — пистолет в руку, магазин в шахту.
            yield return Take(_right, pistol);
            yield return Aim();
            var well = Object.FindFirstObjectByType<MagWell>();
            yield return Put(magGrab, well.Socket);
            yield return WaitPhase(Phase.Fire);

            // 7. Огонь — дослать, снять с предохранителя, стрелять до задержки затвора.
            Cmd(WeaponCommand.SlideBack);
            Cmd(WeaponCommand.SlideForward);
            Cmd(WeaponCommand.SafetyOff);
            var muzzle = _weapon.GetComponentsInChildren<Transform>(true).First(x => x.name == "Muzzle");
            var hand = ((Component)_right).transform;
            _log.Add($"ствол {muzzle.position:F2} → {muzzle.forward:F2}; рука {hand.position:F2} → {hand.forward:F2}; " +
                     $"в секторе {Object.FindFirstObjectByType<SectorDetector>().InSector}");
            for (int shot = 0; shot < 12 && _runner.Phase == Phase.Fire; shot++)
            {
                yield return Aim();                      // прицелиться заново после отдачи
                Cmd(WeaponCommand.TriggerPull);
                Cmd(WeaponCommand.TriggerRelease);
                yield return new WaitForSeconds(0.3f);
                var jam = _weapon.State.Jam;
                if (jam == Jam.Misfire) { _log.Add("осечка"); yield return new WaitForSeconds(5.2f); }
                if (jam == Jam.StovePipe) _log.Add("прихват");
                if (jam != Jam.None) { Cmd(WeaponCommand.SlideBack); Cmd(WeaponCommand.SlideForward); }
            }
            yield return WaitPhase(Phase.Cease);
            yield return new WaitForSeconds(1.5f);       // гильзы долетели и улеглись
            var plane = Object.FindFirstObjectByType<TargetCarriage>().transform.GetComponentsInChildren<Transform>(true).First(x => x.name == "TargetPlane");
            _log.Add($"мишень {plane.position:F2} лицом {(-plane.forward):F2}, рамка {plane.parent.localEulerAngles:F0}; " +
                     $"луч в мишень: {(Physics.Raycast(muzzle.position, muzzle.forward, out var hit, 20f) ? hit.collider.name + " " + hit.point.ToString("F2") : "мимо")}");

            // 8–9. Стой, разряжай — предохранитель, магазин, затвор на задержку.
            Cmd(WeaponCommand.SafetyOn);
            yield return WaitPhase(Phase.Unload);
            Cmd(WeaponCommand.EjectMagazine);
            yield return null;
            Cmd(WeaponCommand.SlideBack);
            yield return WaitPhase(Phase.ShowClear);

            // 10. Оружие к осмотру — пистолет и магазин на стойку.
            yield return Put(pistol, PlaceOf(Place.LineStand, pistol));
            yield return Put(magGrab, PlaceOf(Place.LineStand, magGrab));
            yield return WaitPhase(Phase.Return);

            // 11. Сдача — к шкафу, пистолет в гнездо.
            yield return TeleportTo(new Vector3(-2.4f, 0, 1.7f), -90);
            AimDownrange();
            yield return Take(_right, pistol);
            yield return Put(pistol, Object.FindFirstObjectByType<CabinetSocket>().GetComponent<XRSocketInteractor>());
            yield return WaitPhase(Phase.Done, 3f);
            if (_video != null)
            {
                _video.Focus(GameObject.Find("R1").transform);
                yield return new WaitForSeconds(6f);                       // экран результата у шкафа
            }

            var result = _runner.Director.Result;
            _log.Add($"итог: {result.Verdict} {result.Score} время {result.TotalTime:0.0} попаданий {result.Hits}/{result.Shots}; {Report()}");
            Assert.IsFalse(result.FailedCritical, Report());
            Assert.Greater(result.Hits, 0, "прицельные выстрелы попадают в мишень");
            Assert.IsEmpty(_violations, "чистый проход без нарушений (#163, #164): " + Report());
            Assert.AreEqual(Verdict.Passed, result.Verdict);
            Assert.AreEqual(0, _violations.Count(v => v.Severity == Severity.Critical), Report());
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<ResultScreen>().Visible, "экран результата у шкафа");

            // «Повторить» — упражнение с начала, пистолет снова в шкафу.
            Object.FindFirstObjectByType<ResultScreen>().Repeat();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(Phase.Receive, _runner.Phase);
            Assert.AreEqual(Place.Cabinet, ((IPlaced)Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(m => m is IPlaced p && m.gameObject == _weapon.gameObject)).Place, "пистолет вернулся в шкаф");
        }

        private static XRSocketInteractor Slot(Wearables wear, string field)
        {
            var f = typeof(Wearables).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var s = (XRSocketInteractor)f.GetValue(wear);
            Assert.IsNotNull(s, field);
            return s;
        }
    }
}
