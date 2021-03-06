﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using BotHandlers.Abstracts;
using BotHandlers.Static;
using Telegram.Bot;

namespace TelegramBot.Models
{
    public class TelegramPhoto : AbstractPhoto
    {
        private readonly TelegramBotClient botClient;
        private Action photoUploader;

        public TelegramPhoto(string cachePath, long id, TelegramBotClient botClient) : base(cachePath, id)
        {
            this.botClient = botClient;
        }

        public override bool GetPresetPhoto(string name)
        {
            switch (name)
            {
                case "delve":
                {
                    Content = new[] {"AgADAgADr6wxGxVgSEs6sklltPi6ZAOUwg8ABAEAAwIAA3kAA_pSAQABFgQ"};
                    return true;
                }
                case "incursion":
                {
                    Content = new[] {"AgADAgADsKwxGxVgSEu-Fwh4MWJZolzPuQ8ABAEAAwIAA3cAA91WBgABFgQ"};
                    return true;
                }
                case "betrayal":
                {
                    Content = new[] {"AgADAgADtawxGxVgSEu2WI3Xvp2ohJPYuQ8ABAEAAwIAA3cAA3NNBgABFgQ"};
                    return true;
                }
                default:
                {
                    return false;
                }
            }
        }

        public override string[] GetContent()
        {
            if (Content == null)
            {
                photoUploader?.Invoke();
            }

            return (string[]) Content?.Clone();
        }

        public override bool LoadPhotoFromFile(string name)
        {
            var item = name.Replace(' ', '-').Replace("'", "");
            var lines = File.ReadAllLines(CachePath);
            foreach (var line in lines)
            {
                var data = line.Split(' ');
                if (data[0].Equals(item, StringComparison.OrdinalIgnoreCase))
                {
                    Content = new[] {data[1]};
                    return true;
                }
            }

            return false;
        }

        public override bool SavePhoto(string name, byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            photoUploader = () =>
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    var returnedMessage = botClient.SendPhotoAsync(Id, stream).Result;
                    using var streamWriter = new StreamWriter(CachePath, true, Encoding.Default);
                    streamWriter.WriteLine("{0} {1}", name, returnedMessage.Photo.Last().FileId);
                }
                catch (Exception ex)
                {
                    Common.Logger?.LogError(ex);
                }
            };

            return true;
        }

        public override bool UploadPhoto(byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            photoUploader = async () =>
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    await botClient.SendPhotoAsync(Id, stream);
                }
                catch (Exception ex)
                {
                    Common.Logger?.LogError(ex);
                }
            };

            return true;
        }
    }
}