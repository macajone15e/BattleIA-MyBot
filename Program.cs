namespace MyBot;
using BattleIA;

using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{


    // ****************************************************************************************************
    // Adresse du serveur, à changer si nécessaire
    private static string serverUrl = "ws://10.224.0.89:4226/bot";


    // ****************************************************************************************************
    // Identifiant du bot
    // https://www.guidgenerator.com/
    // il faut utiliser le même identifiant pour le cockpit !
    private static string id = "d537c8ee-7f21-4b10-961d-066ce747d03f";
    // Nom du bot
    private static string botName = "Macaj'Bot";
    // le code du bot
    private static MyIA ia = new MyIA();

    static void Main(string[] args)
    {
        //ia.DoTest();
        //return;
        DoWork().GetAwaiter().GetResult();
        //Console.WriteLine("Press [ENTER] to exit.");
        //Console.ReadLine();
    }


    private static void DebugWriteArray(byte[] data, int length)
    {
        if (length == 0) return;
        Console.Write($"[{data[0]}");
        for (int i = 1; i < length; i++)
        {
            Console.Write($", {data[i]}");
        }
        Console.Write("] ");
    }

    private static Bot bot = new Bot();
    private static UInt16 turn = 0;

    static async Task DoWork()
    {

        // 1 - connect to server

        var client = new ClientWebSocket();
        Console.WriteLine($"Connecting to {serverUrl}");
        try
        {
            await client.ConnectAsync(new Uri(serverUrl), CancellationToken.None);
        }
        catch (Exception err)
        {
            Console.WriteLine($"[ERROR] {err.Message}");
            return;
        }

        ia.DoInit();

        // 2 - Hello message with our GUID

        Guid guid = Guid.Parse(id);
        var bytes = Encoding.UTF8.GetBytes(guid.ToString());
        Console.WriteLine($"Sending our GUID: {guid}");
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        // 3 - wait data from server

        bool nameIsSent = false;
        bool isDead = false;

        var buffer = new byte[1024 * 4];
        while (!isDead)
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            // Console.WriteLine(result.MessageType);
            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
            {
                if (result.Count > 0)
                {
                    string command = System.Text.Encoding.UTF8.GetString(buffer, 0, 1);
                    switch (command)
                    {
                        case "O": // OK, rien à faire
                            if (result.Count != (int)MessageSize.OK) { Console.WriteLine($"[ERROR] wrong size for 'OK': {result.Count}"); break; }
                            if (!nameIsSent)
                            {
                                nameIsSent = true;
                                // sending our name
                                var bName = Encoding.UTF8.GetBytes("N" + botName);
                                Console.WriteLine($"Sending our name: {botName}");
                                await client.SendAsync(new ArraySegment<byte>(bName), WebSocketMessageType.Text, true, CancellationToken.None);
                                break;
                            }
                            Console.WriteLine("OK, waiting our turn...");
                            break;
                        case "T": // nouveau tour, attend le niveau de détection désiré
                            if (result.Count != (int)MessageSize.Turn) { Console.WriteLine($"[ERROR] wrong size for 'T': {result.Count}"); DebugWriteArray(buffer, result.Count); break; }
                            turn = (UInt16)(buffer[1] + (buffer[2] << 8));
                            bot.Energy = (UInt16)(buffer[3] + (buffer[4] << 8));
                            bot.ShieldLevel = (UInt16)(buffer[5] + (buffer[6] << 8));
                            bot.CloakLevel = (UInt16)(buffer[7] + (buffer[8] << 8));
                            Console.WriteLine($"Turn #{turn} - Energy: {bot.Energy}, Shield: {bot.ShieldLevel}, Cloak: {bot.CloakLevel} (Score: {bot.Score})");
                            ia.StatusReport(turn, bot.Energy, bot.ShieldLevel, bot.CloakLevel);
                            if (bot.Energy == 0) break;
                            // must answer with D#
                            var answerD = new byte[2];
                            answerD[0] = System.Text.Encoding.ASCII.GetBytes("D")[0];
                            answerD[1] = ia.GetScanSurface();
                            Console.WriteLine($"Sending Scan: {answerD[1]}");
                            await client.SendAsync(new ArraySegment<byte>(answerD), WebSocketMessageType.Text, true, CancellationToken.None);
                            break;
                        case "C": // nos infos ont changées
                            if (result.Count != (int)MessageSize.Change + 2) { Console.WriteLine($"[ERROR] wrong size for 'C': {result.Count}"); DebugWriteArray(buffer, result.Count); break; }
                            bot.Energy = (UInt16)(buffer[1] + (buffer[2] << 8));
                            bot.ShieldLevel = (UInt16)(buffer[3] + (buffer[4] << 8));
                            bot.CloakLevel = (UInt16)(buffer[5] + (buffer[6] << 8));
                            bot.Score = (UInt16)(buffer[7] + (buffer[8] << 8));
                            Console.WriteLine($"Change - Energy: {bot.Energy}, Shield: {bot.ShieldLevel}, Cloak: {bot.CloakLevel}, Score: {bot.Score}");
                            ia.StatusReport(turn, bot.Energy, bot.ShieldLevel, bot.CloakLevel);
                            // nothing to reply
                            if (bot.Energy == 0) break;
                            break;
                        case "I": // info sur détection, attend l'action à effectuer
                            byte surface = buffer[1];
                            int all = surface * surface;
                            if (result.Count != (2 + all)) { Console.WriteLine($"[ERROR] wrong size for 'I': {result.Count}"); break; } // I#+data donc 2 + surface :)
                            if (surface > 0)
                            {
                                var x = new byte[all];
                                Array.Copy(buffer, 2, x, 0, all);
                                ia.AreaInformation(surface, x);
                            }
                            // Doit répondre avec une action Move / Shield / Cloak / Shoot / None ...
                            var answerA = ia.GetAction();
                            Console.WriteLine($"Sending Action: {(BotAction)answerA[0]}");
                            //await client.SendAsync(new ArraySegment<byte>(answerA), WebSocketMessageType.Text, true, CancellationToken.None);
                            await client.SendAsync(new ArraySegment<byte>(answerA), WebSocketMessageType.Binary, true, CancellationToken.None);
                            break;
                        case "D":
                            isDead = true;
                            Console.WriteLine($"We are dead!");
                            await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            break;
                    }
                } // if count > 1
                else
                {
                    Console.WriteLine("[ERROR] " + Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
            } // if text
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine($"End with code {result.CloseStatus}: {result.CloseStatusDescription}");
                await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                break;
            }
            else
            {
                DebugWriteArray(buffer, result.Count);
                string command = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine(command);
            }
        } // while
        Console.WriteLine("Just dead!");
    } // DoWork
}
