using System;

namespace WiresOnMap;

public enum Direction : byte
{
    Vertical = 0,
    Horizontal = 1
}

[Flags]
public enum WireByte : byte
{
    RedWire    = 1 << 0,
    BlueWire   = 1 << 1,
    GreenWire  = 1 << 2,
    YellowWire = 1 << 3,
    WireHidden = 1 << 4,
    Teleporter = 1 << 5
}
