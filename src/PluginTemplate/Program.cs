using System;
using Terraria;
using TShockAPI;
using TerrariaApi.Server;
using System.Reflection;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;
using System.Threading;

namespace TSREWARD
{
    [ApiVersion(2, 1)]
    public class TSReward : TerrariaPlugin
    {
        public static Config config;
        WebClient wc = new WebClient { Proxy = null };
        public System.Timers.Timer Timer = new System.Timers.Timer();
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        public override string Author
        {
            get { return "Blackwolf"; }
        }
        public override string Name
        {
            get { return "Black Rewards"; }
        }

        public override string Description
        {
            get { return "Lets you claim ingame rewards for voting on Terraria-Servers.com"; }
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(Reward, "reward") { AllowServer = false });
            Commands.ChatCommands.Add(new Command("brreward.reload", Reload_Config, "brreload"));
            ReadConfig();
            Timer.Interval = 1000 * config.IntervalInSeconds;
            Timer.Enabled = config.ShowIntervalMessage;
            Timer.Elapsed += new System.Timers.ElapsedEventHandler(Timer_Elapsed);
        }

        void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            for (int i = 0; i < config.IntervalMessage.Text.Length; i++)
                TSPlayer.All.SendMessage(config.IntervalMessage.Text[i], config.IntervalMessage.GetColor());
        }

        public TSReward(Main game)
            : base(game)
        {
            Order = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }

        private void Reward(CommandArgs args)
        {
            if (!args.Player.IsLoggedIn)
            {
                args.Player.SendErrorMessage("You need to be logged in to use this command!");
                return;
            }
            Thread t = new Thread(() =>
                {
                    switch (CheckVote(args.Player.Name))
                    {
                        case Response.InvalidServerKey:
                            args.Player.SendErrorMessage("The server key is incorrect! Please contact an administrator.");
                            return;
                        case Response.Error:
                            args.Player.SendErrorMessage("There was an error reading your vote on terraria-servers.com!");
                            return;
                        case Response.NotFound:
                            for (int i = 0; i < config.VoteNotFoundMessage.Text.Length; i++)
                                args.Player.SendMessage(config.VoteNotFoundMessage.Text[i], config.VoteNotFoundMessage.GetColor());
                            return;
                        case Response.VotedAndClaimed:
                            args.Player.SendErrorMessage("You have already claimed your reward today!");
                            return;
                        case Response.VotedNotClaimed:
                            for (int i = 0; i < config.OnRewardClaimMessage.Text.Length; i++)
                                args.Player.SendMessage(config.OnRewardClaimMessage.Text[i], config.OnRewardClaimMessage.GetColor());

                            if (SetAsClaimed(args.Player.Name))
                            {
                                if (SEconomyPlugin.Instance != null)
                                {
                                    IBankAccount Server = SEconomyPlugin.Instance.GetBankAccount(TSServerPlayer.Server.UserID);
                                    IBankAccount Player = SEconomyPlugin.Instance.GetBankAccount(args.Player.Index);
                                    Server.TransferToAsync(Player, config.SEconomyReward, config.AnnounceOnReceive ? BankAccountTransferOptions.AnnounceToReceiver : BankAccountTransferOptions.SuppressDefaultAnnounceMessages, "voting on terraria-servers.com", "Voted on terraria-servers.com");

                                    for (int i = 0; i < config.Commands.Length; i++)
                                        Commands.HandleCommand(TSPlayer.Server, config.Commands[i].Replace("%playername%", args.Player.Name));
                                }
                            }
                            return;
                    }
                });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

        }

        public Response CheckVote(string Username)
        {
            try
            {
                string Res = wc.DownloadString(string.Format("http://terraria-servers.com/api/?object=votes&element=claim&key={0}&username={1}", config.ServerKey, Username));
                if (Res.Contains("incorrect server key"))
                    return Response.InvalidServerKey;
                else
                    return (Response)int.Parse(Res);
            }
            catch { return Response.Error; }
        }

        public bool SetAsClaimed(string Username)
        {
            return wc.DownloadString(string.Format("http://terraria-servers.com/api/?action=post&object=votes&element=claim&key={0}&username={1}", config.ServerKey, Username)) == "1";
        }

        public enum Response
        {
            NotFound = 0,
            VotedNotClaimed = 1,
            VotedAndClaimed = 2,
            InvalidServerKey = 3,
            Error = 4
        }

        public class Clr
        {
            public int R;
            public int G;
            public int B;
            public Clr(int r, int g, int b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        public class Message
        {
            public Clr Color;
            public string[] Text;
            public Message(string[] text)
            {
                Text = text;
                Color = new Clr(40, 160, 240);
            }
            public Color GetColor()
            {
                return new Color(Color.R, Color.G, Color.B);
            }
        }

        public class Config
        {
            public string ServerKey = "ServerKeyGoesHere";
            public int SEconomyReward = 1000;
            public bool AnnounceOnReceive = true;
            public string[] Commands = new string[]{ 
                "/heal %playername%", 
                "/firework %playername%"};
            public Message VoteNotFoundMessage = new Message(new string[]{
                "Vote not found!",
                "If you haven't voted yet, please go to terraria-servers.com",
                "and vote for the server to receive ingame rewards!"
            });
            public Message OnRewardClaimMessage = new Message(new string[] {
                "Thank you for voting on terraria-servers.com",
                "We really appreciate it!"          
            });
            public bool ShowIntervalMessage = true;
            public int IntervalInSeconds = 300;
            public Message IntervalMessage = new Message(new string[]{
                "Vote on terraria-servers.com and receive our rewards",
                "After voting you can use the command /reward!"
            });
        }

        private static void CreateConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "BRReward.json");
            try
            {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var sr = new StreamWriter(stream))
                    {
                        config = new Config();
                        var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
                        sr.Write(configString);
                    }
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.ToString());
                config = new Config();
            }
        }

        private static bool ReadConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "BRReward.json");
            try
            {
                if (File.Exists(filepath))
                {
                    using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var configString = sr.ReadToEnd();
                            config = JsonConvert.DeserializeObject<Config>(configString);
                        }
                        stream.Close();
                    }
                    return true;
                }
                else
                {
                    TShock.Log.ConsoleError("BRReward config not found. Creating new one...");
                    CreateConfig();
                    return false;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
            return false;
        }

        private void Reload_Config(CommandArgs args)
        {
            if (ReadConfig())
            {
                Timer.Interval = 1000 * config.IntervalInSeconds;
                Timer.Enabled = config.ShowIntervalMessage;
                args.Player.SendMessage("BRReward config reloaded sucessfully.", Color.Green);
            }
        }
    }
}
