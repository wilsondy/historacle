using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HistoracleTools.Algorithms;

namespace HistoracleTools.Reporting
{
    public class ReportClustering
    {
        
        public void WriteCSV(string outputDirectory, ClusteringSummary summary, bool wasDifferent)
        {
            var req = summary.RequestClusters.First().Value.requests.First().Request;
            var fileName = $"{req.HttpMethod}_{req.Url.Replace("/","")}";
            StreamWriter file = new($"{outputDirectory}/{(wasDifferent? "DIFFERENT_": "")}{summary.SummaryResults[0].AnalysisId}_{fileName}_out.csv");
            file.WriteLine($"analysisId, groupId, ReqNum, RequestClusterId, ResponseClusterId, Endpoint, Method, Req Props, Res Code, Res Props");
            //clusterId => props in order for output
            var reqProps = new Dictionary<string, IEnumerable<string>>();
            var resProps = new Dictionary<string, IEnumerable<string>>();
            foreach (var result in summary.RequestClusters)
            {
                if (!reqProps.ContainsKey(result.Value.ClusterId))
                    reqProps[result.Value.ClusterId] = result.Value.GetPropertiesOrderedByVariety();
            }
            foreach (var result in summary.ResponseClusters)
            {
                if (!resProps.ContainsKey(result.Value.ClusterId))
                    resProps[result.Value.ClusterId] = result.Value.GetPropertiesOrderedByVariety();
            }

            foreach (var result in summary.SummaryResults)
            {
                var requestCluster = summary.RequestClusters[result.RequestClusterId];
                var request = requestCluster.requests.First(model => model.ReqNum.ToString() == result.RequestId && model.GroupId == result.GroupId);
                var responseCluster = summary.ResponseClusters[result.ResponseClusterId];
                var reqPropOrder = requestCluster.GetPropertiesOrderedByVariety();
                var resPropOrder = responseCluster.GetPropertiesOrderedByVariety();
                file.WriteLine(
                    $"${result.AnalysisId},{result.GroupId}, {result.RequestId},{result.RequestClusterId}, {result.ResponseClusterId}," +
                    $" {request.GetEndpoint()} , {request.Method()}, { request.GetSummary(true, reqPropOrder)}, " +
                    $"{request.Response.HttpStatus}, { request.GetSummary(false, resPropOrder)}, {result.OldSummary}, {request.OldGetSummary()} ");
             
            }

            foreach (var VARIABLE in summary.AnalysisParameters)
            {
                file.WriteLine($"{VARIABLE.Key},{VARIABLE.Value}");
            }
            file.Flush();
        }
    }
}