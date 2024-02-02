using Azure;
using Azure.Data.Tables;

namespace Function.model;

public class UserTableModel : ITableEntity
{
    // not used 
    public string PartitionKey { get; set; } = string.Empty;

    // users unique id
    public required string RowKey { get; set; }

    // define last insert / update date
    public DateTimeOffset? Timestamp { get; set; }

    // used for cache validation; can be ignored for our use case
    public ETag ETag { get; set; }

    public required long TotalWatchtime { get; set; }
}