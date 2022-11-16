using Telegram.Bot.Types;

namespace Models.App
{
    public class TempTelegram
    {
        public long Uid { get; set; }
        public int MessageId { get; set; }
        public string Message { get; set; }
        public string ReplyMessage { get; set; }
        public string Callback { get; set; }
        public string Language { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Username { get; set; }
        public Document Document { get; set; }
        public OperationModel Operation { get; set; }

        public TempTelegram(Update update)
        {
            if (update.Message != null)
            {
                Firstname = update.Message.From.FirstName;
                Lastname = update.Message.From.LastName;
                Username = update.Message.From.Username;

                Uid = update.Message.From.Id;
                MessageId = update.Message.MessageId;
                Message = update.Message.Text;
                Document = update.Message.Document;
                Language = update.Message.From.LanguageCode;

                if (update.Message.ReplyToMessage != null)
                {
                    ReplyMessage = update.Message.ReplyToMessage.Text;
                }
            }
            if (update.CallbackQuery != null)
            {
                Firstname = update.CallbackQuery.From.FirstName;
                Lastname = update.CallbackQuery.From.LastName;
                Username = update.CallbackQuery.From.Username;

                Uid = update.CallbackQuery.From.Id;
                MessageId = update.CallbackQuery.Message.MessageId;
                Callback = update.CallbackQuery.Data;
                Language = update.Message.From.LanguageCode;
            }
        }
    }
}