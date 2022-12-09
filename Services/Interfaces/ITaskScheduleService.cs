namespace Services.Interfaces
{
    #region service interface

    /// <summary>
    /// Сервис для планирования очередных заданий
    /// </summary>
    public interface ITaskScheduleService
    {
        /// <summary>
        /// Метод возвращает зарегистрированные потоки
        /// </summary>
        /// <returns>Массив зарегистрированных потоков</returns>
        public string[] GetRegistered();

        /// <summary>
        /// Метод регистрирует поток с заданным именем
        /// </summary>
        /// <param name="name">Имя потока</param>
        public void Register(string name);

        /// <summary>
        /// Метод возвращает поток по его имени
        /// </summary>
        /// <param name="threadName">Имя потока</param>
        /// <returns>Поток по указанному имени, если не существует возвращает null</returns>
        public ITaskScheduleThread In(string threadName);
    }

    #endregion service interface

    #region thread interface

    /// <summary>
    /// Интерфейс потока для сервиса ITaskScheduleService
    /// </summary>
    public interface ITaskScheduleThread
    {
        /// <summary>
        /// Событие когда задание начало работу
        /// </summary>
        public event TaskStartedHandler TaskStarted;

        /// <summary>
        /// Событие когда задание остановлено
        /// </summary>
        public event TaskCanceledHandler TaskCanceled;

        /// <summary>
        /// Событие когда задание закончило работу
        /// </summary>

        public event TaskCompletedHandler TaskCompleted;

        /// <summary>
        /// Запланировать задание в очередь
        /// </summary>
        /// <param name="body">Тело задания</param>
        /// <returns>ИД задания</returns>
        public Guid ScheduleNext(Func<string> body);

        /// <summary>
        /// Останавливает задание с указанным ИД
        /// </summary>
        /// <param name="id">ИД задания</param>
        public void Kill(Guid id);

        /// <summary>
        /// Возвращает результат выполненного задания
        /// </summary>
        /// <param name="taskId">ИД задания</param>
        /// <returns>Результат задания</returns>
        public string GetResult(Guid taskId);

        /// <summary>
        /// Возвращает имя зарегистрированного потока
        /// </summary>
        /// <returns>Имя зарегистрированного потока</returns>
        public string Name();

        /// <summary>
        /// Ожидать завершения всех задач в планироващике
        /// </summary>
        /// <returns>Ничего</returns>
        public Task WaitForSchedulerFinish();
    }

    #endregion thread interface

    #region handlers

    /// <summary>
    /// Обработчик начала выполнения задания
    /// </summary>
    /// <param name="taskId">ИД задания</param>
    public delegate void TaskStartedHandler(Guid taskId, string threadName);

    /// <summary>
    /// Обработчик остановки выполнения задания
    /// </summary>
    /// <param name="taskId">ИД задания</param>
    public delegate void TaskCanceledHandler(Guid taskId, string threadName);

    /// <summary>
    /// Обработчик завершения выполнения задания
    /// </summary>
    /// <param name="taskId">ИД задания</param>
    public delegate void TaskCompletedHandler(Guid taskId, string threadName);

    #endregion handlers
}