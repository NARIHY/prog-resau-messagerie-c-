using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPServer
{
    class Server
    {
        private const int PORT = 8000;
        private static readonly ConcurrentDictionary<int, TcpClient> clients = new();
        private static int prochainId = 1;

        static async Task Main(string[] args)
        {
            TcpListener ecouteur = new(IPAddress.Any, PORT);
            ecouteur.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            ecouteur.Start();
            Console.WriteLine($"[Serveur] Démarré sur le port {PORT}...");

            while (true)
            {
                TcpClient nouveau = await ecouteur.AcceptTcpClientAsync();
                int id = prochainId++;
                clients[id] = nouveau;
                Console.WriteLine($"[Serveur] Client C{id} connecté.");
                _ = HandleClientAsync(nouveau, id);
            }
        }

        private static async Task HandleClientAsync(TcpClient client, int id)
        {
            try
            {
                using NetworkStream flux = client.GetStream();
                byte[] tampon = new byte[1024];

                while (true)
                {
                    int lu = await flux.ReadAsync(tampon, 0, tampon.Length);
                    if (lu == 0) break;

                    string message = Encoding.UTF8.GetString(tampon, 0, lu).Trim();
                    Console.WriteLine($"[C{id}] : {message}");

                    string diffusion = $"[C{id}] {message}{Environment.NewLine}";
                    byte[] donnees = Encoding.UTF8.GetBytes(diffusion);

                    foreach (var pair in clients)
                    {
                        if (pair.Key == id) continue;
                        try
                        {
                            await pair.Value.GetStream().WriteAsync(donnees, 0, donnees.Length);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Serveur] Erreur C{id} : {ex.Message}");
            }
            finally
            {
                clients.TryRemove(id, out _);
                client.Close();
                Console.WriteLine($"[Serveur] Client C{id} déconnecté.");
            }
        }
    }
}
