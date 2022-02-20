using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HistoracleTools.Models;

namespace HistoracleTools.Algorithms
{
    public interface IAlgorithm
    {
        Task<DifferenceReport> IsDifferent(string analysisId, RestlerModel a, RestlerModel b, Func<RestlerModel, IEnumerable<RequestResponseModel>> selector, bool silent);
    }

    public class DifferenceReport
    {
        public DifferenceReport(bool isDifferent, ClusteringSummary summary)
        {
            IsDifferent = isDifferent;
            Summary = summary;
        }

        public ClusteringSummary Summary { get; set; }

        public bool IsDifferent { get; }
    }
}