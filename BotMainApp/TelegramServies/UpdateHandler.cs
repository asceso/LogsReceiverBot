using BotMainApp.Events;
using BotMainApp.External;
using BotMainApp.LocalEvents;
using BotMainApp.ViewModels;
using DataAdapter.Controllers;
using DatabaseEvents;
using Extensions;
using Humanizer.Bytes;
using Models.App;
using Models.Database;
using Models.Enums;
using Newtonsoft.Json.Linq;
using Notification.Wpf;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static Models.Enums.CheckStatus;

namespace BotMainApp.TelegramServices
{
    public class UpdateHandler : IUpdateHandler
    {
        #region services

        private TelegramBotClient botClient;
        private readonly NotificationManager notificationManager;
        private readonly ReplyKeyboardRemove emptyKeyboard = new();
        private readonly List<LocaleStringModel> locales;
        private readonly Dictionary<string, ReplyKeyboardMarkup> keyboards;
        private readonly ObservableCollection<OperationModel> operations;
        private readonly List<Tuple<Guid, long>> userRequests;
        private readonly Dictionary<long, string> userCaptchas;
        private readonly Dictionary<long, int> userCaptchasAttempts;
        private readonly ConfigModel config;
        private readonly IEventAggregator aggregator;
        private readonly IMemorySaver memory;
        private readonly ICaptchaService captcha;
        private readonly ITaskScheduleService taskSchedule;

        #endregion services

        #region ctor

        public UpdateHandler(IEventAggregator aggregator, IMemorySaver memory, ICaptchaService captcha, ITaskScheduleService taskSchedule)
        {
            userRequests = new();
            userCaptchas = new();
            userCaptchasAttempts = new();
            this.aggregator = aggregator;
            this.memory = memory;
            this.captcha = captcha;
            this.taskSchedule = taskSchedule;
            notificationManager = memory.GetItem<NotificationManager>("Notification");
            botClient = memory.GetItem<TelegramBotClient>("BotClient");
            locales = memory.GetItem<List<LocaleStringModel>>("Locales");
            keyboards = memory.GetItem<Dictionary<string, ReplyKeyboardMarkup>>("Keyboards");
            operations = memory.GetItem<ObservableCollection<OperationModel>>("Operations");
            config = memory.GetItem<ConfigModel>("Config");
            aggregator.GetEvent<BotRestartEvent>().Subscribe(OnRestartBot);
        }

        private void OnRestartBot() => botClient = memory.GetItem<TelegramBotClient>("BotClient");

        #endregion ctor

