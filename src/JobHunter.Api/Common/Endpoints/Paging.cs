namespace JobHunter.Api.Common.Endpoints;

internal static class Paging
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

    public static IQueryable<T> Page<T>(this IQueryable<T> query, int page, int pageSize) =>
        query.Skip((page - 1) * pageSize).Take(pageSize);
}
