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

    // Пистолет в руке и его состояние для детекторов (VP-05, VP-06). Реализует интерактивный
    // пистолет Никиты (NG-08); IsHeld — только пока пистолет держат за Grip_Primary.
    public interface IHeldWeapon
    {
        bool IsHeld { get; }
        WeaponState State { get; }
    }

    public interface ICasingCounter
    {
        int CasingsOutsideCatcher { get; }
    }
}
