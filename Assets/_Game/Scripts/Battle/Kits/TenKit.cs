using UnityEngine;

/// <summary>
/// Тень (злодей, класс Стрелок, урон от Силы атаки). Навыки:
///  1) "Колода" (ур. 1) — метает 3 карты друг за другом, каждая — 45% Силы атаки (после пик — 60%).
///  2) "Карта судьбы" (ур. 3) — над Тенью 2 с крутится карта и останавливается на одной из мастей:
///     черви — лечит 20% HP; бубны — следующие 3 авто атаки быстрее; крести — +50% энергии;
///     пики — следующая Колода усилена (60% Силы атаки на карту вместо 45%).
///  3) "Монетка" (ур. 5, пассивный) — при смертельном ранении подбрасывает монетку: решка — оживает с 20% HP,
///     орёл — выбывает. Один раз за бой.
/// Перезарядки — в файле героя (Data/Villains/Villain_Ten). Кадры — в Data/Characters/Sprites_Ten.
/// </summary>
public class TenKit : HeroKit
{
    private const int Deck = 0, FateCard = 1, Coin = 2; // номера скилов

    /// <summary>Числа навыков — из файла героя (Skills → Values; по умолчанию — таблица KitParams).</summary>
    protected override void LoadParams()
    {
        cards = Mathf.Max(1, Count(Deck, "cards"));
        cardDamage = Num(Deck, "damage");
        cardBoosted = Num(Deck, "boosted");
        spinTime = Num(FateCard, "spin");
        heartsHeal = Num(FateCard, "heal");
        diamondsHits = Count(FateCard, "hits");
        diamondsSpeed = Num(FateCard, "speed");
        clubsEnergy = Num(FateCard, "energy");
        coinChance = Num(Coin, "chance");
        coinHp = Num(Coin, "hp");
    }

    [Header("Колода")]
    [SerializeField] private int cards = 3;                 // Карт за бросок
    [SerializeField] private float cardDamage = 0.45f;      // Урон карты (доля Силы атаки)
    [SerializeField] private float cardBoosted = 0.6f;      // Урон карты после пик
    [SerializeField] private float cardInterval = 0.18f;    // Интервал между картами
    [SerializeField] private float cardSpeed = 12f;         // Скорость
    [SerializeField] private float cardRange = 8f;          // Дальность

    [Header("Карта судьбы")]
    [SerializeField] private float spinTime = 2f;           // Сколько крутится карта
    [SerializeField] private float heartsHeal = 0.2f;       // Черви: лечение (доля макс. HP)
    [SerializeField] private int diamondsHits = 3;          // Бубны: сколько авто атак быстрее
    [SerializeField] private float diamondsSpeed = 1.5f;    // Бубны: множитель скорости этих атак
    [SerializeField] private float clubsEnergy = 0.5f;      // Крести: энергия (доля максимума)

    [Header("Монетка (пассивка)")]
    [SerializeField] private float coinChance = 0.5f;       // Шанс решки (ожить)
    [SerializeField] private float coinHp = 0.2f;           // С какой долей HP оживает

    private int current = -1;      // Какой скил сейчас применяется (-1 — никакой)
    private float actionLength;    // Сколько длится действие
    private int cardsThrown;       // Сколько карт уже брошено
    private float cardPower;       // Урон карт этого броска (доля Силы атаки)
    private bool spadesReady;      // Пики: следующая Колода усилена
    private int fastHitsLeft;      // Бубны: сколько авто атак ещё быстрее
    private float spinLeft;        // Сколько ещё крутится карта судьбы (0 — не крутится)
    private SkillFx spinCard;      // Картинка карты над головой
    private bool coinUsed;         // Монетку уже подбрасывали в этом бою

    /// <summary>Масти карты судьбы (название, цвет).</summary>
    private static readonly string[] SuitNames = { "ЧЕРВИ", "БУБНЫ", "КРЕСТИ", "ПИКИ" };
    private static readonly Color[] SuitColors =
    {
        new Color(1f, 0.35f, 0.4f), new Color(1f, 0.55f, 0.3f), new Color(0.5f, 1f, 0.55f), new Color(0.7f, 0.75f, 1f)
    };

    // ---------- Внешний вид и пассивки ----------

    public override int AnimSkill => ActionAlive ? current : -1;

    /// <summary>Бубны: следующие авто атаки быстрее.</summary>
    public override float AttackSpeedMultiplier => fastHitsLeft > 0 ? diamondsSpeed : 1f;

    /// <summary>Авто атака сделана — бубны считают удары.</summary>
    public override void OnAutoAttack()
    {
        if (fastHitsLeft > 0) fastHitsLeft--;
    }

    // ---------- Решение ----------

