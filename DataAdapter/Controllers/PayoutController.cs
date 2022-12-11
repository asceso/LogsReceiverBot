using DatabaseEvents;
using Microsoft.EntityFrameworkCore;
using Models.Database;
using Prism.Events;

namespace DataAdapter.Controllers
{
    public class PayoutController
    {
        public static async Task<List<PayoutModel>> GetPayoutsAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.Payouts.ToListAsync();
            }
            catch (Exception)
            {
                return new List<PayoutModel>();
            }
        }

        public static async Task<PayoutModel> GetByIdAsync(int id)
        {
            try
            {
                using DataContext data = new();
                return await data.Payouts.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<List<PayoutModel>> GetByUserIdAsync(long id)
        {
            try
            {
                using DataContext data = new();
                return await data.Payouts.Where(m => m.FromUserId == id).ToListAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> PostPayoutAsync(PayoutModel model, IEventAggregator aggregator)
        {
            try
            {
                using DataContext data = new();
                await data.Payouts.AddAsync(model);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<PayoutUpdateEvent>().Publish(new("post", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PutPayoutAsync(PayoutModel model, IEventAggregator aggregator)
        {
            try
            {
                PayoutModel target = await GetByIdAsync(model.Id);
                if (target == null) throw new Exception("Not found");

                using DataContext data = new();
                data.Entry(target).CurrentValues.SetValues(model);
                data.Update(target);
                await data.SaveChangesAsync();
                aggregator?.GetEvent<PayoutUpdateEvent>().Publish(new("put", model));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}