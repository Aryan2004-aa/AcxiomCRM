using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

// Server-side data scoping: Admin/Manager see everything, Sales Executives only their own/assigned records.
public static class Scope
{
    public static IQueryable<Customer> ForUser(this IQueryable<Customer> q, string uid, bool priv) =>
        priv ? q : q.Where(c => c.AssignedTo == uid || c.CreatedBy == uid);
    public static IQueryable<Lead> ForUser(this IQueryable<Lead> q, string uid, bool priv) =>
        priv ? q : q.Where(x => x.AssignedTo == uid);
    public static IQueryable<Opportunity> ForUser(this IQueryable<Opportunity> q, string uid, bool priv) =>
        priv ? q : q.Where(x => x.AssignedTo == uid);
    public static IQueryable<FollowUp> ForUser(this IQueryable<FollowUp> q, string uid, bool priv) =>
        priv ? q : q.Where(x => x.AssignedTo == uid);
    public static IQueryable<Activity> ForUser(this IQueryable<Activity> q, string uid, bool priv) =>
        priv ? q : q.Where(x => x.AssignedTo == uid);

    public static bool CanAccess(this Customer c, string uid, bool priv) => priv || c.AssignedTo == uid || c.CreatedBy == uid;
    public static bool CanAccess(this Lead x, string uid, bool priv) => priv || x.AssignedTo == uid;
    public static bool CanAccess(this Opportunity x, string uid, bool priv) => priv || x.AssignedTo == uid;
    public static bool CanAccess(this FollowUp x, string uid, bool priv) => priv || x.AssignedTo == uid;
    public static bool CanAccess(this Activity x, string uid, bool priv) => priv || x.AssignedTo == uid;
}
