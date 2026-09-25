using Application.Abstractions.Contracts;
using Application.IntegrationEvents;
using Domain.Aggregates.ProcessedClick;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Services
{
    internal class ShortLinkClickProcessorService : IShortLinkClickProcessor
    {
        private readonly ShortLinkDbContext _dbContext;

        public ShortLinkClickProcessorService(
            ShortLinkDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> ProcessAsync(
            ShortLinkClickedIntegrationEvent @event,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                var processedEvent =  ProcessedClickEvent.Create(
                    @event.EventId,
                    @event.ShortLinkId,
                    DateTimeOffset.UtcNow);

                _dbContext.ProcessedClickEvents.Add(
                    processedEvent);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                await _dbContext.ShortLinks
                    .Where(x => x.Id == @event.ShortLinkId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                x => x.TotalClicks,
                                x => x.TotalClicks + 1),
                        cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return true;
            }
            catch (DbUpdateException exception)
                when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                // Event already processed.
                return true;
            }
        }

        private static bool IsUniqueConstraintViolation(
            DbUpdateException exception)
        {
            // SQL Server implementation
            return exception.InnerException is PostgresException postgresException
        && postgresException.SqlState ==
           PostgresErrorCodes.UniqueViolation;
             
        }
    }

}
