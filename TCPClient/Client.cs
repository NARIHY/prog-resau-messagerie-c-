namespace TCPClient
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    class Client
    {
        private const string SERVEUR_Ip = "127.0.0.1";
        private const int SERVEUR_Port = 8000;

        static async Task Main(string[] args)
        {
            using TcpClient client = new();
            Console.WriteLine($"[Client] Connexion à {SERVEUR_Ip}:{SERVEUR_Port}...");
            await client.ConnectAsync(SERVEUR_Ip, SERVEUR_Port);
            Console.WriteLine("[Client] Connecté !");

            using NetworkStream flux = client.GetStream();

            var canaux = new Dictionary<string, StringBuilder>();
            string canalActuel = "global";
            if (!canaux.ContainsKey(canalActuel))
                canaux[canalActuel] = new StringBuilder();

            _ = Task.Run(async () =>
            {
                byte[] tampon = new byte[1024];
                while (true)
                {
                    int lu = await flux.ReadAsync(tampon, 0, tampon.Length);
                    if (lu == 0) break;
                    string reponse = Encoding.UTF8.GetString(tampon, 0, lu).Trim();
                    Console.WriteLine($"[Canal {canalActuel}] {reponse}");
                }
            });

            Console.WriteLine("Entrez vos messages ('/exit' pour quitter, '/canal [nom]' pour changer de canal) :");
            while (true)
            {
                string ligne = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(ligne)) continue;
                if (ligne.Equals("/exit", StringComparison.OrdinalIgnoreCase)) break;
                if (ligne.StartsWith("/canal", StringComparison.OrdinalIgnoreCase))
                {
                    string nouveauCanal = ligne.Split(' ')[1];
                    if (!canaux.ContainsKey(nouveauCanal))
                        canaux[nouveauCanal] = new StringBuilder();
                    canalActuel = nouveauCanal;
                    Console.WriteLine($"[Client] Vous êtes maintenant dans le canal {canalActuel}.");
                    continue;
                }

                byte[] envoi = Encoding.UTF8.GetBytes($"[{canalActuel}] {ligne}{Environment.NewLine}");
                await flux.WriteAsync(envoi, 0, envoi.Length);
                canaux[canalActuel].AppendLine(ligne);
            }

            Console.WriteLine("[Client] Déconnexion");
        }
    }
}
