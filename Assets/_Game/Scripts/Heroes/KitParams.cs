using System.Collections.Generic;
using UnityEngine;

/// <summary>Как число навыка превращается в значение для боя.</summary>
public enum ParamKind
{
    Number,  // Как есть (секунды, штуки, энергия, +Защита)
    Percent, // Проценты: 40 → 0.4 (урон от Силы атаки/Магии, шансы, доли HP)
    Bonus    // Прибавка в процентах: 30 → ×1.3 (скорость в дыму)
}

/// <summary>Описание одного числа навыка: к какому скилу, ключ, подпись и значение по умолчанию.</summary>
public class SkillParamDef
{
    public int skill;       // Номер скила (0..2)
    public string key;      // Ключ (по нему код находит число; менять нельзя)
    public string label;    // Подпись в Инспекторе
    public float value;     // Значение по умолчанию
    public ParamKind kind;  // Как превращать в значение для боя

    public SkillParamDef(int skill, string key, string label, float value, ParamKind kind = ParamKind.Number)
    {
        this.skill = skill;
        this.key = key;
        this.label = label;
        this.value = value;
        this.kind = kind;
    }
}

/// <summary>
/// Числа навыков всех героев: урон (% Силы атаки или Магии), шансы, длительности, количество.
/// Здесь — список чисел и их значения по умолчанию. Сами значения хранятся и меняются в файле героя:
/// HeroData → Skills → Values (список появляется сам, когда открываешь файл героя в Инспекторе).
/// Описание скила (текст) само не меняется — если поменяли число, поправьте текст описания.
/// </summary>
public static class KitParams
{
    private const ParamKind P = ParamKind.Percent; // Коротко: проценты
    private const ParamKind B = ParamKind.Bonus;   // Коротко: прибавка в процентах

