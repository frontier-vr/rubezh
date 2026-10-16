namespace Rubezh.Core
{
    // Где лежит предмет. Места — docs/layout.md §3.
    public enum Place
    {
        Unknown,
        Hand,
        Cabinet,        // O-05, гнездо WeaponSlot
        InspectShelf,   // O-08
        PrepTable,      // O-19, O-20
        LineStand,      // O-27, O-28
        InWeapon,       // магазин в шахте пистолета
        Floor
    }

    public enum Anchor { None, A1, A2, A3 }

    // Отдельные действия пользователя, по которым директор ловит StepOrder и IncompleteInspection.
    public enum ExerciseAction
    {
        InsertRound,        // патрон в магазин
        InsertMagazine,     // магазин в шахту пистолета
        PutOnEarmuffs,
        PutOnGlasses,
        Teleport,           // с якорем в аргументе
        CabinetAttempt,     // поднёс пистолет к гнезду шкафа
        PressTargetButton
    }

    // Снимок мира, по которому закрываются фазы (docs/user-flow.md §4.1).
    // Собирает его Gameplay из компонентов сцены, Core только читает.
    public readonly struct ExerciseFacts
    {
        public readonly bool PistolInHand;
        public readonly WeaponState Weapon;
        public readonly int MagazineRounds;         // патронов в магазине, где бы он ни был
        public readonly Place PistolPlace;
        public readonly Place MagazinePlace;
        public readonly bool EarmuffsOn;
        public readonly bool GlassesOn;
        public readonly bool TargetSupplied;
        public readonly int CasingsOutsideCatcher;  // гильзы вне гильзоуловителя (VP-19), для CasingLeft при сдаче

        public ExerciseFacts(bool pistolInHand, WeaponState weapon, int magazineRounds,
                             Place pistolPlace, Place magazinePlace,
                             bool earmuffsOn, bool glassesOn, bool targetSupplied, int casingsOutsideCatcher = 0)
        {
            PistolInHand = pistolInHand;
            Weapon = weapon;
            MagazineRounds = magazineRounds;
            PistolPlace = pistolPlace;
            MagazinePlace = magazinePlace;
            EarmuffsOn = earmuffsOn;
            GlassesOn = glassesOn;
            TargetSupplied = targetSupplied;
            CasingsOutsideCatcher = casingsOutsideCatcher;
        }

        // Стартовое состояние упражнения: пистолет в шкафу с пустым магазином.
        public static ExerciseFacts Start => new ExerciseFacts(
            false, new WeaponState(true, 0, Chamber.Empty, false, true, Jam.None), 0,
            Place.Cabinet, Place.InWeapon, false, false, false);
    }
}
