using Application.Abstractions.Contracts;
using Application.IntegrationEvents;
using Infrastructure.Messaging.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Services
{
    internal sealed class RedisShortLinkClickPublisherService
    : IShortLinkClickPublisher
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly RedisStreamOptions _options;

        public RedisShortLinkClickPublisherService(
            IConnectionMultiplexer redis,
            IOptions<RedisStreamOptions> options)
        {
            _redis = redis;
            _options = options.Value;
        }

        public async Task PublishAsync(
            ShortLinkClickedIntegrationEvent @event,
            CancellationToken cancellationToken = default)
        {
            var database = _redis.GetDatabase();

            var entries = new NameValueEntry[]
            {
            new("eventId", @event.EventId.ToString()),
            new("shortLinkId", @event.ShortLinkId.ToString()),
            new("clickedAt", @event.ClickedAt.ToUnixTimeMilliseconds())
            };

            await database.StreamAddAsync(
                _options.ClickStream,
                entries);
        }
    }
}
