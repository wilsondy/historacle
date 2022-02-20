using System.Collections.Generic;

namespace HistoracleTools.Utils
{
    
    public class Utils
    {
        public static void TrimUnstableProps(Dictionary<string,string> currProps, Dictionary<string,string> globallyStableProps )
        {
            List<string> keysToDump = new List<string>();
            foreach (var entry in globallyStableProps)
            {
                if (!currProps.ContainsKey(entry.Key))
                {
                    keysToDump.Add(entry.Key);
                    continue;
                }

                if (!currProps[entry.Key].Equals(entry.Value)) {
                    keysToDump.Add(entry.Key);
                    continue;
                }
            }

            foreach (var key in keysToDump)
            {
                globallyStableProps.Remove(key);
            }
            
        }
    }
    
    
}