using System;
using System.Collections.Generic;
using System.Text;

namespace Application.IntegrationEvents;
public sealed record ShortLinkClickedIntegrationEvent(
    Guid EventId,
    Guid ShortLinkId,
    DateTimeOffset ClickedAt);
