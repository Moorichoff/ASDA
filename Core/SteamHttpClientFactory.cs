using System;
using System.Linq;
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
        public static HttpClient CreateAuthenticatedClient(SteamAccount account, SettingsManager? settingsManager = null)
        {
            var handler = new HttpClientHandler();
            var cookieContainer = new CookieContainer();

            // Определяем какой прокси использовать
            MaFileProxy? proxyToUse = account.Proxy;

            // Если у аккаунта нет своего прокси, проверяем глобальный
            if (proxyToUse == null && settingsManager != null && !string.IsNullOrEmpty(settingsManager.Settings.GlobalProxy))
            {
                var globalProxyName = settingsManager.Settings.GlobalProxy;
                var globalProxySettings = settingsManager.Settings.Proxies.FirstOrDefault(p => p.Name == globalProxyName);

                if (globalProxySettings != null && !string.IsNullOrEmpty(globalProxySettings.Address))
                {
                    var parts = globalProxySettings.Address.Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int port))
                    {
                        proxyToUse = new MaFileProxy
                        {
                            Id = 1,
                            Data = new ProxyData
                            {
                                Protocol = 0,
                                Address = parts[0],
                                Port = port,
                                Username = globalProxySettings.Username,
                                Password = globalProxySettings.Password,
                                AuthEnabled = !string.IsNullOrEmpty(globalProxySettings.Username)
                            }
                        };
                        AppLogger.Debug($"Using global proxy: {parts[0]}:{port}");
                    }
                }
            }

            // Настройка прокси
            if (proxyToUse?.Data != null && !string.IsNullOrEmpty(proxyToUse.Data.Address))
            {
                var proxyUri = new Uri($"http://{proxyToUse.Data.Address}:{proxyToUse.Data.Port}");
                var proxy = new WebProxy(proxyUri)
                {
                    // КРИТИЧНО: Запрещаем bypass прокси для любых адресов
                    // Это гарантирует, что если прокси упадет - запрос не уйдет напрямую
                    BypassProxyOnLocal = false
                };

                // Если есть авторизация для прокси
                if (proxyToUse.Data.AuthEnabled &&
                    !string.IsNullOrEmpty(proxyToUse.Data.Username) &&
                    !string.IsNullOrEmpty(proxyToUse.Data.Password))
                {
                    proxy.Credentials = new NetworkCredential(
                        proxyToUse.Data.Username,
                        proxyToUse.Data.Password
                    );
                }

                handler.Proxy = proxy;
                handler.UseProxy = true;

                // КРИТИЧНО: Запрещаем использование системных прокси настроек
                // Если наш прокси упадет - соединение должно упасть, а не переключиться на системный прокси
                handler.DefaultProxyCredentials = null;
                handler.UseDefaultCredentials = false;

                AppLogger.Debug($"Using proxy with IP leak protection: {proxyToUse.Data.Address}:{proxyToUse.Data.Port}");
            }
            else
            {
                // Если прокси не настроен, но это требуется - можно добавить проверку
                // Пока разрешаем прямое соединение только если прокси явно не указан
                handler.UseProxy = false;
                AppLogger.Debug("No proxy configured, using direct connection");
            }

            if (!string.IsNullOrEmpty(account.Session?.SessionId))
            {
                // Добавляем для steamcommunity.com
                cookieContainer.Add(new Cookie("sessionid", account.Session.SessionId)
                {
                    Domain = ".steamcommunity.com",
                    Path = "/",
                    Secure = true
                });
                // Добавляем для store.steampowered.com
                cookieContainer.Add(new Cookie("sessionid", account.Session.SessionId)
                {
                    Domain = ".steampowered.com",
                    Path = "/",
                    Secure = true
                });
                AppLogger.Debug($"Added sessionid cookie: {account.Session.SessionId}");
            }
            else
            {
                AppLogger.Warn("SessionId is empty!");
            }

            if (!string.IsNullOrEmpty(account.Session?.SteamLoginSecure))
            {
                // Добавляем для steamcommunity.com
                cookieContainer.Add(new Cookie("steamLoginSecure", account.Session.SteamLoginSecure)
                {
                    Domain = ".steamcommunity.com",
                    Path = "/",
                    Secure = true
                });
                // Добавляем для store.steampowered.com
                cookieContainer.Add(new Cookie("steamLoginSecure", account.Session.SteamLoginSecure)
                {
                    Domain = ".steampowered.com",
                    Path = "/",
                    Secure = true
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

        /// <summary>
        /// Создать защищенный HttpClient с проверкой прокси перед каждым запросом
        /// </summary>
        public static ProxyProtectedHttpClient CreateProtectedAuthenticatedClient(SteamAccount account, SettingsManager? settingsManager = null)
        {
            var client = CreateAuthenticatedClient(account, settingsManager);

            // Определяем параметры прокси для защиты
            string? proxyAddress = null;
            int? proxyPort = null;

            // ВАЖНО: Если прокси настроен - ВСЕГДА требуем его использование для безопасности
            bool requireProxy = false;

            // Проверяем прокси аккаунта
            if (account.Proxy?.Data != null && !string.IsNullOrEmpty(account.Proxy.Data.Address))
            {
                proxyAddress = account.Proxy.Data.Address;
                proxyPort = account.Proxy.Data.Port;
                requireProxy = true; // Прокси настроен - требуем его использование
            }
            // Проверяем глобальный прокси
            else if (settingsManager != null && !string.IsNullOrEmpty(settingsManager.Settings.GlobalProxy))
            {
                var globalProxyName = settingsManager.Settings.GlobalProxy;
                var globalProxySettings = settingsManager.Settings.Proxies.FirstOrDefault(p => p.Name == globalProxyName);

                if (globalProxySettings != null && !string.IsNullOrEmpty(globalProxySettings.Address))
                {
                    var parts = globalProxySettings.Address.Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int port))
                    {
                        proxyAddress = parts[0];
                        proxyPort = port;
                        requireProxy = true; // Прокси настроен - требуем его использование
                    }
                }
            }

            return new ProxyProtectedHttpClient(client, proxyAddress, proxyPort, requireProxy);
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
