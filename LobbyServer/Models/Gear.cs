namespace LobbyServer.Models
{
    public class Gear
    {
        public long InstanceID { get; set; }
        public GearType Type { get; set; }
        public GearInstanceType TypeInstance { get; set; }
        public int Enchant { get; set; }
        public bool IsEquipped { get; set; }
    }

    public enum GearType
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        End
    }

    public enum GearInstanceType
    {
        Pistol = 1,
        Shotgun = 2,
        Sniper = 3,
        MachineGun = 4,

        DummyAromr = 101
    }
}
