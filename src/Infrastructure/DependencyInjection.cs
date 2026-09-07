using Application.Abstractions.Caching;
using Application.Abstractions.Contracts;
using AsyncKeyedLock;
using Domain.Aggregates.ShortLinks.Repositories;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var dbConnection =
                configuration.GetConnectionString("shortlinks")
                ?? throw new InvalidOperationException(
                    "Database connection string is not configured.");

            var redisConnection =
                configuration.GetConnectionString("redis")
                ?? throw new InvalidOperationException(
                    "Redis connection string is not configured.");

            services.AddDbContext<ShortLinkDbContext>(options =>
            {
                options.UseNpgsql(dbConnection);
            });


            services.AddScoped<ICurrentUser, CurrentUser>();

            services.AddScoped<IDomainEventBus, DomainEventBus>();
            services.AddScoped<
                IDomainEventDispatcher,
                MediatrDomainEventDispatcher>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<
                IShortLinkRepository,
                ShortLinkRepository>();

            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnection));

            services.AddSingleton<ShortLinkRedisCache>();
            services.AddMemoryCache();
            services.AddSingleton(new AsyncKeyedLocker<string>());
            services.AddScoped<
                IShortLinkCacheService,
                ShortLinkCacheService>();

            services.AddResiliencePipeline("redis", builder =>
            {
                builder.AddCircuitBreaker(
                    new CircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = 5,
                        SamplingDuration = TimeSpan.FromSeconds(10),
                        BreakDuration = TimeSpan.FromSeconds(30),

                        ShouldHandle = new PredicateBuilder()
                            .Handle<RedisException>()
                            .Handle<TimeoutRejectedException>(),

                        OnOpened = args =>
                        {
                            // metric / logging
                            return default;
                        },

                        OnClosed = args =>
                        {
                            // Redis recovered
                            return default;
                        },

                        OnHalfOpened = args =>
                        {
                            return default;
                        }
                    });

                builder.AddTimeout(
                    TimeSpan.FromMilliseconds(500));
            });

            return services;
        }
    }
}
