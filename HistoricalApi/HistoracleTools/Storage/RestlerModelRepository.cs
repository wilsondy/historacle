using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using HistoracleTools.Models;

namespace HistoracleTools.Storage
{
    public class RestlerModelRepository
    {
        private string RepositoryRoot;

        public RestlerModelRepository(string repositoryRoot)
        {
            RepositoryRoot = repositoryRoot;
        }

        private string GetFilePath(string groupId)
        {
            return $"{RepositoryRoot}{Path.DirectorySeparatorChar}{groupId}.zip";
        }
        
        public RestlerModel Load(string groupId)
        {
            var filePath = GetFilePath(groupId);

            using (var inputFile = new FileStream(filePath, FileMode.Open))
            {
                using (GZipStream gs = new GZipStream(inputFile, CompressionMode.Decompress))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    return (RestlerModel)bf.Deserialize(gs);
                }
            }
        }

        public bool Store(RestlerModel model)
        {
            var filePath = GetFilePath(model.GroupId);
            if (File.Exists(filePath))
                throw new ArgumentException($"{filePath} already exists.  not storing here.");
            
            var formatter = new BinaryFormatter();
            using (var outputFile = new FileStream(filePath, FileMode.CreateNew))
            {
                using (var compressionStream = new GZipStream(
                    outputFile, CompressionMode.Compress))
                {
                    formatter.Serialize(compressionStream, model);
                    compressionStream.Flush();
                }
            }

            return true;
        }
    }
}