    private static readonly Dictionary<HeroKitType, SkillParamDef[]> table = new Dictionary<HeroKitType, SkillParamDef[]>
    {
        [HeroKitType.Soldier] = new[]
        {
            new SkillParamDef(0, "shots", "Выстрелов", 3),
            new SkillParamDef(0, "damage", "Урон пули, % Силы атаки", 70, P),
            new SkillParamDef(0, "interval", "Интервал между выстрелами, сек", 0.5f),
            new SkillParamDef(1, "distance", "Длина скачка", 3.5f),
            new SkillParamDef(2, "shield", "Прочность щита, % макс. HP", 30, P),
            new SkillParamDef(2, "time", "Сколько держится щит, сек", 6),
        },
        [HeroKitType.Stajor] = new[]
        {
            new SkillParamDef(0, "damage", "Урон волны, % Силы атаки", 120, P),
            new SkillParamDef(1, "time", "Сколько держится блок, сек", 0.5f),
            new SkillParamDef(2, "hits", "Ударов", 3),
            new SkillParamDef(2, "damage", "Урон удара, % Силы атаки", 40, P),
            new SkillParamDef(2, "stun", "Стан после удара, сек", 0.5f),
        },
        [HeroKitType.Phantom] = new[]
        {
            new SkillParamDef(0, "count", "Клонов", 2),
            new SkillParamDef(0, "stats", "Характеристики клона, % от Фантома", 30, P),
            new SkillParamDef(1, "energy", "Энергия за звёздочку", 15),
            new SkillParamDef(1, "damage", "Урон звёздочки, % Силы атаки", 45, P),
            new SkillParamDef(1, "min", "Звёздочек минимум", 2),
            new SkillParamDef(1, "max", "Звёздочек максимум", 5),
            new SkillParamDef(2, "dodge", "Шанс увернуться от авто атаки, %", 20, P),
        },
        [HeroKitType.Soyka] = new[]
        {
            new SkillParamDef(0, "energy", "Энергия за рывок", 15),
            new SkillParamDef(0, "damage", "Урон рывка, % Силы атаки", 70, P),
            new SkillParamDef(0, "jumps", "Рывков максимум", 8),
            new SkillParamDef(1, "time", "Сколько держится дым, сек", 4),
            new SkillParamDef(1, "miss", "Шанс промаха врага в дыму, %", 40, P),
            new SkillParamDef(1, "speed", "Скорость Сойки в дыму, +%", 30, B),
            new SkillParamDef(2, "chance", "Шанс кровотечения, %", 15, P),
            new SkillParamDef(2, "bleed", "Кровотечение, % макс. HP цели", 10, P),
            new SkillParamDef(2, "time", "Кровотечение длится, сек", 5),
        },
        [HeroKitType.Sukub] = new[]
        {
            new SkillParamDef(0, "time", "Стан или шарм, сек", 2),
            new SkillParamDef(1, "damage", "Урон сая, % Магии", 100, P),
            new SkillParamDef(1, "life", "Сколько летит сай, сек", 5),
            new SkillParamDef(2, "hp", "Телепорт к помеченному, когда у него HP ниже, %", 10, P),
        },
        [HeroKitType.Charovnica] = new[]
        {
            new SkillParamDef(0, "damage", "Урон стрелы, % Магии", 60, P),
            new SkillParamDef(0, "stun", "Стан, сек", 1.2f),
            new SkillParamDef(1, "damage", "Урон мыши, % Магии", 50, P),
            new SkillParamDef(1, "stun", "Стан цели и врагов рядом, сек", 2),
            new SkillParamDef(1, "radius", "Радиус стана вокруг цели", 2.5f),
            new SkillParamDef(2, "hp", "Телепорт, когда HP ниже, %", 20, P),
        },
        [HeroKitType.Aqua] = new[]
        {
            new SkillParamDef(0, "damage", "Урон шара, % Магии", 70, P),
            new SkillParamDef(0, "stun", "Сколько заперт в пузыре, сек", 3),
            new SkillParamDef(1, "defense", "+Защита", 30),
            new SkillParamDef(2, "spdefense", "+Спец. защита", 30),
            new SkillParamDef(2, "radius", "Радиус взрыва пузыря", 2.2f),
        },
        [HeroKitType.Derevo] = new[]
        {
            new SkillParamDef(0, "damage", "Урон корня, % Магии", 60, P),
            new SkillParamDef(1, "heal", "Лечение цветка, % недостающего HP", 30, P),
            new SkillParamDef(1, "life", "Сколько растёт цветок, сек", 10),
            new SkillParamDef(1, "smoke", "Дым цветка (враги шатаются), сек", 2),
            new SkillParamDef(2, "step1", "1-я ступень: получить урона", 1000),
            new SkillParamDef(2, "step2", "2-я ступень: получить урона", 2500),
            new SkillParamDef(2, "bonus", "Прибавка за ступень, % ко всем характеристикам", 20, P),
        },
        [HeroKitType.Motelek] = new[]
        {
            new SkillParamDef(0, "energy", "Энергия за шар", 15),
            new SkillParamDef(0, "damage", "Урон шара, % Магии", 45, P),
            new SkillParamDef(0, "min", "Шаров минимум", 2),
            new SkillParamDef(0, "max", "Шаров максимум", 5),
            new SkillParamDef(1, "heal", "Лечение, % макс. HP", 30, P),
            new SkillParamDef(1, "time", "Сколько держится пыль, сек", 4),
            new SkillParamDef(2, "damage", "Урон волны, % Силы атаки", 50, P),
            new SkillParamDef(2, "radius", "Радиус волны", 2.5f),
        },
        [HeroKitType.Reyndger] = new[]
        {
            new SkillParamDef(0, "damage", "Урон выстрела, % Силы атаки", 150, P),
            new SkillParamDef(0, "life", "Сколько летит выстрел, сек", 4),
            new SkillParamDef(1, "strikes", "Ударов ястреба", 3),
            new SkillParamDef(1, "interval", "Ястреб пикирует раз в, сек", 3),
            new SkillParamDef(1, "damage", "Урон ястреба, % Силы атаки", 60, P),
            new SkillParamDef(2, "every", "Каждая какая авто атака", 5),
            new SkillParamDef(2, "damage", "Урон стрелы, % Силы атаки", 50, P),
        },
        [HeroKitType.FaerGerl] = new[]
        {
            new SkillParamDef(0, "energy", "Энергия за шар", 15),
            new SkillParamDef(0, "damage", "Урон шара, % Магии", 45, P),
            new SkillParamDef(0, "min", "Шаров минимум", 2),
            new SkillParamDef(0, "max", "Шаров максимум", 5),
            new SkillParamDef(1, "time", "Сколько живёт торнадо, сек", 3),
            new SkillParamDef(1, "hit", "Разовый урон при попадании, % Магии", 50, P),
            new SkillParamDef(1, "damage", "Урон раз в 0.5 сек, % Магии", 20, P),
            new SkillParamDef(2, "hp", "Улетает, когда HP ниже, %", 20, P),
        },
        [HeroKitType.Protatp28] = new[]
        {
            new SkillParamDef(0, "damage", "Урон приземления, % Силы атаки", 120, P),
            new SkillParamDef(1, "damage", "Урон удара, % Силы атаки", 100, P),
            new SkillParamDef(2, "hp", "Работает, пока HP ниже, %", 50, P),
            new SkillParamDef(2, "bonus", "Реген HP больше на, %", 50, P),
        },
        [HeroKitType.America] = new[]
        {
            new SkillParamDef(0, "damage", "Урон бумеранга (туда и обратно), % Силы атаки", 60, P),
            new SkillParamDef(0, "range", "Как далеко летит бумеранг", 5),
            new SkillParamDef(1, "damage", "Урон апперкота, % Силы атаки", 90, P),
            new SkillParamDef(1, "radius", "Досягаемость апперкота", 1.6f),
            new SkillParamDef(2, "damage", "Урон контратаки, % Силы атаки", 100, P),
        },
        [HeroKitType.ZedRock] = new[]
        {
            new SkillParamDef(0, "shots", "Снарядов", 2),
            new SkillParamDef(0, "interval", "Интервал между снарядами, сек", 0.5f),
            new SkillParamDef(0, "damage", "Урон снаряда, % Магии", 80, P),
            new SkillParamDef(1, "damage", "Урон звукового кольца, % Магии", 70, P),
            new SkillParamDef(1, "radius", "Радиус кольца", 2.2f),
            new SkillParamDef(2, "bonus", "Скорость бега и атак союзников, +%", 20, B),
            new SkillParamDef(2, "time", "Сколько длится, сек", 3),
            new SkillParamDef(2, "radius", "Радиус", 4),
        },
        [HeroKitType.Ten] = new[]
        {
            new SkillParamDef(0, "cards", "Карт", 3),
            new SkillParamDef(0, "damage", "Урон карты, % Силы атаки", 45, P),
            new SkillParamDef(0, "boosted", "Урон карты после пик, % Силы атаки", 60, P),
            new SkillParamDef(1, "spin", "Карта крутится, сек", 2),
            new SkillParamDef(1, "heal", "Черви: лечение, % макс. HP", 20, P),
            new SkillParamDef(1, "hits", "Бубны: сколько авто атак быстрее", 3),
            new SkillParamDef(1, "speed", "Бубны: скорость этих атак, +%", 50, B),
            new SkillParamDef(1, "energy", "Крести: энергия, % от максимума", 50, P),
            new SkillParamDef(2, "chance", "Шанс решки (ожить), %", 50, P),
            new SkillParamDef(2, "hp", "Оживает с HP, %", 20, P),
        },
        [HeroKitType.Shtorm] = new[]
        {
            new SkillParamDef(0, "min", "Разрядов минимум", 2),
            new SkillParamDef(0, "max", "Разрядов максимум", 5),
            new SkillParamDef(0, "damage", "Урон разряда, % Магии", 50, P),
            new SkillParamDef(1, "damage", "Урон шаровой молнии, % Магии", 120, P),
            new SkillParamDef(2, "damage", "Урон разряда вокруг, % Магии", 80, P),
            new SkillParamDef(2, "radius", "Радиус разряда", 1.8f),
        },
        [HeroKitType.Mozg] = new[]
        {
            new SkillParamDef(0, "damage", "Урон щупалец, % Силы атаки", 100, P),
            new SkillParamDef(0, "range", "Длина щупалец", 3.5f),
            new SkillParamDef(1, "stats", "Зомби: характеристики, % от погибшего", 50, P),
            new SkillParamDef(2, "bonus", "+Магия за каждого паразита", 40),
        },
        [HeroKitType.Frizen] = new[]
        {
            new SkillParamDef(0, "damage", "Урон ледяного шара, % Магии", 100, P),
            new SkillParamDef(0, "stun", "Заморозка (стан), сек", 3),
            new SkillParamDef(1, "damage", "Урон смерча, % Магии", 80, P),
            new SkillParamDef(1, "time", "Сколько живёт смерч, сек", 3),
            new SkillParamDef(1, "stun", "Стан после падения, сек", 2),
            new SkillParamDef(2, "defense", "+Защита", 30),
            new SkillParamDef(2, "spdefense", "+Спец. защита", 30),
        },
    };

