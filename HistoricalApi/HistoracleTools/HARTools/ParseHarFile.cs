using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using HarSharp;
using HistoracleTools.Models;
using HistoracleTools.Parse;
using Newtonsoft.Json;

namespace HistoracleTools.HARTools
{
    public class ParseHarFile
    {
        public static RestlerModel Parse(string filePath, string groupId)
        {
            //we don't really have sequences here, so we just make one.
            int count = 1;
            RequestSequence sequence = new RequestSequence(1, 1, 1);
            var har = HarConvert.DeserializeFromFile(filePath);
            foreach (var entry in har.Log.Entries)
            {
                var urlsplit = entry.Request.Url.ToString().Split('?');
                if (urlsplit.Length ==1 && entry.Request.Method is "DELETE" or "GET" or "PATCH")
                {
                    var lastBit= urlsplit[0].LastIndexOf('/');
                    //TODO THIS IS VERY FRAGILE AND WORKS JUST FOR TASSO UUIDS
                    if(urlsplit[0].Substring(lastBit).Contains("-"))
                    {
                        urlsplit[0] = urlsplit[0].Replace(urlsplit[0].Substring(lastBit), "");
                        //TODO grab the id and drop into a property
                    }
                }
                var request = new RequestModel(entry.Request.Method, urlsplit[0]);

                if (entry.Request.PostData != null)
                {
                    if (entry.Request.PostData.MimeType.StartsWith("multipart"))
                    {
                        //TODO
                    }
                    else
                        ParseBody(entry.Request.PostData.Text, request.Properties);
                }

                foreach (var queryStringParameter in entry.Request.QueryString)
                {
                    request.Properties.Add($@"q.{queryStringParameter.Name}", queryStringParameter.Value);
                }
                
                var response = new ResponseModel(entry.Response.Status);
                var rawjson = entry.Response.Content.Text;
                if (entry.Response.Content.Encoding == "base64")
                {
                    rawjson = Encoding.UTF8.GetString(Convert.FromBase64String(entry.Response.Content.Text));
                }
                if(rawjson.Length > 0)
                    ParseBody(rawjson, response.Properties);
                sequence.AddRequestResponse(new RequestResponseModel(groupId, count, request, response,1,1));
                count++;
            }

            var model = new RestlerModel(groupId);
            model.AddRequestSequence(sequence);
            return model;
        }

        private static void ParseBody(string rawjson, Dictionary<string, string> properties)
        {
            try
            {
                var flattenedBody = JsonHelper.DeserializeAndFlatten(rawjson,false);
                foreach (var key in flattenedBody.Keys)
                {
                    properties.Add($"b.{key}", flattenedBody[key] == null ? "null" : flattenedBody[key].ToString());
                }
            }
            catch (JsonException e)
            {
                Console.Error.WriteLine("Error parsing json body for line");
                Console.Error.WriteLine(rawjson);
                properties.Add("b.PARSE_ERROR", "true");
                properties.Add("b.RAW_BODY", rawjson);
            }
        }
    }
}