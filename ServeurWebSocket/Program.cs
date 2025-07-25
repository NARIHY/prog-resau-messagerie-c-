// Program.cs
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class ClientInfo
{
    public WebSocket? Ws { get; init; }
    public TcpClient? Tcp { get; init; }
    public string Name { get; set; } = "";
    public string Channel { get; set; } = "global";
}

class Program
{
    static ConcurrentDictionary<int, ClientInfo> clients = new();
    static int nextId = 1;

    static async Task Main()
    {
        _ = RunTcpServer(8000);
        _ = RunWebSocketServer(8080);

        Console.WriteLine("App en écoute sur TCP:8000 et WS:8080. CTRL+C pour arrêter.");
        await Task.Delay(Timeout.Infinite);
    }

   // Serveur TCP
    static async Task RunTcpServer(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Start();
        Console.WriteLine($"[TCP] Démarré sur le port {port}");

        while (true)
        {
            var tcp = await listener.AcceptTcpClientAsync();
            int id = nextId++;
            var info = new ClientInfo { Tcp = tcp };
            clients[id] = info;
            Console.WriteLine($"[TCP] Client #{id} connecté");
            _ = HandleTcpClient(id, info);
        }
    }

    static async Task HandleTcpClient(int id, ClientInfo info)
    {
        using var stream = info.Tcp!.GetStream();
        var buf = new byte[1024];

        // 1er message = pseudo
        int read = await stream.ReadAsync(buf, 0, buf.Length);
        info.Name = Encoding.UTF8.GetString(buf, 0, read).Trim();
        Console.WriteLine($"[TCP#{id}] s’appelle {info.Name}");
        await SendToClient(info, $"[Serveur] Bienvenue {info.Name} sur TCP");

        // boucle de réception
        while (true)
        {
            try
            {
                read = await stream.ReadAsync(buf, 0, buf.Length);
                if (read == 0) break;
                var txt = Encoding.UTF8.GetString(buf, 0, read).Trim();
                await Broadcast(id, txt, isTcp: true);
            }
            catch { break; }
        }

        // cleanup
        clients.TryRemove(id, out _);
        info.Tcp!.Close();
        Console.WriteLine($"[TCP] Client #{id} déconnecté");
    }

   //Serveur WebSocket
    static async Task RunWebSocketServer(int port)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        Console.WriteLine($"[WS] Démarré sur ws://localhost:{port}/");

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            var wsCtx = await ctx.AcceptWebSocketAsync(null);
            int id = nextId++;
            var info = new ClientInfo { Ws = wsCtx.WebSocket };
            clients[id] = info;
            Console.WriteLine($"[WS] Client #{id} connecté");
            _ = HandleWsClient(id, info);
        }
    }

    static async Task HandleWsClient(int id, ClientInfo info)
    {
        var ws = info.Ws!;
        var buf = new byte[1024];

        // 1er message = pseudo
        var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
        info.Name = Encoding.UTF8.GetString(buf, 0, res.Count).Trim();
        Console.WriteLine($"[WS#{id}] s’appelle {info.Name}");
        await SendToClient(info, $"[Serveur] Bienvenue {info.Name} sur WS");

        // boucle de réception
        while (ws.State == WebSocketState.Open)
        {
            try
            {
                res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                if (res.MessageType == WebSocketMessageType.Close) break;
                var txt = Encoding.UTF8.GetString(buf, 0, res.Count).Trim();
                await Broadcast(id, txt, isTcp: false);
            }
            catch { break; }
        }

        // cleanup
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        clients.TryRemove(id, out _);
        Console.WriteLine($"[WS] Client #{id} déconnecté");
    }

    //Diffusion / Envoi

    // Diffuse un message à tous les clients (TCP et WS), incluant ou excluant l’émetteur
    static Task Broadcast(int senderId, string text, bool isTcp)
    {
        var sender = clients[senderId];
        string tag = isTcp ? "TCP" : "WS";
        string full = $"[{tag}#{senderId} {sender.Name}]: {text}";
        Console.WriteLine($"[Broadcast] {full}");

        var tasks = new List<Task>();
        foreach (var kv in clients)
        {
            var info = kv.Value;
            tasks.Add(SendToClient(info, full));
        }
        return Task.WhenAll(tasks);
    }

    // Envoie un texte à un client (cherche s’il a Ws ou Tcp)
    static Task SendToClient(ClientInfo info, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");

        if (info.Tcp is not null && info.Tcp.Connected)
        {
            var stream = info.Tcp.GetStream();
            return stream.WriteAsync(data, 0, data.Length);
        }
        else if (info.Ws is not null && info.Ws.State == WebSocketState.Open)
        {
            return info.Ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        return Task.CompletedTask;
    }
}
