namespace BronyTV.Contract;

public class AdminUserListResponse
{
    public IReadOnlyList<AdminUserSummaryResponse> Items { get; set; } = Array.Empty<AdminUserSummaryResponse>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}
