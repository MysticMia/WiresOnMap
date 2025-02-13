using System;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.Localization;
using Terraria.ModLoader;

namespace WiresOnMap;

public class WireChat
{
    public static ILog Logger => ModContent.GetInstance<WiresOnMap>().Logger;

    /// <summary>
    /// Send a message to the player client in chat with a given color.
    /// </summary>
    /// <param name="msg">The message to send. Is automatically prefixed with the timestamp when the Debug config has been enabled.</param>
    /// <param name="color">The color the message should have.</param>
    public static void LogToPlayer(string msg, Color color)
    {
        if (Main.dedServ) return;

        if (true) //Instance.Debug.Enabled)
        {
            string time = DateTime.Now.Minute + ":" +
                          (DateTime.Now.Second + DateTime.Now.Millisecond * 0.001f).ToString("00.00");
            msg = time + "| " + msg;
        }

        ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(msg), color, Main.myPlayer);
    }
}