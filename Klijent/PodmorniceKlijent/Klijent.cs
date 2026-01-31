using PodmorniceLibrary;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;

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


                #region postavkaTable

                if (dimX == -1 || dimY == -1 || dozvoljenoPromasaja == -1)
                {
                    Console.WriteLine("Greska pri parsiranju dimenzija table ili dozvoljenog broja promasaja.");
                    return;
                }

                bool postavljeno = false;
                while (!postavljeno)
                {
                    int[] uneseneVrednosti = unesiPodmornice(dimX, dimY);
                    clientTCP.Send(Encoding.UTF8.GetBytes(string.Join(",", uneseneVrednosti)));
                    postavljeno = true;
                }

                #endregion postavkaTable

                bool igraSeNastavlja = true;
                while (igraSeNastavlja)
                {
                    #region gejmplej

                    Console.WriteLine("\nCekam da igra pocne...");
                    // Cekamo da server salje start
                    byte[] bufferStart = new byte[1024];
                    int bytesStart = clientTCP.Receive(bufferStart);
                    string startMsg = Encoding.UTF8.GetString(bufferStart, 0, bytesStart);

                    if (startMsg == "start")
                    {
                        Console.WriteLine("\n========== IGRA JE POCELA! ==========\n");
                    }

                    while (!krajIgre)
                    {
                        try
                        {
                            // Cekam poruku od servera, on inicira sve
                            byte[] buffer = new byte[1024];
                            int bytes = clientTCP.Receive(buffer);
                            string poruka = Encoding.UTF8.GetString(buffer, 0, bytes);

                            if (poruka.StartsWith("Potez igraca:"))
                            {
                                int mojID = int.Parse(poruka.Split(':')[1]);
                                Console.WriteLine($"\n========== TVOJ RED MAJSTORE (Vi ste igrac {mojID}) ==========");

                                bool nastavakPoteza = true;

                                while (nastavakPoteza && !krajIgre)
                                {
                                    // ID igraca ciju tablu zelim da vidim
                                    int idMete;
                                    while (true)
                                    {
                                        Console.WriteLine("\nUnesi ID igraca ciju tablu zelite da vidite:");
                                        string input = Console.ReadLine();
                                        if (!Int32.TryParse(input, out idMete))
                                        {
                                            Console.WriteLine("Neispravan ID. Pokusajte ponovo.");
                                            continue;
                                        }

                                        if (idMete == mojID)
                                        {
                                            Console.WriteLine("Ne mozes sebe da biras, glupane XD.");
                                            continue;
                                        }

                                        break;
                                    }

                                    string zahtev = $"pregledTable:{idMete}";
                                    clientTCP.Send(Encoding.UTF8.GetBytes(zahtev));

                                    byte[] bufferBoard = new byte[4096];
                                    int bytesBoard = clientTCP.Receive(bufferBoard);
                                    string odgovorBoard = Encoding.UTF8.GetString(bufferBoard, 0, bytesBoard);

                                    if (odgovorBoard.StartsWith("Greska:"))
                                    {
                                        Console.WriteLine(odgovorBoard.Split(':')[1]);
                                        continue;
                                    }

                                    if (odgovorBoard.StartsWith("Trazena tabla:"))
                                    {
                                        string tablaPodaci = odgovorBoard.Split(':')[1];
                                        Console.WriteLine($"\n===== TABLA IGRACA {idMete} =====");
                                        PrikaziTablu(tablaPodaci, dimX, dimY);
                                    }

                                    Console.WriteLine($"\nUnesite broj polja koje zelite da gadjate (1-{dimX * dimY}):");
                                    int polje = Int32.Parse(Console.ReadLine());

                                    // Saljem gađanje
                                    string gadjanje = $"gadjanje:{idMete},{polje}";
                                    clientTCP.Send(Encoding.UTF8.GetBytes(gadjanje));

                                    // Primanje rezultata
                                    byte[] bufferRezultat = new byte[1024];
                                    int bytesRezultat = clientTCP.Receive(bufferRezultat);
                                    string rezultat = Encoding.UTF8.GetString(bufferRezultat, 0, bytesRezultat);

                                    if (rezultat.StartsWith("Greska:"))
                                    {
                                        Console.WriteLine("Greska: " + rezultat.Split(':')[1]);
                                        continue;
                                    }

                                    if (rezultat.StartsWith("REZULTAT:"))
                                    {
                                        string status = rezultat.Split(':')[1];
                                        Console.WriteLine($"\n*** {status} ***");

                                        if (status == "PROMASIO")
                                        {
                                            nastavakPoteza = false;
                                            Console.WriteLine("Vase potez je zavrsen. Promasio si kume, ocajno");
                                            break;
                                        }
                                        else if (status == "POGODIO")
                                        {
                                            Console.WriteLine("Imate pravo na jos jedan pokusaj!");
                                            nastavakPoteza = true;
                                        }
                                        else if (status == "POTOPIO")
                                        {
                                            Console.WriteLine("Potopili ste podmornicu! Bravo majstore! Imate pravo na jos jedan pokusaj!");
                                            nastavakPoteza = true;
                                        }
                                        else if (status == "POTOPIO_KRAJ")
                                        {
                                            Console.WriteLine("Potopili ste POSLEDNJU podmornicu! Cekanje proglasenja pobednika...");
                                            nastavakPoteza = false;
                                            break;
                                        }
                                    }
                                    else if (rezultat.StartsWith("Eliminisan:"))
                                    {
                                        Console.WriteLine("\n" + rezultat.Split(':')[1]);
                                        krajIgre = true;
                                        nastavakPoteza = false;
                                    }
                                }
                            }
                            else if (poruka.StartsWith("POBEDA:"))
                            {
                                Console.WriteLine("\n========================================");
                                Console.WriteLine("    " + poruka.Split(':')[1]);
                                Console.WriteLine("========================================");
                                krajIgre = true;
                            }
                            else if (poruka.StartsWith("KRAJ:"))
                            {
                                Console.WriteLine("\n" + poruka.Split(':')[1]);
                                krajIgre = true;
                            }
                            else if (poruka.StartsWith("Eliminisan:"))
                            {
                                Console.WriteLine("\n" + poruka.Split(':')[1]);
                                krajIgre = true;
                            }
                            else if (poruka.StartsWith("NERESENO:"))
                            {
                                Console.WriteLine($"\n{poruka.Split(':')[1]}");
                                krajIgre = true;
                            }
                            else if (poruka.StartsWith("UPOZORENJE:"))
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(poruka.Split(':')[1]);
                                Console.ResetColor();
                            }
                            else
                            {
                                // Nije moj potez, cekam
                                Console.WriteLine("Cekamo potez drugih igraca...");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Greska tokom igre: {ex.Message}");
                            krajIgre = true;
                        }
                    }
                    #endregion gejmplej

                    Console.WriteLine("\n========================================");
                    Console.WriteLine("Da li zelite da igrate novu igru? (nova/ne):");
                    string odgovorNovaIgra = Console.ReadLine().Trim().ToLower();

                    try
                    {
                        clientTCP.Send(Encoding.UTF8.GetBytes(odgovorNovaIgra));

                        byte[] bufferFinal = new byte[1024];
                        int bytesFinal = clientTCP.Receive(bufferFinal);
                        string finalPoruka = Encoding.UTF8.GetString(bufferFinal, 0, bytesFinal);

                        if (finalPoruka.StartsWith("RESTART:"))
                        {
                            Console.WriteLine("\n" + finalPoruka.Split(':')[1]);
                            Console.WriteLine("Restartujem igru...");
                            krajIgre = false;
                        }
                        else if (finalPoruka.StartsWith("ZATVARANJE:"))
                        {
                            Console.WriteLine("\n" + finalPoruka.Split(':')[1]);
                            igraSeNastavlja = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Greska: {ex.Message}");
                    }
                }

                if (clientTCP != null)
                    clientTCP.Close();
                Console.ReadKey();
            }
        }
        #region funkcije za komunikaciju
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
        #endregion funkcije za komunikaciju

        #region funkcije igre
        static void PrikaziTablu(string tablaPodaci, int dimX, int dimY)
        {
            string[] redovi = tablaPodaci.Split(';');
            for (int i = 0; i < redovi.Length; i++)
            {
                string[] kolone = redovi[i].Split(',');
                for (int j = 0; j < kolone.Length; j++)
                {
                    Console.Write(kolone[j] + " ");
                }
                Console.WriteLine();
            }
        }

        static int[] unesiPodmornice(int dimX, int dimY)
        {
            int[] brojevi = new int[dimX * dimY];
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
                    if (brojevi.Contains(broj) || brojevi.Contains(broj + 1))
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
        #endregion funkcije igre
    }

}
