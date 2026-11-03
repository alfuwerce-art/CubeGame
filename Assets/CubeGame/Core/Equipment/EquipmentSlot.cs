namespace CubeGame.Core.Equipment
{
    /// <summary>
    /// Слоты экипировки CubeGame (S04).
    ///
    /// Слоты выбраны по перечню из E_combat §E2 (основное оружие <c>0xaa8</c>, вторая рука <c>0x990</c>,
    /// нагрудник <c>0x530</c>, перчатки <c>0x648</c>, обувь <c>0x760</c>, наплечники <c>0x878</c>).
    /// Кольца, амулеты и расходники в эту партию не входят.
    /// </summary>
    public enum EquipmentSlot
    {
        /// <summary>Основное оружие (Alpha слот <c>0xaa8</c>, тип предмета 3).</summary>
        MainHand = 0,

        /// <summary>Вторая рука (Alpha слот <c>0x990</c>, тип предмета 3).</summary>
        OffHand = 1,

        /// <summary>Нагрудник (Alpha слот <c>0x530</c>, тип предмета 4).</summary>
        Chest = 2,

        /// <summary>Перчатки (Alpha слот <c>0x648</c>, тип предмета 6).</summary>
        Gloves = 3,

        /// <summary>Обувь (Alpha слот <c>0x760</c>, тип предмета 5).</summary>
        Boots = 4,

        /// <summary>Наплечники (Alpha слот <c>0x878</c>, тип предмета 7).</summary>
        Shoulders = 5,
    }
}
