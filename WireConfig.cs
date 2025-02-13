using System.ComponentModel;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Config;

namespace WiresOnMap;

public class WireConfig : ModConfig
{
    public static WireConfig Instance;
    public override ConfigScope Mode => ConfigScope.ClientSide;

    #region Functions
    public override void OnChanged()
    {
        WireMap.UpdateWireMap();
    }
    #endregion

    #region Main config
    [Header("MainConfiguration")]
    [DefaultValue(true)] public bool WiresOnMapEnabled;
    [DefaultValue(true)] public bool HideWiresInFogOfWar;
    [DefaultValue(false)] public bool DebugMapDrawMessages;
    [DefaultValue(false)] public bool DebugWireUpdateMessages;
    #endregion

    #region Color configs
    [Header("MapWireColors")]
    [DefaultValue(typeof(Color), "255, 0, 0, 255")] public Color RedWireColor;
    [DefaultValue(typeof(Color), "0, 0, 255, 255")] public Color BlueWireColor;
    [DefaultValue(typeof(Color), "0, 255, 0, 255")] public Color GreenWireColor;
    [DefaultValue(typeof(Color), "255, 255, 0, 255")] public Color YellowWireColor;
    #endregion
}
