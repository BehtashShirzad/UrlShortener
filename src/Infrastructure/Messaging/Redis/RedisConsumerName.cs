using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Messaging.Redis
{
    internal static class RedisConsumerName
    {
        public static string Create()
        {
            return $"{Environment.MachineName}-{Guid.NewGuid():N}";
        }
    }
}
