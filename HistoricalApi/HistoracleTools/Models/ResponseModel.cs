using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace HistoracleTools.Models
{
    [Serializable]  
    public class ResponseModel
    {
        public int HttpStatus { get; }

        public ResponseModel(int httpStatus)
        {
            this.HttpStatus = httpStatus;
        }

        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public override string ToString()
        {
            var propsString = string.Join(":", Properties.ToImmutableSortedDictionary().Select(pair => $"{pair.Key}={pair.Value}"));
            return $"{HttpStatus}, \"{propsString}\"";
        }

        public string getSummary()
        {
            return ToString();
        }
    }
}