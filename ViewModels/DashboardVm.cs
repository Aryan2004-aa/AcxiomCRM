namespace AcxiomCRM.ViewModels;

public class DashboardVm
{
    public string Range { get; set; } = "all";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal PipelineValue { get; set; }
    public decimal WeightedPipeline { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    // Admin only
    public bool IsAdmin { get; set; }
    public int TotalUsers { get; set; }
    public int LockedUsers { get; set; }
    public int FailedLoginsToday { get; set; }

    public List<string> LeadLabels { get; set; } = new();
    public List<int> LeadCounts { get; set; } = new();
    public List<string> StageLabels { get; set; } = new();
    public List<int> StageCounts { get; set; } = new();
    public List<string> MonthLabels { get; set; } = new();
    public List<decimal> MonthSales { get; set; } = new();
}

public class ReportVm : IPaged
{
    public string Title { get; set; }
    public string Action { get; set; }
    public string[] Columns { get; set; }
    public List<string[]> Rows { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? Sort { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int Total { get; set; }
}