    public override bool Think(Fighter target, bool retreating)
    {
        if (target == null || !target.IsAlive) return false;
        Vector2 d = target.Position - self.Position;
        float absX = Mathf.Abs(d.x), absY = Mathf.Abs(d.y);

        // 2) Карта судьбы — идёт бой (крутится над головой, Тень в это время дерётся)
        if (IsReady(FateCard) && spinLeft <= 0f && d.magnitude <= 8f)
        {
            if (!Begin(FateCard, 0.3f, target)) return false;
            StartSpin();
            return true;
        }

        // 1) Колода — враг на одной линии
        if (IsReady(Deck) && target.IsTargetable && absY <= 0.6f && absX >= 1f && absX <= cardRange)
        {
            if (!Begin(Deck, 0.1f + cardInterval * cards + 0.15f, target)) return false;
            cardsThrown = 0;
            cardPower = spadesReady ? cardBoosted : cardDamage;
            if (spadesReady) Pop("ПИКИ: УСИЛЕННЫЕ КАРТЫ", SuitColors[3]);
            spadesReady = false;
            return true;
        }
        return false;
    }

    /// <summary>Начать действие скила skill длиной length против target.</summary>
    private bool Begin(int skill, float length, Fighter target)
    {
        if (!BeginAction(KitPose.Cast)) return false;
        StartCooldown(skill);
        current = skill;
        actionLength = length;
        self.Movement.FaceTowards(target.Position.x);
        return true;
    }

    // ---------- Колода ----------

    /// <summary>Метнуть одну карту.</summary>
    private void ThrowCard()
    {
        cardsThrown++;
        AnimCycle++;
        self.Combat.FireShot(new ShotInfo
        {
            power = self.Stats.attack * cardPower,
            vsSpecial = false,
            speed = cardSpeed,
            range = cardRange,
            size = 0.35f,
            stagger = 1,
            knockback = 1f,
            color = Color.white,
            flicker = new Color(1f, 0.3f, 0.35f)
        });
    }

    // ---------- Карта судьбы ----------

    /// <summary>Карта начинает крутиться над головой.</summary>
    private void StartSpin()
    {
        spinLeft = spinTime;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            spinCard = SkillFx.Spawn(circle, self.transform.position, Color.white, new Vector2(0.35f, 0.5f), new Vector2(0.35f, 0.5f), spinTime + 0.6f, 80)
                .Follow(self.transform, new Vector3(0f, 2.4f, 0f));
        Pop("КАРТА СУДЬБЫ", Color.white);
    }

    /// <summary>Карта крутится (мигает мастями), через 2 с — выпадает масть.</summary>
    private void UpdateSpin()
    {
        if (spinLeft <= 0f) return;
        spinLeft -= Time.deltaTime;
        if (spinLeft > 0f) return;
        if (!self.IsAlive) return;
        int suit = Random.Range(0, 4);
        switch (suit)
        {
            case 0: self.Health.Heal(self.Health.Max * heartsHeal, self); break;          // черви — лечение
            case 1: fastHitsLeft = diamondsHits; break;                                    // бубны — быстрые атаки
            case 2: self.Combat.AddEnergy(self.Combat.MaxEnergy * clubsEnergy); break;    // крести — энергия
            default: spadesReady = true; break;                                            // пики — усиленная Колода
        }
        string effect = suit == 0 ? "лечение" : suit == 1 ? $"{diamondsHits} быстрые атаки" : suit == 2 ? "энергия" : "сильные карты";
        Pop($"{SuitNames[suit]}: {effect}", SuitColors[suit]);
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, self.transform.position, SuitColors[suit], new Vector2(0.4f, 0.55f), new Vector2(0.5f, 0.7f), 0.8f, 81)
                .Fade(1f, 0f).Follow(self.transform, new Vector3(0f, 2.4f, 0f));
        if (spinCard != null) { spinCard.Kill(); spinCard = null; }
    }

    // ---------- Монетка ----------

    /// <summary>Монетка: при смертельном ранении — решка (ожить с 20% HP) или орёл (выбыть). Один раз за бой.</summary>
    public override bool TryCheatDeath(out float hpFraction)
    {
        hpFraction = 0f;
        if (coinUsed || !IsUnlocked(Coin)) return false;
        coinUsed = true;
        bool tails = Random.value < coinChance;
        Sprite circle = self.View != null ? self.View.CircleSprite : null;
        if (circle != null)
            SkillFx.Spawn(circle, self.transform.position, new Color(1f, 0.85f, 0.3f), new Vector2(0.3f, 0.3f), new Vector2(0.3f, 0.3f), 0.8f, 82)
                .Fade(1f, 0f).Move(new Vector3(0f, 2.5f, 0f));
        if (!tails) { Pop("ОРЁЛ...", new Color(1f, 0.85f, 0.3f)); return false; }
        Pop("РЕШКА! ОЖИЛ", new Color(1f, 0.85f, 0.3f));
        hpFraction = coinHp;
        return true;
    }

    // ---------- Ход действия ----------

    protected override void Update()
    {
        base.Update();
        if (self != null) UpdateSpin();
        if (Pose == KitPose.None) return;
        if (!ActionAlive) { Pose = KitPose.None; current = -1; return; } // по Тени попали — действие прервано
        ActionTime += Time.deltaTime;

        if (current == Deck && cardsThrown < cards && ActionTime >= 0.1f + cardsThrown * cardInterval) ThrowCard();

        if (ActionTime >= actionLength)
        {
            current = -1;
            EndAction();
        }
    }
}
