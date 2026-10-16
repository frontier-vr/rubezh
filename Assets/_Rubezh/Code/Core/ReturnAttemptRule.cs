using System;

namespace Rubezh.Core
{
    // Сдача оружия в шкаф (FR-37, VP-11). Шкаф принимает только разряженный пистолет (IsClear).
    // Попытка — это подход: пистолет в руке поднесли к гнезду (начало наведения сокета) и увели
    // (конец). За подход — одно действие CabinetAttempt для директора и, если на шаге сдачи
    // пистолет не разряжен, одно нарушение ReturnedLoaded и событие Refused для подсказки и
    // вибрации. Фильтр выбора XRI вызывает проверку много раз за подход, поэтому нарушение
    // поднимается не из фильтра, а отсюда (NG-14).
    public sealed class ReturnAttemptRule
    {
        private readonly ExerciseDirector _director;
        private bool _approaching;

        public ReturnAttemptRule(ExerciseDirector director)
        {
            _director = director ?? throw new ArgumentNullException(nameof(director));
        }

        public event Action<WeaponState> Refused;

        public bool Approaching => _approaching;

        // Что пропускает фильтр сокета шкафа.
        public static bool Accepts(in WeaponState state) => state.IsClear;

        public void Approach(in WeaponState state)
        {
            if (_approaching) return;
            _approaching = true;
            _director.ReportAction(ExerciseAction.CabinetAttempt);
            if (Accepts(state)) return;
            if (_director.Phase == Phase.Return)
                _director.Raise(Violation.ReturnedLoaded, Severity.Minor);
            Refused?.Invoke(state);
        }

        public void Leave() => _approaching = false;
    }
}
