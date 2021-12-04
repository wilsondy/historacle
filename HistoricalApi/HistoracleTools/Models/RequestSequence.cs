using System;
using System.Collections;
using System.Collections.Generic;

namespace HistoracleTools.Models
{
    [Serializable]  
    public class RequestSequence
    {
        public int Generation { get; }
        public int Sequence { get; }
        public int LineNum { get; }
        public RequestSequence(int generation, int sequence, int linenum)
        {
            Generation = generation;
            Sequence = sequence;
            LineNum = linenum;
        }

        public List<RequestResponseModel> Requests { get; } = new List<RequestResponseModel>();

        public void AddRequestResponse(RequestResponseModel next)
        {
            Requests.Add(next);
        }
       
    }
}