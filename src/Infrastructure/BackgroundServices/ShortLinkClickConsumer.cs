using Application.Abstractions.Contracts;
using Application.IntegrationEvents;
using Infrastructure.Messaging.Redis;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.BackgroundServices;

internal sealed class ShortLinkClickConsumer
    : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisStreamOptions _options;
    private readonly ILogger<ShortLinkClickConsumer> _logger;

    private readonly string _consumerName;

    public ShortLinkClickConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        IOptions<RedisStreamOptions> options,
        ILogger<ShortLinkClickConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        _consumerName = RedisConsumerName.Create();
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var database = _redis.GetDatabase();

        await EnsureConsumerGroupAsync(database);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await database.StreamReadGroupAsync(
                    key: _options.ClickStream,
                    groupName: _options.ConsumerGroup,
                    consumerName: _consumerName,
                    position: ">",
                    count: 100);

                if (entries.Length == 0)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(250),
                        stoppingToken);

                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessAsync(
                        database,
                        entry,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error consuming Redis click stream.");

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerGroupAsync(
        IDatabase database)
    {
        try
        {
            await database.StreamCreateConsumerGroupAsync(
                _options.ClickStream,
                _options.ConsumerGroup,
                "0-0",
                createStream: true);
        }
        catch (RedisServerException exception)
            when (exception.Message.Contains("BUSYGROUP"))
        {
            // Group already exists.
        }
    }

    private async Task ProcessAsync(
        IDatabase database,
        StreamEntry entry,
        CancellationToken cancellationToken)
    {
        var @event = Parse(entry);

        using var scope = _scopeFactory.CreateScope();

        var processor = scope.ServiceProvider
            .GetRequiredService<IShortLinkClickProcessor>();

        var processed = await processor.ProcessAsync(
            @event,
            cancellationToken);

        if (processed)
        {
            await database.StreamAcknowledgeAsync(
                _options.ClickStream,
                _options.ConsumerGroup,
                entry.Id);
        }
    }

    private static ShortLinkClickedIntegrationEvent Parse(
        StreamEntry entry)
    {
        var values = entry.Values.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        return new ShortLinkClickedIntegrationEvent(
            Guid.Parse(values["eventId"]),
            Guid.Parse(values["shortLinkId"]),
            DateTimeOffset.FromUnixTimeMilliseconds(
                long.Parse(values["clickedAt"])));
    }
}