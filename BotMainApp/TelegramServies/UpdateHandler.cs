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
using System.Text;
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
        private readonly List<Tuple<Guid, long>> userRequests;
        private readonly Dictionary<long, string> userCaptchas;
        private readonly Dictionary<long, int> userCaptchasAttempts;
        private readonly ConfigModel config;
        private readonly IEventAggregator aggregator;
        private readonly IMemorySaver memory;
        private readonly ICaptchaService captcha;

        public UpdateHandler(IEventAggregator aggregator, IMemorySaver memory, ICaptchaService captcha)
        {
            userRequests = new();
            userCaptchas = new();
            userCaptchasAttempts = new();
            this.aggregator = aggregator;
            this.memory = memory;
            this.captcha = captcha;
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
            if (userRequests.Count(ur => ur.Item2 == temp.Uid) >= config.RequestsPerMinuteAutoBan)
            {
                await UsersController.PostUserAsync(new()
                {
                    Id = temp.Uid,
                    Firstname = temp.Firstname,
                    Lastname = temp.Lastname,
                    Username = temp.Username,
                    IsBanned = true,
                    IsAccepted = false,
                    RegistrationDate = DateTime.Now,
                    Language = temp.Language,
                    BanReason = "request per minute auto ban"
                }, aggregator);
                return;
            }
            Thread requestsThread = new(new ThreadStart(async () =>
            {
                Tuple<Guid, long> reqTuple = new(Guid.NewGuid(), temp.Uid);
                userRequests.Add(reqTuple);
                await Task.Delay(TimeSpan.FromMinutes(1));
                userRequests.Remove(reqTuple);
            }));
            requestsThread.Start();
            temp.Operation = operations.FirstOrDefault(o => o.UserId == temp.Uid);
            UserModel dbUser = await UsersController.GetUserByIdAsync(temp.Uid);

            #endregion catch data

            #region first visit user

            if (dbUser == null)
            {
                if (userCaptchas.ContainsKey(temp.Uid) && userCaptchasAttempts.ContainsKey(temp.Uid))
                {
                    if (userCaptchas[temp.Uid] != temp.Message)
                    {
                        if (++userCaptchasAttempts[temp.Uid] >= config.CaptchaAttemptNum)
                        {
                            await UsersController.PostUserAsync(new()
                            {
                                Id = temp.Uid,
                                Firstname = temp.Firstname,
                                Lastname = temp.Lastname,
                                Username = temp.Username,
                                IsBanned = true,
                                IsAccepted = false,
                                RegistrationDate = DateTime.Now,
                                Language = temp.Language,
                                BanReason = "captcha wrong code max attempts"
                            }, aggregator);
                            userCaptchas.Remove(temp.Uid);
                            userCaptchasAttempts.Remove(temp.Uid);
                            return;
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(
                                temp.Uid,
                                locales.GetByKey("EnterCaptchaWrongCode",
                                temp.Language),
                                replyMarkup: emptyKeyboard
                                );
                            return;
                        }
                    }
                    else
                    {
                        userCaptchas.Remove(temp.Uid);
                        userCaptchasAttempts.Remove(temp.Uid);
                    }
                }
                else
                {
                    Tuple<string, string> captchaData = captcha.CreateCaptchaForUser();
                    userCaptchas.Add(temp.Uid, captchaData.Item1);
                    userCaptchasAttempts.Add(temp.Uid, 0);
                    FileStream fs = new(captchaData.Item2, FileMode.Open);
                    await botClient.SendPhotoAsync(
                        temp.Uid,
                        new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs),
                        locales.GetByKey("EnterCaptcha",
                        temp.Language),
                        replyMarkup: emptyKeyboard
                        );
                    fs.Close();
                    if (System.IO.File.Exists(captchaData.Item2))
                    {
                        System.IO.File.Delete(captchaData.Item2);
                    }
                    Thread deleteCaptchaThread = new(new ThreadStart(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(config.CaptchaTimer));
                        if (userCaptchas.ContainsKey(temp.Uid))
                        {
                            userCaptchas.Remove(temp.Uid);
                        }
                        if (userCaptchasAttempts.ContainsKey(temp.Uid))
                        {
                            userCaptchasAttempts.Remove(temp.Uid);
                        }
                    }));
                    deleteCaptchaThread.Start();
                    return;
                }

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
            if (temp.Operation != null && temp.Operation.OperationType == OperationType.NewUserWithoutNickname)
            {
                string argument = temp.Message;
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
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("WelcomeNickname", temp.Language).Replace("@USERNAME", argument), replyMarkup: emptyKeyboard);
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
            if (dbUser.IsBanned)
            {
                return;
            }
            if (!dbUser.IsAccepted)
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("NotAccepted", dbUser.Language), replyMarkup: emptyKeyboard);
                return;
            }

            bool payoutEnabled = dbUser.Balance >= config.MinBalanceForPayment;

            #endregion first visit user

            #region operations

            if (temp.Operation != null)
            {
                string argument = temp.Message;
                if (argument.IsAnyEqual("Cancel 🚫", "Отмена 🚫", "/cancel"))
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CanceledOpertaion", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled));
                    operations.Remove(temp.Operation);
                    return;
                }

                switch (temp.Operation.OperationType)
                {
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
                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("Start", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled));
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DbError", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", currentLocale, payoutEnabled));
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
                                    replyMarkup: keyboards.GetByLocale("SelectSubCategory", dbUser.Language, payoutEnabled)
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
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("SendFileInstruction", dbUser.Language),
                                    replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                                    );
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
                                    #region load tg file

                                    Telegram.Bot.Types.File file = await botClient.GetFileAsync(temp.Document.FileId);
                                    string filename = "u_" + dbUser.Id + "_check_" + DateTime.Now.ToString("dd_MM_yyyyy_HH_mm_ss") + ".txt";
                                    using FileStream stream = new(PathCollection.TempFolderPath + filename, FileMode.Create);
                                    await botClient.DownloadFileAsync(file.FilePath, stream);
                                    stream.Close();

                                    #endregion load tg file

                                    #region accept file

                                    operations.Remove(temp.Operation);
                                    ManualCheckModel manualCheckModel = new()
                                    {
                                        StartDateTime = DateTime.Now,
                                        Status = CheckStatus.ManualCheckStatus.Created,
                                        FromUserId = dbUser.Id,
                                        FromUsername = dbUser.Username,
                                    };
                                    await ManualCheckController.PostCheckAsync(manualCheckModel, aggregator);
                                    await botClient.SendTextMessageAsync(
                                        dbUser.Id,
                                        locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                               .Replace("@ID", manualCheckModel.Id.ToString()),
                                        replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                        );

                                    #endregion accept file

                                    ThreadStart threadStart = new(async () =>
                                    {
                                        #region init

                                        string inputFilename = PathCollection.TempFolderPath + "/" + filename;
                                        string folderPath = PathCollection.TempFolderPath + $"u_{dbUser.Id}_d_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}/";
                                        Directory.CreateDirectory(folderPath);
                                        Stopwatch ellapsedWatch = Stopwatch.StartNew();

                                        #endregion init

                                        #region check dublicates

                                        string dublicateData = Runner.RunDublicateChecker(folderPath, inputFilename, config);
                                        JObject dublicateDataJson = JObject.Parse(dublicateData);
                                        if (dublicateDataJson.ContainsKey("Error"))
                                        {
                                            if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                                            {
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                                                           .Replace("@ID", manualCheckModel.Id.ToString()));
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }

                                            MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.Error, ellapsedWatch);
                                            Directory.Delete(folderPath, true);
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при поиске дубликатов файла:\r\n{dublicateData}");
                                            }
                                            catch (Exception)
                                            {
                                            }
                                            return;
                                        }

                                        manualCheckModel.DublicateFilePath = dublicateDataJson["Dublicates"].ToString();
                                        manualCheckModel.WebmailFilePath = dublicateDataJson["Webmail"].ToString();
                                        string cpanelDataFilePath = dublicateDataJson["Cpanel"].ToString();
                                        string whmDataFilePath = dublicateDataJson["Whm"].ToString();
                                        MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.FillingDb, null);

                                        #endregion check dublicates

                                        #region fill logs db

                                        string fillData = Runner.RunDublicateFiller(dbUser.Id, manualCheckModel.WebmailFilePath, cpanelDataFilePath, whmDataFilePath);
                                        JObject fillDataJson = JObject.Parse(fillData);
                                        if (fillDataJson.ContainsKey("Error"))
                                        {
                                            if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                                            {
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                                                           .Replace("@ID", manualCheckModel.Id.ToString()));
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                                            }
                                            catch (Exception)
                                            {
                                            }

                                            MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.Error, ellapsedWatch);
                                            Directory.Delete(folderPath, true);
                                            return;
                                        }

                                        #endregion fill logs db

                                        #region no any unique

                                        int webmailAddedCount = fillDataJson.Value<int>("WebmailAddedCount");
                                        int cpanelAddedCount = fillDataJson.Value<int>("CpanelAddedCount");
                                        int whmAddedCount = fillDataJson.Value<int>("WhmAddedCount");
                                        int totalAddedCount = webmailAddedCount + cpanelAddedCount + whmAddedCount;
                                        if (totalAddedCount == 0)
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUniqueEmptyError", dbUser.Language)
                                                                                                   .Replace("@ID", manualCheckModel.Id.ToString()));

                                            MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.NoAnyUnique, ellapsedWatch);
                                            Directory.Delete(folderPath, true);
                                            return;
                                        }

                                        #endregion no any unique

                                        #region notify when any inserted

                                        if (config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat != 0)
                                        {
                                            try
                                            {
                                                aggregator.GetEvent<LogUpdateEvent>().Publish();
                                                await botClient.SendTextMessageAsync(config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat,
                                                    $"В базу данных дубликатов было добавлено {totalAddedCount} записей:\r\n" +
                                                    $"Новый записей в категории webmail: {webmailAddedCount}\r\n" +
                                                    $"Новый записей в категории cpanel: {cpanelAddedCount}\r\n" +
                                                    $"Новый записей в категории whm: {whmAddedCount}\r\n");
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        }

                                        #endregion notify when any inserted

                                        #region check no data or only webmail

                                        if (cpanelDataFilePath == "" && whmDataFilePath == "")
                                        {
                                            if (webmailAddedCount != 0)
                                            {
                                                MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.OnlyWebmail, ellapsedWatch);
                                                Directory.Delete(folderPath, true);

                                                if (config.Chats.NotifyWhenCheckerEndWorkChat != 0)
                                                {
                                                    try
                                                    {
                                                        await botClient.SendTextMessageAsync(config.Chats.NotifyWhenCheckerEndWorkChat,
                                                            $"Закончена проверка логов ID: {manualCheckModel.Id} от {manualCheckModel.StartDateTime:dd.MM.yyyy} :\r\n" +
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
                                            }
                                            else
                                            {
                                                MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.NoAnyUnique, ellapsedWatch);
                                                Directory.Delete(folderPath, true);
                                                return;
                                            }
                                        }

                                        #endregion check no data or only webmail

                                        #region after checks set status to dublicate deleted

                                        manualCheckModel.Status = CheckStatus.ManualCheckStatus.DublicateDeleted;
                                        await ManualCheckController.PutCheckAsync(manualCheckModel, aggregator);

                                        #endregion after checks set status to dublicate deleted

                                        #region check cpanel

                                        string cpanelData;
                                        if (config.UseOwnCpanelChecker)
                                        {
                                            cpanelData = Runner.RunOwnCpanelChecker(cpanelDataFilePath, whmDataFilePath, folderPath, config.CheckerMaxForThread);
                                        }
                                        else
                                        {
                                            cpanelData = Runner.RunCpanelChecker(folderPath, cpanelDataFilePath, whmDataFilePath, config.CheckerMaxForThread);
                                        }
                                        JObject cpanelDataJson = JObject.Parse(cpanelData);
                                        if (cpanelDataJson.ContainsKey("Error"))
                                        {
                                            if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                                            {
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                                                           .Replace("@ID", manualCheckModel.Id.ToString()));
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                                            }
                                            catch (Exception)
                                            {
                                            }

                                            MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.Error, ellapsedWatch);
                                            Directory.Delete(folderPath, true);
                                            return;
                                        }

                                        #endregion check cpanel

                                        #region set to copying files

                                        manualCheckModel.CpanelGoodFilePath = cpanelDataJson["CpanelGood"].ToString();
                                        manualCheckModel.CpanelBadFilePath = cpanelDataJson["CpanelBad"].ToString();
                                        manualCheckModel.WhmGoodFilePath = cpanelDataJson["WhmGood"].ToString();
                                        manualCheckModel.WhmBadFilePath = cpanelDataJson["WhmBad"].ToString();

                                        MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.CopyingFiles, ellapsedWatch);

                                        #endregion set to copying files

                                        #region fill valid db

                                        string fillValidData = Runner.RunValidFiller(dbUser.Id, manualCheckModel.CpanelGoodFilePath, manualCheckModel.WhmGoodFilePath);
                                        JObject fillValidDataJson = JObject.Parse(fillValidData);
                                        if (fillValidDataJson.ContainsKey("Error"))
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при заполнении валида:\r\n{dublicateData}");
                                            }
                                            catch (Exception)
                                            {
                                            }
                                            return;
                                        }

                                        cpanelAddedCount = fillValidDataJson.Value<int>("CpanelAddedCount");
                                        whmAddedCount = fillValidDataJson.Value<int>("WhmAddedCount");
                                        totalAddedCount = cpanelAddedCount + whmAddedCount;
                                        if (totalAddedCount != 0)
                                        {
                                            if (config.Chats.NotifyWhenDatabaseFillNewValidRecordsChat != 0)
                                            {
                                                try
                                                {
                                                    aggregator.GetEvent<LogUpdateEvent>().Publish();
                                                    await botClient.SendTextMessageAsync(config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat,
                                                        $"В базу данных валида было добавлено {totalAddedCount} записей:\r\n" +
                                                        $"Новый записей в категории cpanel: {cpanelAddedCount}\r\n" +
                                                        $"Новый записей в категории whm: {whmAddedCount}\r\n");
                                                }
                                                catch (Exception)
                                                {
                                                }
                                            }
                                        }

                                        #endregion fill valid db

                                        #region copying last files and set to checked by soft status

                                        MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.CheckedBySoft, null);
                                        Directory.Delete(folderPath, true);
                                        if (config.Chats.NotifyWhenCheckerEndWorkChat != 0)
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(config.Chats.NotifyWhenCheckerEndWorkChat,
                                                    $"Закончена проверка логов ID: {manualCheckModel.Id} от {manualCheckModel.StartDateTime:dd.MM.yyyy} :\r\n" +
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

                                        #endregion copying last files and set to checked by soft status
                                    });
                                    Thread checkThread = new(threadStart);
                                    checkThread.Start();
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(
                                        dbUser.Id,
                                        locales.GetByKey("CanceledOpertaion", dbUser.Language),
                                        replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                        );
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

                    case OperationType.WaitPayoutMethod:
                        {
                            if (argument.IsAnyEqual(config.PayoutMethods.ToArray()))
                            {
                                operations.Remove(temp.Operation);
                                operations.Add(new(temp.Uid, OperationType.WaitAmount,
                                    new KeyValuePair<string, object>("PaymentMethod", argument)
                                    ));
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("EnterAmount", dbUser.Language),
                                    replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                                    );
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PleaseSelectFromKeyboard", dbUser.Language));
                                return;
                            }
                        }
                    case OperationType.WaitAmount:
                        {
                            bool isCorrectedAmmount = int.TryParse(argument, out int ammount);
                            if (isCorrectedAmmount && dbUser.Balance >= ammount)
                            {
                                operations.Remove(temp.Operation);
                                operations.Add(new(temp.Uid, OperationType.WaitRequisites,
                                    new KeyValuePair<string, object>("PaymentMethod", temp.Operation.Params["PaymentMethod"].ToString()),
                                    new KeyValuePair<string, object>("Ammount", ammount)
                                    ));
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("EnterRequisites", dbUser.Language),
                                    replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                                    );
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("EnteredIncorrectAmmount", dbUser.Language));
                                return;
                            }
                        }

                    case OperationType.WaitRequisites:
                        {
                            operations.Remove(temp.Operation);
                            operations.Add(new(temp.Uid, OperationType.WaitAcceptPayout,
                                new KeyValuePair<string, object>("PaymentMethod", temp.Operation.Params["PaymentMethod"].ToString()),
                                new KeyValuePair<string, object>("Ammount", temp.Operation.Params["Ammount"].ToString()),
                                new KeyValuePair<string, object>("PaymentRequisites", argument)
                                ));
                            await botClient.SendTextMessageAsync(
                                dbUser.Id,
                                locales.GetByKey("AcceptPayoutRequest", dbUser.Language)
                                       .Replace("@METHOD", temp.Operation.Params["PaymentMethod"].ToString())
                                       .Replace("@AMMOUNT", temp.Operation.Params["Ammount"].ToString())
                                       .Replace("@REQ", argument),
                                replyMarkup: keyboards.GetByLocale("AcceptCancel", dbUser.Language, payoutEnabled)
                                );
                            return;
                        }

                    case OperationType.WaitAcceptPayout:
                        {
                            if (argument.IsAnyEqual("Accept ✅", "Подтвердить ✅"))
                            {
                                string method = temp.Operation.Params["PaymentMethod"].ToString();
                                string requisites = temp.Operation.Params["PaymentRequisites"].ToString();
                                int ammount = int.Parse(temp.Operation.Params["Ammount"].ToString());

                                dbUser.Balance -= ammount;
                                await UsersController.PutUserAsync(dbUser, aggregator);

                                PayoutModel model = new()
                                {
                                    FromUserId = dbUser.Id,
                                    FromUsername = dbUser.Username,
                                    StartDateTime = DateTime.Now,
                                    Method = method,
                                    Requisites = requisites,
                                    Ammount = ammount,
                                    Status = PayoutStatus.PayoutStatusEnum.Created
                                };
                                if (await PayoutController.PostPayoutAsync(model, aggregator))
                                {
                                    if (config.Chats.NotifyWhenUserMakePayout != 0)
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(config.Chats.NotifyWhenCheckerEndWorkChat,
                                                $"Пользователь {model.FromUserId} | @{model.FromUsername} создал заявку на выплату\r\n" +
                                                $"Дата : {model.StartDateTime:dd.MM.yyyy HH.mm}\r\n" +
                                                $"Метод : {model.Method}\r\n" +
                                                $"Сумма : {model.Ammount}\r\n" +
                                                $"Реквизиты : {model.Requisites}");
                                        }
                                        catch (Exception)
                                        {
                                        }
                                    }
                                }

                                operations.Remove(temp.Operation);
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("PayoutCreated", dbUser.Language),
                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                    );
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PleaseSelectFromKeyboard", dbUser.Language));
                                return;
                            }
                        }
                }
            }

            #endregion operations

            if (temp.Message == "/start")
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id, locales.GetByKey("Start", dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                    );
                return;
            }
            if (temp.Message.IsAnyEqual("Language 🌎", "Язык 🌎"))
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id, locales.GetByKey("SelectNewLanguage",
                    dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("SelectLanguage", dbUser.Language, payoutEnabled)
                    );
                operations.Add(new(temp.Uid, OperationType.ChangeLanguage));
                return;
            }
            if (temp.Message.IsAnyEqual("Upload logs 📄", "Загрузить логи 📄"))
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id,
                    locales.GetByKey("SelectCheckerCategory", dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("SelectCategory", dbUser.Language, payoutEnabled)
                    );
                operations.Add(new(temp.Uid, OperationType.WaitCategoryForChecking));
                return;
            }
            if (temp.Message.IsAnyEqual("Balance 💰", "Баланс 💰"))
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id,
                    locales.GetByKey("BalanceInfo", dbUser.Language)
                           .Replace("@BALANCE", dbUser.Balance.ToString())
                           .Replace("@MINBALANCE", config.MinBalanceForPayment.ToString())
                           .Replace("@CURRENCY", config.Currency)
                    );
                return;
            }
            if (temp.Message.IsAnyEqual("Make payout 🛒", "Запросить выплату 🛒"))
            {
                List<string> keys = new();
                keys.AddRange(config.PayoutMethods);
                keys.Add(dbUser.Language == "en" ? "Cancel 🚫" : "Отмена 🚫");
                List<List<KeyboardButton>> markupButtons = new();
                foreach (var key in keys)
                {
                    markupButtons.Add(new() { new KeyboardButton(key) });
                }
                ReplyKeyboardMarkup markup = new(markupButtons);
                markup.ResizeKeyboard = true;

                await botClient.SendTextMessageAsync(
                    dbUser.Id,
                    locales.GetByKey("SelectPayoutMethodFromKeyboard", dbUser.Language),
                    replyMarkup: markup
                    );
                operations.Add(new(temp.Uid, OperationType.WaitPayoutMethod));
                return;
            }
            if (temp.Message.IsAnyEqual("Logs in process 📄", "Логи в обработке 📄"))
            {
                List<ManualCheckModel> checks = await ManualCheckController.GetChecksByUserIdAsync(dbUser.Id);
                if (!checks.Any())
                {
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        locales.GetByKey("NoAnyLogsInProcess", dbUser.Language)
                        );
                }
                else
                {
                    StringBuilder builder = new();
                    foreach (var check in checks)
                    {
                        switch (check.Status)
                        {
                            case CheckStatus.ManualCheckStatus.Created or
                                 CheckStatus.ManualCheckStatus.FillingDb or
                                 CheckStatus.ManualCheckStatus.DublicateDeleted or
                                 CheckStatus.ManualCheckStatus.CopyingFiles:
                                builder.AppendLine(locales.GetByKey("CheckNotStarted", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case CheckStatus.ManualCheckStatus.Error:
                                builder.AppendLine(locales.GetByKey("CheckError", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case CheckStatus.ManualCheckStatus.CheckedBySoft or
                                 CheckStatus.ManualCheckStatus.OnlyWebmail or
                                 CheckStatus.ManualCheckStatus.SendToManualChecking:
                                builder.AppendLine(locales.GetByKey("CheckInProcess", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case CheckStatus.ManualCheckStatus.End or
                                 CheckStatus.ManualCheckStatus.EndNoValid:
                                builder.AppendLine(locales.GetByKey("CheckDone", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;
                        }
                    }
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        builder.ToString()
                        );
                }
                return;
            }
            if (temp.Message.IsAnyEqual("Payout in process 🛒", "Выплаты в обработке 🛒"))
            {
                List<PayoutModel> payouts = await PayoutController.GetByUserIdAsync(dbUser.Id);
                if (!payouts.Any())
                {
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        locales.GetByKey("NoAnyPayoutsInProcess", dbUser.Language)
                        );
                }
                else
                {
                    StringBuilder builder = new();
                    foreach (var payout in payouts)
                    {
                        switch (payout.Status)
                        {
                            case PayoutStatus.PayoutStatusEnum.Created:
                                builder.AppendLine(locales.GetByKey("PayoutInWork", dbUser.Language)
                                                          .Replace("@ID", payout.Id.ToString()));
                                break;

                            case PayoutStatus.PayoutStatusEnum.Completed:
                                builder.AppendLine(locales.GetByKey("PayoutDone", dbUser.Language)
                                                          .Replace("@ID", payout.Id.ToString()));
                                break;

                            case PayoutStatus.PayoutStatusEnum.Denied:
                                builder.AppendLine(locales.GetByKey("PayoutDenied", dbUser.Language)
                                                          .Replace("@ID", payout.Id.ToString()));
                                break;
                        }
                    }
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        builder.ToString()
                        );
                }
                return;
            }
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("ошибка", TelegramStateModel.RedBrush));
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            botClient.StartReceiving(this);
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("работает", TelegramStateModel.GreenBrush));
        }

        private async void MoveFilesToChecksIdFolderAndUpateCountAndPathes(ManualCheckModel manualCheck, CheckStatus.ManualCheckStatus endStatus, Stopwatch ellapsedWatch)
        {
            string destinationFolderPath = PathCollection.ChecksFolderPath + manualCheck.Id + "/";
            if (!Directory.Exists(destinationFolderPath)) Directory.CreateDirectory(destinationFolderPath);

            string dublicatePath = destinationFolderPath + "dublicates.txt";
            string webmailPath = destinationFolderPath + "webmail.txt";
            string cpanelGoodPath = destinationFolderPath + "cpanel_good.txt";
            string cpanelBadPath = destinationFolderPath + "cpanel_bad.txt";
            string whmGoodPath = destinationFolderPath + "whm_good.txt";
            string whmBadPath = destinationFolderPath + "whm_bad.txt";

            if (System.IO.File.Exists(manualCheck.DublicateFilePath) && manualCheck.DublicateFilePath != dublicatePath)
            {
                System.IO.File.Copy(manualCheck.DublicateFilePath, dublicatePath, true);
                manualCheck.DublicateFilePath = dublicatePath;
                manualCheck.DublicateFoundedCount = System.IO.File.ReadAllLines(manualCheck.DublicateFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.WebmailFilePath) && manualCheck.WebmailFilePath != webmailPath)
            {
                System.IO.File.Copy(manualCheck.WebmailFilePath, webmailPath, true);
                manualCheck.WebmailFilePath = webmailPath;
                manualCheck.WebmailFoundedCount = System.IO.File.ReadAllLines(manualCheck.WebmailFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.CpanelGoodFilePath) && manualCheck.CpanelGoodFilePath != cpanelGoodPath)
            {
                System.IO.File.Copy(manualCheck.CpanelGoodFilePath, cpanelGoodPath, true);
                manualCheck.CpanelGoodFilePath = cpanelGoodPath;
                manualCheck.CpanelGoodCount = System.IO.File.ReadAllLines(manualCheck.CpanelGoodFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.CpanelBadFilePath) && manualCheck.CpanelBadFilePath != cpanelBadPath)
            {
                System.IO.File.Copy(manualCheck.CpanelBadFilePath, cpanelBadPath, true);
                manualCheck.CpanelBadFilePath = cpanelBadPath;
                manualCheck.CpanelBadCount = System.IO.File.ReadAllLines(manualCheck.CpanelBadFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.WhmGoodFilePath) && manualCheck.WhmGoodFilePath != whmGoodPath)
            {
                System.IO.File.Copy(manualCheck.WhmGoodFilePath, whmGoodPath, true);
                manualCheck.WhmGoodFilePath = whmGoodPath;
                manualCheck.WhmGoodCount = System.IO.File.ReadAllLines(manualCheck.WhmGoodFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(manualCheck.WhmBadFilePath) && manualCheck.WhmBadFilePath != whmBadPath)
            {
                System.IO.File.Copy(manualCheck.WhmBadFilePath, whmBadPath, true);
                manualCheck.WhmBadFilePath = whmBadPath;
                manualCheck.WhmBadCount = System.IO.File.ReadAllLines(manualCheck.WhmBadFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }

            manualCheck.Status = endStatus;
            if (ellapsedWatch != null)
            {
                ellapsedWatch.Stop();
                manualCheck.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                manualCheck.EndDateTime = DateTime.Now;
            }
            await ManualCheckController.PutCheckAsync(manualCheck, aggregator);
        }

        public async Task<bool> AcceptTelegramUserAsync(UserModel dbUser)
        {
            try
            {
                dbUser.IsAccepted = true;
                if (await UsersController.PutUserAsync(dbUser, aggregator))
                {
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        locales.GetByKey("AdminAcceptNotify",
                        dbUser.Language),
                        replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, dbUser.Balance >= config.MinBalanceForPayment)
                        );
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
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        locales.GetByKey("AdminMoveFromBlackList", dbUser.Language),
                        replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, dbUser.Balance >= config.MinBalanceForPayment)
                        );
                    await botClient.UnbanChatMemberAsync(dbUser.Id, dbUser.Id);
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
                    await botClient.BanChatMemberAsync(user.Id, user.Id);
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
                    await botClient.SendTextMessageAsync(
                        user.Id,
                        locales.GetByKey("AdminMoveFromBlackList", user.Language),
                        replyMarkup: keyboards.GetByLocale("Main", user.Language, user.Balance >= config.MinBalanceForPayment)
                        );
                }
                catch (Exception)
                {
                }
            }
            return updatedUsersFromDb.Count;
        }

        public async Task<bool> SendMailToUserAsync(UserModel dbUser, string mail, FileStream fs)
        {
            try
            {
                if (fs != null)
                {
                    await botClient.SendPhotoAsync(dbUser.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs), mail);
                }
                else
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, mail);
                }
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
                                                                           replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, dbUser.Balance >= config.MinBalanceForPayment));
                }
                else
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminDownBalanceNotification", dbUser.Language)
                                                                           .Replace("@BALANCE", dbUser.Balance.ToString())
                                                                           .Replace("@CURRENCY", config.Currency),
                                                                           replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, dbUser.Balance >= config.MinBalanceForPayment));
                }
            }
            catch (Exception)
            {
            }
        }

        public async Task NotifyChangeStatusPayoutToClosed(UserModel dbUser, string id)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PayoutStatusChangedToClosed", dbUser.Language).Replace("@ID", id));
            }
            catch (Exception)
            {
            }
        }

        public async Task NotifyChangeStatusPayoutToDenied(UserModel dbUser, string id)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("PayoutStatusChangedToDenied", dbUser.Language).Replace("@ID", id));
            }
            catch (Exception)
            {
            }
        }

        public async Task NotifyUserForEndCheckingFile(UserModel dbUser, ManualCheckModel manualCheck, int totalValid, int addBalance)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingComplete", dbUser.Language)
                                                                       .Replace("@ID", manualCheck.Id.ToString())
                                                                       .Replace("@VALID", totalValid.ToString())
                                                                       .Replace("@BALANCE", addBalance.ToString())
                                                                       .Replace("@CURRENCY", config.Currency)
                                                                       );
            }
            catch (Exception)
            {
            }
        }

        public async Task NotifyUserForEndCheckingFileNoValid(UserModel dbUser, int checkId)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUniqueEmptyError", dbUser.Language)
                                                                       .Replace("@ID", checkId.ToString()));
            }
            catch (Exception)
            {
            }
        }
    }
}