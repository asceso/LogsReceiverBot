using Services.Implementation;
using Services.Interfaces;

namespace ServicesTest
{
    public class Tests
    {
        private const string MainThread = nameof(MainThread);
        private const string SecondThread = nameof(SecondThread);
        private const string ThirdThread = nameof(ThirdThread);
        private ITaskScheduleService taskService;

        [SetUp]
        public void Setup()
        {
            taskService = new TaskScheduleService();
            taskService.Create(MainThread);
            taskService.Create(SecondThread);
            taskService.Create(ThirdThread);
        }

        [Test]
        public async Task TestTaskQueueService()
        {
            Console.WriteLine($"Start at {GetTimestamp}");
            taskService.In(MainThread).TaskCanceled += TaskServiceTaskCanceledHandler;
            taskService.In(MainThread).TaskCompleted += TaskServiceTaskCompletedHandler;
            taskService.In(SecondThread).TaskCompleted += TaskServiceTaskCompletedHandler;
            taskService.In(ThirdThread).TaskCompleted += TaskServiceTaskCompletedHandler;

            taskService.In(MainThread).ScheduleTask(Method).StartNext();
            taskService.In(MainThread).ScheduleTask(Method).StartNext();

            Console.WriteLine("Without queue: " + await taskService.In(MainThread).ScheduleTask(Method).AddParameters(1000).StartNowAsync() + $" ended at {GetTimestamp}");

            taskService.In(MainThread).ScheduleTask(AsyncMethod).StartNext();
            taskService.In(MainThread).ScheduleTask(AsyncMethod).AddParameters("hello").StartNext();
            Guid id = taskService.In(MainThread).ScheduleTask(Method).StartNext();
            await Task.Delay(1000);
            taskService.In(MainThread).Kill(id);

            taskService.In(SecondThread).ScheduleTask(Method).StartNext();
            taskService.In(SecondThread).ScheduleTask(Method).StartNext();
            taskService.In(SecondThread).ScheduleTask(Method).StartNext();
            taskService.In(SecondThread).ScheduleTask(Method).StartNext();

            taskService.In(ThirdThread).ScheduleTask(Method).StartNext();
            taskService.In(ThirdThread).ScheduleTask(Method).StartNext();
            taskService.In(ThirdThread).ScheduleTask(Method).StartNext();
            taskService.In(ThirdThread).ScheduleTask(Method).StartNext();

            await taskService.In(MainThread).WaitForSchedulerFinish();
            await taskService.In(SecondThread).WaitForSchedulerFinish();
            await taskService.In(ThirdThread).WaitForSchedulerFinish();
            Console.WriteLine($"End at {GetTimestamp}");
        }

        private string Method(object[] parameters)
        {
            if (parameters != null && parameters.Length == 1 && parameters[0] is int delay)
            {
                Task.Delay(delay).Wait();
            }
            else
            {
                Task.Delay(2000).Wait();
            }
            return $"END ({parameters?.Length})";
        }

        private async Task<string> AsyncMethod(object[] parameters)
        {
            await Task.Delay(2000);
            return $"ASYNC END ({parameters?.Length})";
        }

        private void TaskServiceTaskCanceledHandler(Guid taskId, string threadName)
        {
            Console.WriteLine($"Task #{taskId} " +
                              $"was canceled at: '{GetTimestamp}' " +
                              $"from thread name: '{threadName}'");
        }

        private void TaskServiceTaskCompletedHandler(Guid taskId, string threadName)
        {
            Console.WriteLine($"Task #{taskId} " +
                              $"result: '{taskService.In(threadName).GetResult(taskId)}', " +
                              $"received at '{GetTimestamp}' " +
                              $"from thread name: '{threadName}'");
        }

        private static string GetTimestamp => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
    }
}