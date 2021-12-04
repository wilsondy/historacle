using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using HistoracleTools.Parse;

namespace HistoracleTools.Models
{
    public enum Comparison{
        RequestRequest,
        RequestResponse,
        ResponseResponse
    }
    
    
    [Serializable]  
    public class RequestResponseModel
    {
        public string GroupId;
        public RequestModel Request;
        public ResponseModel Response;
        private int restlerGeneration;
        private int restlerSequenceNumber;
        private string endpoint;
        public int ReqNum { get; }

        public RequestResponseModel(string groupId, int reqNum, RequestModel request, ResponseModel response, int restlerGeneration, int restlerSequenceNumber)
        {
            GroupId = groupId;
            this.ReqNum = reqNum;
            this.Request = request;
            this.Response = response;
            this.restlerGeneration = restlerGeneration;
            this.restlerSequenceNumber = restlerSequenceNumber;
            var urlInfo = WorkaroundPathParameters(request.Url);
            endpoint = $"{request.HttpMethod}:{urlInfo[0]}";
            if (urlInfo.Length == 2)
            {
                request.Properties.Add("p.urlid", urlInfo[1]);
            }else if (urlInfo.Length > 2)
            {
                Console.WriteLine("Unexpected urlInfo Length");
            }   
        }

        public Dictionary<string,int> RequestResponseLevenshteinDistance()
        {
            return GetDistancesForProps(Request.Properties, Response.Properties);
        }
        public Dictionary<string,int> RequestRequestLevenshteinDistance(RequestResponseModel other)
        {
            return GetDistancesForProps(Request.Properties,  other.Request.Properties);
        }
        public Dictionary<string,int> ResponseResponseLevenshteinDistance(RequestResponseModel other)
        {
            return GetDistancesForResponseProps(Request.Properties, Response.Properties,  other.Request.Properties, other.Response.Properties);
        }
       

        public double GetEuclideanDistance(Comparison c, RequestResponseModel other)
        {
            Dictionary<string, int> distances = new Dictionary<string, int>();
            switch (c)
            {
                case Comparison.RequestRequest:
                    distances = RequestRequestLevenshteinDistance(other);
                    break;
                case Comparison.ResponseResponse:
                    distances = ResponseResponseLevenshteinDistance(other);
                    break;
            }

            double sum = 0;
            foreach (var d in distances)
            {
                sum += d.Value*d.Value;
            }

            if (c == Comparison.ResponseResponse)
            {
                //ad-hoc value to widen responses by Response Code more than the properties underlying.
                var d = 1000*(Response.HttpStatus - other.Response.HttpStatus);
                sum += d*d; 
                //TODO study this ad-hoc value in more depth
            }
            return Math.Sqrt(sum);
        }

        public Dictionary<string,int> GetDistancesForProps(Dictionary<string, string> props1, Dictionary<string, string> props2)
        {
            var result = new Dictionary<string, int>();
            foreach (var keyValuePair in props1)
            {
                props2.TryGetValue(keyValuePair.Key, out var value);
                result.Add(keyValuePair.Key, Levenshtein.ComputeDistance(keyValuePair.Value, value));
            }
            foreach (var keyValuePair in props2)
            {
                if(result.ContainsKey(keyValuePair.Key))
                    continue;
                
                result.Add(keyValuePair.Key, Levenshtein.ComputeDistance(keyValuePair.Value, ""));
            }

            return result;
        }

        /**
         * Under the typical assumption in REST APIs, especially in CRUD - request fields are often reflected back in responses
         * Here, we do not specify a very small distance between two responses if:
         * A field value in a matched request/response pair is faithfully reproduced when comparing the two pairs of responses
         * thus, while the field value is likely different between the pairs of req/responses, the fact that the underlying system
         * acted in the same way we consider the distance to be very small.
         * if the field value is reflected and the is the same value between the two, then the distance is zero.
         */
        public Dictionary<string,int> GetDistancesForResponseProps(Dictionary<string, string> requestProps1, Dictionary<string, string> responseProps1, Dictionary<string, string> requestProps2, Dictionary<string, string> responseProps2)
        {
            var result = new Dictionary<string, int>();
            foreach (var responseProp1KeyValPair in responseProps1)
            {
                responseProps2.TryGetValue(responseProp1KeyValPair.Key, out var value);
                var response1Reflected = wasReflected(requestProps1, responseProps1, responseProp1KeyValPair.Key);
                var response2Reflected = wasReflected(requestProps2, responseProps2, responseProp1KeyValPair.Key);
                if (response1Reflected && response2Reflected)
                {
                    result.Add(responseProp1KeyValPair.Key, 0);
                }
                else if (response1Reflected && !response2Reflected || (!response1Reflected && response2Reflected))
                {
                    //ad-hoc decision to double the underlying distance since the reflection behavior changed
                    result.Add(responseProp1KeyValPair.Key,
                        2 * Levenshtein.ComputeDistance(responseProp1KeyValPair.Value, value));
                }
                else
                {
                    result.Add(responseProp1KeyValPair.Key,
                        Levenshtein.ComputeDistance(responseProp1KeyValPair.Value, value));
                }

                //     string request2Value = null;
                //     
                //     var request1Key = requestProps1.FirstOrDefault(pair => pair.Value == responseProp1KeyValPair.Value).Key;
                //     if (request1Key != null)
                //     {
                //         requestProps2.TryGetValue(request1Key, out request2Value);
                //     }
                //
                //     if (request1Key != null && request2Value != null)
                //     {
                //         //Console.WriteLine($"Setting Distance to Zero for {request1Key} and {request2Value} {keyValuePair.Value} {value}");
                //         result.Add(responseProp1KeyValPair.Key, 0); //TODO try zero
                //     }
                //     else
                //     {   if(responseProp1KeyValPair.Key == "b.quantity")
                //             Console.WriteLine("stop");
                //         result.Add(responseProp1KeyValPair.Key, Levenshtein.ComputeDistance(responseProp1KeyValPair.Value, value));
                //     }
                 }
                foreach (var keyValuePair in responseProps2)
                {
                    if (result.ContainsKey(keyValuePair.Key))
                        continue;

                    result.Add(keyValuePair.Key, Levenshtein.ComputeDistance(keyValuePair.Value, ""));
                }
            

            return result;
        }

        private bool wasReflected(Dictionary<string, string> requestProps, Dictionary<string, string> responseProps, string key)
        {
            requestProps.TryGetValue(key, out var requestValue);
            responseProps.TryGetValue(key, out var responseValue);
           
            return requestValue == responseValue ;
        }

        //TODO this is terrible and can't match GET /users/dylan vs /users/joe (Get by Id)
        //I should be able to reason about this using the OpenAPI spec file(s)
        public string GetEndpoint()
        {
            //var url =request.Url.Split('?')[0];
            
            return endpoint;
        }
        
        //TODO this is terrible and can't match GET /users/dylan vs /users/joe (Get by Id)
        //I should be able to reason about this using the OpenAPI spec file(s)
        private string[] WorkaroundPathParameters(string url)
        {
            var knownurls = new string[] {"/api/v3/user/", "/api/v3/store/order/"};
            foreach (string knownurl in knownurls)
            {
                if (url.StartsWith(knownurl))
                    return new string[]
                    {
                        knownurl, url.Remove(0, knownurl.Length)
                    };
            }

            return new string[] {url};
        }

        public string GetSummary()
        {
            return$"{GetEndpoint()},{Request.GetSummary()},{Response.getSummary()}";
        }
    }
}