﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class AuthRequestRepository : Repository<Core.Auth.Entities.AuthRequest, AuthRequest, Guid>, IAuthRequestRepository
{
    public AuthRequestRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.AuthRequests)
    { }
    public async Task<int> DeleteExpiredAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var expiredRequests = await dbContext.AuthRequests.Where(a => a.CreationDate < DateTime.Now.AddMinutes(-15)).ToListAsync();
            dbContext.AuthRequests.RemoveRange(expiredRequests);
            await dbContext.SaveChangesAsync();
            return 1;
        }
    }

    public async Task<ICollection<Core.Auth.Entities.AuthRequest>> GetManyByUserIdAsync(Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var userAuthRequests = await dbContext.AuthRequests.Where(a => a.UserId.Equals(userId)).ToListAsync();
            return Mapper.Map<List<Core.Auth.Entities.AuthRequest>>(userAuthRequests);
        }
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyPendingByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUserAuthRequests = await (from ar in dbContext.AuthRequests
                                             where ar.OrganizationId.Equals(organizationId) && ar.ResponseDate == null && ar.Type == AuthRequestType.AdminApproval
                                             select ar).ProjectTo<OrganizationAdminAuthRequest>(Mapper.ConfigurationProvider).ToListAsync();

            return orgUserAuthRequests;
        }
    }

    public async Task<ICollection<OrganizationAdminAuthRequest>> GetManyAdminApprovalRequestsByManyIdsAsync(
        Guid organizationId,
        IEnumerable<Guid> ids)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var orgUserAuthRequests = await (from ar in dbContext.AuthRequests
                                             where ar.OrganizationId.Equals(organizationId) && ids.Contains(ar.Id) && ar.Type == AuthRequestType.AdminApproval
                                             select ar).ProjectTo<OrganizationAdminAuthRequest>(Mapper.ConfigurationProvider).ToListAsync();

            return orgUserAuthRequests;
        }
    }
}
