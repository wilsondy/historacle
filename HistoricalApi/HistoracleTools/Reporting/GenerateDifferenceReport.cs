using System;
using System.Collections.Generic;
using System.Linq;
using Accord.Statistics.Distributions;
using Accord.Statistics.Testing;
using HistoracleTools.Algorithms;
using HistoracleTools.Models;
using Microsoft.Extensions.Logging;

namespace HistoracleTools.Reporting
{
    public class GenerateDifferenceReport
    {
        public DifferenceReport GenerateReport(RestlerModel a, RestlerModel b, ClusteringSummary summary, int countOfA, int countOfB, double pValueCutoff, ILogger logger)
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
            var countRequestClusteringSignificantlyDifferent = 0;
            foreach (var tuple in requestSummarize)
            {
                if (IsSignificantDifferenceViaTwoProportionTest(tuple.Value.Item3, countOfA, tuple.Value.Item4,
                        countOfB, pValueCutoff, logger).Item1)
                {
                    countRequestClusteringSignificantlyDifferent++;
                }
                logger.LogDebug(
                    $"Request, {tuple.Key}, {tuple.Value.Item1} ({tuple.Value.Item3}), {tuple.Value.Item2} ({tuple.Value.Item4}), {Math.Abs(tuple.Value.Item1 - tuple.Value.Item2)}");
                
            }

            if (countRequestClusteringSignificantlyDifferent > 0)
            {
                summary.AnalysisParameters["countRequestClusteringSignificantlyDifferent"] =
                    countRequestClusteringSignificantlyDifferent.ToString();

                logger.LogWarning(
                    "Request clusters are different (Significantly). Traffic is too dissimilar. Declining to analyze responses.");
                
                return new DifferenceReport(DifferenceType.RequestsNotCompatible, summary);
            }
            else
            {
                foreach (var tuple in responseSummarize)
                {
                    var diff = Math.Abs(tuple.Value.Item1 - tuple.Value.Item2);
                    
                    
                    logger.LogDebug(
                        $"Response, {tuple.Key}, {tuple.Value.Item1} ({tuple.Value.Item3}), {tuple.Value.Item2} ({tuple.Value.Item4}), {diff}");
                

                    if (IsSignificantDifferenceViaTwoProportionTest(tuple.Value.Item3, countOfA, tuple.Value.Item4,
                            countOfB, pValueCutoff, logger).Item1)
                    {
                        countResponseOverweight++;
                    }
                }

                //Request-Response , percentA, percentB -- that take the transition
                foreach (var requestClusterId in requestClusterIds)
                {
                    var requestIdsGroupAInCluster = summary.SummaryResults.Where(g =>
                            g.RequestClusterId == requestClusterId && g.GroupId == a.GroupId)
                        .Select(g => g.RequestId);

                    var requestIdsGroupBInCluster = summary.SummaryResults.Where(g =>
                            g.RequestClusterId == requestClusterId && g.GroupId == b.GroupId)
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
                                (double) itemsInResponseFromGroupACluster / (double) requestIdsGroupAInCluster.Count();
                        var percentTransitionB = 0.0;

                        if (itemsInResponseFromGroupBCluster > 0)
                            percentTransitionB =
                                (double) itemsInResponseFromGroupBCluster / (double) requestIdsGroupBInCluster.Count();

                        if (itemsInResponseFromGroupBCluster > requestIdsGroupBInCluster.Count() ||
                            itemsInResponseFromGroupACluster > requestIdsGroupAInCluster.Count())
                            throw new Exception("unexpected counts!");

                        var test = IsSignificantDifferenceViaTwoProportionTest(itemsInResponseFromGroupACluster, countOfA,
                            itemsInResponseFromGroupBCluster, countOfB, pValueCutoff, logger);
                        if (test.Item1)
                        {
                            logger.LogInformation(
                                $" Significant transition difference: {requestClusterId}->{responseClusterid} P Value: {test.Item2}");
                        }
                        else
                        {
                            logger.LogDebug($" trans probs: {requestClusterId}->{responseClusterid} P Value: {test.Item2}");                            
                        }


                    }
                }

                if (countResponseOverweight > 0)
                    return new DifferenceReport(DifferenceType.ResponseDifferent, summary);
                else
                {
                    return new DifferenceReport(DifferenceType.ResponseNotDifferent, summary);
                }
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
        
        public (bool, double)  IsSignificantDifferenceViaTwoProportionTest(int countA, int allA, int countB, int allB, double alpha, ILogger logger){
            try
            {
                //These blow up the TwoProportionZTest so we take it off the table
                if (allA == 0 || allB == 0)
                    return (false, 1);
                if (countA == allA && countB == allB)
                    return (false, 1);
                if(countA == countB && allB == allA)
                    return (false, 1);
                var test = new TwoProportionZTest(countA, allA, countB, allB, TwoSampleHypothesis.ValuesAreDifferent);
                return ((test.PValue < alpha), test.PValue);
            }
            catch (ArgumentOutOfRangeException e)
            {
                logger.LogWarning("ProportionalTest Blew Up.  Returning no difference.");
            }

            return (false, double.NaN);
        }
    }
    
   
}

