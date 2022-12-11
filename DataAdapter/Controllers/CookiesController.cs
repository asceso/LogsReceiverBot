using DatabaseEvents;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Prism.Events;

namespace DataAdapter.Controllers
{
    public class CookiesController
    {
        public static async Task<List<CookieModel>> GetCookiesAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.Cookies.ToListAsync();
            }
            catch (Exception)
            {
                return new List<CookieModel>();
            }
        }

        public static async Task<List<CookieModel>> GetCookiesByUserIdAsync(long id)
        {
            try
            {
                using DataContext data = new();
                return await data.Cookies.Where(m => m.UploadedByUserId == id).ToListAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<CookieModel> GetCookieByIdAsync(int id)
        {
            try
            {
                using DataContext data = new();
                return await data.Cookies.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<string> GetCookiesCategories()
        {
            try
            {
                using DataContext data = new();
                var dataNumerable = from l in data.Cookies
                                    select l.Category;
                return dataNumerable.Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static async Task<bool> PostCookieAsync(CookieModel model, IEventAggregator aggregator)
        {
            try
            {
                using DataContext data = new();
                await data.Cookies.AddAsync(model);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<CookieUpdateEvent>().Publish(new("post", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PutCookieAsync(CookieModel model, IEventAggregator aggregator)
        {
            try
            {
                CookieModel target = await GetCookieByIdAsync(model.Id);
                if (target == null) throw new Exception("Not found");

                using DataContext data = new();
                data.Entry(target).CurrentValues.SetValues(model);
                data.Update(target);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<CookieUpdateEvent>().Publish(new("put", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> IsDropMeLinkExist(string link)
        {
            try
            {
                using DataContext data = new();
                var asyncData = data.Cookies.AsAsyncEnumerable();
                await foreach (CookieModel model in asyncData)
                {
                    if (model.FileLink == link)
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

        public static async Task<int> DeleteCookieAsync(CookieModel model)
        {
            try
            {
                using DataContext data = new();
                data.Cookies.Remove(model);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static async Task<int> DeleteCookiesAsync(List<CookieModel> cookies = null)
        {
            try
            {
                using DataContext data = new();
                if (cookies == null) cookies = await GetCookiesAsync();
                data.Cookies.RemoveRange(cookies);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}