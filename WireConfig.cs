using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace WiresOnMap;

public class WireConfig : ModConfig
{
    public static WireConfig Instance;
    public override ConfigScope Mode => ConfigScope.ClientSide;

    #region Functions
    public override void OnChanged()
    {
    }
    #endregion

    #region Main Configuration
    [Header("MainConfiguration")]
    [DefaultValue(true)] public bool WiresOnMapEnabled;
    [DefaultValue(true)] public bool HideWiresInFogOfWar;
    [DefaultValue(false)] public bool DebugMapDrawMessages;
    [DefaultValue(false)] public bool DebugWireUpdateMessages;
    #endregion
}
