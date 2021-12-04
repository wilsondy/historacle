using System;

namespace HistoracleTools.Parse
{
    //https://eximia.co/computing-the-levenshtein-edit-distance-of-two-strings-using-c/
    public static class Levenshtein
    {
        public static int ComputeDistance(
            string first,
            string second
        )
        {
            if (string.IsNullOrEmpty(first))
            {
                return second.Length;
            }

            if (string.IsNullOrEmpty(second))
            {
                return first.Length;
            }

            if (string.IsNullOrEmpty(second) && string.IsNullOrEmpty(first))
                return 0;

            var current = 1;
            var previous = 0;
            var r = new int[2, second.Length + 1];
            for (var i = 0; i <= second.Length; i++)
            {
                r[previous, i] = i;
            }

            for (var i = 0; i < first.Length; i++)
            {
                r[current, 0] = i + 1;

                for (var j = 1; j <= second.Length; j++) 
                { 
                    var cost = (second[j - 1] == first[i]) ? 0 : 1; 
                    r[current, j] = Min( 
                        r[previous, j] + 1, 
                        r[current, j - 1] + 1, 
                        r[previous, j - 1] + cost ); 
                } 
                previous = (previous + 1) % 2; 
                current = (current + 1) % 2; 
            } 
            return r[previous, second.Length];
        } 

        private static int Min(int e1, int e2, int e3) =>
            Math.Min(Math.Min(e1, e2), e3);
    }
}