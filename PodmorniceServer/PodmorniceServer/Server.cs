using PodmorniceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace PodmorniceServer
{
    internal class Server
    {
        static void Main(string[] args)
        {
            int brojIgraca = -1;
            int brojAktivnihIgraca = 0;
            List<Igrac> aktivniIgraci = new List<Igrac>();


            Console.WriteLine("==========SERVER JE POKRENUT==========");
            #region prijave
            do
            {
                Console.WriteLine("Unesite broj igraca (minimum 2): ");
                brojIgraca = Int32.Parse(Console.ReadLine());
            } while (brojIgraca < 2);

            Socket serverUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverPoint = new IPEndPoint(IPAddress.Parse("192.168.2.108"), 15005);  //15005 jer se lako pamti 
            serverUDP.Bind(serverPoint);
            byte[] recievingBuffer = new byte[1024];
            EndPoint clientPoint = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("Prijave su otvorene. Cekam...");
            BinaryFormatter bf = new BinaryFormatter();
            while(brojAktivnihIgraca < brojIgraca)
            {
                try
                {
                    int brojBajta = serverUDP.ReceiveFrom(recievingBuffer, ref clientPoint);
                    if (brojBajta == 0) break;
                    string poruka = Encoding.UTF8.GetString(recievingBuffer, 0, brojBajta);

                    using (MemoryStream ms = new MemoryStream(recievingBuffer, 0, brojBajta))
                    {
                        Igrac noviIgrac = (Igrac)bf.Deserialize(ms);
                        Console.WriteLine("Prijava uspesna!");
                        Console.WriteLine("Igrac broj: " + (brojAktivnihIgraca+1));
                        noviIgrac.identifikacioniBroj = brojAktivnihIgraca + 1;  //svakom igracu ide redni broj ulaska na server, kao u minecraft mini igrama
                        aktivniIgraci.Add(noviIgrac);
                        brojAktivnihIgraca++;
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("\nGreska pri prijavi igraca! " + ex.Message);
                }
            }
            Console.WriteLine("Prijave su zavrsene. Igra pocinje.");
            #endregion prijave
            //to do sve ostalo

            serverUDP.Close();
            Console.ReadKey();
        }
    }
}
