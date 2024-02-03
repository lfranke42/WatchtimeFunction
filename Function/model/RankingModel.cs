namespace Function.model;

public record RankingModel(
    long Position,
    long TotalWatchtime,
    List<AnonRankingModel> ClosestNeighbors);