using System;
using System.Collections.Generic;

namespace BronyTV.Contract;

public class UserActivityItemResponse
{
    public string Type { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public class UserActivityListResponse
{
    public List<UserActivityItemResponse> Activities { get; set; } = new();
}
