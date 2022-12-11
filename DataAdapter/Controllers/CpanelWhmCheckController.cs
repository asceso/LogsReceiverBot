using DatabaseEvents;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Prism.Events;

namespace DataAdapter.Controllers
{
    public class CpanelWhmCheckController
    {
        public static async Task<List<CpanelWhmCheckModel>> GetChecksAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.CpanelWhmChecks.ToListAsync();
            }
            catch (Exception)
            {
                return new List<CpanelWhmCheckModel>();
            }
        }

        public static async Task<CpanelWhmCheckModel> GetCheckByIdAsync(int id)
        {
            try
            {
                using DataContext data = new();
                return await data.CpanelWhmChecks.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<List<CpanelWhmCheckModel>> GetChecksByUserIdAsync(long id)
        {
            try
            {
                using DataContext data = new();
                return await data.CpanelWhmChecks.Where(m => m.FromUserId == id).ToListAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> PostCheckAsync(CpanelWhmCheckModel model, IEventAggregator aggregator)
        {
            try
            {
                using DataContext data = new();
                await data.CpanelWhmChecks.AddAsync(model);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<CpanelWhmCheckUpdateEvent>().Publish(new("post", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PutCheckAsync(CpanelWhmCheckModel model, IEventAggregator aggregator)
        {
            try
            {
                CpanelWhmCheckModel target = await GetCheckByIdAsync(model.Id);
                if (target == null) throw new Exception("Not found");

                using DataContext data = new();
                data.Entry(target).CurrentValues.SetValues(model);
                data.Update(target);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<CpanelWhmCheckUpdateEvent>().Publish(new("put", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<int> DeleteCheckAsync(CpanelWhmCheckModel model)
        {
            try
            {
                using DataContext data = new();
                data.CpanelWhmChecks.Remove(model);
                return await data.SaveChangesAsync();
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}