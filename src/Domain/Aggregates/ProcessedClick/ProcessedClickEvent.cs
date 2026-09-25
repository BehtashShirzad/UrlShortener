using Domain.Abstractions;
using Domain.Abstractions.Aggregates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Aggregates.ProcessedClick
{
    public class ProcessedClickEvent:AggregateRoot<Guid>
    {
        public Guid ShortLinkId { get; private set; }

        public DateTimeOffset ProcessedAt { get; private set; }

        private ProcessedClickEvent()
        {
        }

        private ProcessedClickEvent(Guid eventId,
            Guid shortLinkId,
            DateTimeOffset processedAt)
        {
            Id = eventId;
            ShortLinkId = shortLinkId;
            ProcessedAt = processedAt;
        }
        public static ProcessedClickEvent Create(Guid eventId,Guid shortLinkId,DateTimeOffset processedAt)
        {
            return new ProcessedClickEvent(eventId,shortLinkId, processedAt);
        }

    }
}
