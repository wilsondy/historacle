using System;
using HistoracleTools.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;


namespace HistoracleTools.Algorithms


{
    public interface IDistanceCalculator
    {
        double GetDistance(RequestResponseModel feature1, RequestResponseModel feature2);
    }

    public class EuclideanRequestRequestDistance : IDistanceCalculator
    {
        public double GetDistance(RequestResponseModel feature1, RequestResponseModel feature2)
        {
            return feature1.GetEuclideanDistance(Comparison.RequestRequest, feature2);
        }
    }
    public class EuclideanResponseResponseDistance : IDistanceCalculator
    {
        public double GetDistance(RequestResponseModel feature1, RequestResponseModel feature2)
        {
            return feature1.GetEuclideanDistance(Comparison.ResponseResponse, feature2);
        }
    }

    public class DistanceCache: IDistanceCalculator
    {
        private readonly IDistanceCalculator source;
        private readonly IMemoryCache _memoryCache;
        public int hits = 0;
        public int misses = 0;
        public DistanceCache(IDistanceCalculator source)
        {
            this.source = source;
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }
        
        private string GetCacheKey(RequestResponseModel feature1, RequestResponseModel feature2)
        {
            var p1 = feature1;
            var p2 = feature2;
            //the distance relationship is symmetric, but in case the caller doesn't know that, we order by rec nums to find the distance either way 
            //except when ReqNum are same and then we might have it both ways in the cache
            if (feature1.ReqNum > feature2.ReqNum)
            {
                p1 = feature2;
                p2 = feature1;
            }

            return $"{p1.GroupId}{p1.GetEndpoint()}{p1.ReqNum}:{p2.GroupId}{p2.GetEndpoint()}{p2.ReqNum}";

        }
        public double GetDistance(RequestResponseModel feature1, RequestResponseModel feature2)
        {
            var key = GetCacheKey(feature1, feature2);
            _memoryCache.TryGetValue(key, out var value);
            if (value != null)
            {
                hits++;
                return (double) value;
            }

            misses++;
            var result = this.source.GetDistance(feature1, feature2);
            _memoryCache.Set(key,result);
            return result;

        }
    }
}