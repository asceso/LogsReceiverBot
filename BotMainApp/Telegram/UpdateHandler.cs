using BotMainApp.Events;
using BotMainApp.ViewModels;
using DataAdapter.Controllers;
using Extensions;
using Models.App;
using Models.Database;
using Models.Enums;
using Prism.Events;
using Services.Interfaces;
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

namespace BotMainApp.Telegram
{
    public class UpdateHandler : IUpdateHandler
    {
        private readonly ReplyKeyboardRemove emptyKeyboard = new();
        private TelegramBotClient botClient;
        private List<LocaleStringModel> locales;
        private Dictionary<string, ReplyKeyboardMarkup> keyboards;
        private ObservableCollection<OperationModel> operations;
        private readonly IEventAggregator aggregator;

        public UpdateHandler(IEventAggregator aggregator, IMemorySaver memory)
        {
            this.aggregator = aggregator;
            botClient = memory.GetItem<TelegramBotClient>("BotClient");
            locales = memory.GetItem<List<LocaleStringModel>>("Locales");
            keyboards = memory.GetItem<Dictionary<string, ReplyKeyboardMarkup>>("Keyboards");
            operations = memory.GetItem<ObservableCollection<OperationModel>>("Operations");
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
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("ошибка", TelegramStateModel.RedBrush));
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            botClient.StartReceiving(this);
            aggregator.GetEvent<TelegramStateEvent>().Publish(new("работает", TelegramStateModel.GreenBrush));
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

        public async Task MoveToBLUserAsync(UserModel dbUser)
        {
            dbUser.IsBanned = true;
            if (await UsersController.PutUserAsync(dbUser, aggregator))
            {
                List<OperationModel> userOperations = operations.Where(u => u.UserId == dbUser.Id).ToList();
                foreach (OperationModel operation in userOperations)
                {
                    operations.Remove(operation);
                }
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminMoveToBlackList", dbUser.Language), replyMarkup: emptyKeyboard);
            }
        }

        public async Task MoveFromBLUserAsync(UserModel dbUser)
        {
            dbUser.IsBanned = false;
            if (await UsersController.PutUserAsync(dbUser, aggregator))
            {
                await botClient.SendTextMessageAsync(dbUser.Id, locales.GetByKey("AdminMoveFromBlackList", dbUser.Language), replyMarkup: keyboards.GetByLocale("Main", dbUser.Language));
            }
        }
    }
}