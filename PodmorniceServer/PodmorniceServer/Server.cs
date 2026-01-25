using PodmorniceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace PodmorniceServer
{
    internal class Server
    {
        static void Main(string[] args)
        {
            bool krajIgre = false;
            int brojIgraca = -1;
            int brojAktivnihIgraca = 0;
            string adresa = IPAddress.Loopback.ToString();
            int TCPPort = 15006;
            int dimX, dimY;
            int dozvoljenoPromasaja;
            List<Igrac> aktivniIgraci = new List<Igrac>();
            List<EndPoint> tackeIgraca = new List<EndPoint>();


            Console.WriteLine($"==========SERVER JE POKRENUT NA ADRESI {adresa} ==========");
            #region unos podataka i prijave
            do
            {
                Console.WriteLine("Unesite broj igraca (minimum 2): ");
                brojIgraca = Int32.Parse(Console.ReadLine());
            } while (brojIgraca < 1); //todo na kraju ne zaboravi da proemnis ovo u <2

            Console.WriteLine("Unesite dimenzije table X i Y: ");
            Console.Write("X: ");
            dimX = Int32.Parse(Console.ReadLine());
            Console.Write("Y: ");
            dimY = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Unesite dozvoljeni broj promasaja: ");
            dozvoljenoPromasaja = Int32.Parse(Console.ReadLine());

            string porukaZaPocetak = $"Velicina table je {dimX} x {dimY}, posaljite brojevne vrednosti koje predstavljaju polja vasih podmornica (1 - {dimX * dimY}). Dozvoljen broj promasaja: {dozvoljenoPromasaja}.";

            Socket serverUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverUDPPoint = new IPEndPoint(IPAddress.Parse(adresa), 15005);  //15005 jer se lako pamti 
            serverUDP.Bind(serverUDPPoint);
            byte[] recievingBuffer = new byte[1024];
            EndPoint clientPoint = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("Prijave su otvorene. Cekam...");
            BinaryFormatter bf = new BinaryFormatter();
            while (brojAktivnihIgraca < brojIgraca)
            {
                try
                {
                    int brojBajta = serverUDP.ReceiveFrom(recievingBuffer, ref clientPoint);
                    if (brojBajta == 0) break;
                    string poruka = Encoding.UTF8.GetString(recievingBuffer, 0, brojBajta);

                    //ako je poruka veca od 0 dodajemo EndPoint u listu, mora ovako da ne bi pokazivalo sv na isti EndPoint svaki put
                    IPEndPoint clientEP = (IPEndPoint)clientPoint;
                    tackeIgraca.Add(new IPEndPoint(clientEP.Address, clientEP.Port));

                    using (MemoryStream ms = new MemoryStream(recievingBuffer, 0, brojBajta))
                    {
                        Igrac noviIgrac = (Igrac)bf.Deserialize(ms);
                        Console.WriteLine("Prijava uspesna!");
                        Console.WriteLine("Igrac broj: " + (brojAktivnihIgraca + 1));
                        noviIgrac.identifikacioniBroj = brojAktivnihIgraca + 1;  //svakom igracu ide redni broj ulaska na server, kao u minecraft mini igrama
                        aktivniIgraci.Add(noviIgrac);
                        brojAktivnihIgraca++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\nGreska pri prijavi igraca! " + ex.Message);
                }
            }

            string TCPinfo = $"{adresa}:{TCPPort}";
            byte[] bafer = Encoding.UTF8.GetBytes(TCPinfo);
            int s = bafer.Length;
            foreach (EndPoint e in tackeIgraca)
            {
                serverUDP.SendTo(bafer, 0, s, SocketFlags.None, e);
            }
            serverUDP.Close();
            Console.WriteLine("Prijave su zavrsene. Igra pocinje. Saljem podatke za TCP svim klijentima.");
            #endregion unos podataka i prijave


            #region uspostavljanje TCP veze

            Socket serverTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverTCPPoint = new IPEndPoint(IPAddress.Parse(adresa), TCPPort);
            serverTCP.Bind(serverTCPPoint);
            serverTCP.Listen(brojIgraca);
            List<Socket> clientTCPs = new List<Socket>();
            int prihvacenih = 0;

            while (prihvacenih < brojIgraca)
            {
                Socket clientSocket = serverTCP.Accept();
                clientTCPs.Add(clientSocket);
                prihvacenih++;
            }
            bool porukaPoslata = false;
            while (!porukaPoslata)
            {
                try
                {
                    foreach (Socket clientSocket in clientTCPs)
                    {
                        clientSocket.Send(Encoding.UTF8.GetBytes(porukaZaPocetak));
                    }
                    porukaPoslata = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Greska pri slanju pocetne poruke TCP klijentima: " + ex.Message);
                }
            }
            #endregion uspostavljanje TCP veze

            #region zapravoIgranje

            byte[] buffer = new byte[1024];
            while (!krajIgre)
            {
                foreach (Socket clientTCP in clientTCPs)  //testno primanje, nije gotovo
                {
                    int byteNo = clientTCP.Receive(buffer);
                    string primljenaPoruka = Encoding.UTF8.GetString(buffer, 0, byteNo);
                    int[] podmornice = primljenaPoruka.Split(',').Select(int.Parse).ToArray();
                    foreach (int podmornica in podmornice)
                    {
                        if (podmornica == -1)  //jer niz za nepopunjena polja ima -1
                            continue;
                        Console.WriteLine("Igrac je poslao podmornicu na polju: " + podmornica);
                    }
                }
            }

            #endregion zapravoIgranje

            serverTCP.Close();
            Console.ReadKey();
        }
    }
}
