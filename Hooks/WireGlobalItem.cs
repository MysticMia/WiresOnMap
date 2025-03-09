using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WiresOnMap.Hooks;

public class WireGlobalItem : GlobalItem
{
    public override bool? UseItem(Item item, Player player)
    {
        if (player.whoAmI != Main.myPlayer) return null;
        if (item.type == ItemID.Teleporter)
            WireData.UpdateWireMap($"GlobalItem.UseItem from {item.Name}");
        return null;
    }
}