﻿using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types.ReplyMarkups;
using System;




namespace WeatherBot
{
    class Program
    {
        private static readonly string BotToken = "7799928283:AAESCCosJnkx9NRR6D-PwKT9gIBFdFAWrqs";
        private static readonly string WeatherApiKey = "0fd5ae621f0c01510d4f0b3a57362fc1";

        static async Task Main(string[] args) 
        {
            var botClient = new TelegramBotClient(BotToken);
            var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync("https://api.telegram.org");
            Console.WriteLine($"Telegram API Status: {response.StatusCode}");

            botClient.StartReceiving(HandleUpdate, Error, receiverOptions);
            Console.WriteLine("Starting bot...");

            await Task.Delay(-1); 
        }


        static async Task HandleUpdate(ITelegramBotClient botClient, Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message) return;

            Console.WriteLine($"{message.Text}");

            if (message.Text == "/start")
            {
                KeyboardButton[] keyboard = new KeyboardButton[]
                {
                        KeyboardButton.WithRequestLocation("Поділитися геолокацією"),
                        new KeyboardButton("Інструкція")
                };

                ReplyKeyboardMarkup replyKeyboardMarkup = new ReplyKeyboardMarkup(keyboard)
                {
                    ResizeKeyboard = true
                };
                await botClient.SendMessage(chatId: message.Chat.Id, "Привіт! Обери опцію нижче:", replyMarkup: replyKeyboardMarkup);
                return;
            }

            if (message.Text == "Інструкція")
            {

                await botClient.SendMessage(message.Chat.Id, "📝 Інструкція:\n" +
                                          "1. Використовуй команду '/start', щоб відкрити меню.\n" +
                                          "2. Використовуй команду `/weather (місто)` або просто надай геолокацію, щоб дізнатися погоду в місті.\n" +
                                          "3. Використовуй команду '/forecast (місто)' щоб дізнатися прогноз погоди на 5 днів.");

            }

            if (message.Location != null)
            {
                double latitude = message.Location.Latitude;
                double longitude = message.Location.Longitude;

                string city = await GetCityByCoordinates(latitude, longitude);

                if (city != null)
                {
                    string weather = await GetWeatherAsync(city);
                    await botClient.SendMessage(message.Chat.Id, $"🌍 Ви у {city}.\n{weather}");
                }
                else
                {
                    await botClient.SendMessage(message.Chat.Id, "❌ Не вдалося визначити ваше місто.");
                }
            }

            if (message.Text != null && message.Text.StartsWith("/weather"))
            {
                string[] parts = message.Text.Split(" ", 2);
                if (parts.Length < 2)
                {
                    await botClient.SendMessage(message.Chat.Id, "❌ Вкажи місто після команди! По шаблону: /weather (місто)");
                    return;
                }

                string city = parts[1];
                string weather = await GetWeatherAsync(city);
                await botClient.SendMessage(message.Chat.Id, $"\n{weather}");

                return;
            }
            if (message.Text != null && message.Text.StartsWith("/forecast"))
            {
                string[] parts = message.Text.Split(" ", 2);
                if (parts.Length < 2)
                {
                    await botClient.SendMessage(message.Chat.Id, "❌ Вкажи місто після команди! По шаблону: /forecast (місто)");
                    return;
                }

                string city = parts[1];
                string weather = await GetWeatherForForecastAsync(city);
                await botClient.SendMessage(message.Chat.Id, $"\n{weather}");
            }

        }


        static async Task<string> GetCityByCoordinates(double latitude, double longitude)
        {
            using HttpClient client = new();
            string url = $"http://api.openweathermap.org/geo/1.0/reverse?lat={latitude}&lon={longitude}&limit=1&appid={WeatherApiKey}";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JArray locationData = JArray.Parse(responseBody);
                if (locationData.Count > 0)
                {
                    return locationData[0]["name"].ToString();
                }
            }
            catch (HttpRequestException)
            {
                return null;
            }
            return null;
        }

        static async Task<string> GetWeatherAsync(string city)
        {
            using HttpClient client = new();
            string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={WeatherApiKey}&units=metric&lang=ua";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject weatherData = JObject.Parse(responseBody);
                double temp = weatherData["main"]["temp"].Value<double>();
                string description = weatherData["weather"][0]["description"].ToString();
                string icon = weatherData["weather"][0]["icon"].ToString();
                string emoji = GetWeatherEmoji(icon);

                return $"🌍 Погода в *{city}*:\n{emoji} {description}\n🌡 Температура: *{temp}°C*";
            }
            catch (HttpRequestException)
            {
                return "❌ Не вдалося отримати дані про погоду. Переконайся, що місто вказано правильно.";
            }
        }

        static async Task<string> GetWeatherForForecastAsync(string city)
        {
            using HttpClient client = new();
            string url = $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={WeatherApiKey}&units=metric&lang=ua";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                JObject data = JObject.Parse(json);

                if (data["list"] != null && data["list"].HasValues)
                {
                    var forecasts = data["list"]
                        .Where(f => f["dt_txt"] != null && f["dt_txt"].ToString().Contains("12:00:00"))
                        .Take(5)
                        .Select(forecast =>
                        {
                            double temp = (double)forecast["main"]["temp"];
                            string description = (string)forecast["weather"]?[0]?["description"] ?? "невідомо";
                            string dateTime = (string)forecast["dt_txt"];
                            return $"📅 {dateTime}: 🌡 {temp}°C, {description}";
                        });

                    return $"🌍 Прогноз погоди у {city} на 5 днів:\n" + string.Join("\n", forecasts);
                }
                else
                {
                    return "❌ Дані про погоду відсутні. Спробуй ще раз.";
                }
            }
            catch (HttpRequestException)
            {
                return "❌ Не вдалося отримати дані про погоду. Переконайся, що місто вказано правильно.";
            }
            catch (Exception ex)
            {
                return $"❌ Помилка: {ex.Message}";
            }
        }


        static string GetWeatherEmoji(string icon)
        {
            return icon switch
            {
                var i when i.StartsWith("01") => "☀️",
                var i when i.StartsWith("02") => "🌤",
                var i when i.StartsWith("03") => "🌥",
                var i when i.StartsWith("04") => "☁️",
                var i when i.StartsWith("09") => "🌧",
                var i when i.StartsWith("10") => "🌦",
                var i when i.StartsWith("11") => "⛈",
                var i when i.StartsWith("13") => "❄️",
                var i when i.StartsWith("50") => "🌫"
            };
        }

        private static async Task Error(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            Console.WriteLine($"Error: {exception.Message}");
        }

    }
}
