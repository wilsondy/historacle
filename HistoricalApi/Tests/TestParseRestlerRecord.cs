using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using HistoracleTools.Models;
using HistoracleTools.Parse;
using HistoracleTools.RestlerTools;
using HistoricalApi;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace API.Tests
{
    public class TestParseRestlerRecord
    {
        [Fact]
        public void TestPost()
        {
            var line =
                "2021-09-23 11:35:27.597: Sending: \'POST /api/v3/store/order HTTP/1.1\r\nAccept: application/json\r\nHost: localhost:8080\r\nContent-Type: application/json\r\nContent-Length: 127\r\nUser-Agent: restler/8.0.0\r\n\r\n{\n    \"id\":42,\n    \"petId\":198772,\n    \"quantity\":7,\n    \"shipDate\":\"2021 - 09 - 24\",\n    \"status\":\"placed\",\n    \"complete\":true}\r\n";
            var result = ParseRestlerRecord.ParseRequest(
                line, 1);
            Assert.Equal("/api/v3/store/order", result.Url);
            Assert.Equal("POST", result.HttpMethod);
            //Assert.Equal("localhost:8080",result.Properties["h.Host"]);
            // Assert.Equal("application/json",result.Properties["h.Content-Type"]);
            // Assert.Equal("127",result.Properties["h.Content-Length"]);
            Assert.Equal("placed", result.Properties["b.status"]);
            var resultBreak = ParseRestlerRecord.ParseSequenceBreak(line, 1);
            Assert.Equal(-1, resultBreak.Item1);
            Assert.Equal(-1, resultBreak.Item2);

        }

        [Fact]
        public void TestPUT()
        {
            var line =
                "2021-09-23 11:35:26.651: Sending: 'PUT /api/v3/user/username9d59c23efe HTTP/1.1\r\nAccept: application/json\r\nHost: localhost:8080\r\nContent-Type: application/json\r\nContent-Length: 198\r\nUser-Agent: restler/8.0.0\r\n\r\n{\n    \"id\":42,\n    \"username\":\"theUser\",\n    \"firstName\":\"fuzzstring\",\n    \"lastName\":\"fuzzstring\",\n    \"email\":\"fuzzstring\",\n    \"password\":\"12345\",\n    \"phone\":\"fuzzstring\",\n    \"userStatus\":42}\r\n'";
            var result = ParseRestlerRecord.ParseRequest(
                line, 1);
            Assert.Equal("/api/v3/user/username9d59c23efe", result.Url);
            Assert.Equal("PUT", result.HttpMethod);
            //Assert.Equal("localhost:8080",result.Properties["h.Host"]);
            // Assert.Equal("application/json",result.Properties["h.Content-Type"]);
            Assert.Equal("42", result.Properties["b.userStatus"]);
        }

        [Fact]
        public void TestSequenceBreak()
        {
            var result = ParseRestlerRecord.ParseSequenceBreak("Generation-4: Rendering Sequence-1", 1);
            Assert.Equal(4, result.Item1);
            Assert.Equal(1, result.Item2);
        }

        [Fact]
        public void TestOkResponse()
        {
            var line =
                "2021-09-23 11:35:28.081: Received: 'HTTP/1.1 200 OK\r\nDate: Thu, 23 Sep 2021 15:35:28 GMT\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, DELETE, PUT\r\nAccess-Control-Allow-Headers: Content-Type, api_key, Authorization\r\nAccess-Control-Expose-Headers: Content-Disposition\r\nContent-Type: application/json\r\nContent-Length: 134\r\nServer: Jetty(9.4.9.v20180320)\r\n\r\n{\"id\":0,\"category\":{\"id\":0},\"name\":\"fuzzstring\",\"photoUrls\":[\"fuzzstring\"],\"tags\":[{\"id\":0,\"name\":\"fuzzstring\"}],\"status\":\"available\"}'";
            var result = ParseRestlerRecord.ParseResponse(line, 1);
            Assert.Equal(200, result.HttpStatus);

            //Headers are not currently being included.  Perhaps an option in the future.
            //Assert.Equal("*", result.Properties["h.Access-Control-Allow-Origin"]);
            Assert.Equal("<id>", result.Properties["b.id"]);
            Assert.Equal("available", result.Properties["b.status"]);

        }

        [Fact]
        public void Test500Response()
        {
            var line =
                "2021-09-23 11:35:28.120: Received: 'HTTP/1.1 500 Internal Server Error\r\nDate: Thu, 23 Sep 2021 15:35:28 GMT\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, DELETE, PUT\r\nAccess-Control-Allow-Headers: Content-Type, api_key, Authorization\r\nAccess-Control-Expose-Headers: Content-Disposition\r\nContent-Type: application/json\r\nContent-Length: 110\r\nServer: Jetty(9.4.9.v20180320)\r\n\r\n{\"code\":500,\"message\":\"There was an error processing your request. It has been logged (ID: e142af503dbcee4b)\"}'";
            var result = ParseRestlerRecord.ParseResponse(line, 1);
            Assert.Equal(500, result.HttpStatus);
            //Assert.Equal("Content-Disposition", result.Properties["h.Access-Control-Expose-Headers"]);
            Assert.Equal("500", result.Properties["b.code"]);
            Assert.Equal("There was an error processing your request. It has been logged (ID: e142af503dbcee4b)",
                result.Properties["b.message"]);

        }

        [Fact]
        public void Test400Response()
        {
            var line =
                "2021-09-23 11:35:28.332: Received: 'HTTP/1.1 400 Bad Request\r\nDate: Thu, 23 Sep 2021 15:35:28 GMT\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, DELETE, PUT\r\nAccess-Control-Allow-Headers: Content-Type, api_key, Authorization\r\nAccess-Control-Expose-Headers: Content-Disposition\r\nContent-Type: application/json\r\nContent-Length: 94\r\nServer: Jetty(9.4.9.v20180320)\r\n\r\n{\"code\":400,\"message\":\"Input error: unable to convert input to io.swagger.petstore.model.Pet\"}'";
            var result = ParseRestlerRecord.ParseResponse(line, 1);
            Assert.Equal(400, result.HttpStatus);
            //Assert.Equal("94", result.Properties["h.Content-Length"]);
            Assert.Equal("400", result.Properties["b.code"]);
            Assert.Equal("Input error: unable to convert input to io.swagger.petstore.model.Pet",
                result.Properties["b.message"]);

        }
    }

    class EndpointStat
    {
        public List<RequestResponseModel> Requests = new List<RequestResponseModel>();
        public List<int> RequestRequestDistances = new List<int>();
        public List<int> RequestResponseDistances = new List<int>();
        public List<int> ResponseResponseDistances = new List<int>();
        // Request header/body keys and all values seen for that key
        private Dictionary<string, List<string>> uniquevalues = new Dictionary<string, List<string>>(); 
        public void addRequest(RequestResponseModel newRequest)
        {
            if(Requests.Contains(newRequest))
                return;
            Requests.Add(newRequest);
        }
    }
}
