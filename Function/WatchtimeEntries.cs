using System.Net;
using Azure.Data.Tables;
using Function.model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using WatchtimeFunctions.model;

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

    [OpenApiOperation(operationId: "putUser", tags: ["books"], Summary = "Set a user's watchtime",
        Description = "Create or update the user's total watchtime in the backend.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UserModel))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
        bodyType: typeof(SuccessModel))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json",
        bodyType: typeof(ErrorModel))]
    [Function("HTTPPutUser")]
    public async Task<HttpResponseData> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "ranking/user")]
        HttpRequestData request)
    {
        _logger.LogInformation(
            $"[{request.FunctionContext.InvocationId}] Processing request for update user endpoint.");

        // deserialize request body into BookModel object
        var updateUserReq = await request.ReadFromJsonAsync<UserModel>();

        // if request body cannot be deserialized or is null, return an HTTP 400
        if (updateUserReq == null)
        {
            var errorResponse = request.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new ErrorModel(
                Error: "InvalidRequestBody",
                ErrorMessage: "The request body is invalid."
            ));
            return errorResponse;
        }

        var user = new UserTableModel
            { RowKey = updateUserReq.UserId, TotalWatchtime = updateUserReq.TotalWatchtime };
        var updateResult = await _table.UpsertEntityAsync(user, TableUpdateMode.Replace);

        // return error if table transaction failed
        if (updateResult.IsError)
        {
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync<ErrorModel>(new
            (
                Error: "TableTransactionError",
                ErrorMessage: "There was a problem executing the table transaction."
            ));
            return errorResponse;
        }

        _logger.LogInformation("Update status: " + updateResult.Status);
        // return successful table transaction response
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new SuccessModel("User updated successfully."));
        return response;
    }

    [OpenApiOperation(operationId: "getRanking", tags: ["ranking"],
        Summary = "Get ranking position in leaderboard", Description = "Get the leaderboard position by the users id.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Summary = "UserId of the requested user used for the ranking lookup")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(long))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json",
        bodyType: typeof(long))]
    [Function("HTTPGetRanking")]
    public async Task<HttpResponseData> GetRanking(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ranking/position")]
        HttpRequestData request,
        string userId)
    {
        _logger.LogInformation(
            $"[{request.FunctionContext.InvocationId}] Processing request for list watchtime entries endpoint.");

        // var queryResult = _table.GetEntityIfExists<BookTableModel>(partitionKey: string.Empty, rowKey: isbn);

        // check if user exists

        // get position of user in list

        // return successfull response
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(0);
        return response;
    }
}