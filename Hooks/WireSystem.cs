using Terraria.ModLoader;

namespace WiresOnMap;

public class WireSystem : ModSystem
{
    public override void OnWorldLoad()
    {
        WireData.UpdateWireMap();
        base.OnWorldLoad();
    }
}
