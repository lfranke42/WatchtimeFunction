using System.Net;
using Azure.Data.Tables;
using Function.model;
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
        const string tableName = "timeRecords";

        _logger = loggerFactory.CreateLogger<WatchtimeEntries>();
        // create TableClient for table with name tableName and create table if not exists already
        tableService.CreateTableIfNotExists(tableName);
        _table = tableService.GetTableClient(tableName);
    }

    [OpenApiOperation(operationId: "getRanking", tags: ["ranking"],
        Summary = "Get ranking position in leaderboard", Description = "Get the leaderboard position by the users id.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Summary = "UserId of the requested user used for the ranking lookup")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
        bodyType: typeof(RankingModel))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json",
        bodyType: typeof(ErrorModel))]
    [Function("HTTPGetRanking")]
    public async Task<HttpResponseData> GetRanking(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ranking/position/{userId}")]
        HttpRequestData request,
        string userId)
    {
        _logger.LogInformation(
            $"[{request.FunctionContext.InvocationId}] Processing request for list watchtime entries endpoint.");

        // Get all users from the table
        var query = from entity in _table.Query<UserTableModel>() select entity;
        var userEntries = query.ToList();
        _logger.LogInformation("Found " + userEntries.Count + " entries.");

        // Order the results and find user
        userEntries.Sort((x, y) => x.TotalWatchtime.CompareTo(y.TotalWatchtime));
        userEntries.Reverse();
        var userRank = userEntries.FindIndex(x => x.RowKey == userId);
        _logger.LogInformation("User rank: " + userRank + " for user " + userId + ".");

        // User not found
        if (userRank == -1)
        {
            _logger.LogError($"User with id {userId} not found.");
            var errorResponse = request.CreateResponse(HttpStatusCode.NotFound);
            await errorResponse.WriteAsJsonAsync(new ErrorModel(
                Error: "UserNotFound",
                ErrorMessage: "The user with the given id was not found."
            ), HttpStatusCode.NotFound);
            return errorResponse;
        }

        // Determine closest neighbors
        List<AnonRankingModel> closestNeighbors;
        try
        {
            closestNeighbors = GetClosestNeighbors(userEntries, userRank);
        }
        catch (Exception)
        {
            return request.CreateResponse(HttpStatusCode.InternalServerError);
        }

        var rankingModel = new RankingModel(userRank + 1, userEntries[userRank].TotalWatchtime, closestNeighbors);
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(rankingModel);
        return response;
    }

    private static List<AnonRankingModel> GetClosestNeighbors(IReadOnlyList<UserTableModel> userEntries, int userRank)
    {
        var closestNeighbors = new List<AnonRankingModel>();

        // If there are less than 5 users, return all users except the user itself
        if (userEntries.Count < 5)
        {
            for (var i = 0; i < userEntries.Count; i++)
            {
                if (i == userRank) continue;
                closestNeighbors.Add(new AnonRankingModel(i + 1, userEntries[i].TotalWatchtime));
            }

            return closestNeighbors;
        }

        // If the user is far enough apart from the top and bottom of the list, return the 2 users above and below
        if (userRank + 2 < userEntries.Count && userRank - 2 >= 0)
        {
            for (var i = userRank - 2; i <= userRank + 2; i++)
            {
                if (i == userRank) continue;
                closestNeighbors.Add(new AnonRankingModel(i + 1, userEntries[i].TotalWatchtime));
            }

            return closestNeighbors;
        }

        // If the user is close to the bottom of the list, return the 4 users below
        if (userRank + 2 >= userEntries.Count)
        {
            for (var i = userEntries.Count - 5; i < userEntries.Count; i++)
            {
                if (i == userRank) continue;
                closestNeighbors.Add(new AnonRankingModel(i + 1, userEntries[i].TotalWatchtime));
            }

            return closestNeighbors;
        }

        // If the user is close to the top of the list, return the 4 users above
        if (userRank - 2 >= 0) throw new Exception("Invalid Ranking");

        for (var i = 0; i < 5; i++)
        {
            if (i == userRank) continue;
            closestNeighbors.Add(new AnonRankingModel(i + 1, userEntries[i].TotalWatchtime));
        }

        return closestNeighbors;
    }

    [OpenApiOperation(operationId: "putUser", tags: ["ranking"], Summary = "Set a user's watchtime",
        Description = "Create or update the user's total watchtime in the backend.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UserModel))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Empty Response if successful")]
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
            ), HttpStatusCode.BadRequest);
            return errorResponse;
        }

        var user = new UserTableModel
            { RowKey = updateUserReq.UserId, TotalWatchtime = updateUserReq.TotalWatchtime };
        var updateResult = await _table.UpsertEntityAsync(user, TableUpdateMode.Replace);

        // return error if table transaction failed
        if (updateResult.IsError)
        {
            var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new ErrorModel(
                Error: "TableTransactionError",
                ErrorMessage: "There was a problem executing the table transaction."
            ), HttpStatusCode.InternalServerError);
            return errorResponse;
        }

        _logger.LogInformation("Update status: " + updateResult.Status);
        // return successful table transaction response
        return request.CreateResponse(HttpStatusCode.NoContent);
    }

    [OpenApiOperation(operationId: "deleteUser", tags: ["ranking"], Summary = "Delete a user",
        Description = "Delete a user and their respective watchtime by a given userId.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(name: "userId", In = ParameterLocation.Path, Required = true, Type = typeof(string),
        Summary = "ISBN of the to be deleted book")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Empty response if successful")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json",
        bodyType: typeof(ErrorModel))]
    [Function("HTTPDeleteUser")]
    public async Task<HttpResponseData> DeleteUser(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "ranking/user/{userId}")]
        HttpRequestData request,
        string userId)
    {
        _logger.LogInformation(
            $"[{request.FunctionContext.InvocationId}] Processing request to delete user with userId {userId}.");

        // try to delete user by id
        var deleteResult = await _table.DeleteEntityAsync(partitionKey: string.Empty, rowKey: userId);

        // return HTTP 204 if deletion successful
        if (!deleteResult.IsError) return request.CreateResponse(HttpStatusCode.NoContent);

        // return HTTP 500 if deletion unsuccessful
        var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
        await errorResponse.WriteAsJsonAsync(new ErrorModel(
            ErrorMessage: $"There was an error deleting the user with id {userId}: {deleteResult.ReasonPhrase}.",
            Error: "BookDeletionError"
        ), HttpStatusCode.InternalServerError);
        return errorResponse;
    }
}