    private static readonly SkillParamDef[] none = new SkillParamDef[0]; // У кого навыков нет

    /// <summary>Все числа навыков героя этого типа.</summary>
    public static IReadOnlyList<SkillParamDef> For(HeroKitType type) => table.TryGetValue(type, out var defs) ? defs : none;

    /// <summary>Описание числа (null — такого нет).</summary>
    public static SkillParamDef Find(HeroKitType type, int skill, string key)
    {
        foreach (SkillParamDef d in For(type))
            if (d.skill == skill && d.key == key) return d;
        return null;
    }

    /// <summary>
    /// Число навыка для боя: из файла героя (а если там нет — по умолчанию), уже превращённое
    /// (проценты → доля, прибавка → множитель).
    /// </summary>
    public static float Get(HeroData data, int skill, string key)
    {
        if (data == null) return 0f;
        SkillParamDef def = Find(data.kit, skill, key);
        if (def == null)
        {
            Debug.LogWarning($"[KitParams] У {data.kit} нет числа {key} (скил {skill + 1})");
            return 0f;
        }
        float v = def.value;
        SkillParam p = data.GetSkill(skill).Find(key);
        if (p != null) v = p.value;
        switch (def.kind)
        {
            case ParamKind.Percent: return v / 100f;
            case ParamKind.Bonus: return 1f + v / 100f;
            default: return v;
        }
    }

