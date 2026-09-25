using Domain.Aggregates.ProcessedClick;
using System;
using System.Collections.Generic;
using System.Text;

namespace UrlShortener.UnitTests.Domain
{
    public sealed class ProcessedClickEventTests
    {
        [Fact]
        public void Create_ShouldUseEventIdAsAggregateId()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var shortLinkId = Guid.NewGuid();
            var processedAt = DateTimeOffset.UtcNow;

            // Act
            var result = ProcessedClickEvent.Create(
                eventId,
                shortLinkId,
                processedAt);

            // Assert
            Assert.Equal(eventId, result.Id);
            Assert.Equal(shortLinkId, result.ShortLinkId);
            Assert.Equal(processedAt, result.ProcessedAt);
        }
    }
}
