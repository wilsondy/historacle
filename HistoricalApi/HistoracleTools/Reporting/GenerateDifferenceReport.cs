using System;
using System.Collections.Generic;
using System.Linq;
using HistoracleTools.Algorithms;
using HistoracleTools.Models;

namespace HistoracleTools.Reporting
{
    public class GenerateDifferenceReport
    {
        public DifferenceReport GenerateReport(RestlerModel a, RestlerModel b, ClusteringSummary summary, bool silent)
        {
              //per cluster
            //count number from each group
            //for each path out, count num from group a and num from group b going to response cluster
            //TODO some stats :)
            //For now, report groups with > 20% difference in transition probability
            //Any Clusters that contain less than 20% of any group... especially zero
            //output  Group, Request Cluster, Request Percent, Response Group, Response Group Percent
            var requestClusterIds = summary.SummaryResults.Select(g => g.RequestClusterId).ToHashSet();
            var responseClusterIds = summary.SummaryResults.Select(g => g.ResponseClusterId).ToHashSet();
            var requestSummarize = SummarizeRawGroups(requestClusterIds.ToArray(), summary.SummaryResults, a.GroupId, b.GroupId, false);
            var responseSummarize = SummarizeRawGroups(responseClusterIds.ToArray(), summary.SummaryResults, a.GroupId, b.GroupId,true);
            var countResponseOverweight = 0;
            
            foreach (var tuple in requestSummarize)
            {
                if (!silent)
                {
                    Console.WriteLine(
                        $"Request, {tuple.Key}, {tuple.Value.Item1} ({tuple.Value.Item3}), {tuple.Value.Item2} ({tuple.Value.Item4}), {Math.Abs(tuple.Value.Item1 - tuple.Value.Item2)}");
                }
            }
            foreach (var tuple in responseSummarize)
            {
                var diff = Math.Abs(tuple.Value.Item1 - tuple.Value.Item2);
                if (!silent)
                {
                    Console.WriteLine(
                        $"Response, {tuple.Key}, {tuple.Value.Item1} ({tuple.Value.Item3}), {tuple.Value.Item2} ({tuple.Value.Item4}), {diff}");
                }

                if (diff >= 0.4)
                {
                    countResponseOverweight++; //totally picked out of the air! 0.3-0.7 0.8-0.2
                }
            }
            
            //Request-Response , percentA, percentB -- that take the transition
            foreach (var requestClusterId in requestClusterIds)
            {
                var requestIdsGroupAInCluster = summary.SummaryResults.Where(g => g.RequestClusterId == requestClusterId && g.GroupId == a.GroupId)
                    .Select(g => g.RequestId);
                
                var requestIdsGroupBInCluster = summary.SummaryResults.Where(g => g.RequestClusterId == requestClusterId && g.GroupId == b.GroupId)
                    .Select(g => g.RequestId);
                
                foreach (var responseClusterid in responseClusterIds)
                {
                    var itemsInResponseFromGroupACluster = summary.SummaryResults.Count(g =>
                        g.GroupId == a.GroupId && g.ResponseClusterId == responseClusterid && 
                        requestIdsGroupAInCluster.Contains(g.RequestId));
                    var itemsInResponseFromGroupBCluster = summary.SummaryResults.Count(g =>
                        g.GroupId == b.GroupId && g.ResponseClusterId == responseClusterid &&
                        requestIdsGroupBInCluster.Contains(g.RequestId));

                    var percentTransitionA = 0.0;
                    if (itemsInResponseFromGroupACluster > 0)
                        percentTransitionA =
                            (double)itemsInResponseFromGroupACluster/ (double)requestIdsGroupAInCluster.Count() ;
                    var percentTransitionB = 0.0;
                    
                    if (itemsInResponseFromGroupBCluster > 0)
                        percentTransitionB =
                            (double)itemsInResponseFromGroupBCluster / (double)requestIdsGroupBInCluster.Count() ;

                    if (itemsInResponseFromGroupBCluster > requestIdsGroupBInCluster.Count() ||
                        itemsInResponseFromGroupACluster > requestIdsGroupAInCluster.Count())
                        throw new Exception("unexpected counts!");

                    if (!silent)
                    {
                        if (percentTransitionA > 0.0 || percentTransitionB > 0.0)
                        {
                            Console.WriteLine(
                                $"trans probs: {requestClusterId}->{responseClusterid}, {itemsInResponseFromGroupACluster}/{requestIdsGroupAInCluster.Count()}={percentTransitionA}, {itemsInResponseFromGroupBCluster}/{requestIdsGroupBInCluster.Count()}={percentTransitionB}, {Math.Abs(percentTransitionA - percentTransitionB)}");
                        }
                    }
                }
            }
            if(countResponseOverweight >0)
                return new DifferenceReport(true, summary);
            else
            {
                return new DifferenceReport(false, summary);
            }
        }
        private  Dictionary<string, (double, double,int,int)> SummarizeRawGroups(String[] clusterIds, List<SummaryResult> groupings, string groupIdA, string groupIdB, bool useResponseClusterId)
        {
            Dictionary<string, (double percentA, double percentB, int, int)> results = new  Dictionary<string, (double, double,int,int)>();
            foreach (var clusterId in clusterIds)
            {
                var countGroupA = groupings.Where(g => g.GroupId == groupIdA &&
                                                       clusterId == (useResponseClusterId? g.ResponseClusterId :g.RequestClusterId));
                var countGroupB = groupings.Where(g =>
                    g.GroupId == groupIdB &&
                    clusterId == (useResponseClusterId ? g.ResponseClusterId : g.RequestClusterId));
                double percentA = (double)countGroupA.Count() / (double)(countGroupA.Count() + countGroupB.Count());
                double percentB = (double)countGroupB.Count() / (double)(countGroupA.Count() + countGroupB.Count());
                results.Add(clusterId,(percentA,percentB, countGroupA.Count(), countGroupB.Count()));
            }

            return results;
        }
    }
}