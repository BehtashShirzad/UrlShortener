using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks.Repositories;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,IConfiguration configuration)
        {

            services.AddDbContext<ShortLinkDbContext>(opt =>
            {
                var constr = configuration.GetConnectionString("DefaultConnection");
                opt.UseNpgsql(constr);
            });

            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped<IDomainEventBus, DomainEventBus>();
            services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IShortLinkRepository, ShortLinkRepository>();
            return services;
        }
    }
}