    /// <summary>
    /// Привести списки чисел в файле героя к таблице: добавить недостающие (со значением по умолчанию),
    /// убрать лишние, обновить подписи. Уже изменённые значения сохраняются. Вернёт true, если что-то поменялось.
    /// </summary>
    public static bool Sync(HeroData data)
    {
        if (data == null || data.skills == null) return false;
        bool changed = false;
        IReadOnlyList<SkillParamDef> defs = For(data.kit);
        for (int i = 0; i < data.skills.Length; i++)
        {
            HeroSkill s = data.skills[i];
            if (s == null) continue;
            if (s.values == null) { s.values = new List<SkillParam>(); changed = true; }
            var wanted = new List<SkillParam>();
            foreach (SkillParamDef d in defs)
            {
                if (d.skill != i) continue;
                SkillParam old = s.Find(d.key);
                wanted.Add(new SkillParam { key = d.key, name = d.label, value = old != null ? old.value : d.value });
            }
            if (!Same(s.values, wanted)) { s.values = wanted; changed = true; }
        }
        return changed;
    }

    /// <summary>Совпадают ли два списка чисел (ключи, подписи, значения и порядок).</summary>
    private static bool Same(List<SkillParam> a, List<SkillParam> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] == null || a[i].key != b[i].key || a[i].name != b[i].name || !Mathf.Approximately(a[i].value, b[i].value)) return false;
        return true;
    }
}
