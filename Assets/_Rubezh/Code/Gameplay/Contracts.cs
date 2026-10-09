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

    public interface ICasingCounter
    {
        int CasingsOutsideCatcher { get; }
    }
}
