using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SteamKit2;
using SteamKit2.Internal;

using SteamKit2.GC;
using SteamKit2.GC.Internal;
using SteamKit2.GC.CSGO.Internal;

namespace GameSenseOverwatchUtility
{

    struct AccountInfo
    {
        public String user;
        public String pass;
    }

    class OverwatchUility
    {
        public static System.IO.StreamReader file;
        public static List<AccountInfo> Accounts;
        static SteamClient client;
        static SteamUser user;
        static SteamFriends friends;
        static SteamGameCoordinator gc;
        static SteamUser.LogOnDetails details;
        static CallbackManager manager;
        static SteamID targetID;
        static bool accountConnected = false;

        static bool IsValidSteamID(String SteamID64)
        {
            return !((SteamID64.ToString() == "") || !(SteamID64.ToString().IndexOf("765") > -1) || (SteamID64.Length < 17));
        }

        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if(callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect to Steam!");
                accountConnected = false;
            }
            Console.WriteLine("Connected to steam!");

            user.LogOn(details);
        }

        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Disconnected from steam");
            accountConnected = false;
        }
        static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if(callback.Result != EResult.OK)
            {
                if(callback.Result == EResult.AccountLogonDenied)
                {
                    Console.WriteLine("Failed to log in due to steamguard, remove steamguard or use a different account");
                    Console.WriteLine("Account user in question is: {0}", details.Username);
                    accountConnected = false;
                    return;
                }
                Console.WriteLine("Failed to login to steam: {0} / EXTENDED: {1}", callback.Result, callback.ExtendedResult);

                accountConnected = false;
                return;

            }

            Console.WriteLine("Successfully logged into account");
            var GameMessage = new SteamKit2.ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            GameMessage.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = 730
            });
            client.Send(GameMessage);

            System.Threading.Thread.Sleep(3000);

            var ClientHelloMessage = new ClientGCMsgProtobuf<CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
            gc.Send(ClientHelloMessage, 730);

        }
        static void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            friends.SetPersonaState(EPersonaState.Online);

        }
        static void OnClientWelcome(IPacketGCMsg packetMsg)
        {
            var msg = new ClientGCMsgProtobuf<CMsgClientWelcome>(packetMsg);
            Console.WriteLine("Client Hello Received!");

            var account_id = targetID.AccountID;
            var ReportMessage = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_ClientReportPlayer>((uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_ClientReportPlayer);
            ReportMessage.Body.account_id = account_id;
            ReportMessage.Body.match_id = 8;
            ReportMessage.Body.rpt_aimbot = 2;
            ReportMessage.Body.rpt_wallhack = 3;
            ReportMessage.Body.rpt_speedhack = 4;
            ReportMessage.Body.rpt_teamharm = 5;
            ReportMessage.Body.rpt_textabuse = 6;
            ReportMessage.Body.rpt_voiceabuse = 7;

            Console.WriteLine("Attempting to report");
            gc.Send(ReportMessage, 730);

        }
        static void OnMatchMakingClientHello(IPacketGCMsg packetMsg)
        {
            var msg = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_MatchmakingClient2GCHello>(packetMsg);
            Console.WriteLine("Matchmaking Client Hello Sent!");
        }
        static void OnClientReportResponse(IPacketGCMsg packetMsg)
        {
            var msg = new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_ClientReportResponse>(packetMsg);
            Console.WriteLine("Report with confirmation ID: " + msg.Body.confirmation_id.ToString());

            accountConnected = false;
        }
        static void OnGameCoordinatorMessage(SteamGameCoordinator.MessageCallback callback)
        {
            var map = new Dictionary<uint, Action<IPacketGCMsg>>
            {
                {(uint)EGCBaseClientMsg.k_EMsgGCClientWelcome, OnClientWelcome},
                {(uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_MatchmakingGC2ClientHello, OnMatchMakingClientHello },
                {(uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_ClientReportResponse, OnClientReportResponse },
            };

            Action<IPacketGCMsg> function;
            if(!map.TryGetValue(callback.EMsg, out function)) //Unhandled messages
            {
                return; 
            }

            function(callback.Message); //Execute our callback from the dictionary
        }

        static void ReportFagFromAccount(AccountInfo accountInfo)
        {
            details = new SteamUser.LogOnDetails { Username = accountInfo.user, Password = accountInfo.pass };
            client = new SteamClient();
            user = client.GetHandler<SteamUser>();
            friends = client.GetHandler<SteamFriends>();
            gc = client.GetHandler<SteamGameCoordinator>();
            manager = new CallbackManager(client);

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGameCoordinatorMessage);

            Console.WriteLine("Connecting to Steam...");
            SteamDirectory.Initialize().Wait();
            client.Connect();
            accountConnected = true;

            while(accountConnected)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            client.Disconnect();
        }

        static void Main(string[] args)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            Accounts = new List<AccountInfo>();
            file = new System.IO.StreamReader("accounts.txt");
            int counter = 0;
            String line;
            while((line = file.ReadLine()) != null)
            {
                char first = line[0];
                if (first.ToString() == "*")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Line commented, ignoring...");
                    Console.ForegroundColor = defaultColor;
                }
                else
                {
                    String[] accountInfo = line.Trim().Split(':');
                    AccountInfo temp = new AccountInfo { user = accountInfo[0], pass = accountInfo[1] };
                    Accounts.Add(temp);
                    counter++;
                }
            }
            Console.WriteLine("Number of accounts loaded: {0}", counter);
            if (counter < 3)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] ACCOUNT COUNT IS CRITICALLY LOW");
                Console.ForegroundColor = defaultColor;

            }
            else if(counter < 9)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] ACCOUNT COUNT IS LOW");
                Console.ForegroundColor = defaultColor;
            }
            Console.Write("Please enter an account STEAMID64 to report: ");
            var SteamID64 = Console.ReadLine();
            Console.WriteLine(SteamID64);
            targetID = new SteamID();
            targetID.SetFromUInt64(UInt64.Parse(SteamID64));

            if(!IsValidSteamID(SteamID64))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[CRITICAL ERROR]: {0} is NOT a valid SteamID64", SteamID64);
                System.Environment.Exit(1);
            }
            Console.WriteLine("Target User is: {0}", targetID.AccountID);
            foreach(var accountInfo in Accounts)
            {
                ReportFagFromAccount(accountInfo);
            }
        }
    }
}
