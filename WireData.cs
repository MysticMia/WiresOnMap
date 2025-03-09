using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using WiresOnMap.Hooks;
using static WiresOnMap.WireConfig;

namespace WiresOnMap;

public static class WireData
{
    public static Dictionary<WireByte, List<((int, int),int)>> HorizontalLines;
    public static Dictionary<WireByte, List<((int, int),int)>> VerticalLines;
    public static Dictionary<WireByte, List<(int, int)>> SinglePoints;
    public static List<(int, int)> Teleporters;
    public static bool Initialized;
    private static DateTime _updateTime;
    private static bool _updateBusy;
    private const int YieldThreshold = 100_000;

    public static readonly WireByte[] WireKeys = [
        WireByte.RedWire,
        WireByte.BlueWire,
        WireByte.GreenWire,
        WireByte.YellowWire,
        WireByte.RedWire | WireByte.WireHidden,
        WireByte.BlueWire | WireByte.WireHidden,
        WireByte.GreenWire | WireByte.WireHidden,
        WireByte.YellowWire | WireByte.WireHidden
    ];

    public const WireByte WireBits = WireByte.RedWire | WireByte.BlueWire | WireByte.GreenWire | WireByte.YellowWire;

    private static int GetWireByteIndex(WireByte wire)
    {
        return (wire & ~WireByte.Teleporter) switch
        {
            WireByte.RedWire    => 0,                           // 0b0000_0001
            WireByte.BlueWire   => 1,                           // 0b0000_0010
            WireByte.GreenWire  => 2,                           // 0b0000_0100
            WireByte.YellowWire => 3,                           // 0b0000_1000
            WireByte.RedWire | WireByte.WireHidden    => 4,     // 0b0001_0001
            WireByte.BlueWire | WireByte.WireHidden   => 5,     // 0b0001_0010
            WireByte.GreenWire | WireByte.WireHidden  => 6,     // 0b0001_0100
            WireByte.YellowWire | WireByte.WireHidden => 7,     // 0b0001_1000
            _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, null)
        };
    }

    private static void AddWiresAsLine(WireByte[,] wireMap, List<((int,int),int)> lineList, Direction direction, WireByte wire, (int, int) point, int lineLength)
    {
        if (lineLength < 3) return;

        lineList.Add((point, lineLength));
        for (int d = 0; d < lineLength; d++)
            if (direction == Direction.Horizontal)
                wireMap[point.Item1 + d, point.Item2] &= ~(wire & WireBits);
            else if (direction == Direction.Vertical)
                wireMap[point.Item1, point.Item2 + d] &= ~(wire & WireBits);
    }

    private static (Dictionary<WireByte, List<(int,int)>>, List<(int, int)>) UpdateSingles(WireByte[,] wireMap)
    {
        Dictionary<WireByte, List<(int, int)>> singlePoints = new();
        List<(int, int)> teleporters = new();

        foreach (WireByte wire in WireKeys)
            singlePoints[wire] = new List<(int, int)>();

        for (int x = 0; x < Main.tile.Width; x++)
        {
            for (int y = 0; y < Main.tile.Height; y++)
            {
                if (wireMap[x, y].HasFlag(WireByte.Teleporter))
                    teleporters.Add((x, y));

                if ((wireMap[x, y] & WireBits) == 0) continue;
                foreach (WireByte wire in WireKeys)
                    if ((wireMap[x,y] & WireBits).HasFlag(wire & WireBits) && // tile contains wire color
                        (wireMap[x,y] & WireByte.WireHidden) == (wire & WireByte.WireHidden)
                        // TileHasWireType(wireMap[x,y], wire)
                        // // Copy-pasting the function's code, because the compiler can optimize it much better that way.
                       )
                    {
                        singlePoints[wire].Add((x, y));
                    }
            }
        }

        return (singlePoints, teleporters);
    }

    private static bool TileHasWireType(WireByte tile, WireByte wire)
    {
        return (tile & WireBits).HasFlag(wire & WireBits) && // tile contains wire color
               (tile & WireByte.WireHidden) == (wire & WireByte.WireHidden); // tile's hidden type matches the wire's
    }

    private static async Task<(Dictionary<WireByte, List<((int, int), int)>>, Dictionary<WireByte, List<((int, int), int)>>,
        Dictionary<WireByte, List<(int, int)>>, List<(int, int)>)> CompressWireMap(WireByte[,] wireMap)
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

                    if ((wireMap[x,y] & WireBits).HasFlag(wire & WireBits) && // tile contains wire color
                        (wireMap[x,y] & WireByte.WireHidden) == (wire & WireByte.WireHidden)
                        // TileHasWireType(wireMap[x,y], wire)
                        // // Copy-pasting the function's code, because the compiler can optimize it much better that way.
                        )
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
                    if ((wireMap[x,y] & WireBits).HasFlag(wire & WireBits) && // tile contains wire color
                        (wireMap[x,y] & WireByte.WireHidden) == (wire & WireByte.WireHidden)
                        // TileHasWireType(wireMap[x,y], wire)
                        // // Copy-pasting the function's code, because the compiler can optimize it much better that way.
                       )
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

        (Dictionary<WireByte, List<(int, int)>> singlePoints, List<(int, int)> teleporters) = UpdateSingles(wireMap);

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Copied single points in {(DateTime.Now - _updateTime).TotalSeconds} seconds.", Color.YellowGreen);
        _updateTime = DateTime.Now;

        return (horizontalLines, verticalLines, singlePoints, teleporters);
    }

    private static async Task<WireByte[,]> GetTileWires()
    {
        WireByte[,] wireMap = new WireByte[Main.tile.Width, Main.tile.Height];
        int yieldStepCount = 0;

        for (int x = 0; x < Main.tile.Width; x++)
        {
            if (x * Main.tile.Height - YieldThreshold * yieldStepCount > YieldThreshold) // give cpu time to catch up every x tiles
            {
                await Task.Yield();
                yieldStepCount++;
            }

            for (int y = 0; y < Main.tile.Height; y++)
            {
                Tile tile = Main.tile[x, y];

                if (!Main.Map.IsRevealed(x, y))
                {
                    if (Instance.HideWiresInFogOfWar) continue;
                    if (Instance.FadeWiresInFogOfWar)
                        wireMap[x, y] |= WireByte.WireHidden;
                }
                if (tile.RedWire)
                    wireMap[x, y] |= WireByte.RedWire;

                if (tile.BlueWire)
                    wireMap[x, y] |= WireByte.BlueWire;

                if (tile.GreenWire)
                    wireMap[x, y] |= WireByte.GreenWire;

                if (tile.YellowWire)
                    wireMap[x, y] |= WireByte.YellowWire;

                if (tile.TileType == TileID.Teleporter)
                {
                    // Teleporters are 3 wide. The leftmost tile has TileFrameX = 0. The middle = 18, the right = 36.
                    // We only want to track the middle one.
                    if (tile.TileFrameX == 18)
                        wireMap[x, y] |= WireByte.Teleporter;
                    // WireChat.LogToPlayer($"Teleporter at {x},{y}, frameX={tile.TileFrameX}, frameY={tile.TileFrameY}, " +
                    //                      $"frameN={tile.TileFrameNumber}, tileType={tile.TileType}, blockType{tile.BlockType}", Color.Blue);
                }
            }
        }

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Indexed all wires in {(DateTime.Now - _updateTime).TotalSeconds} seconds ({yieldStepCount} yields).", Color.YellowGreen);
        _updateTime = DateTime.Now;

        return wireMap;
    }

    public static async void UpdateWireMap(string source)
    {
        if (!Instance.WiresOnMapEnabled)
        {
            WireMapLayer mapLayer = ModContent.GetInstance<WireMapLayer>();
            if (mapLayer != null)
                mapLayer.Visible = false;
            return;
        }

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Attempting wire map update from: {source}", Color.Yellow);

        if (_updateBusy) return;
        _updateBusy = true;

        _updateTime = DateTime.Now;
        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Updating wires on {Main.tile.Height * Main.tile.Width} tiles...", Color.YellowGreen);

        try
        {
            WireByte[,] wireMap = await GetTileWires();
            (HorizontalLines, VerticalLines, SinglePoints, Teleporters) = await CompressWireMap(wireMap);
        }
        catch (Exception ex)
        {
            WireChat.Logger.Warn("Error trying to load or compress wire map data: " + ex.Message + "\n" + ex.StackTrace);
            Initialized = false;
            _updateBusy = false;
            return;
        }

        if (Instance.DebugWireUpdateMessages)
            WireChat.LogToPlayer($"Finished wire map update from: {source}", Color.Yellow);

        Initialized = true;
        _updateBusy = false;
    }
}