using System;
using System.Collections.Generic;
using System.Linq;
using HistoracleTools.Algorithms;

namespace HistoracleTools.Models
{
    public class Cluster
    {
        public readonly string ClusterId;
        public bool IsResponseCluster { get; set; }

        public List<RequestResponseModel> requests = new List<RequestResponseModel>();
        private Dictionary<string, RequestResponseModel> byResponseClusterId = new Dictionary<string, RequestResponseModel>();
        //Count of values for each property Key = Property Value = Key = Value Value = Counter of that value
        private Dictionary<string, Dictionary<string, int>> props = new Dictionary<string, Dictionary<string, int>>();
        public Cluster(string clusterId, bool isResponseCluster)
        {
            this.ClusterId = clusterId;
            this.IsResponseCluster = isResponseCluster;
        }
        
        public SummaryResult GetSummary()
        {
            return null;
        }

        public void AddMember(RequestResponseModel request, string requestClusterId, string responseClusterId)
        {
            requests.Add(request);
            byResponseClusterId[responseClusterId] = request;
            AddPropertySummary(IsResponseCluster ? request.Response.Properties : request.Request.Properties);
        }

        private void AddPropertySummary(Dictionary<string, string> properties)
        {
            foreach (var property in properties)
            {
                if (!props.ContainsKey(property.Key))
                {
                    props[property.Key] = new Dictionary<string, int>();
                    props[property.Key][property.Value] = 1;
                }
                else
                {
                    if (props[property.Key].ContainsKey(property.Value))
                        props[property.Key][property.Value] += 1;
                    else
                    {
                        props[property.Key][property.Value] = 1;
                    }
                }
            }
        }
        /// <summary>
        /// Properties that didn't change across any member of the cluster
        /// </summary>
        public List<string> GetStableProperties(){
            var response = new List<string>();
            foreach (var VARIABLE in props)
            {
                if(VARIABLE.Value.Count == 1 && VARIABLE.Value.First().Value == requests.Count)
                    response.Add(VARIABLE.Key);
            }

            return response;
        }
        public IEnumerable<string> GetPropertiesOrderedByVariety() {
        
            var list = new List<KeyValuePair<string, Dictionary<string,int>>>(props.AsEnumerable());
            list.Sort((pair1, pair2) =>
            {

                var diff = pair2.Value.Count - pair1.Value.Count;
                if(diff == 0)
                    return pair1.Key.CompareTo(pair2.Key);
                return diff;
            });
            return list.Select(pair => pair.Key);
        }

        
    }
}