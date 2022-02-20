using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace HistoracleTools.Models
{
    [Serializable]  
    public class RequestModel
    {
        public string HttpMethod { get; }
        public string Url { get; }
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public RequestModel(string httpMethod, string url)
        {
            HttpMethod = httpMethod;
            Url = url;
        }

        public void AddProperty(string key, string value)
        {
            Properties.Add(key,value);
        }

        
        public override string ToString()
        {
            var propsString = string.Join(":", Properties.ToImmutableSortedDictionary().Select(pair => $"{pair.Key}={pair.Value}"));
            return $"{HttpMethod}, {Url}, \"{propsString}\"";
        }

        public string GetSummary(IEnumerable<string> propOrder)
        {
            var response = "";
            foreach (var prop in propOrder)
            {
                if(Properties.ContainsKey(prop))
                    response += $"{prop}={Properties[prop]}";
                // else
                // {
                //     response += $"{prop}=NA";
                // }
            }

            return response;
        }
    }
}