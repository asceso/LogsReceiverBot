using Microsoft.EntityFrameworkCore;
using Models.Database;

namespace DataAdapter.Controllers
{
    public class UsersController
    {
        public static async Task<List<UserModel>> GetUsersAsync()
        {
            try
            {
                using DataContext data = new();
                return await data.Users.ToListAsync();
            }
            catch (Exception)
            {
                return new List<UserModel>();
            }
        }

        public static async Task<UserModel> GetUserByIdAsync(long id)
        {
            try
            {
                using DataContext data = new();
                return await data.Users.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<UserModel> GetUserByUsernameAsync(string username)
        {
            try
            {
                using DataContext data = new();
                return await data.Users.FirstOrDefaultAsync(m => m.Username == username);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> PostUserAsync(UserModel model)
        {
            try
            {
                using DataContext data = new();
                await data.Users.AddAsync(model);
                await data.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> PutUserAsync(UserModel model)
        {
            try
            {
                UserModel target = await GetUserByIdAsync(model.Id);
                if (target == null) throw new Exception("Not found");

                using DataContext data = new();
                data.Entry(target).CurrentValues.SetValues(model);
                data.Update(target);
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