using Domain.Aggregates.ShortLinks.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDomainServices(this IServiceCollection services)
        {
            services.AddScoped<IShortLinkDomainService, ShortLinkDomainService>();

            return services;
        }
    }
}
