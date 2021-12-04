using System;
using System.Collections.Generic;
using System.Linq;

namespace HistoracleTools.Models
{
    [Serializable]  
    public class RestlerModel
    {
        public string GroupId { get; }

        public RestlerModel(string groupId)
        {
            GroupId = groupId;
        }

        public List<RequestSequence> Sequences { get; } = new List<RequestSequence>();
        public void AddRequestSequence(RequestSequence next)
        {
            Sequences.Add(next);
        }

        public IEnumerable<string> GetEndpoints()
        {
            return Sequences.SelectMany(seq => seq.Requests.Select(req => req.GetEndpoint()).ToHashSet()).ToHashSet();
        }
        
       
    }
  

    
}