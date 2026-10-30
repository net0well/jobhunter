using System.Collections.Concurrent;
using System.Threading.Channels;
using Mediator;

namespace JobHunter.Api.Tests.Workers;

// Records what workers send; optionally fails the first call to prove a worker survives a broken cycle.
internal sealed class RecordingSender(bool failFirst = false) : ISender
{
    private readonly Channel<object> _sent = Channel.CreateUnbounded<object>();
    private int _calls;

    public ConcurrentQueue<object> Messages { get; } = new();

    public async Task<object> NextAsync(CancellationToken ct) =>
        await _sent.Reader.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), ct);

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => Record<TResponse>(request);

    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) => Record<TResponse>(command);

    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => Record<TResponse>(query);

    public async ValueTask<object?> Send(object message, CancellationToken cancellationToken = default) => await Record<object?>(message);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private ValueTask<TResponse> Record<TResponse>(object message)
    {
        Messages.Enqueue(message);
        _sent.Writer.TryWrite(message);

        if (failFirst && Interlocked.Increment(ref _calls) == 1)
            throw new InvalidOperationException("Simulated cycle failure");

        return ValueTask.FromResult(default(TResponse)!);
    }
}
