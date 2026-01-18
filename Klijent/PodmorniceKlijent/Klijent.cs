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
            Console.WriteLine("==========KLIJENT JE POKRENUT==========");
            Console.WriteLine("Pokrenuti prijavu na server? (prijava/ne): ");
            string ans = Console.ReadLine();
            if (ans.ToLower().Equals("prijava"))
            {
                Socket clientUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint destinationPoint = new IPEndPoint(IPAddress.Parse("192.168.2.108"), 15005); 
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
                            prijavljen = true;
                        }

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
