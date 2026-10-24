using Rubezh.Core;

namespace Rubezh.Gameplay
{
    public interface ITriggerInput
    {
        float PressValue { get; }   // 0..1
        bool IsTouched { get; }     // ёмкостный датчик или запасной порог
    }

    public interface IWearables
    {
        bool EarmuffsOn { get; }
        bool GlassesOn { get; }
        event System.Action Changed;
    }

    public enum Hand { Left, Right }

    // Пистолет в руке и его состояние для детекторов (VP-05, VP-06). Реализует интерактивный
    // пистолет Никиты (NG-08); IsHeld — только пока пистолет держат за Grip_Primary,
    // PrimaryHand — рука на Grip_Primary (палец на спуске считается только на ней).
    public interface IHeldWeapon
    {
        bool IsHeld { get; }
        Hand PrimaryHand { get; }
        WeaponState State { get; }
    }

    // Где предмет сейчас (NG-09): место-сокет, рука, шахта пистолета или пол.
    public interface IPlaced
    {
        Place Place { get; }
    }

    // Магазин: патронов в нём, где бы он ни был (в шахте — по автомату оружия).
    public interface IMagazine
    {
        int Rounds { get; }
    }

    // Мишень подана на дистанцию (NG-12).
    public interface ITargetSupply
    {
        bool Supplied { get; }
    }

    public interface ICasingCounter
    {
        int CasingsOutsideCatcher { get; }
    }
}
