using AgregatorEvents;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Prism.Events;
using System.Security.Cryptography;

namespace DataAdapter.Controllers
{
    public class LogsController
    {
        public static async Task<List<LogModel>> GetLogsAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.Logs.ToListAsync();
            }
            catch (Exception)
            {
                return new List<LogModel>();
            }
        }

        public static async Task<List<string>> GetLogsDataAsync()
        {
            try
            {
                using DataContext data = new();
                var logsData = from l in data.Logs
                               select l.ToString();
                return logsData.ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static List<LogModel> TakeShuffleLogs(int take)
        {
            try
            {
                using DataContext data = new();
                return data.Logs.OrderBy(l => l.Login).Take(take).ToList();
            }
            catch (Exception)
            {
                return new List<LogModel>();
            }
        }

        public static async Task<bool> IsLogExist(string log)
        {
            try
            {
                using DataContext data = new();
                var asyncLogs = data.Logs.AsAsyncEnumerable();
                await foreach (LogModel logModel in asyncLogs)
                {
                    if (logModel.ToString() == log)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsLogExistV2(string log)
        {
            try
            {
                using DataContext data = new();
                return data.Logs.AsParallel().Any(l => l.ToString() == log);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PostLogAsync(LogModel model)
        {
            try
            {
                using DataContext data = new();
                await data.Logs.AddAsync(model);
                await data.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<int> DeleteLogsAsync(List<LogModel> logs = null)
        {
            try
            {
                using DataContext data = new();
                if (logs == null) logs = await GetLogsAsync();
                data.Logs.RemoveRange(logs);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}