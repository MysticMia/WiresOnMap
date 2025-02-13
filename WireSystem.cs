using Terraria.ModLoader;

namespace WiresOnMap;

public class WireSystem : ModSystem
{
    public override void OnWorldLoad()
    {
        WireOverwrite.UpdateWireMap();
        base.OnWorldLoad();
    }
}