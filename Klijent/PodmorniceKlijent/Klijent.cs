using PodmorniceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PodmorniceKlijent
{
    internal class Klijent
    {
        static void Main(string[] args)
        {
            bool krajIgre = false;
            bool prijavljen = false;
            Igrac ja = new Igrac();
            #region prijava
            Console.WriteLine("==========KLIJENT JE POKRENUT. UNESI ADRESU SERVERA: ==========");
            string ipServera = Console.ReadLine();
            Console.WriteLine("Pokrenuti prijavu na server? (prijava/ne): ");
            string ans = Console.ReadLine();
            int TCPPortServera = 0;
            string adresaServeraZaTCP = "N/A";
            if (ans.ToLower().Equals("prijava"))
            {
                Socket clientUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint destinationPoint = new IPEndPoint(IPAddress.Parse(ipServera), 15005);  //TODO nekako nabavi adresu servera za UDP dinamicki
                EndPoint clientPoint = new IPEndPoint(IPAddress.Any, 0);

                BinaryFormatter bf = new BinaryFormatter();

                while (!prijavljen)
                {
                    try
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bf.Serialize(ms, ja);
                            byte[] podaci = ms.ToArray();

                            int brBajta = clientUDP.SendTo(podaci, 0, podaci.Length, SocketFlags.None, destinationPoint);

                        }
                        //uzimam podatke za TCP konekciju
                        byte[] recievingBuffer = new byte[1024];
                        int bytesRecieved = clientUDP.ReceiveFrom(recievingBuffer, ref clientPoint);
                        string[] data = Encoding.ASCII.GetString(recievingBuffer, 0, bytesRecieved).Split(':');
                        adresaServeraZaTCP = data[0];
                        TCPPortServera = int.Parse(data[1]);
                        Console.WriteLine($"Prijava uspesna! Podaci za TCP konekciju - Adresa: {adresaServeraZaTCP}, Port: {TCPPortServera}");
                        prijavljen = true;
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Doslo je do greske tokom slanja poruke: \n{ex}");
                    }
                }
                #endregion prijava

                #region uspostavljanje TCP veze i primanje podataka za pocetak igre

                int dimX = -1, dimY = -1, dozvoljenoPromasaja = -1;
                bool tcpUspeh = false;
                Socket clientTCP = null;
                while (!tcpUspeh)
                {
                    try
                    {
                        clientTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        if (TCPPortServera == 0)
                        {
                            Console.WriteLine("\nGreska pri preuzimanju podataka za TCP!");
                            return;
                        }
                        IPEndPoint serverTCPPoint = new IPEndPoint(IPAddress.Parse(adresaServeraZaTCP), TCPPortServera);
                        clientTCP.Connect(serverTCPPoint);
                        Console.WriteLine("\nUspesno uspostavljena TCP konekcija sa serverom.");

                        byte[] buffer = new byte[1024];
                        string info;
                        // Prijem početne poruke
                        int byteCount = clientTCP.Receive(buffer);
                        string serverMessage = Encoding.UTF8.GetString(buffer, 0, byteCount);
                        Console.WriteLine(serverMessage);

                        (dimX, dimY, dozvoljenoPromasaja) = ParsirajPoruku(serverMessage);

                        tcpUspeh = true;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode != SocketError.ConnectionRefused)  //mnogo sam ponosan na ovaj deo. U sustini, ako je ovaj exception samo znaci da se ceka i dalje server jer admin unosi podatke,
                    {                                                                                     // pa ga samo zanemarujem da mi ne bi na svakih sekund dok podaci ne stignu iskakao na ekranu 
                        Console.WriteLine($"Doslo je do greske tokom uspostavljanja TCP konekcije: \n{ex}");
                    }
                }

                #endregion uspostavljanje TCP veze i primanje podataka za pocetak igre
                clientUDP.Close();


                #region zapravoIgranje

                if(dimX == -1 || dimY == -1 || dozvoljenoPromasaja == -1)
                {
                    Console.WriteLine("Greska pri parsiranju dimenzija table ili dozvoljenog broja promasaja.");
                    return;
                }

                while (!krajIgre)
                {
                    int[] uneseneVrednosti = unesiPodmornice(dimX, dimY);
                    clientTCP.Send(Encoding.UTF8.GetBytes(string.Join(",", uneseneVrednosti)));
                }

                #endregion zapravoIgranje

                clientTCP.Close();
            }
        }

        static (int, int, int) ParsirajPoruku(string serverMessage)
        {
            Regex regex = new Regex(@"Velicina table je (\d+)\s*x\s*(\d+).*Dozvoljen broj promasaja:\s*(\d+)", RegexOptions.Singleline);

            Match match = regex.Match(serverMessage);

            if (!match.Success)
            {
                throw new Exception("Neispravan format poruke sa servera.");
            }

            int dimX = int.Parse(match.Groups[1].Value);
            int dimY = int.Parse(match.Groups[2].Value);
            int dozvoljenoPromasaja = int.Parse(match.Groups[3].Value);

            return (dimX, dimY, dozvoljenoPromasaja);
        }
        static int[] unesiPodmornice(int dimX, int dimY) 
        {
            int[] brojevi = new int[dimX*dimY];
            int broj;
            int i = 0;
            int dodato = 0;
            if (dimX > dimY)
            {
                Console.WriteLine($"Unesite {dimX} podmornica. (Tabla je {dimX} * {dimY}): ");
                for (int j = 0; j < brojevi.Length; j++)  //da se broj%dimY ne bi desio na unosenju jedinice
                {
                    brojevi[j] = -1;
                }
                while (i < dimX)
                {
                    Console.WriteLine($"Unesite podmornicu broj {i + 1}: ");
                    broj = Int32.Parse(Console.ReadLine());
                    if (broj % dimY == 0)  //JAAAAAAAAKO sam ponosan na ovo. Morao sam u flow state da udjem da bih uspeo
                    {
                        Console.WriteLine("NE TAKO! Ovo bi probilo tablu! Probaj ponovo");
                        continue;
                    }
                    if(brojevi.Contains(broj) || brojevi.Contains(broj + 1))
                    {
                        Console.WriteLine("Ova podmornica bi dodirivala jedan kraj neke od postojecih! Probajte ponovo.");
                        continue;
                    }
                    brojevi[dodato] = broj;
                    brojevi[dodato + 1] = broj + 1;   //posto su podmornice duzine 2
                    dodato += 2;
                    i++;
                }
            }
            else
            {
                Console.WriteLine($"Unesite {dimY} podmornica. (Tabla je {dimX} * {dimY}): ");
                for (int j = 0; j < brojevi.Length; j++)  //da se broj%dimY ne bi desio na unosenju jedinice
                {
                    brojevi[j] = -1;
                }
                while (i < dimY)
                {
                    Console.WriteLine($"Unesite podmornicu broj {i + 1}: ");
                    broj = Int32.Parse(Console.ReadLine());
                    if (broj % dimY == 0)  //JAAAAAAAAKO sam ponosan na ovo. Morao sam u flow state da udjem da bih uspeo
                    {
                        Console.WriteLine("NE TAKO! Ovo bi probilo tablu! Probaj ponovo");
                        continue;
                    }
                    if (brojevi.Contains(broj) || brojevi.Contains(broj + 1))
                    {
                        Console.WriteLine("Ova podmornica bi dodirivala jedan kraj neke od postojecih! Probajte ponovo.");
                        continue;
                    }
                    brojevi[dodato] = broj;
                    brojevi[dodato + 1] = broj + 1;  //posto su podmornice duzine 2
                    dodato += 2;
                    i++;
                }
            }
            return brojevi;
        }
    }

}
