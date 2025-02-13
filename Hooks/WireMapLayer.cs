using System;
using System.Drawing;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using static WiresOnMap.WireConfig;

namespace WiresOnMap.Hooks;

public class WireMapLayer : ModMapLayer
{
    private static Color GetWireColor(WireByte wire)
    {
        return wire switch
        {
            WireByte.RedWire => Instance.RedWireColor, //Color.Red,
            WireByte.BlueWire => Instance.BlueWireColor, //Color.Blue,
            WireByte.GreenWire => Instance.GreenWireColor, //Color.Green,
            WireByte.YellowWire => Instance.YellowWireColor, //Color.Yellow,
            _ => Color.White
        };
    }

    private static RectangleF LineInsideCull(RectangleF clippingRect, RectangleF lineRectangle)
    {
        if (!clippingRect.IntersectsWith(lineRectangle)) return new RectangleF();

        // Function largely copied from Rectangle.Intersect(Rectangle, Rectangle), but now for RectangleF
        float rightSide = MathF.Min(
            clippingRect.X + clippingRect.Width,
            lineRectangle.X + lineRectangle.Width
        );
        float leftSide = MathF.Max(clippingRect.X, lineRectangle.X);
        float topSide = MathF.Max(clippingRect.Y, lineRectangle.Y);
        float bottomSide = MathF.Min(
            clippingRect.Y + clippingRect.Height,
            lineRectangle.Y + lineRectangle.Height
        );
        RectangleF result = new RectangleF(
            leftSide,
            topSide,
            rightSide - leftSide,
            bottomSide - topSide
        );
        return result;
    }

    private static void CustomDraw(MapOverlayDrawContext context, Vector2 position, Vector2 scale, Color color, Alignment alignment)
    {

        Texture2D texture = TextureAssets.MagicPixel.Value;
        SpriteFrame frame = new SpriteFrame(1, 1, 0, 0);

        Vector2 _mapPosition = (Vector2)context.GetType().GetField("_mapPosition", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).GetValue(context);
        Vector2 _mapOffset = (Vector2)context.GetType().GetField("_mapOffset", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).GetValue(context);
        float _mapScale = (float)context.GetType().GetField("_mapScale", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).GetValue(context);
        float _drawScale = (float)context.GetType().GetField("_drawScale", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).GetValue(context);
        Rectangle? _clippingRect = (Rectangle?)context.GetType().GetField("_clippingRect", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).GetValue(context);

        position = (position - _mapPosition) * _mapScale + _mapOffset;
        scale *= _mapScale;

        if (_clippingRect.HasValue)
        {
            RectangleF newClippingRect = new RectangleF(_clippingRect.Value.X, _clippingRect.Value.Y, _clippingRect.Value.Width, _clippingRect.Value.Height);
            RectangleF lineRectangle = new RectangleF(position.X, position.Y, scale.X, scale.Y);
            RectangleF result = LineInsideCull(newClippingRect, lineRectangle);
            position = new Vector2(result.X, result.Y);
            scale = new Vector2(result.Width, result.Height);
            if (scale == Vector2.Zero) return;
        }

        scale.Y /= 1000;

        Rectangle sourceRectangle = frame.GetSourceRectangle(texture);
        Vector2 origin = sourceRectangle.Size() * alignment.OffsetMultiplier;

        //Main.spriteBatch.Draw(texture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
        Main.spriteBatch.Draw(texture, position, sourceRectangle, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private static bool IsWireEnabled(WireByte wire)
    {
        return wire switch
        {
            // builderAccStatus[x] returns a WireVisibilityBuilderToggle which is a VanillaBuilderToggle.
            // In VanillaBuilderToggle.DisplayValue(), VanillaBuilderToggle.CurrentState == 0 returns "GameUI.Bright".
            // We want to show the wires only if they are shown as `Bright`
            WireByte.RedWire => Main.LocalPlayer.builderAccStatus[Player.BuilderAccToggleIDs.WireVisibility_Red] == 0,
            WireByte.BlueWire => Main.LocalPlayer.builderAccStatus[5] == 0, // blue and green are swapped lmao
            WireByte.GreenWire => Main.LocalPlayer.builderAccStatus[6] == 0,
            WireByte.YellowWire => Main.LocalPlayer.builderAccStatus[Player.BuilderAccToggleIDs.WireVisibility_Yellow] == 0,
            _ => false
        };
    }

    public override void Draw(ref MapOverlayDrawContext context, ref string text)
    {
        if (!WireData.Initialized ||
            !WiresUI.Settings.DrawWires ||
            !Instance.WiresOnMapEnabled)
            return;

        if (Instance.DebugMapDrawMessages)
            WireChat.LogToPlayer("Drawing Map", Color.Green);

        foreach (WireByte wire in WireData.WireKeys)
        {
            if (!IsWireEnabled(wire)) continue;
            Color color = GetWireColor(wire);

            foreach (((int, int) point, int length) in WireData.HorizontalLines[wire])
            {
                CustomDraw(context, new Vector2(point.Item1, point.Item2), new Vector2(length, 1), color, Alignment.TopLeft);
            }
            foreach (((int, int) point, int length) in WireData.VerticalLines[wire])
            {
                CustomDraw(context, new Vector2(point.Item1, point.Item2), new Vector2(1, length), color, Alignment.TopLeft);
            }
            foreach ((int, int) point in WireData.SinglePoints[wire])
            {
                CustomDraw(context, new Vector2(point.Item1, point.Item2), new Vector2(1, 1), color, Alignment.TopLeft);
            }
        }
    }
}
