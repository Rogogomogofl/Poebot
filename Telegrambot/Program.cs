﻿using Bot;
using MihaZupan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using Telegram.Bot;

namespace Telegrambot
{
    internal class Program
    {
        private static TelegramBotClient _telegramBot;
        private const string SubPath = @"bot/telegramsub.txt";
        private const string CachePath = @"bot/telegramcache.txt";
        private const string LangPath = @"bot/telegramlang.txt";
        private const string LogPath = @"bot/telegramlog.txt";
        private static readonly Poewatch Poewatch = new Poewatch();
        private static SyndicationItem _lastEn, _lastRu;
        private static Timer _rssUpdate;

        private static void Main()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (!File.Exists(SubPath)) File.Create(SubPath).Close();
            if (!File.Exists(CachePath)) File.Create(CachePath).Close();
            if (!File.Exists(LangPath)) File.Create(LangPath).Close();
            if (!File.Exists(LogPath)) File.Create(LogPath).Close();

            _rssUpdate = new Timer(5 * 60 * 1000);
            _rssUpdate.Elapsed += UpdateRss;
            _rssUpdate.AutoReset = true;
            _rssUpdate.Enabled = true;

            var proxy = new HttpToSocks5Proxy("103.111.183.18",1080)
            {
                ResolveHostnamesLocally = true
            };
            _telegramBot = new TelegramBotClient(File.ReadAllText("bot/telegramtoken.txt"), proxy);
            //var result = telegramBot.GetMeAsync().Result;
            _telegramBot.OnMessage += TelegramBot_OnMessage;
            _telegramBot.StartReceiving();
            Console.WriteLine("Working");
            while (true) ;
        }

        private static async void TelegramBot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
#pragma warning disable CA2007 // Попробуйте вызвать ConfigureAwait для ожидаемой задачи
            await Task.Run(() =>
            {
                if (e.Message.Text != null)
                {
                    if (e.Message.Date.AddMinutes(2) < DateTime.Now.ToUniversalTime()) return;
                    var sobaka = e.Message.Text.Split('@');
                    if (sobaka.Length > 1)
                    {
                        if (sobaka[1] == "poeinfobot") e.Message.Text = sobaka[0];
                        else return;
                    }
                    var chats = File.ReadAllLines(LangPath);//to do
                    Poebot poebot = new Poebot(Poewatch, new TelegramPhoto(CachePath, e.Message.Chat.Id, _telegramBot));
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    string request = e.Message.Text;
                    if (request.Contains("/sub ")) request += "+" + e.Message.Chat.Id + "+" + SubPath;

                    Message message = poebot.ProcessRequest(request);
                    if (message == null) return;
                    if (message.Text != null) _telegramBot.SendTextMessageAsync(chatId: e.Message.Chat.Id, text: message.Text);
                    var content = message.Photo?.GetContent();
                    if (content != null)
                    {
                        _telegramBot.SendPhotoAsync(chatId: e.Message.Chat.Id, photo: content[0]);
                    }
                    sw.Stop();
                    if (!(request.Contains("/help") || request.Contains("/start")))
                        Log(request, message.Text ?? "", sw.ElapsedMilliseconds.ToString());
                }
            });
#pragma warning restore CA2007 // Попробуйте вызвать ConfigureAwait для ожидаемой задачи
        }

        private static void UpdateRss(object sender, ElapsedEventArgs e)
        {
            try
            {
                List<string> subs = File.ReadAllLines(SubPath).ToList();
                using (var r = XmlReader.Create("https://www.pathofexile.com/news/rss"))
                {
                    var feed = SyndicationFeed.Load(r);
                    var last = feed.Items.OrderByDescending(x => x.PublishDate).First();
                    if (_lastEn == null) _lastEn = last;
                    if (last.Links[0].Uri != _lastEn.Links[0].Uri)
                    {
                        _lastEn = last;
                        var enSubs = subs.Where(x => Regex.IsMatch(x, @"\d+\sen"));
                        foreach (var sub in enSubs)
                            _telegramBot.SendTextMessageAsync(chatId: long.Parse(sub.Split(' ')[0]), text: _lastEn.Title.Text + '\n' + _lastEn.Links[0].Uri);
                    }
                }
                using (var r = XmlReader.Create("https://ru.pathofexile.com/news/rss"))
                {
                    var feed = SyndicationFeed.Load(r);
                    var last = feed.Items.OrderByDescending(x => x.PublishDate).First();
                    if (_lastRu == null) _lastRu = last;
                    if (last.Links[0].Uri != _lastRu.Links[0].Uri)
                    {
                        _lastRu = last;
                        var ruSubs = subs.Where(x => Regex.IsMatch(x, @"\d+\sru"));
                        foreach (var sub in ruSubs)
                            _telegramBot.SendTextMessageAsync(chatId: long.Parse(sub.Split(' ')[0]), text: _lastRu.Title.Text + '\n' + _lastRu.Links[0].Uri);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: {ex.Message} at {GetType()}");
            }
        }

        public new static Type GetType()
        {
            return typeof(Program);
        }

        private static void Log(string request, string responce, string time)
        {
            using (StreamWriter streamWriter = new StreamWriter(LogPath, true, Encoding.Default))
            {
                streamWriter.WriteLine($"{DateTime.Now}\nЗапрос:\n{request}\n\nОтвет:\n{responce}\nВремя ответа: {time}\n------------");
            }
        }
    }
}
