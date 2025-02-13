using Terraria;
using Terraria.ModLoader;

namespace WiresOnMap;

public class WirePlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        WireOverwrite.UpdateWireMap();
    }
}