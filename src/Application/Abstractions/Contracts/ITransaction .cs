using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Abstractions.Contracts
{
    public interface ITransaction : IAsyncDisposable
    {
        Task CommitAsync(
            CancellationToken cancellationToken = default);

        Task RollbackAsync(
            CancellationToken cancellationToken = default);
    }
}
