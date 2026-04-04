namespace LobbyServer.Models
{
    public class GearModel
    {
        public long InstanceID { get; set; }
        public GearType Type { get; set; }
        public int TypeInstance { get; set; }
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

    public enum WeaponInstance
    {
        Pistol = 1,
        Shotgun = 2,
        Sniper = 3,
        MachineGun = 4
    }

    public enum AromrInstance
    {
        Dummy = 101,
    }
}
