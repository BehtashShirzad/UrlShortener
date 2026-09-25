using Application;
using Domain;
using Infrastructure;
using Infrastructure.Messaging.Redis;
namespace Api
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddUrlShortlinkServices(this IServiceCollection services,IConfiguration configuration)
        {

            services.AddDomainServices();
            services.AddApplicationServices();
            services.AddInfrastructureServices(configuration);

            return services;
        }
    }
}