        #region handling

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
            if (temp.Uid == 0)
            {
                return;
            }

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
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("QuestionForumInfo", temp.Language), replyMarkup: keyboards.GetByLocale("ForumsInfoAnswers", temp.Language, false));
                        operations.Add(new(temp.Uid, OperationType.WaitUserForumInfo));
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("DbError", temp.Language), replyMarkup: emptyKeyboard);
                        return;
                    }
                }
            }
            if (temp.Operation != null &&
                (temp.Operation.OperationType == OperationType.NewUserWithoutNickname ||
                 temp.Operation.OperationType == OperationType.WaitUserForumInfo ||
                 temp.Operation.OperationType == OperationType.WaitUserLogsOriginInfo))
            {
                await CheckUserOperationAsync(dbUser, temp, false).ConfigureAwait(false);
                return;
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
                await CheckUserOperationAsync(dbUser, temp, payoutEnabled).ConfigureAwait(false);
                return;
            }

            #endregion operations

            #region messages

            await CheckUserMessageAsync(dbUser, temp, payoutEnabled).ConfigureAwait(false);
            return;

            #endregion messages
        }

        #region operations

        private async Task CheckUserOperationAsync(UserModel dbUser, TempTelegram temp, bool payoutEnabled)
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
                        if (argument.IsAnyEqual("Cookies", "Cookie файлы"))
                        {
                            operations.Remove(temp.Operation);
                            operations.Add(new(temp.Uid, OperationType.WaitCookiesCategoryForChecking, new KeyValuePair<string, object>("Category", argument)));
                            await botClient.SendTextMessageAsync(
                                dbUser.Id,
                                locales.GetByKey("SelectCheckerCategory", dbUser.Language),
                                replyMarkup: keyboards.GetByLocale("SelectCookiesCategory", dbUser.Language, payoutEnabled)
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
                        else if (argument.IsAnyEqual("wp-login.php"))
                        {
                            operations.Remove(temp.Operation);
                            operations.Add(new(temp.Uid, OperationType.WaitFileForChecking,
                                new KeyValuePair<string, object>("Category", temp.Operation.Params["Category"].ToString()),
                                new KeyValuePair<string, object>("SubCategory", argument)
                                ));
                            await botClient.SendTextMessageAsync(
                                dbUser.Id,
                                locales.GetByKey("SendWpLoginInstruction", dbUser.Language),
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
                case OperationType.WaitCookiesCategoryForChecking:
                    {
                        if (argument.IsAnyEqual("Instagram"))
                        {
                            operations.Remove(temp.Operation);
                            operations.Add(new(temp.Uid, OperationType.WaitFileForChecking,
                                new KeyValuePair<string, object>("Category", temp.Operation.Params["Category"].ToString()),
                                new KeyValuePair<string, object>("SubCategory", argument)
                                ));
                            await botClient.SendTextMessageAsync(
                                dbUser.Id,
                                locales.GetByKey("SendCookiesInstruction", dbUser.Language).Replace("@SOFT", config.CookiesSoft),
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
                            #region common regex for dropmefiles

                            Regex dropMeRegex = new(@"https:\/\/dropmefiles\.com\/.*");

                            #endregion common regex for dropmefiles

                            #region checks for port :20

                            if (temp.Operation.Params["Category"].ToString().IsAnyEqual("Private requests", "Приватные запросы") &&
                                temp.Operation.Params["SubCategory"].ToString().IsAnyEqual(":20"))
                            {
                                #region load tg file

                                Telegram.Bot.Types.File file = await botClient.GetFileAsync(temp.Document.FileId);
                                string filename = "u_" + dbUser.Id + "_check_" + DateTime.Now.GetFilenameTimestamp() + ".txt";
                                using FileStream stream = new(PathCollection.TempFolderPath + filename, FileMode.Create);
                                await botClient.DownloadFileAsync(file.FilePath, stream);
                                stream.Close();

                                #endregion load tg file

                                #region accept file

                                CpanelWhmCheckModel manualCheckModel = new()
                                {
                                    StartDateTime = DateTime.Now,
                                    Status = ManualCheckStatus.Created,
                                    FromUserId = dbUser.Id,
                                    FromUsername = dbUser.Username,
                                };
                                await CpanelWhmCheckController.PostCheckAsync(manualCheckModel, aggregator);

                                operations.Remove(temp.Operation);
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                           .Replace("@ID", manualCheckModel.Id.ToString()),
                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                    );

                                #endregion accept file

                                #region start async task and create model

                                manualCheckModel.OriginalFilePath = CopyOriginalTelegramFileToChecksFolder(filename, dbUser, manualCheckModel.Id.ToString(), PathCollection.CpanelAndWhmFolderPath, ".txt");
                                await CpanelWhmCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                await Task.Factory.StartNew(async () => await StandartCheckProcessForPort20(dbUser, filename, manualCheckModel, true)).ConfigureAwait(false);
                                return;

                                #endregion start async task and create model
                            }

                            #endregion checks for port :20

                            #region admin checking port :20

                            else if (temp.Operation.Params["Category"].ToString().IsAnyEqual("AdminCheckingPrivateRequests") &&
                                     temp.Operation.Params["SubCategory"].ToString().IsAnyEqual("AdminCheckingPort20"))
                            {
                                #region load tg file

                                Telegram.Bot.Types.File file = await botClient.GetFileAsync(temp.Document.FileId);
                                string filename = "u_" + dbUser.Id + "_check_" + DateTime.Now.GetFilenameTimestamp() + ".txt";
                                using FileStream stream = new(PathCollection.TempFolderPath + filename, FileMode.Create);
                                await botClient.DownloadFileAsync(file.FilePath, stream);
                                stream.Close();

                                #endregion load tg file

                                #region accept file

                                CpanelWhmCheckModel manualCheckModel = new()
                                {
                                    StartDateTime = DateTime.Now,
                                    Status = ManualCheckStatus.Created,
                                    FromUserId = dbUser.Id,
                                    FromUsername = dbUser.Username,
                                };
                                await CpanelWhmCheckController.PostCheckAsync(manualCheckModel, aggregator);

                                operations.Remove(temp.Operation);
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                           .Replace("@ID", manualCheckModel.Id.ToString()),
                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                    );

                                #endregion accept file

                                #region start async task and create model

                                manualCheckModel.OriginalFilePath = CopyOriginalTelegramFileToChecksFolder(filename, dbUser, manualCheckModel.Id.ToString(), PathCollection.CpanelAndWhmFolderPath, ".txt");
                                await CpanelWhmCheckController.PutCheckAsync(manualCheckModel, aggregator);
                                await Task.Factory.StartNew(async () => await StandartCheckProcessForPort20(dbUser, filename, manualCheckModel, false)).ConfigureAwait(false);
                                return;

                                #endregion start async task and create model
                            }

                            #endregion admin checking port :20

                            #region checks for wp-login

                            else if (temp.Operation.Params["Category"].ToString().IsAnyEqual("Private requests", "Приватные запросы") &&
                                     temp.Operation.Params["SubCategory"].ToString().IsAnyEqual("wp-login.php"))
                            {
                                string filename = "u_" + dbUser.Id + "_check_" + DateTime.Now.GetFilenameTimestamp() + ".txt";
                                if (!argument.IsNullOrEmptyString())
                                {
                                    #region check regex for link

                                    if (dropMeRegex.IsMatch(argument))
                                    {
                                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CheckingWpLoginDropmelinkFile", dbUser.Language));
                                    }
                                    else
                                    {
                                        await botClient.SendTextMessageAsync(
                                            dbUser.Id,
                                            locales.GetByKey("SendWpLoginInstruction", dbUser.Language),
                                            replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                                            );
                                        return;
                                    }

                                    #endregion check regex for link

                                    #region check dublicate

                                    if (await CookiesController.IsDropMeLinkExist(argument))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkDublicateError", dbUser.Language));
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        return;
                                    }

                                    #endregion check dublicate

                                    #region check drop me link and download file

                                    Guid taskId = taskSchedule.In(ConstStrings.SeleniumThread).ScheduleTask(DropMeCheckingAsync).StartNext();
                                    async Task<string> DropMeCheckingAsync(object[] arg) => await Runner.RunDropMeLinkChecker(argument, true, PathCollection.TempFolderPath, filename);

                                    await taskSchedule.In(ConstStrings.SeleniumThread).WaitForTaskFinish(taskId).ConfigureAwait(false);
                                    string dropmelinkCheckerInfo = taskSchedule.In(ConstStrings.SeleniumThread).GetResult(taskId);

                                    JObject dropmelinkCheckerJson = JObject.Parse(dropmelinkCheckerInfo);
                                    if (dropmelinkCheckerJson.ContainsKey("Error"))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingError", dbUser.Language));
                                            if (System.IO.File.Exists(PathCollection.TempFolderPath + filename))
                                            {
                                                System.IO.File.Delete(PathCollection.TempFolderPath + filename);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        return;
                                    }

                                    bool isOnlyTxtFilesFounded = dropmelinkCheckerJson["IsOnlyTxtFiles"].Value<bool>();
                                    if (!isOnlyTxtFilesFounded)
                                    {
                                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("WpLoginInvalidFilesOnDropmelink", dbUser.Language));
                                        return;
                                    }

                                    bool isFilesDownloaded = dropmelinkCheckerJson["FilesDownloaded"].Value<bool>();
                                    if (isFilesDownloaded)
                                    {
                                        List<string> mainFileList = new();
                                        using StreamReader reader = new(PathCollection.TempFolderPath + filename);
                                        string buffer = await reader.ReadToEndAsync();
                                        reader.Close();
                                        foreach (var line in buffer.Split(Environment.NewLine).Where(l => !l.IsNullOrEmptyString()))
                                        {
                                            mainFileList.Add(line);
                                        }
                                        if (System.IO.File.Exists(PathCollection.TempFolderPath + filename))
                                        {
                                            System.IO.File.Delete(PathCollection.TempFolderPath + filename);
                                        }

                                        List<int> checkingModelsList = new();
                                        List<List<string>> partitions = mainFileList.Split(50000);
                                        foreach (List<string> partition in partitions)
                                        {
                                            WpLoginCheckModel checkModel = new()
                                            {
                                                StartDateTime = DateTime.Now,
                                                Status = ManualCheckStatus.Created,
                                                FromUserId = dbUser.Id,
                                                FromUsername = dbUser.Username,
                                            };
                                            await WpLoginCheckController.PostCheckAsync(checkModel, aggregator);

                                            string folderPath = PathCollection.WpLoginFolderPath + $"/{checkModel.Id}";
                                            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                                            string checkModelOriginalFilename = PathCollection.WpLoginFolderPath + $"{checkModel.Id}/@{dbUser.Username}_id{checkModel.Id}.txt";
                                            await partition.SaveToFile(checkModelOriginalFilename);
                                            checkModel.OriginalFilePath = checkModelOriginalFilename;
                                            await WpLoginCheckController.PutCheckAsync(checkModel, aggregator);

                                            taskSchedule.In(ConstStrings.FoxCheckerThread)
                                                        .ScheduleTask(FoxThreadAsync)
                                                        .AddParameters(dbUser, checkModelOriginalFilename, checkModel, true)
                                                        .StartNext();
                                            async Task FoxThreadAsync(object[] args) => await StandartCheckProcessForWpLogin((UserModel)args[0],
                                                                                                                             (string)args[1],
                                                                                                                             (WpLoginCheckModel)args[2],
                                                                                                                             (bool)args[3]);
                                            checkingModelsList.Add(checkModel.Id);
                                        }

                                        if (checkingModelsList.Count == 1)
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(
                                                    dbUser.Id,
                                                    locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                                           .Replace("@ID", checkingModelsList.FirstOrDefault().ToString()),
                                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                                    );
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        }
                                        else
                                        {
                                            string ids = string.Empty;
                                            for (int i = 0; i < checkingModelsList.Count; i++)
                                            {
                                                if (i == checkingModelsList.Count - 1)
                                                {
                                                    ids += checkingModelsList[i];
                                                }
                                                else
                                                {
                                                    ids += checkingModelsList[i] + ", ";
                                                }
                                            }
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(
                                                    dbUser.Id,
                                                    locales.GetByKey("FileListAcceptedWaitResult", dbUser.Language)
                                                           .Replace("@ID", ids),
                                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                                    );
                                            }
                                            catch (Exception)
                                            {
                                            }
                                        }

                                        operations.Remove(temp.Operation);
                                        return;
                                    }

                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingError", dbUser.Language));
                                    return;

                                    #endregion check drop me link and download file
                                }
                                else if (temp.Document.FileName.EndsWith(".txt"))
                                {
                                    #region load tg file

                                    Telegram.Bot.Types.File file = await botClient.GetFileAsync(temp.Document.FileId);
                                    using FileStream stream = new(PathCollection.TempFolderPath + filename, FileMode.Create);
                                    await botClient.DownloadFileAsync(file.FilePath, stream);
                                    stream.Close();

                                    #endregion load tg file

                                    #region accept file

                                    List<string> mainFileList = new();
                                    using StreamReader reader = new(PathCollection.TempFolderPath + filename);
                                    string buffer = await reader.ReadToEndAsync();
                                    reader.Close();
                                    foreach (var line in buffer.Split(Environment.NewLine).Where(l => !l.IsNullOrEmptyString()))
                                    {
                                        mainFileList.Add(line);
                                    }
                                    if (System.IO.File.Exists(PathCollection.TempFolderPath + filename))
                                    {
                                        System.IO.File.Delete(PathCollection.TempFolderPath + filename);
                                    }

                                    List<int> checkingModelsList = new();
                                    List<List<string>> partitions = mainFileList.Split(50000);
                                    foreach (List<string> partition in partitions)
                                    {
                                        WpLoginCheckModel checkModel = new()
                                        {
                                            StartDateTime = DateTime.Now,
                                            Status = ManualCheckStatus.Created,
                                            FromUserId = dbUser.Id,
                                            FromUsername = dbUser.Username,
                                        };
                                        await WpLoginCheckController.PostCheckAsync(checkModel, aggregator);

                                        string folderPath = PathCollection.WpLoginFolderPath + $"/{checkModel.Id}";
                                        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                                        string checkModelOriginalFilename = PathCollection.WpLoginFolderPath + $"{checkModel.Id}/@{dbUser.Username}_id{checkModel.Id}.txt";
                                        await partition.SaveToFile(checkModelOriginalFilename);
                                        checkModel.OriginalFilePath = checkModelOriginalFilename;
                                        await WpLoginCheckController.PutCheckAsync(checkModel, aggregator);

                                        taskSchedule.In(ConstStrings.FoxCheckerThread)
                                                    .ScheduleTask(FoxThreadAsync)
                                                    .AddParameters(dbUser, checkModelOriginalFilename, checkModel, true)
                                                    .StartNext();
                                        async Task FoxThreadAsync(object[] args) => await StandartCheckProcessForWpLogin((UserModel)args[0],
                                                                                                                         (string)args[1],
                                                                                                                         (WpLoginCheckModel)args[2],
                                                                                                                         (bool)args[3]);
                                        checkingModelsList.Add(checkModel.Id);
                                    }

                                    if (checkingModelsList.Count == 1)
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(
                                                dbUser.Id,
                                                locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                                       .Replace("@ID", checkingModelsList.FirstOrDefault().ToString()),
                                                replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                                );
                                        }
                                        catch (Exception)
                                        {
                                        }
                                    }
                                    else
                                    {
                                        string ids = string.Empty;
                                        for (int i = 0; i < checkingModelsList.Count; i++)
                                        {
                                            if (i == checkingModelsList.Count - 1)
                                            {
                                                ids += checkingModelsList[i];
                                            }
                                            else
                                            {
                                                ids += checkingModelsList[i] + ", ";
                                            }
                                        }
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(
                                                dbUser.Id,
                                                locales.GetByKey("FileListAcceptedWaitResult", dbUser.Language)
                                                       .Replace("@ID", ids),
                                                replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                                );
                                        }
                                        catch (Exception)
                                        {
                                        }
                                    }

                                    operations.Remove(temp.Operation);
                                    return;

                                    #endregion accept file
                                }
                                else
                                {
                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUploadError", dbUser.Language));
                                    return;
                                }
                            }

                            #endregion checks for wp-login

                            #region instagram cookies

                            else if (temp.Operation.Params["Category"].ToString().IsAnyEqual("Cookies", "Cookie файлы") &&
                                     temp.Operation.Params["SubCategory"].ToString().IsAnyEqual("Instagram"))
                            {
                                #region dropmefiles checking

                                if (!argument.IsNullOrEmptyString())
                                {
                                    #region check regex for link

                                    if (dropMeRegex.IsMatch(argument))
                                    {
                                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CheckingCookieFile", dbUser.Language));
                                    }
                                    else
                                    {
                                        await botClient.SendTextMessageAsync(
                                            dbUser.Id,
                                            locales.GetByKey("SendCookiesInstruction", dbUser.Language).Replace("@SOFT", config.CookiesSoft),
                                            replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                                            );
                                        return;
                                    }

                                    #endregion check regex for link

                                    #region check dublicate

                                    if (await CookiesController.IsDropMeLinkExist(argument))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkDublicateError", dbUser.Language));
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        return;
                                    }

                                    #endregion check dublicate

                                    #region check drop me link info

                                    string fileInfoData = await Runner.RunDropMeLinkChecker(argument, false, null, null);
                                    JObject fileInfoJson = JObject.Parse(fileInfoData);
                                    if (fileInfoJson.ContainsKey("Error"))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingError", dbUser.Language));
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        return;
                                    }

                                    string filesize = fileInfoJson["Filesize"].ToString();
                                    string unit = fileInfoJson["Unit"].ToString();
                                    if (unit.IsAnyEqual("B", "KB"))
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingLessThatMinSize", dbUser.Language)
                                                                                                   .Replace("@MIN", config.MinAcceptingFileSize.ToString()));
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        return;
                                    }
                                    if (unit.IsAnyEqual("MB"))
                                    {
                                        if (double.TryParse(filesize.Replace(".", ","), out double cookiesSize))
                                        {
                                            if (cookiesSize < config.MinAcceptingFileSize)
                                            {
                                                try
                                                {
                                                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingLessThatMinSize", dbUser.Language)
                                                                                                           .Replace("@MIN", config.MinAcceptingFileSize.ToString()));
                                                }
                                                catch (Exception)
                                                {
                                                }
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingError", dbUser.Language));
                                            }
                                            catch (Exception)
                                            {
                                            }
                                            return;
                                        }
                                    }

                                    #endregion check drop me link info

                                    #region post cookie

                                    CookieModel model = new()
                                    {
                                        Category = "Instagram",
                                        UploadedDateTime = DateTime.Now,
                                        Status = CheckStatus.CookieCheckStatus.Uploaded,
                                        Filesize = filesize,
                                        Unit = unit,
                                        FileLink = argument,
                                        FolderPath = string.Empty,
                                        UploadedByUserId = dbUser.Id,
                                        UploadedByUsername = dbUser.Username
                                    };
                                    await CookiesController.PostCookieAsync(model, aggregator);

                                    #endregion post cookie

                                    #region end

                                    operations.Remove(temp.Operation);
                                    await botClient.SendTextMessageAsync(
                                        dbUser.Id,
                                        locales.GetByKey("CookiesAcceptedWaitResult", dbUser.Language)
                                                .Replace("@ID", model.Id.ToString()),
                                        replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                        );
                                    return;

                                    #endregion end
                                }

                                #endregion dropmefiles checking

                                #region archive checking

                                if (temp.Document != null && (
                                    temp.Document.FileName.EndsWith(".rar") ||
                                    temp.Document.FileName.EndsWith(".zip")
                                    ))
                                {
                                    #region checking filesize

                                    ByteSize fileSize = ByteSize.FromBytes((double)temp.Document.FileSize);
                                    if (fileSize.Megabytes < config.MinAcceptingFileSize)
                                    {
                                        try
                                        {
                                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("DropMeLinkCheckingLessThatMinSize", dbUser.Language)
                                                                                                   .Replace("@MIN", config.MinAcceptingFileSize.ToString()));
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        return;
                                    }

                                    #endregion checking filesize

                                    #region post cookie

                                    CookieModel model = new()
                                    {
                                        Category = "Instagram",
                                        UploadedDateTime = DateTime.Now,
                                        Status = CheckStatus.CookieCheckStatus.Created,
                                        FileLink = string.Empty,
                                        Filesize = Math.Round(fileSize.Megabytes, 0).ToString(),
                                        Unit = "MB",
                                        UploadedByUserId = dbUser.Id,
                                        UploadedByUsername = dbUser.Username
                                    };
                                    await CookiesController.PostCookieAsync(model, aggregator);

                                    #endregion post cookie

                                    #region send answer

                                    operations.Remove(temp.Operation);
                                    await botClient.SendTextMessageAsync(
                                        dbUser.Id,
                                        locales.GetByKey("CookiesAcceptedWaitResult", dbUser.Language)
                                                .Replace("@ID", model.Id.ToString()),
                                        replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                        );

                                    #endregion send answer

                                    #region copy cookie files to directory

                                    string fileDirectory = PathCollection.CookiesFolderPath + $"{model.Id}/";
                                    if (!Directory.Exists(fileDirectory)) Directory.CreateDirectory(fileDirectory);

                                    Telegram.Bot.Types.File file = await botClient.GetFileAsync(temp.Document.FileId);
                                    string filename = temp.Document.FileName;
                                    using FileStream stream = new(fileDirectory + filename, FileMode.Create);
                                    await botClient.DownloadFileAsync(file.FilePath, stream);
                                    stream.Close();

                                    model.UploadedDateTime = DateTime.Now;
                                    model.Status = CheckStatus.CookieCheckStatus.Uploaded;
                                    model.FolderPath = fileDirectory;
                                    await CookiesController.PutCookieAsync(model, aggregator);
                                    return;

                                    #endregion copy cookie files to directory
                                }

                                #endregion archive checking

                                #region end

                                await botClient.SendTextMessageAsync(
                                                           dbUser.Id,
                                                           locales.GetByKey("SendCookiesInstruction", dbUser.Language).Replace("@SOFT", config.CookiesSoft),
                                                           replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                                                           );
                                return;

                                #endregion end
                            }

                            #endregion instagram cookies

                            #region cant found variant, cancel operation

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

                            #endregion cant found variant, cancel operation
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message == "Bad Request: file is too big")
                            {
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileTooBigError", dbUser.Language));
                            }
                            else
                            {
                                if (config.Chats.ErrorNotificationChat != 0)
                                {
                                    try
                                    {
                                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при загрузке файла:\r\n{ex.Message}");
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUploadError", dbUser.Language));
                            }
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
                                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("QuestionForumInfo", dbUser.Language), replyMarkup: keyboards.GetByLocale("ForumsInfoAnswers", dbUser.Language, false));
                                operations.Remove(temp.Operation);
                                operations.Add(new(temp.Uid, OperationType.WaitUserForumInfo));
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(temp.Uid, locales.GetByKey("DbError", temp.Language), replyMarkup: emptyKeyboard);
                                return;
                            }
                        }
                    }
                case OperationType.WaitUserForumInfo:
                    {
                        dbUser.ForumsInfo = argument;
                        await UsersController.PutUserAsync(dbUser, aggregator).ConfigureAwait(false);
                        await botClient.SendTextMessageAsync(
                            dbUser.Id,
                            locales.GetByKey("QuestionLogsOriginInfo", dbUser.Language),
                            replyMarkup: keyboards.GetByLocale("LogsOriginAnswers", dbUser.Language, false)
                            );
                        operations.Remove(temp.Operation);
                        operations.Add(new(temp.Uid, OperationType.WaitUserLogsOriginInfo));
                        return;
                    }
                case OperationType.WaitUserLogsOriginInfo:
                    {
                        dbUser.LogsOriginInfo = argument;
                        await UsersController.PutUserAsync(dbUser, aggregator).ConfigureAwait(false);
                        if (dbUser.IsAccepted)
                        {
                            await botClient.SendTextMessageAsync(
                                dbUser.Id,
                                locales.GetByKey("QuestionCompleteForAcceptedUsers", dbUser.Language),
                                replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                );
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("WelcomeNickname", dbUser.Language).Replace("@USERNAME", dbUser.Username), replyMarkup: emptyKeyboard);
                        }
                        operations.Remove(temp.Operation);
                        return;
                    }
            }
        }

        #endregion operations

        #region messages

        private async Task CheckUserMessageAsync(UserModel dbUser, TempTelegram temp, bool payoutEnabled)
        {
            if (temp.Message == "/start")
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id, locales.GetByKey("Start", dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                    );
                return;
            }
            else if (temp.Message.IsAnyEqual("Language 🌎", "Язык 🌎"))
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id, locales.GetByKey("SelectNewLanguage",
                    dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("SelectLanguage", dbUser.Language, payoutEnabled)
                    );
                operations.Add(new(temp.Uid, OperationType.ChangeLanguage));
                return;
            }
            else if (temp.Message.IsAnyEqual("Upload logs 📄", "Загрузить логи 📄"))
            {
                await botClient.SendTextMessageAsync(
                    dbUser.Id,
                    locales.GetByKey("SelectCheckerCategory", dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("SelectCategory", dbUser.Language, payoutEnabled)
                    );
                operations.Add(new(temp.Uid, OperationType.WaitCategoryForChecking));
                return;
            }
            else if (temp.Message.IsAnyEqual("Balance 💰", "Баланс 💰"))
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
            else if (temp.Message.IsAnyEqual("Make payout 🛒", "Запросить выплату 🛒"))
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
            else if (temp.Message.IsAnyEqual("Logs in process 📄", "Логи в обработке 📄"))
            {
                List<CpanelWhmCheckModel> cpanelWhmChecks = await CpanelWhmCheckController.GetChecksByUserIdAsync(dbUser.Id);
                List<WpLoginCheckModel> wpLoginChecks = await WpLoginCheckController.GetChecksByUserIdAsync(dbUser.Id);
                if (!cpanelWhmChecks.Any() && !wpLoginChecks.Any())
                {
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        locales.GetByKey("NoAnyLogsInProcess", dbUser.Language)
                        );
                }
                else
                {
                    StringBuilder builder = new();
                    if (cpanelWhmChecks.Any())
                    {
                        builder.AppendLine(locales.GetByKey("ChecksPort20Header", dbUser.Language));
                    }
                    foreach (var check in cpanelWhmChecks)
                    {
                        switch (check.Status)
                        {
                            case ManualCheckStatus.Created or
                                 ManualCheckStatus.FillingDb or
                                 ManualCheckStatus.SendedToSoftCheck or
                                 ManualCheckStatus.CopyingFiles:
                                builder.AppendLine(locales.GetByKey("CheckNotStarted", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case ManualCheckStatus.Error:
                                builder.AppendLine(locales.GetByKey("CheckError", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case ManualCheckStatus.CheckedBySoft or
                                 ManualCheckStatus.OnlyWebmail or
                                 ManualCheckStatus.SendToManualChecking:
                                builder.AppendLine(locales.GetByKey("CheckInProcess", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case ManualCheckStatus.End or
                                 ManualCheckStatus.EndNoValid:
                                builder.AppendLine(locales.GetByKey("CheckDone", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;
                        }
                    }
                    if (wpLoginChecks.Any())
                    {
                        builder.AppendLine(locales.GetByKey("ChecksWpLoginHeader", dbUser.Language));
                    }
                    foreach (var check in wpLoginChecks)
                    {
                        switch (check.Status)
                        {
                            case ManualCheckStatus.Created or
                                 ManualCheckStatus.FillingDb or
                                 ManualCheckStatus.SendedToSoftCheck or
                                 ManualCheckStatus.CopyingFiles:
                                builder.AppendLine(locales.GetByKey("CheckNotStarted", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case ManualCheckStatus.Error:
                                builder.AppendLine(locales.GetByKey("CheckError", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case ManualCheckStatus.CheckedBySoft or
                                 ManualCheckStatus.OnlyWebmail or
                                 ManualCheckStatus.SendToManualChecking:
                                builder.AppendLine(locales.GetByKey("CheckInProcess", dbUser.Language)
                                                          .Replace("@ID", check.Id.ToString()));
                                break;

                            case ManualCheckStatus.End or
                                 ManualCheckStatus.EndNoValid:
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
            else if (temp.Message.IsAnyEqual("Cookies in process 📄", "Cookies в обработке 📄"))
            {
                List<CookieModel> cookies = await CookiesController.GetCookiesByUserIdAsync(dbUser.Id);
                if (!cookies.Any())
                {
                    await botClient.SendTextMessageAsync(
                        dbUser.Id,
                        locales.GetByKey("NoAnyCookiesInProcess", dbUser.Language)
                        );
                }
                else
                {
                    StringBuilder builder = new();
                    foreach (var cookie in cookies)
                    {
                        switch (cookie.Status)
                        {
                            case CookieCheckStatus.Created or
                                 CookieCheckStatus.Proceed or
                                 CookieCheckStatus.Uploaded:
                                builder.AppendLine(locales.GetByKey("CheckInProcess", dbUser.Language)
                                                          .Replace("@ID", cookie.Id.ToString()));
                                break;

                            case CookieCheckStatus.End or
                                 CookieCheckStatus.EndNoValid:
                                builder.AppendLine(locales.GetByKey("CookieDone", dbUser.Language)
                                                          .Replace("@ID", cookie.Id.ToString()));
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
            else if (temp.Message.IsAnyEqual("Payout in process 🛒", "Выплаты в обработке 🛒"))
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
            else if (temp.Message.IsAnyEqual("/check_port20_logs_admin") && config.EnableAdminCheckCommand)
            {
                operations.Add(new(temp.Uid, OperationType.WaitFileForChecking,
                    new KeyValuePair<string, object>("Category", "AdminCheckingPrivateRequests"),
                    new KeyValuePair<string, object>("SubCategory", "AdminCheckingPort20")
                    ));
                await botClient.SendTextMessageAsync(
                    dbUser.Id,
                    locales.GetByKey("SendFileInstruction", dbUser.Language),
                    replyMarkup: keyboards.GetByLocale("Cancel", dbUser.Language, payoutEnabled)
                    );
                return;
            }
        }

        #endregion messages

        #region error handler

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("ошибка", TelegramStateModel.RedBrush));
            notificationManager.Show(exception);
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            botClient.StartReceiving(this);
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("работает", TelegramStateModel.GreenBrush));
        }

        #endregion error handler

        #endregion handling

        #region common methods

        private static string CopyOriginalTelegramFileToChecksFolder(string originalFilename, UserModel user, string checkingId, string checkFolderPath, string extension)
        {
            if (!Directory.Exists(checkFolderPath + $"/{checkingId}/")) Directory.CreateDirectory(checkFolderPath + $"/{checkingId}/");
            string inputFilename = PathCollection.TempFolderPath + "/" + originalFilename;
            string targetFilename = checkFolderPath + $"/{checkingId}/" + $"@{user.Username}_id{checkingId}{extension}";
            System.IO.File.Copy(inputFilename, targetFilename, true);
            return targetFilename;
        }

        #endregion common methods

        #region cpanel and whm checking

        private async Task StandartCheckProcessForPort20(UserModel dbUser, string filename, CpanelWhmCheckModel checkModel, bool fillDublicates)
        {
            #region init

            string inputFilename = PathCollection.TempFolderPath + "/" + filename;
            string folderPath = PathCollection.TempFolderPath + $"u_{dbUser.Id}_d_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}/";
            Directory.CreateDirectory(folderPath);
            Stopwatch ellapsedWatch = Stopwatch.StartNew();

            #endregion init

            #region check dublicates

            string dublicateData = await Runner.RunDublicateChecker(folderPath, inputFilename, config);
            JObject dublicateDataJson = JObject.Parse(dublicateData);
            if (dublicateDataJson.ContainsKey("Error"))
            {
                if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                               .Replace("@ID", checkModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.Error, ellapsedWatch);
                Directory.Delete(folderPath, true);
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при поиске дубликатов файла:\r\n{dublicateData}");
                    }
                    catch (Exception)
                    {
                    }
                }
                return;
            }

            checkModel.DublicateFilePath = dublicateDataJson["Dublicates"].ToString();
            checkModel.WebmailFilePath = dublicateDataJson["Webmail"].ToString();
            string cpanelDataFilePath = dublicateDataJson["Cpanel"].ToString();
            string whmDataFilePath = dublicateDataJson["Whm"].ToString();
            MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.FillingDb, null);

            #endregion check dublicates

            #region fill logs db

            string fillData = await Runner.RunDublicateFiller(dbUser.Id, checkModel.WebmailFilePath, cpanelDataFilePath, whmDataFilePath, null, fillDublicates);
            JObject fillDataJson = JObject.Parse(fillData);
            if (fillDataJson.ContainsKey("Error"))
            {
                if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                               .Replace("@ID", checkModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{fillData}");
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.Error, ellapsedWatch);
                Directory.Delete(folderPath, true);
                return;
            }

            #endregion fill logs db

            #region no any unique

            int webmailAddedCount = fillDataJson.Value<int>("WebmailAddedCount");
            int cpanelAddedCount = fillDataJson.Value<int>("CpanelAddedCount");
            int whmAddedCount = fillDataJson.Value<int>("WhmAddedCount");
            int totalAddedCount = webmailAddedCount + cpanelAddedCount + whmAddedCount;

            if (fillDublicates)
            {
                if (totalAddedCount == 0)
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUniqueEmptyError", dbUser.Language)
                                                                           .Replace("@ID", checkModel.Id.ToString()));

                    MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.NoAnyUnique, ellapsedWatch);
                    Directory.Delete(folderPath, true);
                    return;
                }
            }

            #endregion no any unique

            #region notify when any inserted

            if (fillDublicates)
            {
                if (config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat != 0)
                {
                    try
                    {
                        aggregator.GetEvent<DublicateUpdateEvent>().Publish();
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
            }

            #endregion notify when any inserted

            #region check no data or only webmail

            if (cpanelDataFilePath == "" && whmDataFilePath == "")
            {
                if (webmailAddedCount != 0)
                {
                    MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, CheckStatus.ManualCheckStatus.OnlyWebmail, ellapsedWatch);
                    Directory.Delete(folderPath, true);

                    if (config.Chats.NotifyWhenCheckerEndWorkChat != 0)
                    {
                        try
                        {
                            await botClient.SendTextMessageAsync(config.Chats.NotifyWhenCheckerEndWorkChat,
                                $"Закончена проверка логов ID: {checkModel.Id} от {checkModel.StartDateTime:dd.MM.yyyy} :\r\n" +
                                $"Загрузил : @{checkModel.FromUsername}\r\n" +
                                $"Затрачено всего : {Math.Round(checkModel.CheckingTimeEllapsed.TotalMinutes, 2)} минут\r\n" +
                                $"Дубликатов найдено: {checkModel.DublicateFoundedCount}\r\n" +
                                $"Webmail найдено: {checkModel.WebmailFoundedCount}\r\n" +
                                $"Cpanel (good) найдено: {checkModel.CpanelGoodCount}\r\n" +
                                $"Cpanel (bad) найдено: {checkModel.CpanelBadCount}\r\n" +
                                $"Whm (good) найдено: {checkModel.WhmGoodCount}\r\n" +
                                $"Whm (bad) найдено: {checkModel.WhmBadCount}");
                        }
                        catch (Exception)
                        {
                        }
                    }

                    return;
                }
                else
                {
                    MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, CheckStatus.ManualCheckStatus.NoAnyUnique, ellapsedWatch);
                    Directory.Delete(folderPath, true);
                    return;
                }
            }

            #endregion check no data or only webmail

            #region after checks set status to dublicate deleted

            checkModel.Status = CheckStatus.ManualCheckStatus.SendedToSoftCheck;
            await CpanelWhmCheckController.PutCheckAsync(checkModel, aggregator);

            #endregion after checks set status to dublicate deleted

            #region check cpanel

            string cpanelData;
            if (config.UseOwnCpanelChecker)
            {
                cpanelData = await Runner.RunOwnCpanelChecker(cpanelDataFilePath, whmDataFilePath, folderPath, config.CheckerMaxForThread);
            }
            else
            {
                cpanelData = await Runner.RunCpanelChecker(folderPath, cpanelDataFilePath, whmDataFilePath, config.CheckerMaxForThread);
            }
            JObject cpanelDataJson = JObject.Parse(cpanelData);
            if (cpanelDataJson.ContainsKey("Error"))
            {
                if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                               .Replace("@ID", checkModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{cpanelDataJson}");
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.Error, ellapsedWatch);
                Directory.Delete(folderPath, true);
                return;
            }

            #endregion check cpanel

            #region set to copying files

            checkModel.CpanelGoodFilePath = cpanelDataJson["CpanelGood"].ToString();
            checkModel.CpanelBadFilePath = cpanelDataJson["CpanelBad"].ToString();
            checkModel.WhmGoodFilePath = cpanelDataJson["WhmGood"].ToString();
            checkModel.WhmBadFilePath = cpanelDataJson["WhmBad"].ToString();

            MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.CopyingFiles, ellapsedWatch);

            #endregion set to copying files

            #region fill valid db

            string fillValidData = await Runner.RunValidFiller(dbUser.Id, checkModel.CpanelGoodFilePath, checkModel.WhmGoodFilePath, null, null, null, null);
            JObject fillValidDataJson = JObject.Parse(fillValidData);
            if (fillValidDataJson.ContainsKey("Error"))
            {
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при заполнении валида:\r\n{fillValidData}");
                    }
                    catch (Exception)
                    {
                    }
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
                        aggregator.GetEvent<DublicateUpdateEvent>().Publish();
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

            MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.CheckedBySoft, null);
            Directory.Delete(folderPath, true);
            if (config.Chats.NotifyWhenCheckerEndWorkChat != 0)
            {
                try
                {
                    await botClient.SendTextMessageAsync(config.Chats.NotifyWhenCheckerEndWorkChat,
                        $"Закончена проверка логов ID: {checkModel.Id} от {checkModel.StartDateTime:dd.MM.yyyy} :\r\n" +
                        $"Загрузил : @{checkModel.FromUsername}\r\n" +
                        $"Затрачено всего : {Math.Round(checkModel.CheckingTimeEllapsed.TotalMinutes, 2)} минут\r\n" +
                        $"Дубликатов найдено: {checkModel.DublicateFoundedCount}\r\n" +
                        $"Webmail найдено: {checkModel.WebmailFoundedCount}\r\n" +
                        $"Cpanel (good) найдено: {checkModel.CpanelGoodCount}\r\n" +
                        $"Cpanel (bad) найдено: {checkModel.CpanelBadCount}\r\n" +
                        $"Whm (good) найдено: {checkModel.WhmGoodCount}\r\n" +
                        $"Whm (bad) найдено: {checkModel.WhmBadCount}");
                }
                catch (Exception)
                {
                }
            }
            return;

            #endregion copying last files and set to checked by soft status
        }

        private async void MoveFilesToChecksIdFolderAndUpateCountAndPathes(CpanelWhmCheckModel checkModel, ManualCheckStatus endStatus, Stopwatch ellapsedWatch)
        {
            string destinationFolderPath = PathCollection.CpanelAndWhmFolderPath + checkModel.Id + "/";
            if (!Directory.Exists(destinationFolderPath)) Directory.CreateDirectory(destinationFolderPath);

            string dublicatePath = destinationFolderPath + "dublicates.txt";
            string webmailPath = destinationFolderPath + "webmail.txt";
            string cpanelGoodPath = destinationFolderPath + "cpanel_good.txt";
            string cpanelBadPath = destinationFolderPath + "cpanel_bad.txt";
            string whmGoodPath = destinationFolderPath + "whm_good.txt";
            string whmBadPath = destinationFolderPath + "whm_bad.txt";

            if (System.IO.File.Exists(checkModel.DublicateFilePath) && checkModel.DublicateFilePath != dublicatePath)
            {
                System.IO.File.Copy(checkModel.DublicateFilePath, dublicatePath, true);
                checkModel.DublicateFilePath = dublicatePath;
                checkModel.DublicateFoundedCount = System.IO.File.ReadAllLines(checkModel.DublicateFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.WebmailFilePath) && checkModel.WebmailFilePath != webmailPath)
            {
                System.IO.File.Copy(checkModel.WebmailFilePath, webmailPath, true);
                checkModel.WebmailFilePath = webmailPath;
                checkModel.WebmailFoundedCount = System.IO.File.ReadAllLines(checkModel.WebmailFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.CpanelGoodFilePath) && checkModel.CpanelGoodFilePath != cpanelGoodPath)
            {
                System.IO.File.Copy(checkModel.CpanelGoodFilePath, cpanelGoodPath, true);
                checkModel.CpanelGoodFilePath = cpanelGoodPath;
                checkModel.CpanelGoodCount = System.IO.File.ReadAllLines(checkModel.CpanelGoodFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.CpanelBadFilePath) && checkModel.CpanelBadFilePath != cpanelBadPath)
            {
                System.IO.File.Copy(checkModel.CpanelBadFilePath, cpanelBadPath, true);
                checkModel.CpanelBadFilePath = cpanelBadPath;
                checkModel.CpanelBadCount = System.IO.File.ReadAllLines(checkModel.CpanelBadFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.WhmGoodFilePath) && checkModel.WhmGoodFilePath != whmGoodPath)
            {
                System.IO.File.Copy(checkModel.WhmGoodFilePath, whmGoodPath, true);
                checkModel.WhmGoodFilePath = whmGoodPath;
                checkModel.WhmGoodCount = System.IO.File.ReadAllLines(checkModel.WhmGoodFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.WhmBadFilePath) && checkModel.WhmBadFilePath != whmBadPath)
            {
                System.IO.File.Copy(checkModel.WhmBadFilePath, whmBadPath, true);
                checkModel.WhmBadFilePath = whmBadPath;
                checkModel.WhmBadCount = System.IO.File.ReadAllLines(checkModel.WhmBadFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }

            checkModel.Status = endStatus;
            if (ellapsedWatch != null)
            {
                ellapsedWatch.Stop();
                checkModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                checkModel.EndDateTime = DateTime.Now;
            }
            await CpanelWhmCheckController.PutCheckAsync(checkModel, aggregator);
        }

        #endregion cpanel and whm checking

        #region wp login checking

        private async Task StandartCheckProcessForWpLogin(UserModel dbUser, string filename, WpLoginCheckModel checkModel, bool fillDublicates)
        {
            #region init

            string inputFilename = filename;
            string folderPath = PathCollection.TempFolderPath + $"u_{dbUser.Id}_d_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}/";
            Directory.CreateDirectory(folderPath);
            System.IO.File.Copy(inputFilename, folderPath + "input.txt", true);
            Stopwatch ellapsedWatch = Stopwatch.StartNew();
            string workingFile = folderPath + "input.txt";

            #endregion init

            #region check dublicates

            string preparedFileData = await Runner.RunWpLoginFilePreparer(folderPath, workingFile);
            JObject preparedFileJson = JObject.Parse(preparedFileData);
            if (preparedFileJson.ContainsKey("Error"))
            {
                if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                               .Replace("@ID", checkModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.Error, ellapsedWatch);
                Directory.Delete(folderPath, true);
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при подготовке wp-login файла:\r\n{preparedFileJson}");
                    }
                    catch (Exception)
                    {
                    }
                }
                return;
            }
            checkModel.DublicateFilePath = preparedFileJson["Dublicates"].ToString();
            workingFile = preparedFileJson["Unique"].ToString();

            #endregion check dublicates

            #region fill logs db

            string fillData = await Runner.RunDublicateFiller(dbUser.Id, null, null, null, workingFile, fillDublicates);
            JObject fillDataJson = JObject.Parse(fillData);
            if (fillDataJson.ContainsKey("Error"))
            {
                if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                               .Replace("@ID", checkModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{fillData}");
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.Error, ellapsedWatch);
                Directory.Delete(folderPath, true);
                return;
            }

            #endregion fill logs db

            #region no any unique

            int wpLoginAddedCount = fillDataJson.Value<int>("WpLoginAddedCount");

            if (fillDublicates)
            {
                if (wpLoginAddedCount == 0)
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUniqueEmptyError", dbUser.Language)
                                                                           .Replace("@ID", checkModel.Id.ToString()));

                    MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.NoAnyUnique, ellapsedWatch);
                    Directory.Delete(folderPath, true);
                    return;
                }
            }

            #endregion no any unique

            #region notify when any inserted

            if (fillDublicates)
            {
                if (config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat != 0)
                {
                    try
                    {
                        aggregator.GetEvent<DublicateUpdateEvent>().Publish();
                        await botClient.SendTextMessageAsync(config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat,
                            $"В базу данных дубликатов было добавлено {wpLoginAddedCount} записей:\r\n" +
                            $"Новый записей в категории wp-login: {wpLoginAddedCount}");
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            #endregion notify when any inserted

            #region after checks set status to dublicate deleted

            checkModel.Status = ManualCheckStatus.SendedToSoftCheck;
            await WpLoginCheckController.PutCheckAsync(checkModel, aggregator);

            #endregion after checks set status to dublicate deleted

            #region check wp-login

            string wpLoginCheckingData = await Runner.RunFoxChecker(folderPath, workingFile, config.FoxCheckerMaxForThread);
            JObject wpLoginCheckingJson = JObject.Parse(wpLoginCheckingData);
            if (wpLoginCheckingJson.ContainsKey("Error"))
            {
                if (config.NotifyUserWhenAnyErrorOcuredInCheckingProcess)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileCheckingError", dbUser.Language)
                                                                               .Replace("@ID", checkModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{wpLoginCheckingData}");
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.Error, ellapsedWatch);
                Directory.Delete(folderPath, true);
                return;
            }

            #endregion check wp-login

            #region set to copying files

            checkModel.ShellsFilePath = wpLoginCheckingJson["Shells"].ToString();
            checkModel.CpanelsFilePath = wpLoginCheckingJson["CpanelsReseted"].ToString();
            checkModel.SmtpsFilePath = wpLoginCheckingJson["Smtps"].ToString();
            checkModel.LoggedWordpressFilePath = wpLoginCheckingJson["LoggedWordpress"].ToString();

            MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.CopyingFiles, ellapsedWatch);

            #endregion set to copying files

            #region fill valid db

            string fillValidData = await Runner.RunValidFiller(dbUser.Id, null, null, checkModel.ShellsFilePath, checkModel.CpanelsFilePath, checkModel.SmtpsFilePath, checkModel.LoggedWordpressFilePath);
            JObject fillValidDataJson = JObject.Parse(fillValidData);
            if (fillValidDataJson.ContainsKey("Error"))
            {
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при заполнении валида:\r\n{fillValidData}");
                    }
                    catch (Exception)
                    {
                    }
                }
                return;
            }

            int shellsAddedCount = fillValidDataJson.Value<int>("ShellsAddedCount");
            int cpanelsAddedCount = fillValidDataJson.Value<int>("CpanelsResetedAddedCount");
            int smtpsAddedCount = fillValidDataJson.Value<int>("SmtpsAddedCount");
            int loggedWordpressAddedCount = fillValidDataJson.Value<int>("LoggedWordpressAddedCount");
            int totalAddedCount = shellsAddedCount + cpanelsAddedCount + smtpsAddedCount + loggedWordpressAddedCount;
            if (totalAddedCount != 0)
            {
                if (config.Chats.NotifyWhenDatabaseFillNewValidRecordsChat != 0)
                {
                    try
                    {
                        aggregator.GetEvent<DublicateUpdateEvent>().Publish();
                        await botClient.SendTextMessageAsync(config.Chats.NotifyWhenDatabaseFillNewLogRecordsChat,
                            $"В базу данных валида было добавлено {totalAddedCount} записей:\r\n" +
                            $"Новый записей в категории Shells: {shellsAddedCount}\r\n" +
                            $"Новый записей в категории Cpanels_Reseted: {cpanelsAddedCount}\r\n" +
                            $"Новый записей в категории SMTPs: {smtpsAddedCount}\r\n" +
                            $"Новый записей в категории Logged_Wordpress: {loggedWordpressAddedCount}\r\n");
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            #endregion fill valid db

            #region copying last files and set to checked by soft status

            MoveFilesToChecksIdFolderAndUpateCountAndPathes(checkModel, ManualCheckStatus.CheckedBySoft, null);
            Directory.Delete(folderPath, true);
            if (config.Chats.NotifyWhenCheckerEndWorkChat != 0)
            {
                try
                {
                    await botClient.SendTextMessageAsync(config.Chats.NotifyWhenCheckerEndWorkChat,
                        $"Закончена проверка логов ID: {checkModel.Id} от {checkModel.StartDateTime:dd.MM.yyyy} :\r\n" +
                        $"Загрузил : @{checkModel.FromUsername}\r\n" +
                        $"Затрачено всего : {Math.Round(checkModel.CheckingTimeEllapsed.TotalMinutes, 2)} минут\r\n" +
                        $"Дубликатов найдено: {checkModel.DublicateFoundedCount}\r\n" +
                        $"Shells найдено: {checkModel.ShellsFoundedCount}\r\n" +
                        $"CpanelsReseted найдено: {checkModel.CpanelsResetedFoundedCount}\r\n" +
                        $"SMTP найдено: {checkModel.SmtpsFoundedCount}\r\n" +
                        $"Logged Wordpress найдено: {checkModel.LoggedWordpressFoundedCount}");
                }
                catch (Exception)
                {
                }
            }

            #endregion copying last files and set to checked by soft status
        }

        private async void MoveFilesToChecksIdFolderAndUpateCountAndPathes(WpLoginCheckModel checkModel, ManualCheckStatus endStatus, Stopwatch ellapsedWatch)
        {
            string destinationFolderPath = PathCollection.WpLoginFolderPath + checkModel.Id + "/";
            if (!Directory.Exists(destinationFolderPath)) Directory.CreateDirectory(destinationFolderPath);

            string dublicatePath = destinationFolderPath + "dublicates.txt";
            string shellsPath = destinationFolderPath + "shells.txt";
            string cpanelsResetedPath = destinationFolderPath + "cpanels_reseted.txt";
            string smtpsPath = destinationFolderPath + "smtps.txt";
            string loggedWordpressPath = destinationFolderPath + "logged_wordpress.txt";

            if (System.IO.File.Exists(checkModel.DublicateFilePath) && checkModel.DublicateFilePath != dublicatePath)
            {
                System.IO.File.Copy(checkModel.DublicateFilePath, dublicatePath, true);
                checkModel.DublicateFilePath = dublicatePath;
                checkModel.DublicateFoundedCount = System.IO.File.ReadAllLines(checkModel.DublicateFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.ShellsFilePath) && checkModel.ShellsFilePath != shellsPath)
            {
                System.IO.File.Copy(checkModel.ShellsFilePath, shellsPath, true);
                checkModel.ShellsFilePath = shellsPath;
                checkModel.ShellsFoundedCount = System.IO.File.ReadAllLines(checkModel.ShellsFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.CpanelsFilePath) && checkModel.CpanelsFilePath != cpanelsResetedPath)
            {
                System.IO.File.Copy(checkModel.CpanelsFilePath, cpanelsResetedPath, true);
                checkModel.CpanelsFilePath = cpanelsResetedPath;
                checkModel.CpanelsResetedFoundedCount = System.IO.File.ReadAllLines(checkModel.CpanelsFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.SmtpsFilePath) && checkModel.SmtpsFilePath != smtpsPath)
            {
                System.IO.File.Copy(checkModel.SmtpsFilePath, smtpsPath, true);
                checkModel.SmtpsFilePath = smtpsPath;
                checkModel.SmtpsFoundedCount = System.IO.File.ReadAllLines(checkModel.SmtpsFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }
            if (System.IO.File.Exists(checkModel.LoggedWordpressFilePath) && checkModel.LoggedWordpressFilePath != loggedWordpressPath)
            {
                System.IO.File.Copy(checkModel.LoggedWordpressFilePath, loggedWordpressPath, true);
                checkModel.LoggedWordpressFilePath = loggedWordpressPath;
                checkModel.LoggedWordpressFoundedCount = System.IO.File.ReadAllLines(checkModel.LoggedWordpressFilePath).Where(l => !l.IsNullOrEmptyString()).Count();
            }

            checkModel.Status = endStatus;
            if (ellapsedWatch != null)
            {
                ellapsedWatch.Stop();
                checkModel.CheckingTimeEllapsed = ellapsedWatch.Elapsed;
                checkModel.EndDateTime = DateTime.Now;
            }
            await WpLoginCheckController.PutCheckAsync(checkModel, aggregator);
        }

        #endregion wp login checking

        #region user managment

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

        #endregion user managment

        #region mail sender

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

        public async Task<bool> SendQuestionsToUser(UserModel dbUser)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("QuestionForumInfo", dbUser.Language), replyMarkup: keyboards.GetByLocale("ForumsInfoAnswers", dbUser.Language, false));
                OperationModel userOperation = operations.FirstOrDefault(o => o.UserId == dbUser.Id);
                if (userOperation != null)
                {
                    operations.Remove(userOperation);
                }
                operations.Add(new(dbUser.Id, OperationType.WaitUserForumInfo));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsUserAlreadyInProgressQuestions(UserModel dbUser)
        {
            OperationModel userOperation = operations.FirstOrDefault(o => o.UserId == dbUser.Id);
            if (userOperation != null &&
                (userOperation.OperationType == OperationType.WaitUserForumInfo ||
                 userOperation.OperationType == OperationType.WaitUserLogsOriginInfo))
            {
                return true;
            }
            return false;
        }

        #endregion mail sender

        #region notifications

        public async Task NotifyUserBalanceChanged(UserModel dbUser, bool isPositiveBalance)
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

        public async Task NotifyUserForEndCheckingFile(UserModel dbUser, CpanelWhmCheckModel manualCheck, int totalValid, int addBalance)
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

        public async Task NotifyUserForEndCheckingCookies(UserModel dbUser, CookieModel model)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CookieCheckingComplete", dbUser.Language)
                                                                       .Replace("@ID", model.Id.ToString())
                                                                       .Replace("@VALID", model.ValidFound.ToString())
                                                                       .Replace("@BALANCE", model.BalanceToUser.ToString())
                                                                       .Replace("@CURRENCY", config.Currency)
                                                                       );
            }
            catch (Exception)
            {
            }
        }

        public async Task NotifyUserForEndCheckingCookiesNoValid(UserModel dbUser, int checkId)
        {
            try
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("CookieUniqueEmptyError", dbUser.Language)
                                                                       .Replace("@ID", checkId.ToString()));
            }
            catch (Exception)
            {
            }
        }

        #endregion notifications
    }
}