using System;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace WiresOnMap;

public class WireOverwrite : ModMapLayer
{
    private static readonly WireByte[] WireKeys = Enum.GetValues(typeof(WireByte)).Cast<WireByte>().ToArray();

    private static WireByte[,] _wireMap;
    private static Dictionary<WireByte, List<((int, int),int)>> _horizontalLines;
    private static Dictionary<WireByte, List<((int, int),int)>> _verticalLines;
    private static Dictionary<WireByte, List<(int, int)>> _singlePoints;
    private static bool initialized = false;

    private static void AddWiresAsLine(Direction direction, WireByte wire, (int, int) point, int lineLength)
    {
        if (lineLength < 3) return;

        if (direction == Direction.Horizontal)
        {
            _horizontalLines[wire].Add((point, lineLength));
            for (int d = 0; d <  lineLength; d++) {
                _wireMap[point.Item1 + d, point.Item2] &= ~wire;
            }
        }
        if (direction == Direction.Vertical)
        {
            _verticalLines[wire].Add((point, lineLength));
            for (int d = 0; d < lineLength; d++) {
                _wireMap[point.Item1, point.Item2 + d] &= ~wire;
            }
        }
    }

    private static void UpdateSingles()
    {
        _singlePoints = new Dictionary<WireByte, List<(int, int)>>();
        foreach (WireByte wire in WireKeys)
            _singlePoints[wire] = new List<(int, int)>();

        for (int x = 0; x < Main.tile.Width; x++)
        {
            for (int y = 0; y < Main.tile.Height; y++)
            {
                if (_wireMap[x, y] == 0) continue;
                foreach (WireByte wire in WireKeys)
                    if (_wireMap[x, y].HasFlag(wire))
                        _singlePoints[wire].Add((x, y));
            }
        }
    }

    private static void CompressWireMap()
    {
        _horizontalLines = new Dictionary<WireByte, List<((int, int), int)>>();
        _verticalLines = new Dictionary<WireByte, List<((int, int), int)>>();

        bool wiresInLine;
        int lineStartPosition = 0;

        // Iterate each column. If wires are next to each other, save them as line
        foreach (WireByte wire in WireKeys)
        {
            _verticalLines[wire] = new List<((int, int), int)>();
            for (int x = 0; x < Main.tile.Width; x++)
            {
                wiresInLine = false;
                for (int y = 0; y < Main.tile.Height; y++)
                {
                    if (_wireMap[x, y].HasFlag(wire))
                    {
                        if (wiresInLine) continue;

                        wiresInLine = true;
                        lineStartPosition = y;
                    }
                    else if (wiresInLine)
                    {
                        wiresInLine = false;
                        AddWiresAsLine(Direction.Vertical, wire, (x, lineStartPosition), y - lineStartPosition);
                    }
                }

                if (wiresInLine)
                    AddWiresAsLine(Direction.Vertical, wire, (x, lineStartPosition), Main.tile.Height - 1 - lineStartPosition);
            }
        }

        // Iterate each row. If wires are next to each other, save them as line
        foreach (WireByte wire in WireKeys)
        {
            _horizontalLines[wire] = new List<((int, int), int)>();
            for (int y = 0; y < Main.tile.Height; y++)
            {
                wiresInLine = false;
                for (int x = 0; x < Main.tile.Width; x++)
                {
                    if (_wireMap[x, y].HasFlag(wire))
                    {
                        if (wiresInLine) continue;

                        wiresInLine = true;
                        lineStartPosition = x;
                    }
                    else if (wiresInLine)
                    {
                        wiresInLine = false;
                        AddWiresAsLine(Direction.Horizontal, wire, (lineStartPosition, y), x - lineStartPosition);
                    }
                }

                if (wiresInLine)
                    AddWiresAsLine(Direction.Horizontal, wire, (lineStartPosition, y), Main.tile.Width - 1 - lineStartPosition);
            }
        }

        UpdateSingles();

    }

    public static void UpdateWireMap()
    {
        _wireMap = new WireByte[Main.tile.Width, Main.tile.Height];

        for (int x = 0; x < Main.tile.Width; x++)
        {
            for (int y = 0; y < Main.tile.Height; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.RedWire)
                    _wireMap[x, y] |= WireByte.RedWire;
                else
                    _wireMap[x, y] &= ~WireByte.RedWire;

                if (tile.BlueWire)
                    _wireMap[x, y] |= WireByte.BlueWire;
                else
                    _wireMap[x, y] &= ~WireByte.BlueWire;

                if (tile.GreenWire)
                    _wireMap[x, y] |= WireByte.GreenWire;
                else
                    _wireMap[x, y] &= ~WireByte.GreenWire;

                if (tile.YellowWire)
                    _wireMap[x, y] |= WireByte.YellowWire;
                else
                    _wireMap[x, y] &= ~WireByte.YellowWire;
            }
        }

        CompressWireMap();
        initialized = true;
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
        scale = new Vector2(scale.X, scale.Y / 1000) * _mapScale;

        if (_clippingRect.HasValue)
        {
            RectangleF newClippingRect = new RectangleF(_clippingRect.Value.X, _clippingRect.Value.Y, _clippingRect.Value.Width, _clippingRect.Value.Height);
            RectangleF lineRectangle = new RectangleF(position.X, position.Y, scale.X, scale.Y);
            RectangleF result = LineInsideCull(newClippingRect, lineRectangle);
            position = new Vector2(result.X, result.Y);
            scale = new Vector2(result.Width, result.Height);
            if (scale == Vector2.Zero) return;
        }

        Rectangle sourceRectangle = frame.GetSourceRectangle(texture);
        Vector2 origin = sourceRectangle.Size() * alignment.OffsetMultiplier;

        //Main.spriteBatch.Draw(texture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
        Main.spriteBatch.Draw(texture, position, sourceRectangle, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    public override void Draw(ref MapOverlayDrawContext context, ref string text)
    {
        if (!initialized) return;

        // for (int x = 0; x < Main.tile.Width; x++)
        // {
        //     for (int y = 0; y < Main.tile.Height; y++)
        //     {
        //         WireByte wire = _wireMap[x, y];
        //         if (wire == 0) continue;
        //         Color color;
        //         if (wire.HasFlag(WireByte.RedWire)) color = Color.Red;
        //         else if (wire.HasFlag(WireByte.BlueWire)) color = Color.Blue;
        //         else if (wire.HasFlag(WireByte.GreenWire)) color = Color.Green;
        //         else if (wire.HasFlag(WireByte.YellowWire)) color = Color.Yellow;
        //         else color = Color.White;
        //         CustomDraw(context, new Vector2(x, y), new Vector2(1,1), color, Alignment.TopLeft);
        //     }
        // }

        foreach (WireByte wire in WireKeys)
        {
            Color color = wire switch
            {
                WireByte.RedWire => Color.Red,
                WireByte.BlueWire => Color.Blue,
                WireByte.GreenWire => Color.Green,
                WireByte.YellowWire => Color.Yellow,
                _ => Color.White
            };

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