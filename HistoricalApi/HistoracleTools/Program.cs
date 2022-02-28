using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using HistoracleTools.Algorithms;
using HistoracleTools.HARTools;
using HistoracleTools.Models;
using HistoracleTools.Reporting;
using HistoracleTools.RestlerTools;
using HistoracleTools.Storage;
using Microsoft.Extensions.Logging;

namespace HistoracleTools
{
    [Verb("parse", HelpText = "Parse Restler file")]
    class ParseOptions { 
        [Option('i', "input", Required = true, HelpText = "Input File")]
        public string InputFile { get; set; }
        
        //[Option('o', "output", Required = true, HelpText = "Output File")]
        //public string OutputFile { get; set;}
        [Option('t', "type", Required = false, HelpText = "File Type")]
        public string FileType { get; set; }

        [Option('g', "groupId", Required = true, HelpText = "GroupId to assign in the model")]
        public string GroupId { get; set; }
        
        [Option('r', "repoRoot", Required = false, HelpText = "Root dir for storage of data", Default = "/Users/dwilson/school/historaclerepo")]
        public string RepoRoot { get; set; }
    }
    [Verb("analyze", HelpText = "Run Algorithm")]
    class AnalyzeOptions { //normal options here
        [Option('o', "reportDirectory", Required = true, HelpText = "Directory to write reports")]
        public string ReportDir { get; set; }
        
        [Option('i', "analysisId", Required = true, HelpText = "Analysis ID")]
        public string AnalysisId { get; set; }
        [Option('a', "groupIdA", Required = true, HelpText = "GroupId for A")]
        public string GroupIdA { get; set; }
        [Option('b', "groupIdB", Required = true, HelpText = "GroupId for B")]
        public string GroupIdB { get; set; }
        
        [Option('r', "repoRoot", Required = false, HelpText = "Root dir for storage of data", Default = "/Users/dwilson/school/historaclerepo")]
        public string RepoRoot { get; set; }
        
        [Option('e', "endpoint", Required = false, HelpText = "REST Endpoint to analyze", Default = "POST:/api/v3/store/order")]
        public string Endpoint { get; set; }
        
        [Option('m', "minpoints", Required = false, HelpText = "Min Points for DBSCAN", Default = Double.NaN)]
        public double MinPoints { get; set; }
        
        [Option('s', "epsilon", Required = false, HelpText = "Epsilon for DBSCAN. If less than 1, it scales the auto-calculated value accordingly", Default = Double.NaN)]
        public double Epsilon { get; set; }
        
        [Option('c', "alpha", Required = false, HelpText = "Alpha used to determine significance", Default = 0.05)]
        public double Alpha { get; set; }
    }
   
    class Program
    {
        private static ILogger logger;
        static void Main(string[] args)
        {
            
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("HistoracleTools.Program", LogLevel.Warning)
                    .AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = false;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    });
            });
            logger = loggerFactory.CreateLogger<Program>();
            
            
            Parser.Default.ParseArguments<ParseOptions,AnalyzeOptions>(args)
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
                    await Analyze(o, logger);
                    break;
            }
        }

        private static async Task Analyze(AnalyzeOptions o, ILogger logger)
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
                   await AnalyzeEndpoint(o, dbscan, groupA, groupB, e, logger);
               }
            }
            else
            {
                await AnalyzeEndpoint(o, dbscan, groupA, groupB, endpoint, logger);
            }

            return;
        }

        private static async Task AnalyzeEndpoint(AnalyzeOptions o, DbScanAlgorithm dbscan, RestlerModel groupA,
            RestlerModel groupB, string endpoint, ILogger logger)
        {
            var difReport = await dbscan.IsDifferent(o.AnalysisId,
                groupA,
                groupB,
                model => model.Sequences.SelectMany(seq => seq.Requests.FindAll(x => x.GetEndpoint() == endpoint)),
               o.Alpha, o.MinPoints, o.Epsilon, logger);
            if (difReport == null)
                Console.WriteLine($"{endpoint}, NO RESULT- DATA INSUFFICIENT");
            else
            {
                if (difReport.IsDifferent == DifferenceType.ResponseDifferent)
                    Console.WriteLine($"{endpoint}, different");
                else if (difReport.IsDifferent == DifferenceType.ResponseNotDifferent)
                    Console.WriteLine($"{endpoint}, NOT different");
                else if (difReport.IsDifferent == DifferenceType.RequestsNotCompatible)
                    Console.WriteLine($"{endpoint}, declined analysis - requests not similar");
                var report = new ReportClustering();
                report.WriteCSV(o.ReportDir, difReport.Summary);
            }
        }


        

        private static void ParseRestlerNetworkFile(ParseOptions parseOptions)
        {
            RestlerModel input = null;
            if (parseOptions.FileType == "har")
            {
                input = ParseHarFile.Parse(parseOptions.InputFile, parseOptions.GroupId);

            }
            else
            {
                input = ParseRestlerRecord.Parse(parseOptions.InputFile, parseOptions.GroupId);
            }
            
            var repo = new RestlerModelRepository(parseOptions.RepoRoot);
            if(input != null)
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