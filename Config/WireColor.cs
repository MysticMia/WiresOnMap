using System;
using System.Runtime.Serialization;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Config;

namespace WiresOnMap.Config;

public class WireColor
{
    public Color color;

    public override bool Equals(object obj) {
        if (obj is WireColor other)
            return color == other.color;
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return new { color }.GetHashCode();
    }
}