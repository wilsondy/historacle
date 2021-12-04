using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HistoracleTools.Models;
using HistoracleTools.Parse;
using Newtonsoft.Json;

namespace HistoracleTools.RestlerTools
{
    //2021-09-22 11:15:18.738: Received: 'HTTP/1.1 500 Internal Server Error
    //errorRegex := regexp.MustCompile(`(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Received: 'HTTP/1.1 500 Internal Server Error`)
    //2021-09-23 11:35:27.255: Received: 'HTTP/1.1 400 Bad Request\r\
    //badRequestRegex := regexp.MustCompile(`(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Received: 'HTTP/1.1 400 Bad Request`)
    public class ParseRestlerRecord
    {
        public static RestlerModel Parse(string filePath, string groupId)
        {
            var restler = new RestlerModel(groupId);
            // Memory<byte> userBuffer = new Memory<byte>(_userBuffers[userBufferSize]);
            // long bytesRead = 0;
            // using (FileStream fileStream = new FileStream(_sourceFilePaths[fileSize], FileMode.Open, FileAccess.Read, FileShare.Read, streamBufferSize, options))
            // {
            //     while (bytesRead < fileSize)
            //     {
            //         bytesRead += await fileStream.ReadAsync(userBuffer, cancellationToken);
            //     }
            // }
            //
            // return bytesRead;
            using (StreamReader sr = File.OpenText(filePath))
            {
                string s;
                RequestModel request = null;
                ResponseModel response = null;
                RequestResponseModel roundTrip = null;
                RequestSequence sequence = null;
                int lineNum = 0;
                int reqNum = 0;
                while ((s = sr.ReadLine()) != null)
                {
                    lineNum++;
                    try
                    {
                        var seqBreak = ParseSequenceBreak(s, lineNum);
                        
                        if (seqBreak.Item1 > 0 /*&& (sequence?.Generation != seqBreak.Item1 ||
                            sequence?.Sequence != seqBreak.Item2)*/)
                        {
                            sequence = new RequestSequence(seqBreak.Item1, seqBreak.Item2, lineNum);
                            restler.AddRequestSequence(sequence);
                            continue;
                        }

                        var req = ParseRequest(s, lineNum);
                        if (req != null)
                        {
                            request = req;
                            continue;
                        }

                        var res = ParseResponse(s, lineNum);
                        if (res != null)
                        {
                            response = res;
                            roundTrip = new RequestResponseModel(groupId,reqNum++, request, response, sequence.Generation,
                                sequence.Sequence);
                            sequence.AddRequestResponse(roundTrip);
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Error occured while parsing line " + lineNum);
                        Console.Error.WriteLine(s);
                        throw e;
                    }

                }
            }

            return restler;
        }

        public static ResponseModel ParseResponse(string line, int lineNumber)
        {
            Regex r = new Regex(@"(?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Received: 'HTTP/1.1 (?<httpcode>[0-9]+)");
            Match m = r.Match(line);
            if (m.Success)
            {
                var responseModel = new ResponseModel(int.Parse(m.Groups["httpcode"].Value));
                ParseBody(line, responseModel.Properties);
                //ParseKeyValuePairs(line, responseModel.Properties);
                return responseModel;
            }

            return null;
        }

        public static (int, int) ParseSequenceBreak(string line, int lineNumber)
            {
                Regex r = new Regex(@"Generation-(?<generation>[0-9]+): Rendering Sequence-(?<sequence>[0-9]+)",
                    RegexOptions.None, TimeSpan.FromMilliseconds(150));
                var match = r.Match(line);
                if (match.Success)
                {
                    return (Int32.Parse(match.Groups["generation"].Value), Int32.Parse(match.Groups["sequence"].Value));
                }
                return (-1,-1);
            }
            public static RequestModel ParseRequest(string line, int lineNumber)
            {
            
                Regex r = new Regex(@"(?<date>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Sending: '(?<method>POST|GET|PATCH|DELETE|PUT) (?<url>[^\s]+)",
                    RegexOptions.None, TimeSpan.FromMilliseconds(150));
                Match m = r.Match(line);
                if (m.Success)
                {
                    var urlsplit = m.Groups["url"].Value.Split('?');
                    string url = urlsplit[0];
                    var requestModel = new RequestModel(m.Groups["method"].Value, url);
                    for (int i = 1; i < urlsplit.Length; i++)
                    {
                        var ampsplit = urlsplit[i].Split('&');
                        foreach (var varsplit in ampsplit)
                        {
                            var vars =varsplit.Split('=');
                            if (vars.Length != 2)
                                continue;
                            requestModel.Properties.Add($@"q.{vars[0]}", vars[1]);
                            
                        }
                        
                    }
                      //Unit tests vs actual file reading are at odds.
                      //Not sure if we really want these as they are generally very stable and not of much use.
                      //Stuff like Accept Headers Content-Length 
                      //TODO solve the bug between unit test and actual runs
                    //ParseKeyValuePairs(line, requestModel.Properties);

                    ParseBody(line, requestModel.Properties);
                    return requestModel;
                }
                return null;
            }

            private static void ParseBody(string line, Dictionary<string,string> properties)
            {
                var balancedreg = @"\{(?>\{(?<c>)|[^{}]+|\}(?<-c>))*(?(c)(?!))\}";
                Regex body = new Regex(balancedreg, RegexOptions.Multiline, TimeSpan.FromMilliseconds(150));
             //   Regex body = new Regex(@"(?<body>{[^}]+})", RegexOptions.Multiline, TimeSpan.FromMilliseconds(150));
                var bodyMatch = body.Match(line);

                if (bodyMatch.Success)
                {
                    var rawjson = bodyMatch.Value;
                    try
                    {
                        var flattenedBody = JsonHelper.DeserializeAndFlatten(rawjson);
                        foreach (var key in flattenedBody.Keys)
                        {
                            properties.Add($"b.{key}", flattenedBody[key] == null  ? "null" : flattenedBody[key].ToString());
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

            private static void ParseKeyValuePairs(string line, Dictionary<string,string> properties)
            {
                Regex keyValues = new Regex(@"(\\n(?<key>[^\s]+): (?<value>[^\s]+)\\r)",
                    RegexOptions.None, TimeSpan.FromMilliseconds(150));
                var matches = keyValues.Matches(line);
                for (int i = 0; i < matches.Count; i++)
                {
                    properties.Add($"h.{matches[i].Groups["key"].Value}", matches[i].Groups["value"].Value);
                }
            }
    }
}

