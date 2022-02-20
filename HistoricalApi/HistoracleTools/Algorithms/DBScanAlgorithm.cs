using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExternalTools.DbscanImplementation;
using HistoracleTools.Models;
using HistoracleTools.Reporting;
using HistoracleTools.Utils;
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
            var summary = RunDBSCAN(analysisId, input);
            var report = new GenerateDifferenceReport();
            return report.GenerateReport(a, b, summary, silent);
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
                    Utils.Utils.TrimUnstableProps(reqModel.Request.Properties, globallyReqStableProps);
                    Utils.Utils.TrimUnstableProps(reqModel.Response.Properties, globallyResStableProps);

                }
              
                dimensions += reqModel.Request.Properties.Count + reqModel.Response.Properties.Count;
                
                count += 2;
            }
           // Console.WriteLine($"Globally Stable Request Props {String.Join(":", globallyReqStableProps)}");
           // Console.WriteLine($"Globally Stable Response Props {String.Join(":", globallyResStableProps)}");
           
         //TODO FIX LISTS OF RESULTS CAUSING OVER_COUNTING!!!!
            dimensions /= count;
            dimensions = dimensions - globallyReqStableProps.Count - globallyResStableProps.Count;
            if (dimensions < 0)
                dimensions = 2;
            return /*3;//*/Math.Max(3,dimensions * 2);
        }

        
        public ClusteringSummary RunDBSCAN(string analysisName,/* string groupIdA, string groupIDB,*/ IEnumerable<RequestResponseModel> data)
        {
            var analysisDetails = new Dictionary<string, string>();
            //var data = Sequences.SelectMany(seq => seq.Requests.FindAll(x => x.GetEndpoint() == endpoint));
            //data = data.Take(100);
            var minPts = PickMinPts(data);
            var r = new Random();
            var reqreqDist = new DistanceCache(new EuclideanRequestRequestDistance());
            var resresDist = new DistanceCache(new EuclideanResponseResponseDistance());
            //var reqreqDist = new EuclideanRequestRequestDistance();
            //var resresDist = new EuclideanResponseResponseDistance();
            var epsilonSample = data;//.OrderBy(x => r.NextDouble()).Take(200);
            analysisDetails["min points"] = minPts.ToString();
            
            
            var requestEpsilon = /*1.9;*/ PickEpsilon(minPts, epsilonSample, reqreqDist.GetDistance);
            var responseEpsilon = /*1.9;*/ PickEpsilon(minPts, epsilonSample, resresDist.GetDistance);
            analysisDetails["response epsilon"] = responseEpsilon.ToString();
            analysisDetails["request epsilon"] = requestEpsilon.ToString();
            // Console.WriteLine($"Response epsilon {responseEpsilon}");
            // Console.WriteLine($"Request epsilon {requestEpsilon}");
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
                var results  = PrepareSummary(analysisName, dataArr, reqreqResult, resresResult, analysisDetails);
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
            //Console.WriteLine("end 2nd deriv");
            // collect 'reasonably' large values from D2
            foreach (PointF p in D2) if (Math.Abs(p.Y / cutOff ) > 1) M.Add(p);
            // foreach (var d in distances)
            // {
            //     Console.Write($"{d},");
            // }
            // Console.WriteLine("");
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
            // Console.WriteLine($"Epsilon = {epsilon}");
            return epsilon;
        }

        private ClusteringSummary PrepareSummary(string analysisId, IEnumerable<RequestResponseModel> data, 
            DbscanResult<RequestResponseModel> reqreqResult, DbscanResult<RequestResponseModel> resresResult,
            Dictionary<string,string> analysisDetails)
        {
            var summary = new ClusteringSummary(analysisDetails);
            var reqClusters = summary.RequestClusters;
            var resClusters = summary.ResponseClusters;
            var results = summary.SummaryResults;
            foreach (var request in data)
            {
                var foundReqReq = reqreqResult.Clusters.Values.SelectMany(list => list.Where(x => x.Feature.ReqNum == request.ReqNum)).FirstOrDefault();
                var foundResRes = resresResult.Clusters.Values.SelectMany(list => list.Where(x => x.Feature.ReqNum == request.ReqNum)).FirstOrDefault();
                var requestClusterId = foundReqReq == null ? "Noise" : foundReqReq.ClusterId.ToString();
                var responseClusterId = foundResRes == null ? "Noise" : foundResRes.ClusterId.ToString();
                //   var foundReqReqNoise = reqreqResult.Noise.FirstOrDefault(x => x.Feature.ReqNum == request.ReqNum);
                //   var foundResResNoise = resresResult.Noise.FirstOrDefault(x => x.Feature.ReqNum == request.ReqNum);
                results.Add(new SummaryResult(analysisId,
                    request.GroupId,
                    request.ReqNum.ToString(),
                    requestClusterId,
                    responseClusterId,
                    request.OldGetSummary()
                    ));
                
                if (!reqClusters.ContainsKey(requestClusterId))
                {
                    reqClusters[requestClusterId] = new Cluster(requestClusterId, false);
                }
                reqClusters[requestClusterId].AddMember(request, requestClusterId, responseClusterId );
                
                if (!resClusters.ContainsKey(responseClusterId))
                {
                    resClusters[responseClusterId] = new Cluster(responseClusterId, true);
                }
                resClusters[responseClusterId].AddMember(request, requestClusterId, responseClusterId );
            }

            

            return summary;
        }
        
    
       
    }

    public class ClusteringSummary
    {
        public Dictionary<string,Cluster> RequestClusters = new Dictionary<string,Cluster>();
        public Dictionary<string,Cluster> ResponseClusters = new Dictionary<string,Cluster>();
        public List<SummaryResult> SummaryResults = new List<SummaryResult>();
        public Dictionary<string, string> AnalysisParameters = new Dictionary<string, string>();

        public ClusteringSummary(Dictionary<string, string> analysisDetails)
        {
            AnalysisParameters = analysisDetails;
        }
    }
    public class SummaryResult
    {
        public string GroupId;
        public string RequestClusterId;
        public string ResponseClusterId;
        public string OldSummary;
        public string AnalysisId;
        public string RequestId;
        public SummaryResult(string analysisId, string groupId, string requestId, string requestClusterId, string responseClusterId, string oldSummary)
        {
            AnalysisId = analysisId;
            GroupId = groupId;
            RequestId = requestId;
            RequestClusterId = requestClusterId;
            ResponseClusterId = responseClusterId;
            OldSummary = oldSummary;

        }
    }
}