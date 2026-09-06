using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Abstractions.Entities
{
   public interface IEntity<out TId>
        where TId : notnull
    {
        TId Id { get; }
            Guid ModifierId { get; set; }
      DateTime ModifiedAt { get; set; }
      Guid CreatorId { get; set; }
      DateTime CreatedAt { get; set; }
    }
}
