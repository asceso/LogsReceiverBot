using DatabaseEvents;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Prism.Events;

namespace DataAdapter.Controllers
{
    public class WpLoginCheckController
    {
        public static async Task<List<WpLoginCheckModel>> GetChecksAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.WpLoginChecks.ToListAsync();
            }
            catch (Exception)
            {
                return new List<WpLoginCheckModel>();
            }
        }

        public static async Task<WpLoginCheckModel> GetCheckByIdAsync(int id)
        {
            try
            {
                using DataContext data = new();
                return await data.WpLoginChecks.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<List<WpLoginCheckModel>> GetChecksByUserIdAsync(long id)
        {
            try
            {
                using DataContext data = new();
                return await data.WpLoginChecks.Where(m => m.FromUserId == id).ToListAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> PostCheckAsync(WpLoginCheckModel model, IEventAggregator aggregator)
        {
            try
            {
                using DataContext data = new();
                await data.WpLoginChecks.AddAsync(model);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<WpLoginCheckUpdateEvent>().Publish(new("post", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PutCheckAsync(WpLoginCheckModel model, IEventAggregator aggregator)
        {
            try
            {
                WpLoginCheckModel target = await GetCheckByIdAsync(model.Id);
                if (target == null) throw new Exception("Not found");

                using DataContext data = new();
                data.Entry(target).CurrentValues.SetValues(model);
                data.Update(target);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<WpLoginCheckUpdateEvent>().Publish(new("put", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<int> DeleteCheckAsync(WpLoginCheckModel model)
        {
            try
            {
                using DataContext data = new();
                data.WpLoginChecks.Remove(model);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}