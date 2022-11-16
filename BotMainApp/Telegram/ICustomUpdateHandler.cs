using Models.Database;
using System.Threading.Tasks;
using Telegram.Bot.Extensions.Polling;

namespace BotMainApp.Telegram
{
    public interface ICustomUpdateHandler : IUpdateHandler
    {
        Task<bool> AcceptTelegramUserAsync(UserModel dbUser);

        Task MoveToBLUserAsync(UserModel dbUser);

        Task MoveFromBLUserAsync(UserModel dbUser);
    }
}