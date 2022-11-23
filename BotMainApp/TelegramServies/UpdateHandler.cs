using BotMainApp.Events;
using BotMainApp.External;
using BotMainApp.LocalEvents;
using BotMainApp.ViewModels;
using DataAdapter.Controllers;
using DatabaseEvents;
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
                            if (argument.IsAnyEqual("Private requests", "Приватные запросы"))
                            {
                                operations.Remove(temp.Operation);
                                operations.Add(new(temp.Uid, OperationType.WaitSubCategoryForChecking, new KeyValuePair<string, object>("Category", argument)));
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("SelectCheckerCategory", dbUser.Language),
                                    replyMarkup: keyboards.GetByLocale("SelectSubCategory", dbUser.Language)
                                    );
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PleaseSelectFromKeyboard", dbUser.Language));
                                return;
                            }
                        }
                    case OperationType.WaitSubCategoryForChecking:
                        {
                            if (argument.IsAnyEqual(":20"))
                            {
                                operations.Remove(temp.Operation);
                                operations.Add(new(temp.Uid, OperationType.WaitFileForChecking,
                                    new KeyValuePair<string, object>("Category", temp.Operation.Params["Category"].ToString()),
                                    new KeyValuePair<string, object>("SubCategory", argument)
                                    ));
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
                                if (temp.Operation.Params["Category"].ToString().IsAnyEqual("Private requests", "Приватные запросы") &&
                                    temp.Operation.Params["SubCategory"].ToString().IsAnyEqual(":20"))
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
                                        string inputFilename = PathCollection.TempFolderPath + "/" + filename;
                                        string folderPath = PathCollection.TempFolderPath + $"u_{dbUser.Id}_d_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}/";
                                        Directory.CreateDirectory(folderPath);
                                        Stopwatch ellapsedWatch = Stopwatch.StartNew();

                                        string dublicateData = Runner.RunDublicateChecker(folderPath, inputFilename, config);
                                        JObject dublicateDataJson = JObject.Parse(dublicateData);
                                        if (dublicateDataJson.ContainsKey("Error"))
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language));
                                                await botClient.SendTextMessageAsync(config.TelegramNotificationChat, $"Ошибка при поиске дубликатов файла:\r\n{dublicateData}");
                                            }
                                            catch (Exception ex)
                                            {
                                                ellapsedWatch.Stop();
                                                manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                                manualCheckModel.EndDateTime = DateTime.Now;
                                                manualCheckModel.Status = CheckStatus.ManualCheckStatus.Error;
                                                Directory.Delete(folderPath, true);
                                                await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(config.ErrorNotificationChat, $"Ошибка в процессе проверки:\r\n{ex.Message}");
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }
                                            return;
                                        }

                                        manualCheckModel.DublicateFilePath = dublicateDataJson["Dublicates"].ToString();
                                        if (System.IO.File.Exists(manualCheckModel.DublicateFilePath))
                                        {
                                            manualCheckModel.DublicateFoundedCount = System.IO.File.ReadAllLines(manualCheckModel.DublicateFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
                                        }
                                        manualCheckModel.WebmailFilePath = dublicateDataJson["Webmail"].ToString();
                                        if (System.IO.File.Exists(manualCheckModel.WebmailFilePath))
                                        {
                                            manualCheckModel.WebmailFoundedCount = System.IO.File.ReadAllLines(manualCheckModel.WebmailFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
                                        }
                                        string cpanelDataFilePath = dublicateDataJson["Cpanel"].ToString();
                                        string whmDataFilePath = dublicateDataJson["Whm"].ToString();

                                        manualCheckModel.Status = CheckStatus.ManualCheckStatus.FillingDb;
                                        await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);

                                        string fillData = Runner.RunDublicateFiller(dbUser.Id, manualCheckModel.WebmailFilePath, cpanelDataFilePath, whmDataFilePath);
                                        JObject fillDataJson = JObject.Parse(fillData);
                                        if (fillDataJson.ContainsKey("Error"))
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language));
                                                await botClient.SendTextMessageAsync(config.TelegramNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                                            }
                                            catch (Exception ex)
                                            {
                                                ellapsedWatch.Stop();
                                                manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                                manualCheckModel.EndDateTime = DateTime.Now;
                                                manualCheckModel.Status = CheckStatus.ManualCheckStatus.Error;
                                                Directory.Delete(folderPath, true);
                                                await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(config.ErrorNotificationChat, $"Ошибка в процессе проверки:\r\n{ex.Message}");
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }
                                            return;
                                        }

                                        int webmailAddedCount = fillDataJson.Value<int>("WebmailAddedCount");
                                        int cpanelAddedCount = fillDataJson.Value<int>("CpanelAddedCount");
                                        int whmAddedCount = fillDataJson.Value<int>("WhmAddedCount");
                                        int totalAddedCount = webmailAddedCount + cpanelAddedCount + whmAddedCount;
                                        if (totalAddedCount == 0)
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUniqueEmptyError", dbUser.Language));
                                            ellapsedWatch.Stop();
                                            manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                            manualCheckModel.EndDateTime = DateTime.Now;
                                            manualCheckModel.Status = CheckStatus.ManualCheckStatus.NoAnyUnique;
                                            Directory.Delete(folderPath, true);
                                            await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                            return;
                                        }

                                        if (config.NotifyWhenDatabaseFillNewLogRecordsChat != 0)
                                        {
                                            try
                                            {
                                                aggregator.GetEvent<LogUpdateEvent>().Publish();
                                                await botClient.SendTextMessageAsync(config.NotifyWhenDatabaseFillNewLogRecordsChat,
                                                    $"В базу данных было добавлено {totalAddedCount} записей:\r\n" +
                                                    $"Новый записей в категории webmail: {webmailAddedCount}\r\n" +
                                                    $"Новый записей в категории cpanel: {cpanelAddedCount}\r\n" +
                                                    $"Новый записей в категории whm: {whmAddedCount}\r\n");
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        }

                                        if (cpanelDataFilePath == "" && whmDataFilePath == "")
                                        {
                                            ellapsedWatch.Stop();
                                            manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                            manualCheckModel.EndDateTime = DateTime.Now;
                                            manualCheckModel.Status = CheckStatus.ManualCheckStatus.NoAnyUnique;
                                            Directory.Delete(folderPath, true);
                                            await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                            return;
                                        }

                                        manualCheckModel.Status = CheckStatus.ManualCheckStatus.DublicateDeleted;
                                        await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);

                                        string cpanelData;
                                        if (config.UseOwnCpanelChecker)
                                        {
                                            cpanelData = Runner.RunOwnCpanelChecker(cpanelDataFilePath, whmDataFilePath, folderPath, config.CheckerMaxForThread);
                                        }
                                        else
                                        {
                                            cpanelData = await Runner.RunCpanelCheckerAsync(folderPath, cpanelDataFilePath, whmDataFilePath, config.CheckerMaxForThread);
                                        }
                                        JObject cpanelDataJson = JObject.Parse(cpanelData);
                                        if (cpanelDataJson.ContainsKey("Error"))
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language));
                                                await botClient.SendTextMessageAsync(config.TelegramNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                                            }
                                            catch (Exception ex)
                                            {
                                                ellapsedWatch.Stop();
                                                manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                                manualCheckModel.EndDateTime = DateTime.Now;
                                                manualCheckModel.Status = CheckStatus.ManualCheckStatus.Error;
                                                Directory.Delete(folderPath, true);
                                                await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(config.ErrorNotificationChat, $"Ошибка в процессе проверки:\r\n{ex.Message}");
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }
                                            return;
                                        }

                                        ellapsedWatch.Stop();
                                        manualCheckModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                                        manualCheckModel.Status = CheckStatus.ManualCheckStatus.CopyingFiles;
                                        await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);

                                        manualCheckModel.CpanelGoodFilePath = cpanelDataJson["CpanelGood"].ToString();
                                        manualCheckModel.CpanelBadFilePath = cpanelDataJson["CpanelBad"].ToString();
                                        manualCheckModel.WhmGoodFilePath = cpanelDataJson["WhmGood"].ToString();
                                        manualCheckModel.WhmBadFilePath = cpanelDataJson["WhmBad"].ToString();

                                        await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                        MoveFilesToChecksIdFolderAndUpatePathes(manualCheckModel, folderPath);
                                        if (config.NotifyWhenCheckerEndWorkChat != 0)
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(config.NotifyWhenCheckerEndWorkChat,
                                                    $"Закончена проверка файла №{manualCheckModel.Id} от {manualCheckModel.StartDateTime:dd.MM.yyyy} :\r\n" +
                                                    $"Загрузил : @{manualCheckModel.FromUsername}\r\n" +
                                                    $"Затрачено всего : {Math.Round(manualCheckModel.CheckingTimeEllapsed.TotalMinutes, 2)} минут\r\n" +
                                                    $"Дубликатов найдено: {manualCheckModel.DublicateFoundedCount}\r\n" +
                                                    $"Webmail найдено: {manualCheckModel.WebmailFoundedCount}\r\n" +
                                                    $"Cpanel (good) найдено: {manualCheckModel.CpanelGoodCount}\r\n" +
                                                    $"Cpanel (bad) найдено: {manualCheckModel.CpanelBadCount}\r\n" +
                                                    $"Whm (good) найдено: {manualCheckModel.WhmGoodCount}\r\n" +
                                                    $"Whm (bad) найдено: {manualCheckModel.WhmBadCount}");
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        }
                                        return;
                                    });
                                    Thread checkThread = new(threadStart);
                                    checkThread.Start();
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CanceledOpertaion", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
                                    operations.Remove(temp.Operation);
                                    return;
                                }
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
                if (temp.Username.IsNullOrEmptyString())
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
            if (temp.Message.IsAnyEqual("Upload logs 📄", "Загрузить логи 📄"))
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
            if (Directory.Exists(destinationFolderPath)) Directory.Delete(destinationFolderPath, true);
            Directory.CreateDirectory(destinationFolderPath);

            string cpanelGoodPath = destinationFolderPath + "cpanel_good.txt";
            string cpanelBadPath = destinationFolderPath + "cpanel_bad.txt";
            string whmGoodPath = destinationFolderPath + "whm_good.txt";
            string whmBadPath = destinationFolderPath + "whm_bad.txt";

            if (System.IO.File.Exists(manualCheck.CpanelGoodFilePath))
            {
                System.IO.File.Copy(manualCheck.CpanelGoodFilePath, cpanelGoodPath, true);
                manualCheck.CpanelGoodFilePath = cpanelGoodPath;
                manualCheck.CpanelGoodCount = System.IO.File.ReadAllLines(manualCheck.CpanelGoodFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.CpanelBadFilePath))
            {
                System.IO.File.Copy(manualCheck.CpanelBadFilePath, cpanelBadPath, true);
                manualCheck.CpanelBadFilePath = cpanelBadPath;
                manualCheck.CpanelBadCount = System.IO.File.ReadAllLines(manualCheck.CpanelBadFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.WhmGoodFilePath))
            {
                System.IO.File.Copy(manualCheck.WhmGoodFilePath, whmGoodPath, true);
                manualCheck.WhmGoodFilePath = whmGoodPath;
                manualCheck.WhmGoodCount = System.IO.File.ReadAllLines(manualCheck.WhmGoodFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.WhmBadFilePath))
            {
                System.IO.File.Copy(manualCheck.WhmBadFilePath, whmBadPath, true);
                manualCheck.WhmBadFilePath = whmBadPath;
                manualCheck.WhmBadCount = System.IO.File.ReadAllLines(manualCheck.WhmBadFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
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