using Extensions;
using Microsoft.EntityFrameworkCore;
using Models.Database;

namespace DataAdapter.Controllers
{
    public class ValidController
    {
        public static async Task<List<ValidModel>> GetValidAsync(UserModel user, string category = null)
        {
            try
            {
                using DataContext data = new();
                if (user.Id != 0 && category.IsNullOrEmptyString())
                {
                    return await data.Valid.Where(d => d.UploadedByUserId == user.Id).ToListAsync();
                }
                if (user.Id == 0 && !category.IsNullOrEmptyString())
                {
                    return await data.Valid.Where(d => d.Category == category).ToListAsync();
                }
                if (user.Id != 0 && !category.IsNullOrEmptyString())
                {
                    return await data.Valid.Where(d => d.UploadedByUserId == user.Id && d.Category == category).ToListAsync();
                }
                else
                {
                    return await data.Valid.ToListAsync();
                }
            }
            catch (Exception)
            {
                return new List<ValidModel>();
            }
        }

        public static List<string> GetValidCategories()
        {
            try
            {
                using DataContext data = new();
                var logsData = from l in data.Valid
                               select l.Category;
                return logsData.Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static async Task<bool> PostValidAsync(ValidModel model)
        {
            try
            {
                using DataContext data = new();
                await data.Valid.AddAsync(model);
                await data.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}