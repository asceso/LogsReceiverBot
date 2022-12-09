using Services.Implementation;
using Services.Interfaces;

namespace ServicesTest
{
    public class Tests
    {
        private const string MainThread = nameof(MainThread);
        private const string SecondThread = nameof(SecondThread);
        private ITaskScheduleService taskService;

        [SetUp]
        public void Setup()
        {
            taskService = new TaskScheduleService();
            taskService.Register(MainThread);
            taskService.Register(SecondThread);
        }

        [Test]
        public async Task TestTaskQueueService()
        {
            Console.WriteLine($"Start at {GetTimestamp}");
            taskService.In(MainThread).TaskCompleted += TaskServiceTaskCompletedMain;
            taskService.In(MainThread).TaskCanceled += TaskServiceTaskCanceledMain;
            taskService.In(SecondThread).TaskCompleted += TaskServiceTaskCompletedSecond;

            taskService.In(MainThread).ScheduleNext(Method).StartNext();
            taskService.In(MainThread).ScheduleNext(Method).AddParameters("hello").StartNext();
            //Guid id = taskService.In(MainThread).ScheduleNext(Method);
            //await Task.Delay(1000);
            //taskService.In(MainThread).Kill(id);

            //taskService.In(SecondThread).ScheduleNext(Method);
            //taskService.In(SecondThread).ScheduleNext(Method);

            await taskService.In(MainThread).WaitForSchedulerFinish();
            await taskService.In(SecondThread).WaitForSchedulerFinish();
            Console.WriteLine($"End at {GetTimestamp}");
        }

        private string Method(object[] parameters)
        {
            Task.Delay(2000).Wait();
            return $"END ({parameters?.Length})";
        }

        private void TaskServiceTaskCanceledMain(Guid taskId, string threadName)
        {
            Console.WriteLine($"Task #{taskId} " +
                              $"was canceled at: '{GetTimestamp}' " +
                              $"from thread name: '{threadName}'");
        }

        private void TaskServiceTaskCompletedMain(Guid taskId, string threadName)
        {
            Console.WriteLine($"Task #{taskId} " +
                              $"result: '{taskService.In(threadName).GetResult(taskId)}', " +
                              $"received at '{GetTimestamp}' " +
                              $"from thread name: '{threadName}'");
        }

        private void TaskServiceTaskCompletedSecond(Guid taskId, string threadName)
        {
            Console.WriteLine($"Task #{taskId} " +
                              $"result: '{taskService.In(threadName).GetResult(taskId)}', " +
                              $"received at '{GetTimestamp}' " +
                              $"from thread name: '{threadName}'");
        }

        private static string GetTimestamp => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
    }
}