using PodmorniceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

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
                        noviIgrac.brojPromasaja = 0; //olaksava posao posle
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


            #region uspostavljanje TCP veze SA SELECT

            Socket serverTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverTCPPoint = new IPEndPoint(IPAddress.Parse(adresa), TCPPort);
            serverTCP.Bind(serverTCPPoint);
            serverTCP.Listen(brojIgraca);
            List<Socket> clientTCPs = new List<Socket>();

            List<Socket> socketsZaCitanje = new List<Socket>();
            socketsZaCitanje.Add(serverTCP);

            int prihvacenih = 0;
            bool sviKlijentiPovezani = false;

            Console.WriteLine("Cekam TCP konekcije od klijenata...");

            while (!sviKlijentiPovezani)
            {
                List<Socket> checkRead = new List<Socket>(socketsZaCitanje);
                Socket.Select(checkRead, null, null, -1);

                foreach (Socket socket in checkRead)
                {
                    if (socket == serverTCP)
                    {
                        Socket noviKlijent = serverTCP.Accept();
                        clientTCPs.Add(noviKlijent);
                        socketsZaCitanje.Add(noviKlijent);
                        prihvacenih++;

                        Console.WriteLine($"Novi klijent prihvacen: {prihvacenih}/{brojIgraca}");

                        if (prihvacenih >= brojIgraca)
                        {
                            sviKlijentiPovezani = true;
                            socketsZaCitanje.Remove(serverTCP);
                        }
                    }
                }
            }

            Console.WriteLine("Svi klijenti su povezani.");

            for (int i = 0; i < clientTCPs.Count; i++)
            {
                try
                {
                    int brojTrenutnogIgraca = i + 1;
                    string dodatakPoruci = $"Vi ste igrac {brojTrenutnogIgraca}";
                    clientTCPs[i].Send(Encoding.UTF8.GetBytes(porukaZaPocetak + dodatakPoruci));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greska pri slanju pocetne poruke klijentu {i + 1}: " + ex.Message);
                }
            }

            #endregion uspostavljanje TCP veze SA SELECT

            #region postavkeTabli SA SELECT

            bool postavljeno = false;
            byte[] buffer = new byte[1024];
            HashSet<Socket> klijentiKojiSuPoslali = new HashSet<Socket>();

            Console.WriteLine("Cekam podatke o podmornicama od svih klijenata...");

            while (!postavljeno)
            {
                List<Socket> checkRead = new List<Socket>(clientTCPs);
                Socket.Select(checkRead, null, null, 100000);

                foreach (Socket clientSocket in checkRead)
                {
                    try
                    {
                        int dostupnoBajtova = clientSocket.Available;

                        if (dostupnoBajtova > 0)
                        {
                            int byteNo = clientSocket.Receive(buffer);
                            string primljenaPoruka = Encoding.UTF8.GetString(buffer, 0, byteNo);
                            int[] podmornice = primljenaPoruka.Split(',').Select(int.Parse).ToArray();

                            int brojAktivnogIgraca = clientTCPs.IndexOf(clientSocket) + 1;
                            aktivniIgraci[brojAktivnogIgraca - 1].podmornice = podmornice;

                            Console.WriteLine($"\nPodaci od igraca {brojAktivnogIgraca}");
                            foreach (int podmornica in podmornice)
                            {
                                if (podmornica != -1)
                                {
                                    Console.WriteLine($"  - Podmornica na polju: {podmornica}");
                                }
                            }

                            int[][] praznaTabla = new int[dimX][];
                            for (int i = 0; i < dimX; i++)
                            {
                                praznaTabla[i] = new int[dimY]; //moram i kolone da inicijalizujem da ne bi bio null error opet
                                for (int j = 0; j < dimY; j++)
                                    praznaTabla[i][j] = Simboli.nijeGadjano;
                            }

                            Console.WriteLine($"Tabla igraca {brojAktivnogIgraca}:");
                            IspisiTablu(praznaTabla, dimX, dimY);

                            aktivniIgraci[brojAktivnogIgraca - 1].tabla = praznaTabla;
                            klijentiKojiSuPoslali.Add(clientSocket);

                            Console.WriteLine($"Primljeno {klijentiKojiSuPoslali.Count}/{brojIgraca} tabli.\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        int brojIgraca_err = clientTCPs.IndexOf(clientSocket) + 1;
                        Console.WriteLine($"Greska pri obradi podataka od igraca {brojIgraca_err}: " + ex.Message);
                    }
                }

                if (klijentiKojiSuPoslali.Count >= brojIgraca)
                {
                    postavljeno = true;
                    Console.WriteLine("\nSVI IGRACI SU POSTAVILI SVOJE PODMORNICE");
                }
            }

            #endregion postavkeTabli SA SELECT

            serverTCP.Close();
            Console.ReadKey();
        }

        static void IspisiTablu(int[][] tabla, int dimX, int dimY)
        {
            for (int i = 0; i < dimX; i++)
            {
                for (int j = 0; j < dimY; j++)
                {
                    Console.Write((char)tabla[i][j] + " ");
                }
                Console.WriteLine();
            }
        }
    }
}