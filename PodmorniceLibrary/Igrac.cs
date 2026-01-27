using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PodmorniceLibrary
{
    [Serializable]
    public class Igrac
    {
        public int identifikacioniBroj;
        public int brojPromasaja;
        public int[] podmornice;
        public int[][] tabla;
    }

    public static class Simboli
    {
        public static char vecGadjano = 'M';
        public static char pogodjeno = 'H';
        public static char nijeGadjano = '_';
    }
}
