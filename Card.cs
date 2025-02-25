namespace Monster_Trading_Cards_Game;

public enum CardType
{
    MonsterCard,
    SpellCard
};

public enum ElementType
{
    Normal,
    Fire,
    Water
};

public class Card
{
    public string Name { get; private set; }
    public int Damage { get; private set; }
    public CardType Type { get; private set; }
    public ElementType ElementType { get; private set; }
}