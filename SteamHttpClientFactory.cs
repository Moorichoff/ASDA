using System;
using System.Net;
using System.Net.Http;

namespace SteamGuard
{
    /// <summary>
    /// Фабрика для создания настроенных HttpClient экземпляров
    /// </summary>
    public class SteamHttpClientFactory
    {
        private static readonly Lazy<HttpClient> _sharedClient = new Lazy<HttpClient>(() => CreateDefaultClient());

        /// <summary>
        /// Получить общий HttpClient для простых запросов без cookies
        /// </summary>
        public static HttpClient GetSharedClient()
        {
            return _sharedClient.Value;
        }

        /// <summary>
        /// Создать HttpClient с cookies для аутентифицированных запросов
        /// </summary>
        public static HttpClient CreateAuthenticatedClient(SteamAccount account)
        {
            var handler = new HttpClientHandler();
            var cookieContainer = new CookieContainer();

            if (!string.IsNullOrEmpty(account.Session?.SessionId))
            {
                cookieContainer.Add(new Cookie("sessionid", account.Session.SessionId)
                {
                    Domain = "steamcommunity.com",
                    Path = "/"
                });
                AppLogger.Debug($"Added sessionid cookie: {account.Session.SessionId}");
            }
            else
            {
                AppLogger.Warn("SessionId is empty!");
            }

            if (!string.IsNullOrEmpty(account.Session?.SteamLoginSecure))
            {
                cookieContainer.Add(new Cookie("steamLoginSecure", account.Session.SteamLoginSecure)
                {
                    Domain = "steamcommunity.com",
                    Path = "/"
                });
                AppLogger.Debug($"Added steamLoginSecure cookie (length: {account.Session.SteamLoginSecure.Length})");
            }
            else
            {
                AppLogger.Warn("SteamLoginSecure is empty!");
            }

            handler.CookieContainer = cookieContainer;
            var client = new HttpClient(handler);
            ConfigureDefaultHeaders(client);
            return client;
        }

        /// <summary>
        /// Создать HttpClient для Steam Community API
        /// </summary>
        public static HttpClient CreateCommunityClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(Constants.SteamCommunityUrl)
            };
            ConfigureDefaultHeaders(client);
            return client;
        }

        /// <summary>
        /// Создать HttpClient для Steam Web API
        /// </summary>
        public static HttpClient CreateApiClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(Constants.SteamApiBase)
            };
            ConfigureDefaultHeaders(client);
            return client;
        }

        private static HttpClient CreateDefaultClient()
        {
            var client = new HttpClient();
            ConfigureDefaultHeaders(client);
            return client;
        }

        private static void ConfigureDefaultHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }
    }
}
