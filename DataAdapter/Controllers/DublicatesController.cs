using Extensions;
using Microsoft.EntityFrameworkCore;
using Models.Database;

namespace DataAdapter.Controllers
{
    public class DublicatesController
    {
        public static async Task<List<DublicateModel>> GetLogsAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.Dublicates.ToListAsync();
            }
            catch (Exception)
            {
                return new List<DublicateModel>();
            }
        }

        public static async Task<List<DublicateModel>> GetLogsAsync(UserModel user, string category = null)
        {
            try
            {
                using DataContext data = new();
                if (user.Id != 0 && category.IsNullOrEmptyString())
                {
                    return await data.Dublicates.Where(d => d.UploadedByUserId == user.Id).ToListAsync();
                }
                if (user.Id == 0 && !category.IsNullOrEmptyString())
                {
                    return await data.Dublicates.Where(d => d.Category == category).ToListAsync();
                }
                if (user.Id != 0 && !category.IsNullOrEmptyString())
                {
                    return await data.Dublicates.Where(d => d.UploadedByUserId == user.Id && d.Category == category).ToListAsync();
                }
                else
                {
                    return await data.Dublicates.ToListAsync();
                }
            }
            catch (Exception)
            {
                return new List<DublicateModel>();
            }
        }

        public static List<string> GetLogsData()
        {
            try
            {
                using DataContext data = new();
                var logsData = from l in data.Dublicates
                               select l.ToString();
                return logsData.ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static List<string> GetLogsDataByCategory(string category)
        {
            try
            {
                using DataContext data = new();
                var logsData = from l in data.Dublicates
                               where category == l.Category
                               select l.ToString();
                return logsData.ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static List<string> GetLogsCategories()
        {
            try
            {
                using DataContext data = new();
                var dataNumerable = from l in data.Dublicates
                                    select l.Category;
                return dataNumerable.Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static List<DublicateModel> TakeShuffleLogs(int take)
        {
            try
            {
                using DataContext data = new();
                return data.Dublicates.OrderBy(l => l.Login).Take(take).ToList();
            }
            catch (Exception)
            {
                return new List<DublicateModel>();
            }
        }

        public static async Task<bool> IsLogExist(string log)
        {
            try
            {
                using DataContext data = new();
                var asyncLogs = data.Dublicates.AsAsyncEnumerable();
                await foreach (DublicateModel logModel in asyncLogs)
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
                return data.Dublicates.AsParallel().Any(l => l.ToString() == log);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PostLogAsync(DublicateModel model)
        {
            try
            {
                using DataContext data = new();
                await data.Dublicates.AddAsync(model);
                await data.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<int> DeleteLogsAsync(List<DublicateModel> logs = null)
        {
            try
            {
                using DataContext data = new();
                if (logs == null) logs = await GetLogsAsync(new());
                data.Dublicates.RemoveRange(logs);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}