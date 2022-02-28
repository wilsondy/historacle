using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HistoracleTools.Models;
using Microsoft.Extensions.Logging;

namespace HistoracleTools.Algorithms
{
    public interface IAlgorithm
    {
        Task<DifferenceReport> IsDifferent(string analysisId, RestlerModel a, RestlerModel b, Func<RestlerModel, IEnumerable<RequestResponseModel>> selector, double pValueCutoff, double? minPoints, double? epsilon, ILogger logger);
    }

    public enum DifferenceType
    {
        RequestsNotCompatible, //when significant different in request clusters - no further analysis!
        ResponseDifferent, 
        ResponseNotDifferent
    }
    public class DifferenceReport
    {
        public DifferenceReport(DifferenceType isDifferent, ClusteringSummary summary)
        {
            IsDifferent = isDifferent;
            Summary = summary;
        }

        public ClusteringSummary Summary { get; set; }

        public DifferenceType IsDifferent { get; }
    }
}