using BotMainApp.Events;
using BotMainApp.ViewModels;
using DataAdapter.Controllers;
using Extensions;
using Models.App;
using Models.Database;
using Models.Enums;
using Prism.Events;
using Services.Interfaces;
using SimpleLogger.FileService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramSimpleService;

namespace BotMainApp.Telegram
{
    public class UpdateHandler : IUpdateHandler
    {
        private static readonly ReplyKeyboardRemove emptyKeyboard = new();
        private static List<LocaleStringModel> locales;
        private static Dictionary<string, ReplyKeyboardMarkup> keyboards;
        private static ObservableCollection<OperationModel> operations;
        private IFileLogger logger;
        private IEventAggregator aggregator;
        private static TelegramBotClient botClient;

        public async Task ConfigureServicesAsync(IFileLogger logger, IJsonAdapter jsonAdapter, IKeyboardService keyboardService, IEventAggregator aggregator, TelegramBotClient botClient)
        {
            this.logger = logger;
            this.aggregator = aggregator;
            UpdateHandler.botClient = botClient;
            operations = await jsonAdapter.ReadJsonOperationsAsync();
            locales = await jsonAdapter.ReadJsonLocaleStringsAsync();
            keyboardService.SetStoreFileName("/config/keys.json", null);
            keyboards = keyboardService.LoadOneRowKeyboards();
            logger.Info("tg handler init done");
        }

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
                                if (await UsersController.PutUserAsync(dbUser))
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
                                if (await UsersController.PutUserAsync(dbUser))
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
                    }))
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
                    }))
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
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("ошибка", TelegramStateModel.RedBrush));
            logger.Fatal(exception.Message);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            botClient.StartReceiving(this);
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("работает", TelegramStateModel.GreenBrush));
        }

        public static async Task<bool> AcceptTelegramUserAsync(UserModel dbUser)
        {
            try
            {
                dbUser.IsAccepted = true;
                if (await UsersController.PutUserAsync(dbUser))
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

        public static async Task MoveToBLUserAsync(UserModel dbUser)
        {
            dbUser.IsBanned = true;
            if (await UsersController.PutUserAsync(dbUser))
            {
                List<OperationModel> userOperations = operations.Where(u => u.UserId == dbUser.Id).ToList();
                foreach (OperationModel operation in userOperations)
                {
                    operations.Remove(operation);
                }
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminMoveToBlackList", dbUser.Language), replyMarkup: emptyKeyboard);
            }
        }

        public static async Task MoveFromBLUserAsync(UserModel dbUser)
        {
            dbUser.IsBanned = false;
            if (await UsersController.PutUserAsync(dbUser))
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminMoveFromBlackList", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
            }
        }
    }
}