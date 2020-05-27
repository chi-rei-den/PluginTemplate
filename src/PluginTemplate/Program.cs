using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Plugin
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Author => "Author";

        public override string Description => "Description";

        public override string Name => "Plugin Name";

        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(
                permissions: new List<string> { "plugin.permission1", "plugin.permission2", },
                cmd: this.Cmd,
                "command1", "alias1", "alias2"));
        }

        private void Cmd(CommandArgs args)
        {
            args.Player.SendSuccessMessage("Welcome to your first command!");
        }
    }
}
