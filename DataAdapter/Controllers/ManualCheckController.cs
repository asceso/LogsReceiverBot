using DatabaseEvents;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Prism.Events;

namespace DataAdapter.Controllers
{
    public class ManualCheckController
    {
        public static async Task<List<ManualCheckModel>> GetChecksAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.ManualChecks.ToListAsync();
            }
            catch (Exception)
            {
                return new List<ManualCheckModel>();
            }
        }

        public static async Task<ManualCheckModel> GetCheckByIdAsync(int id)
        {
            try
            {
                using DataContext data = new();
                return await data.ManualChecks.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<List<ManualCheckModel>> GetChecksByUserIdAsync(long id)
        {
            try
            {
                using DataContext data = new();
                return await data.ManualChecks.Where(m => m.FromUserId == id).ToListAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> PostCheckAsync(ManualCheckModel model, IEventAggregator aggregator)
        {
            try
            {
                using DataContext data = new();
                await data.ManualChecks.AddAsync(model);
                await data.SaveChangesAsync();
                aggregator.GetEvent<ManualCheckUpdateEvent>().Publish(new("post", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PutCheckAsync(ManualCheckModel model, IEventAggregator aggregator)
        {
            try
            {
                ManualCheckModel target = await GetCheckByIdAsync(model.Id);
                if (target == null) throw new Exception("Not found");

                using DataContext data = new();
                data.Entry(target).CurrentValues.SetValues(model);
                data.Update(target);
                await data.SaveChangesAsync();
                if (aggregator != null)
                {
                    aggregator.GetEvent<ManualCheckUpdateEvent>().Publish(new("put", model));
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<int> DeleteManualCheckAsync(ManualCheckModel model)
        {
            try
            {
                using DataContext data = new();
                data.ManualChecks.Remove(model);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}