using System;
using Terraria;
using Terraria.ModLoader;

namespace WiresOnMap;
public class WiresOnMap : Mod
{

    public WiresOnMap()
    {
        On_WorldGen.PlaceWire += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.PlaceWire");
        On_WorldGen.PlaceWire2 += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.PlaceWire2");
        On_WorldGen.PlaceWire3 += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.PlaceWire3");
        On_WorldGen.PlaceWire4 += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.PlaceWire4");
        On_WorldGen.KillWire += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.KillWire");
        On_WorldGen.KillWire2 += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.KillWire2");
        On_WorldGen.KillWire3 += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.KillWire3");
        On_WorldGen.KillWire4 += (orig, x, y) => HookWireUpdate(orig, x, y, "WorldGen.KillWire4");
    }

    private bool HookWireUpdate<T>(T orig, int x, int y, string source) where T : Delegate
    {
        // Woah AI wrote this. Otherwise, I'd have to make 8 identical functions for each return type:
        // - On_WorldGen.orig_PlaceWire, PlaceWire2, 3, 4, and KillWire, KillWire2, 3, 4.
        WireData.UpdateWireMap(source);
        return (bool)orig.DynamicInvoke(x, y);
    }
}
