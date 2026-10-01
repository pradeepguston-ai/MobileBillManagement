using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.MasterData;

public sealed partial class EfMasterDataService(MobileBillDbContext dbContext) : IMasterDataService
{
    private readonly MobileBillDbContext _dbContext = dbContext;

    private static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        IQueryable<T> query,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, pageNumber, pageSize, totalCount);
    }

    private static IQueryable<T> ApplyStatusFilter<T>(IQueryable<T> query, bool? isActive, Expression<Func<T, bool>> activeProperty)
    {
        return isActive is null ? query : query.Where(BuildStatusExpression(activeProperty, isActive.Value));
    }

    private static Expression<Func<T, bool>> BuildStatusExpression<T>(Expression<Func<T, bool>> property, bool value)
    {
        var body = Expression.Equal(property.Body, Expression.Constant(value));
        return Expression.Lambda<Func<T, bool>>(body, property.Parameters);
    }
}
