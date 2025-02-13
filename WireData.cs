using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using WiresOnMap.Hooks;
using static WiresOnMap.WireConfig;

namespace WiresOnMap;

public static class WireData
{
    public static Dictionary<WireByte, List<((int, int),int)>> HorizontalLines;
    public static Dictionary<WireByte, List<((int, int),int)>> VerticalLines;
    public static Dictionary<WireByte, List<(int, int)>> SinglePoints;
    public static bool Initialized;
    private static DateTime _updateTime;
    private static bool _updateBusy;
    private const int YieldThreshold = 100_000;

    public static readonly WireByte[] WireKeys = Enum.GetValues(typeof(WireByte)).Cast<WireByte>().ToArray();

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
        for (int d = 0; d < lineLength; d++)
            if (direction == Direction.Horizontal)
                wireMap[point.Item1 + d, point.Item2] &= ~wire;
            else if (direction == Direction.Vertical)
                wireMap[point.Item1, point.Item2 + d] &= ~wire;
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

    private static async Task<(Dictionary<WireByte, List<((int, int), int)>>, Dictionary<WireByte, List<((int, int), int)>>,
        Dictionary<WireByte, List<(int, int)>>)> CompressWireMap(WireByte[,] wireMap)
    {
        Dictionary<WireByte, List<((int, int), int)>> horizontalLines = new();
        Dictionary<WireByte, List<((int, int), int)>> verticalLines = new();

        int[] wireLineStartPositions = new int[WireKeys.Length]; // defaults to 0
        bool[] wireInLine = new bool[WireKeys.Length]; // defaults to false

        int yieldStepCount = 0;

        foreach (WireByte wire in WireKeys)
        {
            verticalLines[wire] = new List<((int, int), int)>();
            horizontalLines[wire] = new List<((int, int), int)>();
        }

        // Iterate each column. If wires are next to each other, save them as line
        for (int x = 0; x < Main.tile.Width; x++)
        {
            if (x * Main.tile.Height - YieldThreshold * yieldStepCount > YieldThreshold) // give cpu time to catch up every x tiles
            {
                await Task.Yield();
                yieldStepCount++;
            }

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
            WireChat.LogToPlayer($"Compressed vertical wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds ({yieldStepCount} yields).", Color.YellowGreen);
        _updateTime = DateTime.Now;

        yieldStepCount = 0;
        // Iterate each row. If wires are next to each other, save them as line
        for (int y = 0; y < Main.tile.Height; y++)
        {
            if (y * Main.tile.Width - YieldThreshold * yieldStepCount > YieldThreshold) // give cpu time to catch up every x tiles
            {
                await Task.Yield();
                yieldStepCount++;
            }

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
            WireChat.LogToPlayer($"Compressed horizontal wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds ({yieldStepCount} yields).", Color.YellowGreen);
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
            if (x * Main.tile.Height - YieldThreshold * yieldStepCount > YieldThreshold) // give cpu time to catch up every x tiles
            {
                await Task.Yield();
                yieldStepCount++;
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
            WireChat.LogToPlayer($"Indexed all wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds ({yieldStepCount} yields).", Color.YellowGreen);
        _updateTime = DateTime.Now;

        return wireMap;
    }

    public static async void UpdateWireMap()
    {
        if (!Instance.WiresOnMapEnabled)
        {
            WireMapLayer mapLayer = ModContent.GetInstance<WireMapLayer>();
            if (mapLayer != null)
                mapLayer.Visible = false;
            return;
        }

        if (_updateBusy) return;
        _updateBusy = true;

        _updateTime = DateTime.Now;
        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Updating wires on {Main.tile.Height * Main.tile.Width} tiles...", Color.YellowGreen);

        WireByte[,] wireMap = await GetTileWires();
        (HorizontalLines, VerticalLines, SinglePoints) = await CompressWireMap(wireMap);

        Initialized = true;
        _updateBusy = false;
    }
}