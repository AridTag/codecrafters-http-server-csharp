using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HttpServer;

/// <summary>
/// Fire and forget tasks can be registered with this class in order to observe exceptions they may generate
/// </summary>
internal class TaskObserver
{
    private struct WorkerState
    {
        public Task Thread;
        public CancellationTokenSource CancellationTokenSource;
    }
    
    private static readonly Lazy<TaskObserver> LazyInstance = new (() => new TaskObserver(), LazyThreadSafetyMode.PublicationOnly);
    
    private readonly ILogger _Log = new FauxLogger();
    private ConcurrentBag<Task> _Tasks = new();
    private ConcurrentBag<Task> _Tasks2 = new();
    private WorkerState? _WorkerState;

    private TaskObserver()
    {
    }

    public static TaskObserver Instance => LazyInstance.Value;
    
    public async Task Start(CancellationToken workerCancellationToken)
    {
        await StopWorker();
        
        _Tasks.Clear();
        _Tasks2.Clear();

        var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(workerCancellationToken);
        _WorkerState = new WorkerState
        {
            Thread = Task.Factory.StartNew(async () => await WorkerMethod(tokenSource.Token), TaskCreationOptions.LongRunning),
            CancellationTokenSource = tokenSource
        };
    }

    private async Task WorkerMethod(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            CheckTasks();
            await Task.Delay(100, CancellationToken.None);
        }
    }

    public async Task StopWorker()
    {
        if (_WorkerState == null)
            return;
        
        _WorkerState.Value.CancellationTokenSource.Cancel();
        await _WorkerState.Value.Thread;
        _WorkerState.Value.CancellationTokenSource.Dispose();
        _WorkerState = null;
    }

    private void CheckTasks()
    {
        _Tasks2 = Interlocked.Exchange(ref _Tasks, _Tasks2);
        while (_Tasks2.TryTake(out var task))
        {
            if (Observe(task) == Observation.Running)
                _Tasks.Add(task);
        }
    }

    private Observation Observe(Task task)
    {
        if (task.IsCanceled)
            return Observation.Cancelled;

        if (task.IsFaulted)
        {
            _Log.Log<object?>(
                LogLevel.Error,
                new EventId(),
                null,
                task.Exception,
                (_, exception) => exception?.ToString() ?? string.Empty
            );
            
            return Observation.Faulted;
        }

        if (task.IsCompleted)
            return Observation.Completed;
        
        return Observation.Running;
    }

    public void Register(Task t)
    {
        if (Observe(t) == Observation.Running)
        {
            _Tasks.Add(t);
        }
    }

    enum Observation
    {
        Running,
        Completed,
        Faulted,
        Cancelled
    }
}