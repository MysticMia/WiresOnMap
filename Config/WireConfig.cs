using System.ComponentModel;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using WiresOnMap.Config;
using WiresOnMap.Hooks;

namespace WiresOnMap;

public class WireConfig : ModConfig
{
    public static WireConfig Instance;
    public override ConfigScope Mode => ConfigScope.ClientSide;

    #region Functions
    public override void OnChanged()
    {
        WireMapLayer mapLayer = ModContent.GetInstance<WireMapLayer>();
        if (mapLayer != null)
            mapLayer.Visible = Instance.WiresOnMapEnabled;

        WireData.UpdateWireMap("ModConfig.OnChanged");
    }
    #endregion

    #region Main config
    [Header("MainConfiguration")]
    [DefaultValue(true)] public bool WiresOnMapEnabled;
    [DefaultValue(true)] public bool DrawTeleporters;
    [Range(0.5f,5)] [Increment(0.25f)] [DefaultValue(1f)] public float WireThickness;
    [DefaultValue(true)] public bool FadeWiresInFogOfWar;
    [Range(0f,1f)] [Increment(0.01f)] [DefaultValue(0.3f)] public float OpacityMultiplier;
    [DefaultValue(false)] public bool HideWiresInFogOfWar;
    #endregion

    #region Color configs
    [Header("MapWireColors")]
    [DefaultValue(typeof(Color), "255, 0, 0, 255")] public Color RedWireColor;
    [DefaultValue(typeof(Color), "0, 0, 255, 255")] public Color BlueWireColor;
    [DefaultValue(typeof(Color), "0, 255, 0, 255")] public Color GreenWireColor;
    [DefaultValue(typeof(Color), "255, 255, 0, 255")] public Color YellowWireColor;
    #endregion

    #region Debug
    [Header("Debug")]
    [DefaultValue(false)] public bool DebugMapDrawMessages;
    [DefaultValue(false)] public bool DebugWireUpdateMessages;
    #endregion
}
