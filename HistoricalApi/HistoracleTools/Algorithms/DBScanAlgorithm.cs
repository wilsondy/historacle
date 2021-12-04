using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExternalTools.DbscanImplementation;
using HistoracleTools.Models;

namespace HistoracleTools.Algorithms
{
    public class DbScanAlgorithm : IAlgorithm
    {
        public async Task<DifferenceReport> IsDifferent(string analysisId, RestlerModel a, RestlerModel b, Func<RestlerModel, IEnumerable<RequestResponseModel>> selector, bool silent)
        {
            var r = new Random();
            var modelA = selector(a).OrderBy(x => new Guid()).Take(200);
            var modelB = selector(b).OrderBy(x =>new Guid()).Take(200);
            var input = modelA.Concat(modelB);
            if (input.Count() < 10)
            {
                Console.WriteLine($"Skipping due to no data {analysisId}");
                return null;
            }

            Console.WriteLine($" model a contributes {modelA.Count()} and modelB {modelB.Count()}");
            var groupings = RunDBSCAN(analysisId, input).ToArray();
            //per cluster
            //count number from each group
            //for each path out, count num from group a and num from group b going to response cluster
            //TODO some stats :)
            //For now, report groups with > 20% difference in transition probability
            //Any Clusters that contain less than 20% of any group... especially zero
            //output  Group, Request Cluster, Request Percent, Response Group, Response Group Percent
            var requestClusterIds = groupings.Select(g => g.RequestClusterId).ToHashSet();
            var responseClusterIds = groupings.Select(g => g.ResponseClusterId).ToHashSet();
            var requestSummarize = SummarizeRawGroups(requestClusterIds.ToArray(), groupings, a.GroupId, b.GroupId, false);
            var responseSummarize = SummarizeRawGroups(responseClusterIds.ToArray(), groupings, a.GroupId, b.GroupId,true);
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
                var requestIdsGroupAInCluster = groupings.Where(g => g.RequestClusterId == requestClusterId && g.GroupId == a.GroupId)
                    .Select(g => g.RequestId);
                
                var requestIdsGroupBInCluster = groupings.Where(g => g.RequestClusterId == requestClusterId && g.GroupId == b.GroupId)
                    .Select(g => g.RequestId);
                
                foreach (var responseClusterid in responseClusterIds)
                {
                    var itemsInResponseFromGroupACluster = groupings.Count(g =>
                        g.GroupId == a.GroupId && g.ResponseClusterId == responseClusterid && 
                        requestIdsGroupAInCluster.Contains(g.RequestId));
                    var itemsInResponseFromGroupBCluster = groupings.Count(g =>
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
                return new DifferenceReport(true);
            else
            {
                return new DifferenceReport(false);
            }
        }

        private  Dictionary<string, (double, double,int,int)> SummarizeRawGroups(String[] clusterIds, SummaryResult[] groupings, string groupIdA, string groupIdB, bool useResponseClusterId)
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
        
         //https://medium.com/@tarammullin/dbscan-parameter-estimation-ff8330e3a3bd
        //Rule of thumb 2X number of dimensions
        //Throwing out dimensions that NEVER change during and endpoint
        private int PickMinPts(IEnumerable<RequestResponseModel> requestResponseModels)
        {
            int count = 1;
            var dimensions = 0;
            Dictionary<string, string> globallyReqStableProps = new Dictionary<string, string>();
            Dictionary<string, string> globallyResStableProps = new Dictionary<string, string>();
            foreach (var reqModel in requestResponseModels)
            {
                if (count == 1)
                {
                    globallyReqStableProps = reqModel.Request.Properties.ToDictionary(entry => entry.Key,
                        entry => entry.Value);
                    globallyResStableProps = reqModel.Response.Properties.ToDictionary(entry => entry.Key,
                        entry => entry.Value);
                }
                else
                {
                    TrimUnstableProps(reqModel.Request.Properties, globallyReqStableProps);
                    TrimUnstableProps(reqModel.Response.Properties, globallyResStableProps);

                }
              
                dimensions += reqModel.Request.Properties.Count + reqModel.Response.Properties.Count;
                
                count += 2;
            }
            Console.WriteLine($"Globally Stable Request Props {String.Join(":", globallyReqStableProps)}");
            Console.WriteLine($"Globally Stable Response Props {String.Join(":", globallyResStableProps)}");
           
         
            dimensions /= count;
            dimensions = dimensions - globallyReqStableProps.Count - globallyResStableProps.Count;
            if (dimensions < 0)
                dimensions = 2;
            return Math.Max(3,dimensions * 2);
        }

        private void TrimUnstableProps(Dictionary<string,string> currProps, Dictionary<string,string> globallyStableProps )
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
        public IEnumerable<SummaryResult> RunDBSCAN(string analysisName,/* string groupIdA, string groupIDB,*/ IEnumerable<RequestResponseModel> data)
        {
            //var data = Sequences.SelectMany(seq => seq.Requests.FindAll(x => x.GetEndpoint() == endpoint));
            //data = data.Take(100);
            var minPts = PickMinPts(data);
            var r = new Random();
            var reqreqDist = new DistanceCache(new EuclideanRequestRequestDistance());
            var resresDist = new DistanceCache(new EuclideanResponseResponseDistance());
            //var reqreqDist = new EuclideanRequestRequestDistance();
            //var resresDist = new EuclideanResponseResponseDistance();
            var epsilonSample = data;//.OrderBy(x => r.NextDouble()).Take(200);
            
            
            Console.WriteLine($"Min Points is {minPts}");
            var requestEpsilon = 1.9; //PickEpsilon(minPts, epsilonSample, reqreqDist.GetDistance);
            var responseEpsilon = 1.9; //PickEpsilon(minPts, epsilonSample, resresDist.GetDistance); 
            Console.WriteLine($"Response epsilon {responseEpsilon}");
            Console.WriteLine($"Request epsilon {requestEpsilon}");
            var reqreqDBScan = new DbscanAlgorithm<RequestResponseModel>(reqreqDist.GetDistance);
            var resresDBScan = new DbscanAlgorithm<RequestResponseModel>(resresDist.GetDistance);
            var dataArr = data.ToArray();
            Task<DbscanResult<RequestResponseModel>> requestRequestTask =
                Task<DbscanResult<RequestResponseModel>>.Factory.StartNew(() =>
                    {
                        
                        return reqreqDBScan.ComputeClusterDbscan(allPoints: dataArr, epsilon: requestEpsilon, minimumPoints: minPts);
                    });
            
            Task<DbscanResult<RequestResponseModel>> responseResponseTask =
                Task<DbscanResult<RequestResponseModel>>.Factory.StartNew(() =>
                {
                    
                    return resresDBScan.ComputeClusterDbscan(allPoints: dataArr, epsilon: responseEpsilon,
                        minimumPoints: minPts);
                });
            Console.WriteLine("Tasks running...");
            var reqreqResult = requestRequestTask.Result;
            var resresResult = responseResponseTask.Result;
            //Console.WriteLine($"After REQREQ DBSCAN {reqreqDist.hits} {reqreqDist.misses}");
            //Req Num, Cluster Num ReqReq, Cluster Num ReqRes
            try
            {
                var results  = PrepareSummary(analysisName, dataArr, reqreqResult, resresResult);
                WriteCSV(results);
                return results;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
            }
        }

        private double PickEpsilon(int minPts, IEnumerable<RequestResponseModel> data, Func<RequestResponseModel, RequestResponseModel, double> metricFunc)
        {
            int localSampleSize = 10;
            //https://towardsdatascience.com/machine-learning-clustering-dbscan-determine-the-optimal-value-for-epsilon-eps-python-example-3100091cfbc
            List<float> distances = new List<float>(data.Count()*localSampleSize);
            
            foreach (var item in data)
            {
                var myDistances = new List<float>();
                foreach (var innerItem in data)
                {
                    if (innerItem == item)
                        continue;
                    myDistances.Add((float) metricFunc(item, innerItem ));
                }
                myDistances.Sort();
                //TODO read the paper if we are going to use this.  when there are many data points, and many members per cluster, this can
                //fail badly since you'll get mostly 0 distances within 3 nearest points
                distances.AddRange(myDistances.Take(localSampleSize));
            }
            
            distances.Sort();
            //https://stackoverflow.com/questions/25512297/how-to-find-the-point-where-the-slope-of-a-line-changes
            
            List<PointF> D1 = new List<PointF>();     // 1st derivative
            List<PointF> D2 = new List<PointF>();     // 2nd derivative
            List<PointF> M = new List<PointF>();      // reasonably large values from D2

            float cutOff = 2.0f;      // cutoff value to determine 'reasonably' large slope changes
            // 1st derivative
            for (int i = 1; i < distances.Count; i++) D1.Add(new PointF(i, distances[i - 1] - distances[i]));
            // 2nd derivative
            for (int i = 1; i < D1.Count; i++)
            {
                D2.Add(new PointF(i, D1[i - 1].Y - D1[i].Y));
               //s Console.Write($"{D1[i - 1].Y - D1[i].Y} M={D1[i - 1].Y - D1[i].Y / cutOff},");
            }
            Console.WriteLine("end 2nd deriv");
            // collect 'reasonably' large values from D2
            foreach (PointF p in D2) if (Math.Abs(p.Y / cutOff ) > 1) M.Add(p);
            foreach (var d in distances)
            {
                Console.Write($"{d},");
            }
            Console.WriteLine("");
            var epsilon =0f;
            //last one
            //int targetX = (int) M[M.Count -1 ].X;
            //epsilon = Math.Max(epsilon, distances[targetX]);
            //first nonzero one
            int count = 0;
            while(epsilon < 0.001 && M.Count > count){
                epsilon = distances[(int) M[count].X];
                count++;
            }

            epsilon = Math.Max(epsilon, 1.0f);
            Console.WriteLine($"Epsilon = {epsilon}");
            return epsilon;
        }

        private IEnumerable<SummaryResult> PrepareSummary(string analysisId, IEnumerable<RequestResponseModel> data, DbscanResult<RequestResponseModel> reqreqResult, DbscanResult<RequestResponseModel> resresResult)
        {
            var results = new List<SummaryResult>();
            foreach (var request in data)
            {
                var foundReqReq = reqreqResult.Clusters.Values.SelectMany(list => list.Where(x => x.Feature.ReqNum == request.ReqNum)).FirstOrDefault();
                var foundResRes = resresResult.Clusters.Values.SelectMany(list => list.Where(x => x.Feature.ReqNum == request.ReqNum)).FirstOrDefault();
                
                //   var foundReqReqNoise = reqreqResult.Noise.FirstOrDefault(x => x.Feature.ReqNum == request.ReqNum);
                //   var foundResResNoise = resresResult.Noise.FirstOrDefault(x => x.Feature.ReqNum == request.ReqNum);
                results.Add(new SummaryResult(analysisId,
                    request.GroupId,
                    request.ReqNum.ToString(),
                    foundReqReq == null ? "Noise" : foundReqReq.ClusterId.ToString(),
                    foundResRes == null ? "Noise" : foundResRes.ClusterId.ToString(),
                    request.GetSummary()));
            }
            return results;
        }
        
        private void WriteCSV(IEnumerable<SummaryResult> summaryResults)
        {
            //new DateTime().Ticks
            StreamWriter file = new("/Users/dwilson/school/historaclerepo/" +new DateTime().Ticks+  "_out.csv");
            file.WriteLine($"analysisId, groupId, ReqNum, RequestClusterId, ResponseClusterId, Summary ");
            foreach (var result in summaryResults)
            {

                file.WriteLine(
                    $"${result.AnalysisId},{result.GroupId}, {result.RequestId},{result.RequestClusterId}, {result.ResponseClusterId}, {result.RequestSummary}");
            }
            file.Flush();
            

        }
       
    }

    public class SummaryResult
    {
        public string GroupId;
        public string RequestClusterId;
        public string ResponseClusterId;
        public string RequestSummary;
        public string AnalysisId;
        public string RequestId;
        public SummaryResult(string analysisId, string groupId, string requestId, string requestClusterId, string responseClusterId, string requestSummary)
        {
            AnalysisId = analysisId;
            GroupId = groupId;
            RequestId = requestId;
            RequestClusterId = requestClusterId;
            ResponseClusterId = responseClusterId;
            RequestSummary = requestSummary;
        }

      
    }
}