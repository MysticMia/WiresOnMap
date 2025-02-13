using System;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
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

namespace WiresOnMap;

public class WireMap : ModMapLayer
{
    private static readonly WireByte[] WireKeys = Enum.GetValues(typeof(WireByte)).Cast<WireByte>().ToArray();
    private static Dictionary<WireByte, List<((int, int),int)>> _horizontalLines;
    private static Dictionary<WireByte, List<((int, int),int)>> _verticalLines;
    private static Dictionary<WireByte, List<(int, int)>> _singlePoints;
    private static DateTime _updateTime;
    private static bool _initialized = false;
    private static bool _updateBusy = false;

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

    private static int GetWireByteIndex(WireByte wire)
    {
        return wire switch
        {
            WireByte.RedWire => 0,
            WireByte.BlueWire => 1,
            WireByte.GreenWire => 2,
            WireByte.YellowWire => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, null)
        };
    }

    private static void AddWiresAsLine(WireByte[,] wireMap, List<((int,int),int)> lineList, Direction direction, WireByte wire, (int, int) point, int lineLength)
    {
        if (lineLength < 3) return;

        lineList.Add((point, lineLength));
        for (int d = 0; d < lineLength; d++) {
            if (direction == Direction.Horizontal)
                wireMap[point.Item1 + d, point.Item2] &= ~wire;
            else if (direction == Direction.Vertical)
                wireMap[point.Item1, point.Item2 + d] &= ~wire;
        }
    }

    private static Dictionary<WireByte, List<(int,int)>> UpdateSingles(WireByte[,] wireMap)
    {
        Dictionary<WireByte, List<(int, int)>> singlePoints = new();
        foreach (WireByte wire in WireKeys)
            singlePoints[wire] = new List<(int, int)>();

        for (int x = 0; x < Main.tile.Width; x++)
        {
            for (int y = 0; y < Main.tile.Height; y++)
            {
                if (wireMap[x, y] == 0) continue;
                foreach (WireByte wire in WireKeys)
                    if (wireMap[x, y].HasFlag(wire))
                        singlePoints[wire].Add((x, y));
            }
        }

        return singlePoints;
    }

    private static (Dictionary<WireByte, List<((int, int), int)>>, Dictionary<WireByte, List<((int, int), int)>>,
        Dictionary<WireByte, List<(int, int)>>) CompressWireMap(WireByte[,] wireMap)
    {
        Dictionary<WireByte, List<((int, int), int)>> horizontalLines = new();
        Dictionary<WireByte, List<((int, int), int)>> verticalLines = new();

        int[] wireLineStartPositions = new int[WireKeys.Length]; // defaults to 0
        bool[] wireInLine = new bool[WireKeys.Length]; // defaults to false

        foreach (WireByte wire in WireKeys)
        {
            verticalLines[wire] = new List<((int, int), int)>();
            horizontalLines[wire] = new List<((int, int), int)>();
        }

        // Iterate each column. If wires are next to each other, save them as line
        for (int x = 0; x < Main.tile.Width; x++)
        {
            for (int y = 0; y < Main.tile.Height; y++)
            {
                foreach (WireByte wire in WireKeys)
                {
                    int wireIndex = GetWireByteIndex(wire);
                    if (wireMap[x, y].HasFlag(wire))
                    {
                        if (wireInLine[wireIndex]) continue;
                        wireInLine[wireIndex] = true;
                        wireLineStartPositions[wireIndex] = y;
                    }
                    else if (wireInLine[wireIndex])
                    {
                        wireInLine[wireIndex] = false;
                        int wireStart = wireLineStartPositions[wireIndex];
                        AddWiresAsLine(wireMap, verticalLines[wire], Direction.Vertical, wire, (x, wireStart),
                            y - wireStart);
                    }
                }
            }

            foreach (WireByte wire in WireKeys)
            {
                int wireIndex = GetWireByteIndex(wire);
                if (wireInLine[wireIndex])
                {
                    int wireStart = wireLineStartPositions[wireIndex];
                    AddWiresAsLine(wireMap, verticalLines[wire], Direction.Vertical, wire, (x, wireStart),
                        Main.tile.Height - 1 - wireStart);
                }
            }
        }

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Compressed vertical wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds.", Color.YellowGreen);
        _updateTime = DateTime.Now;

        // Iterate each row. If wires are next to each other, save them as line
        for (int y = 0; y < Main.tile.Height; y++)
        {
            for (int x = 0; x < Main.tile.Width; x++)
            {
                foreach (WireByte wire in WireKeys)
                {
                    int wireIndex = GetWireByteIndex(wire);
                    if (wireMap[x, y].HasFlag(wire))
                    {
                        if (wireInLine[wireIndex]) continue;
                        wireInLine[wireIndex] = true;
                        wireLineStartPositions[wireIndex] = x;
                    }
                    else if (wireInLine[wireIndex])
                    {
                        wireInLine[wireIndex] = false;
                        int wireStart = wireLineStartPositions[wireIndex];
                        AddWiresAsLine(wireMap, horizontalLines[wire], Direction.Horizontal, wire, (wireStart, y),
                            x - wireStart);
                    }
                }
            }

            foreach (WireByte wire in WireKeys)
            {
                int wireIndex = GetWireByteIndex(wire);
                if (wireInLine[wireIndex])
                {
                    int wireStart = wireLineStartPositions[wireIndex];
                    AddWiresAsLine(wireMap, horizontalLines[wire], Direction.Horizontal, wire, (wireStart, y),
                        Main.tile.Width - 1 - wireStart);
                }
            }
        }

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Compressed horizontal wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds.", Color.YellowGreen);
        _updateTime = DateTime.Now;

        Dictionary<WireByte, List<(int, int)>> singlePoints = UpdateSingles(wireMap);

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Copied single points in {(DateTime.Now - _updateTime).TotalSeconds} seconds.", Color.YellowGreen);
        _updateTime = DateTime.Now;

        return (horizontalLines, verticalLines, singlePoints);
    }

    private static async Task<WireByte[,]> GetTileWires()
    {
        WireByte[,] wireMap = new WireByte[Main.tile.Width, Main.tile.Height];

        int yieldStepCount = 0;
        bool hideInFog = Instance.HideWiresInFogOfWar;

        for (int x = 0; x < Main.tile.Width; x++)
        {
            if (x * Main.tile.Height - yieldStepCount > 100000) // give cpu time to catch up every x tiles
            {
                await Task.Yield();
                yieldStepCount = x * Main.tile.Height;
            }

            for (int y = 0; y < Main.tile.Height; y++)
            {
                bool hidden = hideInFog && !Main.Map.IsRevealed(x, y);

                Tile tile = Main.tile[x, y];
                if (tile.RedWire && !hidden)
                    wireMap[x, y] |= WireByte.RedWire;
                else
                    wireMap[x, y] &= ~WireByte.RedWire;

                if (tile.BlueWire && !hidden)
                    wireMap[x, y] |= WireByte.BlueWire;
                else
                    wireMap[x, y] &= ~WireByte.BlueWire;

                if (tile.GreenWire && !hidden)
                    wireMap[x, y] |= WireByte.GreenWire;
                else
                    wireMap[x, y] &= ~WireByte.GreenWire;

                if (tile.YellowWire && !hidden)
                    wireMap[x, y] |= WireByte.YellowWire;
                else
                    wireMap[x, y] &= ~WireByte.YellowWire;
            }
        }

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Indexed all wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds ({Math.Ceiling(yieldStepCount / 100000.0)} yields).", Color.YellowGreen);
        _updateTime = DateTime.Now;

        return wireMap;
    }

    public static async void UpdateWireMap()
    {
        if (_updateBusy) return;
        _updateBusy = true;

        _updateTime = DateTime.Now;
        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Updating wires on {Main.tile.Height * Main.tile.Width} tiles...", Color.YellowGreen);

        WireByte[,] wireMap = await GetTileWires();
        (_horizontalLines, _verticalLines, _singlePoints) = CompressWireMap(wireMap);

        _initialized = true;
        _updateBusy = false;
    }

    private static RectangleF LineInsideCull(RectangleF clippingRect, RectangleF lineRectangle)
    {
        if (clippingRect.IntersectsWith(lineRectangle))
        {
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
        return new RectangleF();
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
        if (!_initialized ||
            !WiresUI.Settings.DrawWires ||
            !Instance.WiresOnMapEnabled)
            return;


        if (Instance.DebugMapDrawMessages)
            WireChat.LogToPlayer("Drawing Map", Color.Green);

        foreach (WireByte wire in WireKeys)
        {
            if (!IsWireEnabled(wire)) continue;
            Color color = GetWireColor(wire);

            foreach (((int, int) point, int length) in _horizontalLines[wire])
            {
                CustomDraw(context, new Vector2(point.Item1, point.Item2), new Vector2(length, 1), color, Alignment.TopLeft);
            }
            foreach (((int, int) point, int length) in _verticalLines[wire])
            {
                CustomDraw(context, new Vector2(point.Item1, point.Item2), new Vector2(1, length), color, Alignment.TopLeft);
            }
            foreach ((int, int) point in _singlePoints[wire])
            {
                CustomDraw(context, new Vector2(point.Item1, point.Item2), new Vector2(1, 1), color, Alignment.TopLeft);
            }
        }
    }
}
