using Terraria;
using Terraria.ModLoader;

namespace WiresOnMap;

public class WirePlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        if (Main.myPlayer != Player.whoAmI) return;
        WireData.UpdateWireMap("ModPlayer.OnEnterWorld");
    }
}
