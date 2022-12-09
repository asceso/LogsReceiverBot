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

        private readonly NotificationManager notificationManager;
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
                        if (argument.IsAnyEqual(":20", "wp-login.php"))
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
                            Regex dropMeRegex = new(@"https:\/\/dropmefiles\.com\/.*");
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
                                CpanelWhmCheckModel manualCheckModel = new()
                                {
                                    StartDateTime = DateTime.Now,
                                    Status = CheckStatus.ManualCheckStatus.Created,
                                    FromUserId = dbUser.Id,
                                    FromUsername = dbUser.Username,
                                };
                                await CpanelWhmCheckController.PostCheckAsync(manualCheckModel, aggregator);
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                           .Replace("@ID", manualCheckModel.Id.ToString()),
                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                    );

                                #endregion accept file

                                await Task.Factory.StartNew(async () => await StandartCheckProcess(dbUser, filename, manualCheckModel, true)).ConfigureAwait(false);
                            }
                            if (temp.Operation.Params["Category"].ToString().IsAnyEqual("Private requests", "Приватные запросы") &&
                                temp.Operation.Params["SubCategory"].ToString().IsAnyEqual(":20"))
                            {
                            }
                            else if (temp.Operation.Params["Category"].ToString().IsAnyEqual("AdminChecking") &&
                                     temp.Operation.Params["SubCategory"].ToString().IsAnyEqual("AdminChecking"))
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
                                CpanelWhmCheckModel manualCheckModel = new()
                                {
                                    StartDateTime = DateTime.Now,
                                    Status = CheckStatus.ManualCheckStatus.Created,
                                    FromUserId = dbUser.Id,
                                    FromUsername = dbUser.Username,
                                };
                                await CpanelWhmCheckController.PostCheckAsync(manualCheckModel, aggregator);
                                await botClient.SendTextMessageAsync(
                                    dbUser.Id,
                                    locales.GetByKey("FileAcceptedWaitResult", dbUser.Language)
                                           .Replace("@ID", manualCheckModel.Id.ToString()),
                                    replyMarkup: keyboards.GetByLocale("Main", dbUser.Language, payoutEnabled)
                                    );

                                #endregion accept file

                                await Task.Factory.StartNew(async () => await StandartCheckProcess(dbUser, filename, manualCheckModel, false)).ConfigureAwait(false);
                            }
                            else if (temp.Operation.Params["Category"].ToString().IsAnyEqual("Cookies", "Cookie файлы") &&
                                     temp.Operation.Params["SubCategory"].ToString().IsAnyEqual("Instagram"))
                            {
                                #region dropmefiles checking

                                if (!argument.IsNullOrEmptyString())
                                {
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
                List<CpanelWhmCheckModel> checks = await CpanelWhmCheckController.GetChecksByUserIdAsync(dbUser.Id);
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
            else if (temp.Message.IsAnyEqual("/check_log_admin") && config.EnableAdminCheckCommand)
            {
                operations.Add(new(temp.Uid, OperationType.WaitFileForChecking,
                    new KeyValuePair<string, object>("Category", "AdminChecking"),
                    new KeyValuePair<string, object>("SubCategory", "AdminChecking")
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

        #region cpanel and whm checking

        private async Task StandartCheckProcess(UserModel dbUser, string filename, CpanelWhmCheckModel manualCheckModel, bool fillDublicates)
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
                                                                               .Replace("@ID", manualCheckModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }

                MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.Error, ellapsedWatch);
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

            manualCheckModel.DublicateFilePath = dublicateDataJson["Dublicates"].ToString();
            manualCheckModel.WebmailFilePath = dublicateDataJson["Webmail"].ToString();
            string cpanelDataFilePath = dublicateDataJson["Cpanel"].ToString();
            string whmDataFilePath = dublicateDataJson["Whm"].ToString();
            MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.FillingDb, null);

            #endregion check dublicates

            #region fill logs db

            string fillData = await Runner.RunDublicateFiller(dbUser.Id, manualCheckModel.WebmailFilePath, cpanelDataFilePath, whmDataFilePath, fillDublicates);
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
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                    }
                    catch (Exception)
                    {
                    }
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

            if (fillDublicates)
            {
                if (totalAddedCount == 0)
                {
                    await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("FileUniqueEmptyError", dbUser.Language)
                                                                           .Replace("@ID", manualCheckModel.Id.ToString()));

                    MoveFilesToChecksIdFolderAndUpateCountAndPathes(manualCheckModel, CheckStatus.ManualCheckStatus.NoAnyUnique, ellapsedWatch);
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

            manualCheckModel.Status = CheckStatus.ManualCheckStatus.SendedToSoftCheck;
            await CpanelWhmCheckController.PutCheckAsync(manualCheckModel, aggregator);

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
                                                                               .Replace("@ID", manualCheckModel.Id.ToString()));
                    }
                    catch (Exception)
                    {
                    }
                }
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при проверке файла:\r\n{dublicateData}");
                    }
                    catch (Exception)
                    {
                    }
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

            string fillValidData = await Runner.RunValidFiller(dbUser.Id, manualCheckModel.CpanelGoodFilePath, manualCheckModel.WhmGoodFilePath);
            JObject fillValidDataJson = JObject.Parse(fillValidData);
            if (fillValidDataJson.ContainsKey("Error"))
            {
                if (config.Chats.ErrorNotificationChat != 0)
                {
                    try
                    {
                        await botClient.SendTextMessageAsync(config.Chats.ErrorNotificationChat, $"Ошибка при заполнении валида:\r\n{dublicateData}");
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
        }

        private async void MoveFilesToChecksIdFolderAndUpateCountAndPathes(CpanelWhmCheckModel manualCheck, CheckStatus.ManualCheckStatus endStatus, Stopwatch ellapsedWatch)
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
            await CpanelWhmCheckController.PutCheckAsync(manualCheck, aggregator);
        }

        #endregion cpanel and whm checking

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