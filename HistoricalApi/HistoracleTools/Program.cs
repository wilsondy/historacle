﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using HistoracleTools.Algorithms;
using HistoracleTools.Models;
using HistoracleTools.RestlerTools;
using HistoracleTools.Storage;

namespace HistoracleTools
{
    [Verb("parse", HelpText = "Parse Restler file")]
    class ParseOptions { 
        [Option('i', "input", Required = true, HelpText = "Input File")]
        public string InputFile { get; set; }
        
        //[Option('o', "output", Required = true, HelpText = "Output File")]
        //public string OutputFile { get; set;}
        
        [Option('g', "groupId", Required = true, HelpText = "GroupId to assign in the model")]
        public string GroupId { get; set; }
        
        [Option('r', "repoRoot", Required = false, HelpText = "Root dir for storage of data", Default = "/Users/dwilson/school/historaclerepo")]
        public string RepoRoot { get; set; }
    }
    [Verb("analyze", HelpText = "Run Algorithm")]
    class AnalyzeOptions { //normal options here
        [Option('i', "analysisId", Required = true, HelpText = "Anslysis ID")]
        public string AnalysisId { get; set; }
        [Option('a', "groupIdA", Required = true, HelpText = "GroupId for A")]
        public string GroupIdA { get; set; }
        [Option('b', "groupIdB", Required = true, HelpText = "GroupId for B")]
        public string GroupIdB { get; set; }
        
        [Option('r', "repoRoot", Required = false, HelpText = "Root dir for storage of data", Default = "/Users/dwilson/school/historaclerepo")]
        public string RepoRoot { get; set; }
        
        [Option('e', "endpoint", Required = false, HelpText = "REST Endpoint to analyze", Default = "POST:/api/v3/store/order")]
        public string Endpoint { get; set; }
    }
   
    class Program
    {
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<ParseOptions,AnalyzeOptions>(args)
                .WithParsed(Run)
                .WithNotParsed(HandleParseError);
        }

        private static async void Run(object obj)
        {
            switch (obj)
            {
                case ParseOptions c:
                    ParseRestlerNetworkFile(c);
                    break;
                case AnalyzeOptions o:
                    await Analyze(o);
                    break;
            }
        }

        private static async Task Analyze(AnalyzeOptions o)
        {
            var dbscan = new DbScanAlgorithm();
            var repo = new RestlerModelRepository(o.RepoRoot);
            var groupA = repo.Load(o.GroupIdA);
            var groupB = repo.Load(o.GroupIdB);
            var endpoint = o.Endpoint;
            if (endpoint == "All")
            {
               var a= groupA.Sequences.SelectMany(seq => seq.Requests.Select(x => x.GetEndpoint())).ToHashSet();
               var b= groupB.Sequences.SelectMany(seq => seq.Requests.Select(x => x.GetEndpoint())).ToHashSet();
               var endpoints = a.Concat(b).ToHashSet();
               foreach (var e in endpoints)
               {
                   await AnalyzeEndpoint(o, dbscan, groupA, groupB, e);
               }
            }
            else
            {
                await AnalyzeEndpoint(o, dbscan, groupA, groupB, endpoint);
            }

            return;
        }

        private static async Task AnalyzeEndpoint(AnalyzeOptions o, DbScanAlgorithm dbscan, RestlerModel groupA,
            RestlerModel groupB, string endpoint)
        {
            var difReport = await dbscan.IsDifferent(o.AnalysisId,
                groupA,
                groupB,
                model => model.Sequences.SelectMany(seq => seq.Requests.FindAll(x => x.GetEndpoint() == endpoint)), false);
            if (difReport.IsDifferent)
                Console.WriteLine($"{endpoint}, different");
            else
                Console.WriteLine($"{endpoint}, NOT different");
        }

        private static void ParseRestlerNetworkFile(ParseOptions parseOptions)
        {
            var input = ParseRestlerRecord.Parse(parseOptions.InputFile, parseOptions.GroupId);
            var repo = new RestlerModelRepository(parseOptions.RepoRoot);
            repo.Store(input);
        }

        static void HandleParseError(IEnumerable<Error> errs)
        {
            Console.WriteLine("Options Parse Error");
            foreach (var e in errs)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}