using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace Function;

public class WatchtimeEntries
{
    private readonly ILogger _logger;
    private readonly TableClient _table;
    
    public WatchtimeEntries(ILoggerFactory loggerFactory, TableServiceClient tableService)
    {
        // name of the azure storage account table where to create, store, lookup and delete books
        string tableName = "timeRecords";

        _logger = loggerFactory.CreateLogger<WatchtimeEntries>();
        // create TableClient for table with name tableName and create table if not exists already
        tableService.CreateTableIfNotExists(tableName);
        _table = tableService.GetTableClient(tableName);
    }
    
    [OpenApiOperation(operationId: "getRanking", tags: new[] {"ranking"}, Summary = "Get ranking position in leaderboard", Description = "Get the leaderboard position by the users id.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Summary = "UserId of the requested user used for the ranking lookup")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(long))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json", bodyType: typeof(long))]
    [Function("HTTPGetRanking")]
    public async Task<HttpResponseData> GetRanking([HttpTrigger(AuthorizationLevel.Function, "get", Route = "ranking/position")] HttpRequestData request, string userId)
    {
        _logger.LogInformation($"[{request.FunctionContext.InvocationId}] Processing request for list watchtime entries endpoint.");
        
        // get all users and sort them by their total watchtime
        // var queryResult = _table.GetEntityIfExists<BookTableModel>(partitionKey: string.Empty, rowKey: isbn);
        
        // check if user exists
        
        // get position of user in list

        // return successfull response
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(0);
        return response;
    }
    
}