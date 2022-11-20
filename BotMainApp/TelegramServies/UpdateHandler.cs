using BotMainApp.Events;
using BotMainApp.External;
using BotMainApp.LocalEvents;
using BotMainApp.ViewModels;
using DataAdapter.Controllers;
using Extensions;
using Models.App;
using Models.Database;
using Models.Enums;
using Newtonsoft.Json.Linq;
using Prism.Events;
using Services;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotMainApp.TelegramServices
{
    public class UpdateHandler : IUpdateHandler
    {
        private readonly ReplyKeyboardRemove emptyKeyboard = new();
        private TelegramBotClient botClient;
        private List<LocaleStringModel> locales;
        private Dictionary<string, ReplyKeyboardMarkup> keyboards;
        private ObservableCollection<OperationModel> operations;
        private readonly ConfigModel config;
        private readonly IEventAggregator aggregator;
        private readonly IMemorySaver memory;

        public UpdateHandler(IEventAggregator aggregator, IMemorySaver memory)
        {
            this.aggregator = aggregator;
            this.memory = memory;
            botClient = memory.GetItem<TelegramBotClient>("BotClient");
            locales = memory.GetItem<List<LocaleStringModel>>("Locales");
            keyboards = memory.GetItem<Dictionary<string, ReplyKeyboardMarkup>>("Keyboards");
            operations = memory.GetItem<ObservableCollection<OperationModel>>("Operations");
            config = memory.GetItem<ConfigModel>("Config");
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnRestartBot);
        }

        private void OnRestartBot() => botClient = memory.GetItem<TelegramBotClient>("BotClient");

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            #region catch data

            TempTelegram temp = new(update);
            temp.Operation = operations.FirstOrDefault(o => o.UserId == temp.Uid);
            UserModel dbUser = await UsersController.GetUserByIdAsync(temp.Uid);

            #endregion catch data

            #region operations

            if (temp.Operation != null)
            {
                string argument = temp.Message;
                if (argument.IsAnyEqual("Cancel 🚫", "Отмена 🚫"))
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CanceledOpertaion", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                    operations.Remove(temp.Operation);
                    return;
                }

                switch (temp.Operation.OperationType)
                {
                    case OperationType.NewUserWithoutNickname:
                        {
                            if (argument.StartsWith("/"))
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("NicknameWrong", dbUser.Language), replyMarkup: emptyKeyboard);
                                return;
                            }

                            UserModel existUser = await UsersController.GetUserByUsernameAsync(argument);
                            if (existUser is not null)
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("NicknameExist", dbUser.Language), replyMarkup: emptyKeyboard);
                                return;
                            }
                            else
                            {
                                dbUser.Username = argument;
                                if (await UsersController.PutUserAsync(dbUser, aggregator))
                                {
                                    await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("WelcomeNickname", temp.Language).Replace("@USERNAME", temp.Username), replyMarkup: emptyKeyboard);
                                    operations.Remove(temp.Operation);
                                    return;
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("DbError", temp.Language), replyMarkup: emptyKeyboard);
                                    return;
                                }
                            }
                        }
                    case OperationType.ChangeLanguage:
                        {
                            if (argument.IsAnyEqual("Russian 🇷🇺", "Русский 🇷🇺"))
                            {
                                await changeLanguageAsync("ru");
                                operations.Remove(temp.Operation);
                            }
                            else if (argument.IsAnyEqual("English 🇺🇸", "Английский 🇺🇸"))
                            {
                                await changeLanguageAsync("en");
                                operations.Remove(temp.Operation);
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PleaseSelectFromKeyboard", dbUser.Language));
                            }
                            return;

                            async Task changeLanguageAsync(string toLocale)
                            {
                                string currentLocale = dbUser.Language;
                                dbUser.Language = toLocale;
                                if (await UsersController.PutUserAsync(dbUser, aggregator))
                                {
                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("Start", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DbError", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", currentLocale));
                                }
                                return;
                            }
                        }
                    case OperationType.WaitCategoryForChecking:
                        {
                            if (argument.IsAnyEqual("Cpanel", "Whm"))
                            {
                                operations.Remove(temp.Operation);
                                operations.Add(new(temp.Uid, OperationType.WaitFileForChecking, new KeyValuePair<string, object>("Category", argument)));
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("SendFileInstruction", dbUser.Language), replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language));
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PleaseSelectFromKeyboard", dbUser.Language));
                                return;
                            }
                        }
                    case OperationType.WaitFileForChecking:
                        {
                            try
                            {
                                Telegram.Bot.Types.File file = await botClient.GetFileAsync(temp.Document.FileId);
                                string filename = "u_" + dbUser.Id + "_check_" + DateTime.Now.ToString("dd_MM_yyyyy_HH_mm_ss") + ".txt";
                                using FileStream stream = new(PathCollection.TempFolderPath + filename, FileMode.Create);
                                await botClient.DownloadFileAsync(file.FilePath, stream);
                                stream.Close();

                                operations.Remove(temp.Operation);
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileAcceptedWaitResult", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                                ManualCheckModel manualCheckModel = new()
                                {
                                    StartDateTime = DateTime.Now,
                                    Status = CheckStatus.ManualCheckStatus.Created,
                                    FromUserId = dbUser.Id,
                                    FromUsername = dbUser.Username,
                                };
                                await ManualCheckController.PostCheckAsync(manualCheckModel, aggregator);

                                ThreadStart threadStart = new(async () =>
                                {
                                    Stopwatch ellapsedWatch = Stopwatch.StartNew();
                                    Dictionary<string, string> jsonAnswer = new()
                                    {
                                        { "FolderPath", string.Empty },
                                        { "DublicateFilePath", string.Empty },
                                        { "UniqueFilePath", string.Empty },
                                        { "CpanelGoodFilePath", string.Empty },
                                        { "CpanelBadFilePath", string.Empty },
                                        { "WhmGoodFilePath", string.Empty },
                                        { "WhmBadFilePath", string.Empty }
                                    };

                                    string dublicateData = Runner.RunDublicateChecker(filename, dbUser.Id, config);
                                    JObject dublicateDataJson = JObject.Parse(dublicateData);
                                    if (dublicateDataJson.ContainsKey("Error"))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language));
                                            await botClient.SendTextMessageAsync(config.TelegramNotificationChat, $"Ошибка при поиске дубликатов файла:\r\n{dublicateData}");
                                        }
                                        catch (Exception)
                                        {
                                            ellapsedWatch.Stop();
                                            manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                            manualCheckModel.EndDateTime = DateTime.Now;
                                            manualCheckModel.Status = CheckStatus.ManualCheckStatus.Error;
                                            await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                        }
                                        return;
                                    }
                                    jsonAnswer["FolderPath"] = dublicateDataJson["FolderPath"].ToString();
                                    jsonAnswer["DublicateFilePath"] = dublicateDataJson["Dublicates"].ToString();
                                    jsonAnswer["UniqueFilePath"] = dublicateDataJson["Unique"].ToString();

                                    if (jsonAnswer["UniqueFilePath"] == "")
                                    {
                                        ellapsedWatch.Stop();
                                        manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                        manualCheckModel.EndDateTime = DateTime.Now;
                                        manualCheckModel.Status = CheckStatus.ManualCheckStatus.Error;
                                        await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                        return;
                                    }

                                    manualCheckModel.Status = CheckStatus.ManualCheckStatus.DublicateDeleted;
                                    manualCheckModel.DublicateFilePath = jsonAnswer["DublicateFilePath"];
                                    manualCheckModel.UniqueFilePath = jsonAnswer["UniqueFilePath"];
                                    if (System.IO.File.Exists(manualCheckModel.DublicateFilePath))
                                        manualCheckModel.DublicateFoundedCount = System.IO.File.ReadAllLines(manualCheckModel.DublicateFilePath).Where(l => !l.IsNullOrEmpty()).Count();
                                    if (System.IO.File.Exists(manualCheckModel.UniqueFilePath))
                                        manualCheckModel.UniqueFoundedCount = System.IO.File.ReadAllLines(manualCheckModel.UniqueFilePath).Where(l => !l.IsNullOrEmpty()).Count();
                                    await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);

                                    string cpanelData = Runner.RunCpanelChecker(jsonAnswer["FolderPath"], jsonAnswer["UniqueFilePath"], dbUser.Id, temp.Operation.Params["Category"].ToString().ToLower());
                                    JObject cpanelDataJson = JObject.Parse(cpanelData);
                                    if (cpanelDataJson.ContainsKey("Error"))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language));
                                            await botClient.SendTextMessageAsync(config.TelegramNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                                        }
                                        catch (Exception)
                                        {
                                            ellapsedWatch.Stop();
                                            manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                            manualCheckModel.EndDateTime = DateTime.Now;
                                            manualCheckModel.Status = CheckStatus.ManualCheckStatus.Error;
                                            await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                        }
                                        return;
                                    }

                                    jsonAnswer["CpanelGoodFilePath"] = cpanelDataJson["CpanelGood"].ToString();
                                    jsonAnswer["CpanelBadFilePath"] = cpanelDataJson["CpanelBad"].ToString();
                                    jsonAnswer["WhmGoodFilePath"] = cpanelDataJson["WhmGood"].ToString();
                                    jsonAnswer["WhmBadFilePath"] = cpanelDataJson["WhmBad"].ToString();
                                    ;
                                    ellapsedWatch.Stop();

                                    manualCheckModel.Status = CheckStatus.ManualCheckStatus.CopyingFiles;
                                    manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                    manualCheckModel.UniqueFilePath = jsonAnswer["UniqueFilePath"];
                                    manualCheckModel.DublicateFilePath = jsonAnswer["DublicateFilePath"];
                                    manualCheckModel.CpanelFilePath = jsonAnswer["CpanelGoodFilePath"];
                                    manualCheckModel.WhmFilePath = jsonAnswer["WhmGoodFilePath"];
                                    await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                    MoveFilesToChecksIdFolderAndUpatePathes(manualCheckModel, jsonAnswer["FolderPath"]);
                                    return;
                                });
                                Thread checkThread = new(threadStart);
                                checkThread.Start();
                            }
                            catch (Exception)
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUploadError", dbUser.Language));
                            }
                            return;
                        }
                }
            }

            #endregion operations

            #region first visit user

            if (dbUser == null)
            {
                if (temp.Username.IsNullOrEmpty())
                {
                    if (await UsersController.PostUserAsync(new()
                    {
                        Id = temp.Uid,
                        Firstname = temp.Firstname,
                        Lastname = temp.Lastname,
                        Username = "",
                        IsBanned = false,
                        IsAccepted = false,
                        RegistrationDate = DateTime.Now,
                        Language = temp.Language
                    }, aggregator))
                    {
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("WelcomeNoName", temp.Language), replyMarkup: emptyKeyboard);
                        operations.Add(new(temp.Uid, OperationType.NewUserWithoutNickname));
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("DbError", temp.Language), replyMarkup: emptyKeyboard);
                        return;
                    }
                }
                else
                {
                    if (await UsersController.PostUserAsync(new()
                    {
                        Id = temp.Uid,
                        Firstname = temp.Firstname,
                        Lastname = temp.Lastname,
                        Username = temp.Username,
                        IsBanned = false,
                        IsAccepted = false,
                        RegistrationDate = DateTime.Now,
                        Language = temp.Language
                    }, aggregator))
                    {
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("WelcomeNickname", temp.Language).Replace("@USERNAME", temp.Username), replyMarkup: emptyKeyboard);
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("DbError", temp.Language), replyMarkup: emptyKeyboard);
                        return;
                    }
                }
            }
            if (dbUser.IsBanned)
            {
                return;
            }
            if (!dbUser.IsAccepted)
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("NotAccepted", dbUser.Language), replyMarkup: emptyKeyboard);
                return;
            }

            #endregion first visit user

            if (temp.Message == "/start")
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("Start", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                return;
            }
            if (temp.Message.IsAnyEqual("Language 🌎", "Язык 🌎"))
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("SelectNewLanguage", dbUser.Language), replyMarkup: keyboards.GetByLocale("SelectLanguage", dbUser.Language));
                operations.Add(new(temp.Uid, OperationType.ChangeLanguage));
                return;
            }
            if (temp.Message.IsAnyEqual("Upload file 📄", "Загрузить файл 📄"))
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("SelectCheckerCategory", dbUser.Language), replyMarkup: keyboards.GetByLocale("SelectCategory", dbUser.Language));
                operations.Add(new(temp.Uid, OperationType.WaitCategoryForChecking));
                return;
            }
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("ошибка", TelegramStateModel.RedBrush));
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            botClient.StartReceiving(this);
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("работает", TelegramStateModel.GreenBrush));
        }

        private async void MoveFilesToChecksIdFolderAndUpatePathes(ManualCheckModel manualCheck, string sourceFolderPath)
        {
            string destinationFolderPath = PathCollection.ChecksFolderPath + manualCheck.Id + "/";
            Directory.CreateDirectory(destinationFolderPath);

            string uniqueFilePath = destinationFolderPath + "unique.txt";
            string dublicatesFilePath = destinationFolderPath + "dublicates.txt";
            string cpanelFilePath = destinationFolderPath + "cpanel.txt";
            string whmFilePath = destinationFolderPath + "whm.txt";

            if (System.IO.File.Exists(manualCheck.UniqueFilePath))
            {
                System.IO.File.Copy(manualCheck.UniqueFilePath, uniqueFilePath);
                manualCheck.UniqueFilePath = uniqueFilePath;
                manualCheck.UniqueFoundedCount = System.IO.File.ReadAllLines(manualCheck.UniqueFilePath).Where(l => !l.IsNullOrEmpty()).Count();
            }
            if (System.IO.File.Exists(manualCheck.DublicateFilePath))
            {
                System.IO.File.Copy(manualCheck.DublicateFilePath, dublicatesFilePath);
                manualCheck.DublicateFilePath = dublicatesFilePath;
                manualCheck.DublicateFoundedCount = System.IO.File.ReadAllLines(manualCheck.DublicateFilePath).Where(l => !l.IsNullOrEmpty()).Count();
            }
            if (System.IO.File.Exists(manualCheck.CpanelFilePath))
            {
                System.IO.File.Copy(manualCheck.CpanelFilePath, cpanelFilePath);
                manualCheck.CpanelFilePath = cpanelFilePath;
                manualCheck.CpFoundedCount = System.IO.File.ReadAllLines(manualCheck.CpanelFilePath).Where(l => !l.IsNullOrEmpty()).Count();
            }
            if (System.IO.File.Exists(manualCheck.WhmFilePath))
            {
                System.IO.File.Copy(manualCheck.WhmFilePath, whmFilePath);
                manualCheck.WhmFilePath = whmFilePath;
                manualCheck.WhmFoundedCount = System.IO.File.ReadAllLines(manualCheck.WhmFilePath).Where(l => !l.IsNullOrEmpty()).Count();
            }
            Directory.Delete(sourceFolderPath, true);
            manualCheck.Status = CheckStatus.ManualCheckStatus.CheckedBySoft;
            await ManualCheckController.PutCheckAsync(manualCheck, aggregator);
        }

        public async Task<bool> AcceptTelegramUserAsync(UserModel dbUser)
        {
            try
            {
                dbUser.IsAccepted = true;
                if (await UsersController.PutUserAsync(dbUser, aggregator))
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminAcceptNotify", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> MoveToBLUserAsync(UserModel dbUser)
        {
            if (dbUser.IsBanned)
            {
                return false;
            }
            dbUser.IsBanned = true;
            if (await UsersController.PutUserAsync(dbUser, aggregator))
            {
                List<OperationModel> userOperations = operations.Where(u => u.UserId == dbUser.Id).ToList();
                foreach (OperationModel operation in userOperations)
                {
                    operations.Remove(operation);
                }
                try
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminMoveToBlackList", dbUser.Language), replyMarkup: emptyKeyboard);
                }
                catch (Exception)
                {
                }
                return true;
            }
            return false;
        }

        public async Task<bool> MoveFromBLUserAsync(UserModel dbUser)
        {
            if (!dbUser.IsBanned)
            {
                return false;
            }
            dbUser.IsBanned = false;
            if (await UsersController.PutUserAsync(dbUser, aggregator))
            {
                try
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminMoveFromBlackList", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                }
                catch (Exception)
                {
                }
                return true;
            }
            return false;
        }

        public async Task<int> MoveUsersToBL(List<UserModel> users)
        {
            List<UserModel> updateUsers = users.Where(u => !u.IsBanned).ToList();
            foreach (var user in updateUsers)
            {
                user.IsBanned = true;
            }
            List<UserModel> updatedUsersFromDb = await UsersController.PutUsersAsync(updateUsers, aggregator);
            foreach (var user in updatedUsersFromDb)
            {
                List<OperationModel> userOperations = operations.Where(u => u.UserId == user.Id).ToList();
                foreach (OperationModel operation in userOperations)
                {
                    operations.Remove(operation);
                }
                try
                {
                    await botClient.SendTextMessageAsync(user.Id, locales.GetByKey("AdminMoveToBlackList", user.Language), replyMarkup: emptyKeyboard);
                }
                catch (Exception)
                {
                }
            }
            return updatedUsersFromDb.Count;
        }

        public async Task<int> MoveUsersFromBL(List<UserModel> users)
        {
            List<UserModel> updateUsers = users.Where(u => u.IsBanned).ToList();
            foreach (var user in updateUsers)
            {
                user.IsBanned = false;
            }
            List<UserModel> updatedUsersFromDb = await UsersController.PutUsersAsync(updateUsers, aggregator);
            foreach (var user in updatedUsersFromDb)
            {
                try
                {
                    await botClient.SendTextMessageAsync(user.Id, locales.GetByKey("AdminMoveFromBlackList", user.Language), replyMarkup: keyboards.GetByLocale("Main", user.Language));
                }
                catch (Exception)
                {
                }
            }
            return updatedUsersFromDb.Count;
        }

        public async Task<bool> SendMailToUserAsync(UserModel dbUser, string mail)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, mail);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task SendBalanceInfoToUser(UserModel dbUser, bool isPositiveBalance)
        {
            try
            {
                if (isPositiveBalance)
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminUpBalanceNotification", dbUser.Language)
                                                                           .Replace("@BALANCE", dbUser.Balance.ToString())
                                                                           .Replace("@CURRENCY", config.Currency),
                                                                           replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                }
                else
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminDownBalanceNotification", dbUser.Language)
                                                                           .Replace("@BALANCE", dbUser.Balance.ToString())
                                                                           .Replace("@CURRENCY", config.Currency),
                                                                           replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                }
            }
            catch (Exception)
            {
            }
        }
    }
}