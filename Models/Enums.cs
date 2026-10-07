namespace AcxiomCRM.Models;

public enum CustomerStatus { Active, Inactive, Prospect }
public enum LeadStatus { New, Contacted, Qualified, Unqualified, Converted, Lost }
public enum LeadPriority { Low, Medium, High }
public enum OpportunityStage { Qualification, Proposal, Negotiation, Won, Lost }
public enum FollowUpStatus { Planned, Completed, Missed, Cancelled }
public enum ActivityType { Call, Meeting, Email, Task }
public enum ActivityStatus { Open, Completed, Cancelled }

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";
    public const string Privileged = "Admin,Manager";
    public static readonly string[] All = { Admin, Manager, SalesExecutive };
}

public static class Rules
{
    public const string PhonePattern = @"^[6-9]\d{9}$";
    public const string PhoneMessage = "Enter a valid phone number.";
    public const string EmailMessage = "Enter a valid email address.";
}
