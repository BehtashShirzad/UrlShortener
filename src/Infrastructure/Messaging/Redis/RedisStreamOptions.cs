using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Messaging.Redis
{
    public sealed class RedisStreamOptions
    {
        public const string SectionName = "RedisStreams";

        public string ClickStream { get; init; } = "short-link-clicks";

        public string ConsumerGroup { get; init; } = "short-link-click-workers";
    }
}
