namespace CubeGame.Core.Items
{
    /// <summary>
    /// Типы предметов Alpha (поле <c>+0x00</c>, таблица имён <c>World.cpp:2815-2969</c>, DIRECT).
    /// Значения совпадают с оригинальными идентификаторами, чтобы ссылки на спецификацию оставались
    /// однозначными; это не перенос кода Alpha.
    /// </summary>
    public enum ItemType : byte
    {
        /// <summary>Формула/рецепт (Alpha 2).</summary>
        Formula = 2,

        /// <summary>Оружие (Alpha 3).</summary>
        Weapon = 3,

        /// <summary>Нагрудник (Alpha 4).</summary>
        Chest = 4,

        /// <summary>Обувь (Alpha 5).</summary>
        Boots = 5,

        /// <summary>Перчатки (Alpha 6).</summary>
        Gloves = 6,

        /// <summary>Наплечники (Alpha 7).</summary>
        Shoulders = 7,

        /// <summary>Амулет (Alpha 8).</summary>
        Amulet = 8,

        /// <summary>Кольцо (Alpha 9).</summary>
        Ring = 9,
    }

    /// <summary>
    /// Подтипы оружия Alpha (поле <c>+0x01</c>, DIRECT — имена из таблицы данных).
    /// Подтипы 6 и 7 присутствуют в данных и участвуют в «спецоружии», но их имена не установлены (UNKNOWN).
    /// </summary>
    public enum WeaponSubtype : byte
    {
        None = 0,
        Mace = 1,
        Fist = 2,
        Sword = 3,
        Dagger = 4,
        Longsword = 5,

        /// <summary>Подтип 6: имя в данных не восстановлено (UNKNOWN), входит в группу «спецоружия».</summary>
        Reserved06 = 6,

        /// <summary>Подтип 7: имя в данных не восстановлено (UNKNOWN), входит в группу «спецоружия».</summary>
        Reserved07 = 7,

        Crossbow = 8,

        /// <summary>Стрела (Alpha 9).</summary>
        Arrow = 9,
        Staff = 0x0A,
        Wand = 0x0B,
        Bracelet = 0x0C,
        Shield = 0x0D,
        Boomerang = 0x0E,
        Greatsword = 0x0F,
        Greataxe = 0x10,
        Greatmace = 0x11,
        Torch = 0x14,
    }

    /// <summary>
    /// Модификатор/материал предмета (поле <c>+0x0d</c>, DIRECT — используется в формулах).
    /// Смысл значений (названия материалов) — OQ-09, поэтому члены названы по hex-значению, а не догадкой.
    /// </summary>
    public enum ItemModifier : byte
    {
        None = 0x00,
        M01 = 0x01,
        M0B = 0x0B,
        M0C = 0x0C,
        M12 = 0x12,
        M13 = 0x13,
        M17 = 0x17,
        M19 = 0x19,
        M1A = 0x1A,
        M1B = 0x1B,
    }
}
