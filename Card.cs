namespace Monster_Trading_Cards_Game;

public class Card
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Damage { get; set; }
    public string Element { get; set; }
    public string CardType { get; set; }

    public Card(int id, string name, int damage, string element, string cardType)
    {
        Id = id;
        Name = name;
        Damage = damage;
        Element = element;
        CardType = cardType;
    }
}