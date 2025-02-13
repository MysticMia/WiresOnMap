using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WiresOnMap;

public class WireGlobalItem : GlobalItem
{
    public override bool? UseItem(Item item, Player player)
    {
        if (item.type == ItemID.WireKite || // Grand Design? what a name.
            item.type == ItemID.Wire)
            WireOverwrite.UpdateWireMap();
        return base.UseItem(item, player);
    }
}