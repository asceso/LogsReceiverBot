using Services.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Services.Implementation
{
    #region service implementation

    public sealed class TaskScheduleService : ITaskScheduleService
    {
        private readonly Dictionary<string, ITaskScheduleThread> threads;

        public TaskScheduleService()
        {
            threads = new();
        }

        public void Create(string name)
        {
            ITaskScheduleThread thread = new TaskScheduleThread(name);
            threads.Add(name, thread);
        }

        public string[] Threads() => threads.Keys.ToArray();

        public ITaskScheduleThread In(string name)
        {
            if (threads.ContainsKey(name))
            {
                return threads[name];
            }
            return null;
        }
    }

    #endregion service implementation

    #region thread implementation

    public sealed class TaskScheduleThread : ITaskScheduleThread
    {
        #region events

        public event TaskCompletedHandler TaskCompleted;

        public event TaskStartedHandler TaskStarted;

        public event TaskCanceledHandler TaskCanceled;

        #endregion events

        #region fields

        private readonly string registeredName;
        private readonly ObservableCollection<TaskWithId> tasks;
        private readonly Dictionary<Guid, string> results;
        private readonly Dictionary<Guid, CancellationTokenSource> tokens;

        #endregion fields

        #region ctor

        public TaskScheduleThread(string threadName)
        {
            tasks = new();
            results = new();
            tokens = new();
            tasks.CollectionChanged += TasksCollectionChanged;
            this.registeredName = threadName;
        }

        #endregion ctor

        #region queue scheduling

        private async void TasksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewStartingIndex == 0)
                    {
                        await RunNextTaskAsync().ConfigureAwait(false);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    {
                        if (tasks.Count != 0)
                        {
                            await RunNextTaskAsync().ConfigureAwait(false);
                        }
                    }
                    break;
            }
        }

        private async Task RunNextTaskAsync()
        {
            TaskWithId taskWithId = tasks.FirstOrDefault();
            if (!tokens[taskWithId.Id].IsCancellationRequested)
            {
                TaskStarted?.Invoke(taskWithId.Id, Name());
                string result = string.Empty;
                if (!taskWithId.IsRunAsync)
                {
                    result = await Task.Run(() => taskWithId.Body(taskWithId.Parameters), tokens[taskWithId.Id].Token);
                }
                else
                {
                    if (taskWithId.AsyncBody != null)
                    {
                        result = await Task.Run(async () => await taskWithId.AsyncBody(taskWithId.Parameters), tokens[taskWithId.Id].Token);
                    }
                    else
                    {
                        await Task.Run(async () => await taskWithId.AsyncBodyWithoutResult(taskWithId.Parameters), tokens[taskWithId.Id].Token);
                    }
                }
                if ((taskWithId.IsRunAsync && taskWithId.AsyncBody != null) || !taskWithId.IsRunAsync)
                {
                    results.Add(taskWithId.Id, result);
                }
                TaskCompleted?.Invoke(taskWithId.Id, Name());
            }
            tasks.Remove(taskWithId);
            if (tokens.ContainsKey(taskWithId.Id)) tokens.Remove(taskWithId.Id);
        }

        #endregion queue scheduling

        #region public

        public IScheduledTask ScheduleTask(Func<object[], string> body)
        {
            CancellationTokenSource cts = new();
            TaskWithId taskWithId = new(false)
            {
                Id = Guid.NewGuid(),
                Body = body
            };
            tokens.Add(taskWithId.Id, cts);
            IScheduledTask scheduledTask = new ScheduledTask(taskWithId, tasks);
            return scheduledTask;
        }

        public IScheduledTask ScheduleTask(Func<object[], Task<string>> body)
        {
            CancellationTokenSource cts = new();
            TaskWithId taskWithId = new(true)
            {
                Id = Guid.NewGuid(),
                AsyncBody = body,
                AsyncBodyWithoutResult = null
            };
            tokens.Add(taskWithId.Id, cts);
            IScheduledTask scheduledTask = new ScheduledTask(taskWithId, tasks);
            return scheduledTask;
        }

        public IScheduledTask ScheduleTask(Func<object[], Task> body)
        {
            CancellationTokenSource cts = new();
            TaskWithId taskWithId = new(true)
            {
                Id = Guid.NewGuid(),
                AsyncBody = null,
                AsyncBodyWithoutResult = body
            };
            tokens.Add(taskWithId.Id, cts);
            IScheduledTask scheduledTask = new ScheduledTask(taskWithId, tasks);
            return scheduledTask;
        }

        public string GetResult(Guid taskId)
        {
            if (results.ContainsKey(taskId))
            {
                string result = results[taskId];
                results.Remove(taskId);
                return result;
            }
            return default;
        }

        public async Task WaitForSchedulerFinish()
        {
            while (tasks.Count > 0)
            {
                await Task.Delay(1);
            }
            return;
        }

        public async Task WaitForTaskFinish(Guid taskId)
        {
            while (tasks.Any(t => t.Id == taskId))
            {
                await Task.Delay(1);
            }
            return;
        }

        public string Name() => registeredName;

        public void Kill(Guid id)
        {
            if (tokens.ContainsKey(id))
            {
                tokens[id].Cancel();
                TaskCanceled?.Invoke(id, Name());
            }
        }

        #endregion public
    }

    #endregion thread implementation

    #region scheduled task

    public sealed class ScheduledTask : IScheduledTask
    {
        private readonly TaskWithId taskWithId;
        private readonly ObservableCollection<TaskWithId> tasks;

        public ScheduledTask(TaskWithId taskWithId, ObservableCollection<TaskWithId> tasks)
        {
            this.taskWithId = taskWithId;
            this.tasks = tasks;
        }

        public IScheduledTask AddParameters(params object[] parameters)
        {
            taskWithId.Parameters = parameters;
            return this;
        }

        public Guid StartNext()
        {
            tasks.Add(taskWithId);
            return taskWithId.Id;
        }

        public async Task<string> StartNowAsync() => !taskWithId.IsRunAsync
                ? await Task.Run(() => taskWithId.Body(taskWithId.Parameters))
                : await Task.Run(async () => await taskWithId.AsyncBody(taskWithId.Parameters));

        public async Task<string> StartNowAsync(CancellationTokenSource cancellationToken) => !taskWithId.IsRunAsync
                ? await Task.Run(() => taskWithId.Body(taskWithId.Parameters), cancellationToken.Token)
                : await Task.Run(async () => await taskWithId.AsyncBody(taskWithId.Parameters), cancellationToken.Token);
    }

    #endregion scheduled task

    #region id task model

    public sealed class TaskWithId
    {
        public readonly bool IsRunAsync;

        public Guid Id { get; set; }
        public Func<object[], string> Body { get; set; }
        public Func<object[], Task<string>> AsyncBody { get; set; }
        public Func<object[], Task> AsyncBodyWithoutResult { get; set; }
        public int TaskCount { get; set; }
        public object[] Parameters { get; set; }

        public TaskWithId(bool runAsync)
        {
            IsRunAsync = runAsync;
        }
    }

    #endregion id task model
}