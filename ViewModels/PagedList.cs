using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.ViewModels;

public interface IPaged { int Page { get; } int TotalPages { get; } }

public class PagedList<T> : IPaged
{
    public List<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Total { get; }
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);

    public PagedList(List<T> items, int page, int pageSize, int total)
    { Items = items; Page = page; PageSize = pageSize; Total = total; }

    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedList<T>(items, page, pageSize, total);
    }
}
