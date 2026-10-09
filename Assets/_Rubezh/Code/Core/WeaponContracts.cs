using System;

namespace Rubezh.Core
{
    public enum WeaponCommand
    {
        EjectMagazine,
        SlideBack, SlideForward, SlideStopEngage, SlideStopRelease,
        SafetyOn, SafetyOff,
        TriggerPull, TriggerRelease
    }

    public enum Chamber { Empty, Loaded, Spent }
    public enum Jam { None, Misfire, StovePipe }

    public readonly struct WeaponState
    {
        public readonly bool MagazineInserted;
        public readonly int RoundsInMagazine;
        public readonly Chamber Chamber;
        public readonly bool SlideLocked;
        public readonly bool SafetyOn;
        public readonly Jam Jam;

        public WeaponState(bool magazineInserted, int roundsInMagazine, Chamber chamber,
                           bool slideLocked, bool safetyOn, Jam jam)
        {
            MagazineInserted = magazineInserted;
            RoundsInMagazine = roundsInMagazine;
            Chamber = chamber;
            SlideLocked = slideLocked;
            SafetyOn = safetyOn;
            Jam = jam;
        }

        public bool IsLoaded => Chamber == Chamber.Loaded || (MagazineInserted && RoundsInMagazine > 0);
        public bool IsClear => !MagazineInserted && Chamber == Chamber.Empty && SlideLocked;
    }

    public interface IWeapon
    {
        WeaponState State { get; }
        bool Apply(WeaponCommand command);   // false: команда недопустима в текущем состоянии
        bool InsertMagazine(int rounds);     // магазин приходит со своим числом патронов
        event Action<WeaponState> Changed;
        event Action Fired;
        event Action DryFire;
        event Action<Jam> Jammed;
        event Action CasingEjected;
    }
}
