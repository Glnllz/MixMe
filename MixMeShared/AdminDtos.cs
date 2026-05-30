namespace MixMeShared;

public class AdminDashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalEngineers { get; set; }
    public int TotalCustomers { get; set; }
    public decimal TotalTurnover { get; set; }
    public int PendingPostsCount { get; set; }
    public int PendingReviewsCount { get; set; }
    public int ActiveDisputesCount { get; set; }
    public List<TurnoverChartItem> TurnoverHistory { get; set; } = new();
}

public class AdminUserListItemDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public int RoleId { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class TurnoverChartItem
{
    public string Month { get; set; } = ""; // Название месяца
    public decimal Amount { get; set; }     // Сумма
}