using PodmorniceLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
            string adresa = PronadjiIPAdresu().ToString();
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

            #region gejmplej
            Console.WriteLine("\n========== IGRA POCINJE! ==========\n");

            int trenutniIgrac = 0; // Index igraca na potezu (0-based)
            bool[] igraciAktivni = new bool[brojIgraca];
            for (int i = 0; i < brojIgraca; i++)
                igraciAktivni[i] = true;


            string porukaPocetak = "start";
            foreach (Socket client in clientTCPs)
            {
                client.Send(Encoding.UTF8.GetBytes(porukaPocetak));
            }

            while (!krajIgre)
            {
                // ako igrac nije aktivan, preskacem sta ce mi
                if (!igraciAktivni[trenutniIgrac])
                {
                    trenutniIgrac = (trenutniIgrac + 1) % brojIgraca;

                    // ako je to poslednji aktivan igra je gotova
                    int brojAktivnih = igraciAktivni.Count(x => x);
                    if (brojAktivnih <= 1)
                    {
                        krajIgre = true;
                        break;
                    }
                    continue;
                }

                Socket trenutniSocket = clientTCPs[trenutniIgrac];
                int idTrenutnogIgraca = trenutniIgrac + 1;


                string porukaPotez = $"Potez igraca:{idTrenutnogIgraca}";
                trenutniSocket.Send(Encoding.UTF8.GetBytes(porukaPotez));

                bool ponovniPokusaj = true;

                while (ponovniPokusaj && !krajIgre)
                {
                    try
                    {
                        // Cekam da klijent zatrazi tablu neciju
                        byte[] bufferReq = new byte[1024];
                        int bytesReq = trenutniSocket.Receive(bufferReq);
                        string zahtev = Encoding.UTF8.GetString(bufferReq, 0, bytesReq);

                        if (zahtev.StartsWith("pregledTable:"))
                        {
                            int idCiljnogIgraca = int.Parse(zahtev.Split(':')[1]);

                            if (idCiljnogIgraca < 1 || idCiljnogIgraca > brojIgraca)
                            {
                                trenutniSocket.Send(Encoding.UTF8.GetBytes("Greska:Nevazeci ID igraca"));
                                continue;
                            }

                            // Saljem trenutno stanje table tog igraca
                            Igrac ciljniIgrac = aktivniIgraci[idCiljnogIgraca - 1];
                            string tablaPoruka = SerijalizujTablu(ciljniIgrac.tabla, dimX, dimY);
                            trenutniSocket.Send(Encoding.UTF8.GetBytes($"Trazena tabla:{tablaPoruka}"));

                            Console.WriteLine($"Igrac {idTrenutnogIgraca} gleda tablu igraca broj {idCiljnogIgraca}");
                        }
                        else if (zahtev.StartsWith("gadjanje:"))
                        {
                            // Format: "gadjanje:idIgraca,polje"
                            string[] delovi = zahtev.Split(':')[1].Split(',');
                            int idMete = int.Parse(delovi[0]);
                            int polje = int.Parse(delovi[1]);

                            if (idMete < 1 || idMete > brojIgraca || idMete == idTrenutnogIgraca)
                            {
                                trenutniSocket.Send(Encoding.UTF8.GetBytes("Greska:Nevazeci cilj"));
                                continue;
                            }

                            Igrac meta = aktivniIgraci[idMete - 1];

                            int red = (polje - 1) / dimY;
                            int kolona = (polje - 1) % dimY;

                            if (red < 0 || red >= dimX || kolona < 0 || kolona >= dimY)
                            {
                                trenutniSocket.Send(Encoding.UTF8.GetBytes("Greska:Polje van granica!"));
                                continue;
                            }

                            // Da li je vec gadjano ?
                            if (meta.tabla[red][kolona] == Simboli.vecGadjano || meta.tabla[red][kolona] == Simboli.pogodjeno)
                            {
                                trenutniSocket.Send(Encoding.UTF8.GetBytes("Greska:Vec gadjano!"));
                                continue;
                            }

                            //  Pogodak bre?
                            bool jePogodak = meta.podmornice.Contains(polje);
                            string rezultat = "";

                            if (jePogodak)
                            {
                                meta.tabla[red][kolona] = Simboli.pogodjeno;

                                // Provera potopa
                                bool podmornicaPotopljena = ProveriPotopio(meta, polje, dimY);

                                if (podmornicaPotopljena)
                                {
                                    rezultat = "POTOPIO";
                                }
                                else
                                {
                                    rezultat = "POGODIO";
                                }

                                ponovniPokusaj = true;
                            }
                            else
                            {
                                meta.tabla[red][kolona] = Simboli.vecGadjano;
                                rezultat = "PROMASIO";
                                aktivniIgraci[trenutniIgrac].brojPromasaja++;

                                if (dozvoljenoPromasaja - aktivniIgraci[trenutniIgrac].brojPromasaja == 2)
                                {
                                    string upozorenje = "\n!!! UPOZORENJE !!! Imate jos samo 2 dozvoljenih promasaja pre eliminacije!";
                                    trenutniSocket.Send(Encoding.UTF8.GetBytes($"UPOZORENJE:{upozorenje}"));
                                    Console.WriteLine($"Igrac {idTrenutnogIgraca} je dobio upozorenje (preostalo promasaja: 2)");
                                }

                                // Sledeci igrac na potezu
                                ponovniPokusaj = false;
                            }

                            Console.WriteLine($"[Igrac {idTrenutnogIgraca}] -> [Igrac {idMete}]: {polje}, {rezultat}");

                            // Rezultat ide igracu koji je gadjao
                            trenutniSocket.Send(Encoding.UTF8.GetBytes($"REZULTAT:{rezultat}"));

                            // Je li nesrecnik eliminisan?
                            if (aktivniIgraci[trenutniIgrac].brojPromasaja >= dozvoljenoPromasaja)
                            {
                                igraciAktivni[trenutniIgrac] = false;
                                trenutniSocket.Send(Encoding.UTF8.GetBytes("Eliminisan:Dostignut limit promasaja"));
                                Console.WriteLine($"Igrac {idTrenutnogIgraca} je eliminisan (dostigao limit promasaja)");
                                ponovniPokusaj = false;
                            }

                            // Provera da li su sve podmornice mete unistene
                            if (SvePodmornicePotopljene(meta, dimX, dimY))
                            {
                                igraciAktivni[idMete - 1] = false;
                                Console.WriteLine($"Igrac {idMete} je eliminisan (sve podmornice potopljene)");
                            }

                            // Da li je ostao samo jedan igrac
                            int aktivnihIgraca = igraciAktivni.Count(x => x);
                            if (aktivnihIgraca <= 1)
                            {
                                krajIgre = true;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Greska tokom poteza igraca {idTrenutnogIgraca}: {ex.Message}");
                        ponovniPokusaj = false;
                    }
                }

                // Sledeci igrac na potezu
                trenutniIgrac = (trenutniIgrac + 1) % brojIgraca;
            }

            // Ko je pobedio ?
            for (int i = 0; i < brojIgraca; i++)
            {
                if (igraciAktivni[i])
                {
                    Console.WriteLine($"\n========== IGRAC {i + 1} JE POBEDNIK! ==========");
                    clientTCPs[i].Send(Encoding.UTF8.GetBytes("POBEDA:Cestitamo, pobedili ste!"));
                }
                else
                {
                    try
                    {
                        clientTCPs[i].Send(Encoding.UTF8.GetBytes("KRAJ:Izgubili ste"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Greska pri slanju poruke o kraju igre igracu {i + 1}: {e.Message}");
                    }
                }
            }
            #endregion gejmplej

            serverTCP.Close();
            Console.ReadKey();
        }
        static string SerijalizujTablu(int[][] tabla, int dimX, int dimY)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < dimX; i++)
            {
                for (int j = 0; j < dimY; j++)
                {
                    sb.Append((char)tabla[i][j]);
                    if (j < dimY - 1)
                        sb.Append(',');
                }
                if (i < dimX - 1)
                    sb.Append(';');
            }
            return sb.ToString();
        }

        static bool ProveriPotopio(Igrac igrac, int pogodjenoPolje, int dimY)
        {
            // jer podmornice imaju 2 polja svaka
            int drugiDeo = -1;


            if (igrac.podmornice.Contains(pogodjenoPolje - 1))
                drugiDeo = pogodjenoPolje - 1;
            else if (igrac.podmornice.Contains(pogodjenoPolje + 1))
                drugiDeo = pogodjenoPolje + 1;

            if (drugiDeo == -1)
                return false;


            int red1 = (pogodjenoPolje - 1) / dimY;
            int kol1 = (pogodjenoPolje - 1) % dimY;
            int red2 = (drugiDeo - 1) / dimY;
            int kol2 = (drugiDeo - 1) % dimY;

            // oba dela pogodjena?
            return (igrac.tabla[red1][kol1] == Simboli.pogodjeno && igrac.tabla[red2][kol2] == Simboli.pogodjeno);
        }

        static bool SvePodmornicePotopljene(Igrac igrac, int dimX, int dimY)
        {

            for (int i = 0; i < igrac.podmornice.Length; i++)
            {
                int polje = igrac.podmornice[i];
                if (polje == -1)
                    continue;

                int red = (polje - 1) / dimY;
                int kol = (polje - 1) % dimY;

                if (igrac.tabla[red][kol] != Simboli.pogodjeno)
                    return false;
            }
            return true;
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

        #region pronalazenje adrede servera
        static string PronadjiIPAdresu()
        {
            try
            {
                // Prvo Ethernet
                string ethernetIP = PronadjiIPPoTipu(NetworkInterfaceType.Ethernet);
                if (!string.IsNullOrEmpty(ethernetIP))
                {
                    return ethernetIP;
                }

                // Ako nema Ethernet, onda WiFi
                string wifiIP = PronadjiIPPoTipu(NetworkInterfaceType.Wireless80211);
                if (!string.IsNullOrEmpty(wifiIP))
                {
                    return wifiIP;
                }

                // Ako nema ni Ethernet ni WiFi, Loopback
                return IPAddress.Loopback.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska pri pronalazenju IP adrese: " + ex.Message);
                return IPAddress.Loopback.ToString();
            }
        }

        static string PronadjiIPPoTipu(NetworkInterfaceType tip)
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Da li je interfejs aktivan i odgovarajuceg tipa
                if (ni.NetworkInterfaceType == tip && ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        // Samo IPv4 adrese (ne IPv6)
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            //Console.WriteLine($"  - Pronadjen {tip} interfejs: {ni.Name} -> {ip.Address}");
                            return ip.Address.ToString();
                        }
                    }
                }
            }
            return null;
        }
        #endregion pronalazenje adrede servera
    }
}