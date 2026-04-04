namespace LobbyAPI.Models
{
    public class Character
    {
        public long CharacterInstanceID { get; set; }           
        public int HeroTypeId { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
    }
}