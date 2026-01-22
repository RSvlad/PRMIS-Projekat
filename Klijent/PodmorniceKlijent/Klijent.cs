using PodmorniceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PodmorniceKlijent
{
    internal class Klijent
    {
        static void Main(string[] args)
        {
            bool prijavljen = false;
            Igrac ja = new Igrac();
            #region prijava
            Console.WriteLine("==========KLIJENT JE POKRENUT. UNESI ADRESU SERVERA: ==========");
            string ipServera = Console.ReadLine();
            Console.WriteLine("Pokrenuti prijavu na server? (prijava/ne): ");
            string ans = Console.ReadLine();
            int TCPPortServera;
            string adresaServeraZaTCP;
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
                clientUDP.Close();
                Console.ReadKey();
            }
        }
    }
}
