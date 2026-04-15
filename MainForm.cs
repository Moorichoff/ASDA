using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace SteamGuard
{
    public partial class MainForm : Form
    {
        private WebView2? webView;
        private AccountManager _accountManager;
        private SettingsManager _settingsManager;
        private SteamAuthenticator? _authenticator;
        private ConfirmationService? _confirmationService;
        private TradeService? _tradeService;
        private MarketService? _marketService;
        private System.Windows.Forms.Timer? _codeTimer;
        private int _timeLeft = 30;
        private readonly string _mafileDirectory;

        public MainForm()
        {
            InitializeComponent();
            _mafileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.MaFileDirectory);

            // Создаём папку mafile если её нет
            if (!Directory.Exists(_mafileDirectory))
            {
                Directory.CreateDirectory(_mafileDirectory);
            }

            _accountManager = new AccountManager(_mafileDirectory);
            _accountManager.LoadAccounts();
            _settingsManager = new SettingsManager();

            InitializeCodeTimer();
        }

        private void InitializeComponent()
        {
            this.Text = "";
            this.Size = new Size(Constants.WindowWidth, Constants.WindowHeight);
            this.MinimumSize = new Size(Constants.WindowWidth, Constants.WindowHeight);
            this.MaximumSize = new Size(Constants.WindowWidth, Constants.WindowHeight);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.Padding = new Padding(0);

            // Load icon
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.IconFileName);
                if (File.Exists(iconPath))
                    this.Icon = new System.Drawing.Icon(iconPath);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Не удалось загрузить иконку: {ex.Message}");
            }

            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };

            webView.NavigationCompleted += WebView_NavigationCompleted;
            webView.WebMessageReceived += WebView_WebMessageReceived;
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;

            this.Controls.Add(webView);
            this.Load += MainForm_Load;
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess && webView?.CoreWebView2 != null)
            {
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            }
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            try
            {
                await webView?.EnsureCoreWebView2Async(null)!;

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.IndexHtmlPath);

                if (File.Exists(htmlPath))
                {
                    webView!.Source = new Uri(htmlPath);
                }
                else
                {
                    MessageBox.Show($"Не найден файл index.html", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                // Initialize services after accounts are set
                InitializeServices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateAccountsList();

            if (_accountManager.CurrentAccount != null && _authenticator != null)
            {
                UpdateCodes();
            }
        }

        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.WebMessageAsJson;

            try
            {
                var message = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                string type = message?["type"]?.ToString() ?? "";

                switch (type)
                {
                    case "RefreshCodes":
                        UpdateCodes();
                        break;

                    case "SwitchAccount":
                        string accountName = message?["accountName"]?.ToString() ?? "";
                        SwitchAccount(accountName);
                        break;

                    case "GenerateCodeForAccount":
                        string targetAccountName = message?["accountName"]?.ToString() ?? "";
                        GenerateCodeForAccount(targetAccountName);
                        break;

                    case "OpenUrl":
                        string url = message?["url"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(url))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        break;

                    case "RefreshAccounts":
                        UpdateAccountsList();
                        break;

                    case "AddAccount":
                        var accountObj = message?["account"];
                        if (accountObj != null)
                        {
                            var account = JsonConvert.DeserializeObject<SteamAccount>(accountObj.ToString() ?? "");
                            if (account != null)
                            {
                                _accountManager.AddAccount(account);
                                UpdateAccountsList();
                                SendToJS("AccountAdded", new { message = "Аккаунт добавлен!" });
                            }
                        }
                        break;

                    case "ImportMaFile":
                        string content = message?["content"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(content))
                        {
                            ImportMaFile(content);
                        }
                        break;

                    case "RefreshConfirmations":
                        _ = RefreshConfirmationsAsync();
                        break;

                    case "AcceptAllConfirmations":
                        _ = AcceptAllConfirmationsAsync();
                        break;

                    case "AcceptConfirmation":
                        string confId = message?["confirmationId"]?.ToString() ?? "";
                        _ = AcceptConfirmationAsync(confId);
                        break;

                    case "DenyConfirmation":
                        string denyId = message?["confirmationId"]?.ToString() ?? "";
                        _ = DenyConfirmationAsync(denyId);
                        break;

                    case "RefreshTrades":
                        _ = RefreshTradesAsync();
                        break;

                    case "AcceptTrade":
                        string tradeId = message?["tradeId"]?.ToString() ?? "";
                        _ = AcceptTradeAsync(tradeId);
                        break;

                    case "DeclineTrade":
                        string declineTradeId = message?["tradeId"]?.ToString() ?? "";
                        _ = DeclineTradeAsync(declineTradeId);
                        break;

                    case "RefreshMarket":
                        _ = RefreshMarketAsync();
                        break;

                    case "CancelListing":
                        string listingId = message?["listingId"]?.ToString() ?? "";
                        _ = CancelListingAsync(listingId);
                        break;

                    case "MinimizeWindow":
                        this.WindowState = FormWindowState.Minimized;
                        break;

                    case "CloseWindow":
                        this.Close();
                        break;

                    case "DragWindow":
                        int deltaX = Convert.ToInt32(message?["deltaX"] ?? 0);
                        int deltaY = Convert.ToInt32(message?["deltaY"] ?? 0);
                        this.Location = new Point(this.Location.X + deltaX, this.Location.Y + deltaY);
                        break;

                    case "OpenAccountSettings":
                        string settingsAccount = message?["accountName"]?.ToString() ?? "";
                        OpenAccountSettingsDialog(settingsAccount);
                        break;

                    case "UpdateAccountSettings":
                        HandleUpdateAccountSettings(message!);
                        break;

                    case "AddAccountDialog":
                        ShowAddAccountDialog();
                        break;

                    case "SubmitLoginCredentials":
                        HandleLoginCredentials(message!);
                        break;

                    case "SubmitEmailCode":
                        HandleEmailCode(message!);
                        break;

                    case "SubmitGuardCode":
                        HandleGuardCode(message!);
                        break;

                    case "CreateGroupDialog":
                        ShowCreateGroupDialog();
                        break;

                    case "CreateGroup":
                        HandleCreateGroup(message!);
                        break;

                    case "OpenSettings":
                        ShowSettingsDialog();
                        break;

                    case "SaveSettings":
                        HandleSaveSettings(message!);
                        break;

                    case "CopyRevocationCode":
                        string revCode = message?["code"]?.ToString() ?? "";
                        Clipboard.SetText(revCode);
                        break;

                    case "GetGroups":
                        SendGroupsToJS();
                        break;

                    case "RemoveAccount":
                        string removeAccount = message?["accountName"]?.ToString() ?? "";
                        RemoveAccount(removeAccount);
                        break;

                    case "RefreshSession":
                        string sessionAccount = message?["accountName"]?.ToString() ?? "";
                        RefreshSession(sessionAccount);
                        break;

                    case "ToggleFavorite":
                        string favoriteAccount = message?["accountName"]?.ToString() ?? "";
                        ToggleFavorite(favoriteAccount);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeCodeTimer()
        {
            _codeTimer = new System.Windows.Forms.Timer();
            _codeTimer.Interval = Constants.CodeTimerIntervalMs;
            _codeTimer.Tick += CodeTimer_Tick;
            _codeTimer.Start();
        }

        private void CodeTimer_Tick(object? sender, EventArgs e)
        {
            _timeLeft--;

            if (_timeLeft <= 0)
            {
                _timeLeft = Constants.TotpPeriodSeconds;
                UpdateCodes();
            }

            _ = ExecuteScriptAsync($"updateTimer({_timeLeft})");
        }

        private void UpdateCodes()
        {
            if (_authenticator == null) return;

            try
            {
                var codes = _authenticator.GetCodes();
                var codesObj = new
                {
                    previous = codes.Previous,
                    current = codes.Current,
                    next = codes.Next
                };

                SendToJS("UpdateCodes", new { codes = codesObj, timeLeft = _timeLeft });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации кода: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateAccountsList()
        {
            var accountsData = _accountManager.Accounts.Select(a => new
            {
                username = a.Username,
                group = a.Group,
                hasSession = a.HasSession,
                autoTrade = a.AutoTrade,
                autoMarket = a.AutoMarket,
                hasProxy = a.Proxy != null,
                isFavorite = a.IsFavorite,
                steamId = a.SteamId.ToString()
            }).ToList();

            SendToJS("UpdateAccounts", new { accounts = accountsData });
        }

        private void SwitchAccount(string accountName)
        {
            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);

            if (account != null)
            {
                _accountManager.SetCurrentAccount(account);

                try
                {
                    _authenticator = new SteamAuthenticator(account.SharedSecret);
                    InitializeServices();
                    UpdateCodes();

                    SendToJS("AccountSwitched", new { username = accountName });
                }
                catch (Exception ex)
                {
                    SendToJS("Error", new { message = ex.Message });
                }
            }
        }

        private void GenerateCodeForAccount(string accountName)
        {
            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);
            if (account != null && !string.IsNullOrEmpty(account.SharedSecret))
            {
                try
                {
                    var authenticator = new SteamAuthenticator(account.SharedSecret);
                    var code = authenticator.GenerateCode();
                    SendToJS("CodeGenerated", new { accountName = accountName, code = code });
                }
                catch (Exception ex)
                {
                    SendToJS("Error", new { message = $"Ошибка генерации кода: {ex.Message}" });
                }
            }
        }

        private void InitializeServices()
        {
            if (_accountManager.CurrentAccount != null && _authenticator != null)
            {
                _confirmationService = new ConfirmationService(
                    _accountManager.CurrentAccount,
                    _authenticator);

                _tradeService = new TradeService(_accountManager.CurrentAccount);
                _marketService = new MarketService(_accountManager.CurrentAccount);
            }
        }

        /// <summary>
        /// Проверить, действительна ли сессия
        /// </summary>
        private async Task<bool> IsSessionValidAsync(SteamAccount account)
        {
            // Сессия недействительна, если нет токенов
            if (string.IsNullOrEmpty(account.Session?.AccessToken) ||
                string.IsNullOrEmpty(account.Session?.SteamLoginSecure))
            {
                return false;
            }

            // Пробуем быстрый запрос к Steam для проверки сессии
            try
            {
                var handler = new System.Net.Http.HttpClientHandler();
                var cookieContainer = new System.Net.CookieContainer();
                
                cookieContainer.Add(new System.Net.Cookie("sessionid", account.Session.SessionId ?? "")
                {
                    Domain = "steamcommunity.com",
                    Path = "/"
                });
                cookieContainer.Add(new System.Net.Cookie("steamLoginSecure", account.Session.SteamLoginSecure)
                {
                    Domain = "steamcommunity.com",
                    Path = "/"
                });

                handler.CookieContainer = cookieContainer;

                using var client = new System.Net.Http.HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(Constants.SessionValidationTimeoutSeconds);

                // Простой запрос для проверки сессии
                var response = await client.GetAsync("https://steamcommunity.com/actions/GetNotificationCounts");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Попытаться обновить сессию
        /// </summary>
        private async Task<bool> TryRefreshSessionAsync(SteamAccount account)
        {
            // 1. Пробуем через RefreshToken
            if (await SessionRefreshService.RefreshSessionAsync(account))
            {
                account.HasSession = true;
                account.SteamId = account.Session.SteamId > 0 ? account.Session.SteamId : account.SteamId;
                _accountManager.SaveAccountSettings(account);
                UpdateAccountsList();
                return true;
            }

            // 2. Если нет RefreshToken или он истёк - пробуем полную авторизацию
            if (!string.IsNullOrEmpty(account.Password))
            {
                var loginResult = await SessionLoginService.FullLoginAsync(
                    account.Username,
                    account.Password,
                    account.SharedSecret
                );

                if (loginResult.Success)
                {
                    account.Session = new MaFileSession
                    {
                        AccessToken = loginResult.AccessToken ?? "",
                        RefreshToken = loginResult.RefreshToken ?? "",
                        SteamLoginSecure = loginResult.SteamLoginSecure ?? "",
                        SessionId = loginResult.SessionId ?? "",
                        SteamId = (long)loginResult.SteamId
                    };
                    account.SteamId = (long)loginResult.SteamId;
                    account.HasSession = true;
                    _accountManager.SaveAccountSettings(account);
                    UpdateAccountsList();
                    return true;
                }
            }

            return false;
        }

        private void ImportMaFile(string content)
        {
            try
            {
                var maFileData = JsonConvert.DeserializeObject<SteamAccount>(content);

                if (maFileData != null && !string.IsNullOrEmpty(maFileData.Username))
                {
                    _accountManager.AddAccount(maFileData);
                    UpdateAccountsList();
                    SendToJS("AccountAdded", new { message = "Аккаунт импортирован!" });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private void ToggleFavorite(string accountName)
        {
            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);
            if (account == null) return;

            account.IsFavorite = !account.IsFavorite;
            _accountManager.SaveAccountSettings(account);

            // Обновляем список аккаунтов в UI
            UpdateAccountsList();
        }

        private async Task RefreshConfirmationsAsync()
        {
            if (_confirmationService == null || _accountManager.CurrentAccount == null) return;

            var account = _accountManager.CurrentAccount;

            try
            {
                var confirmations = await _confirmationService.GetConfirmationsAsync();
                SendToJS("UpdateConfirmations", new { confirmations = confirmations });
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Ошибка получения подтверждений: {ex.Message}");

                // Если ошибка связана с авторизацией, пробуем обновить сессию
                if (ex.Message.Contains("needauth") || ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    try
                    {
                        AppLogger.Info("Обнаружена ошибка авторизации, обновляем сессию...");
                        await SessionRefreshService.RefreshSessionAsync(account);

                        // Пересоздаем сервис с новой сессией
                        _confirmationService?.Dispose();
                        _confirmationService = new ConfirmationService(account, _authenticator);

                        // Повторяем запрос
                        var confirmations = await _confirmationService.GetConfirmationsAsync();
                        SendToJS("UpdateConfirmations", new { confirmations = confirmations });
                    }
                    catch (Exception refreshEx)
                    {
                        AppLogger.Error($"Не удалось обновить сессию и получить подтверждения: {refreshEx.Message}");
                        SendToJS("Error", new { message = "Не удалось обновить сессию" });
                    }
                }
                else
                {
                    SendToJS("Error", new { message = ex.Message });
                }
            }
        }

        private async Task AcceptAllConfirmationsAsync()
        {
            if (_confirmationService == null) return;

            try
            {
                bool success = await _confirmationService.AcceptAllConfirmationsAsync();
                SendToJS("ConfirmationsAccepted", new { success });
                await RefreshConfirmationsAsync();
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task AcceptConfirmationAsync(string confirmationId)
        {
            if (_confirmationService == null) return;

            try
            {
                var confirmations = await _confirmationService.GetConfirmationsAsync();
                var confirmation = confirmations.FirstOrDefault(c => c.Id == confirmationId);

                if (confirmation != null)
                {
                    bool success = await _confirmationService.AcceptConfirmationAsync(confirmation);
                    SendToJS("ConfirmationAccepted", new { success = success });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task DenyConfirmationAsync(string confirmationId)
        {
            if (_confirmationService == null) return;

            try
            {
                var confirmations = await _confirmationService.GetConfirmationsAsync();
                var confirmation = confirmations.FirstOrDefault(c => c.Id == confirmationId);

                if (confirmation != null)
                {
                    bool success = await _confirmationService.DenyConfirmationAsync(confirmation);
                    SendToJS("ConfirmationDenied", new { success = success });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task RefreshTradesAsync()
        {
            if (_tradeService == null || _accountManager.CurrentAccount == null) return;

            var account = _accountManager.CurrentAccount;

            try
            {
                var trades = await _tradeService.GetActiveTradesAsync();
                SendToJS("UpdateTrades", trades);
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task RefreshMarketAsync()
        {
            if (_marketService == null || _accountManager.CurrentAccount == null) return;

            var account = _accountManager.CurrentAccount;

            try
            {
                var listings = await _marketService.GetActiveListingsAsync();
                SendToJS("UpdateMarketListings", listings);
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task AcceptTradeAsync(string tradeId)
        {
            if (_tradeService == null) return;

            try
            {
                bool success = await _tradeService.AcceptTradeAsync(tradeId);
                if (success)
                {
                    SendToJS("TradeAccepted", new { tradeId, success });
                }
                else
                {
                    SendToJS("Error", new { message = "Не удалось принять трейд" });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task DeclineTradeAsync(string tradeId)
        {
            if (_tradeService == null) return;

            try
            {
                bool success = await _tradeService.DeclineTradeAsync(tradeId);
                if (success)
                {
                    SendToJS("TradeDeclined", new { tradeId, success });
                }
                else
                {
                    SendToJS("Error", new { message = "Не удалось отклонить трейд" });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private async Task CancelListingAsync(string listingId)
        {
            if (_marketService == null) return;

            try
            {
                bool success = await _marketService.CancelListingAsync(listingId);
                if (success)
                {
                    SendToJS("ListingCancelled", new { listingId, success });
                }
                else
                {
                    SendToJS("Error", new { message = "Не удалось отменить листинг" });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        private void SendToJS(string type, object data)
        {
            if (webView?.CoreWebView2 != null)
            {
                var message = new Dictionary<string, object>
                {
                    ["type"] = type,
                    ["data"] = data
                };
                string json = JsonConvert.SerializeObject(message);
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
        }

        // ===== ДИАЛОГИ =====

        /// <summary>
        /// Открыть настройки аккаунта (двойной клик)
        /// </summary>
        private void OpenAccountSettingsDialog(string accountName)
        {
            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);
            if (account == null) return;

            var groups = _accountManager.GetGroups();
            var proxies = _settingsManager.Settings.Proxies ?? new List<ProxySettings>();
            
            SendToJS("ShowAccountSettings", new
            {
                account = new
                {
                    username = account.Username,
                    group = account.Group,
                    hasSession = account.HasSession,
                    autoTrade = account.AutoTrade,
                    autoMarket = account.AutoMarket,
                    proxy = account.Proxy?.Data?.Address != null ? $"{account.Proxy.Data.Address}:{account.Proxy.Data.Port}" : "",
                    revocationCode = account.RevocationCode ?? ""
                },
                groups = groups,
                proxies = proxies.Select(p => new { name = p.Name ?? "", address = p.Address ?? "" }).ToList()
            });
        }

        /// <summary>
        /// Обновить настройки аккаунта
        /// </summary>
        private void HandleUpdateAccountSettings(Dictionary<string, object> message)
        {
            try
            {
                string accountName = message["accountName"]?.ToString() ?? "";
                AppLogger.Info($"HandleUpdateAccountSettings called for account: {accountName}");

                var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);
                if (account == null)
                {
                    AppLogger.Warn($"Account not found: {accountName}");
                    return;
                }

                account.Group = message["group"]?.ToString() ?? account.Group;
                account.AutoTrade = Convert.ToBoolean(message["autoTrade"] ?? false);
                account.AutoMarket = Convert.ToBoolean(message["autoMarket"] ?? false);

                // Обновляем прокси
                string proxyStr = message["proxy"]?.ToString() ?? "";
                AppLogger.Info($"Proxy string received: '{proxyStr}'");
                if (!string.IsNullOrEmpty(proxyStr))
                {
                    var parts = proxyStr.Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int port))
                    {
                        account.Proxy = new MaFileProxy
                        {
                            Id = 1,
                            Data = new ProxyData
                            {
                                Protocol = 0,
                                Address = parts[0],
                                Port = port,
                                Username = null,
                                Password = null,
                                AuthEnabled = false
                            }
                        };
                        AppLogger.Debug($"Proxy set for {accountName}: {parts[0]}:{port}");
                    }
                }
                else
                {
                    account.Proxy = null;
                    AppLogger.Debug($"Proxy removed for {accountName}");
                }

                _accountManager.SaveAccountSettings(account);
                UpdateAccountsList();
                SendToJS("AccountSettingsSaved", new { success = true, message = "Настройки сохранены" });
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Показать диалог добавления аккаунта
        /// </summary>
        private void ShowAddAccountDialog()
        {
            var groups = _accountManager.GetGroups();
            SendToJS("ShowAddAccountDialog", new {
                groups = groups,
                defaultGroup = _settingsManager.Settings.DefaultGroup
            });
        }

        /// <summary>
        /// Обработать логин/пароль
        /// </summary>
        private void HandleLoginCredentials(Dictionary<string, object> message)
        {
            try
            {
                string login = message["login"]?.ToString() ?? "";
                string password = message["password"]?.ToString() ?? "";
                string group = message["group"]?.ToString() ?? _settingsManager.Settings.DefaultGroup;

                // Здесь должна быть реальная авторизация через Steam API
                // Пока заглушка - запрашиваем код с почты
                SendToJS("RequestEmailCode", new { message = "Введите код с почты" });
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Обработать код с почты для авторизации
        /// </summary>
        private void HandleEmailCode(Dictionary<string, object> message)
        {
            try
            {
                string code = message["code"]?.ToString() ?? "";

                // Если это обновление сессии
                if (!string.IsNullOrEmpty(_pendingLoginAccount))
                {
                    SubmitEmailCodeForSession(code);
                    return;
                }

                // Здесь должна быть проверка кода
                // После успешной проверки - запрашиваем код для Steam Guard
                SendToJS("RequestGuardCode", new { message = "Введите код для подключения Steam Guard" });
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Обработать код для Steam Guard
        /// </summary>
        private void HandleGuardCode(Dictionary<string, object> message)
        {
            try
            {
                string code = message["code"]?.ToString() ?? "";
                string login = message["login"]?.ToString() ?? "";
                string password = message["password"]?.ToString() ?? "";
                string group = message["group"]?.ToString() ?? _settingsManager.Settings.DefaultGroup;

                // Генерируем заглушку R-кода
                string revocationCode = GenerateRevocationCode();

                // Создаем аккаунт
                var newAccount = new SteamAccount
                {
                    Username = login,
                    Group = group,
                    HasSession = true,
                    RevocationCode = revocationCode
                };

                _accountManager.AddAccount(newAccount);
                UpdateAccountsList();

                // Показываем R-код
                SendToJS("ShowRevocationCode", new
                {
                    code = revocationCode,
                    account = newAccount.Username,
                    message = "Сохраните этот код! Он необходим для восстановления аккаунта."
                });
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Генерация R-кода (заглушка)
        /// </summary>
        private string GenerateRevocationCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray()) + "-" +
                   new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray()) + "-" +
                   new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Показать диалог создания группы
        /// </summary>
        private void ShowCreateGroupDialog()
        {
            SendToJS("ShowCreateGroupDialog", new { });
        }

        /// <summary>
        /// Обработать создание группы
        /// </summary>
        private void HandleCreateGroup(Dictionary<string, object> message)
        {
            try
            {
                string groupName = message["groupName"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    SendToJS("Error", new { message = "Название группы не может быть пустым" });
                    return;
                }

                // Группы теперь автоматически формируются из аккаунтов
                UpdateAccountsList();
                SendToJS("GroupCreated", new { success = true, groupName = groupName });
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Показать диалог настроек
        /// </summary>
        private void ShowSettingsDialog()
        {
            var groups = _accountManager.GetGroups();
            SendToJS("ShowSettingsDialog", new
            {
                defaultGroup = _settingsManager.Settings.DefaultGroup,
                groups = groups,
                auto2FA = _settingsManager.Settings.Auto2FA,
                proxies = _settingsManager.Settings.Proxies.Select(p => new
                {
                    name = p.Name ?? "",
                    address = p.Address ?? "",
                    username = p.Username ?? "",
                    password = p.Password ?? "",
                    isActive = p.IsActive
                }).ToList()
            });
        }

        /// <summary>
        /// Сохранить настройки
        /// </summary>
        private void HandleSaveSettings(Dictionary<string, object> message)
        {
            try
            {
                string defaultGroup = message["defaultGroup"]?.ToString() ?? "";
                bool auto2FA = Convert.ToBoolean(message["auto2FA"] ?? false);

                _settingsManager.SetDefaultGroup(defaultGroup);
                _settingsManager.Settings.Auto2FA = auto2FA;

                // Handle proxies
                if (message.TryGetValue("proxies", out var proxiesObj) && proxiesObj != null)
                {
                    var proxiesJson = proxiesObj.ToString();
                    AppLogger.Info($"Proxies JSON received: {proxiesJson}");
                    var proxies = JsonConvert.DeserializeObject<List<ProxySettings>>(proxiesJson ?? "[]");

                    // Фильтруем пустые прокси
                    _settingsManager.Settings.Proxies = (proxies ?? new List<ProxySettings>())
                        .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Address))
                        .ToList();

                    AppLogger.Info($"Proxies count after deserialization and filtering: {_settingsManager.Settings.Proxies.Count}");
                }

                _settingsManager.SaveSettings();
                AppLogger.Info("Settings saved successfully");
                SendToJS("SettingsSaved", new { success = true, message = "Настройки сохранены" });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error saving settings", ex);
                SendToJS("Error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Отправить список групп в JS
        /// </summary>
        private void SendGroupsToJS()
        {
            var groups = _accountManager.GetGroups();
            SendToJS("ApplyGroups", new { groups = groups });
        }

        /// <summary>
        /// Удалить аккаунт
        /// </summary>
        private void RemoveAccount(string accountName)
        {
            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);
            if (account != null)
            {

                _accountManager.RemoveAccount(account);

                // Если удалили текущий аккаунт - переключиться на первый
                if (_accountManager.CurrentAccount?.Username == accountName)
                {
                    if (_accountManager.Accounts.Count > 0)
                    {
                        _accountManager.SetCurrentAccount(_accountManager.Accounts[0]);
                        _authenticator = new SteamAuthenticator(_accountManager.CurrentAccount.SharedSecret);
                        InitializeServices();
                        UpdateCodes();
                    }
                    else
                    {
                        _accountManager.SetCurrentAccount(null!);
                        _authenticator = null;
                    }
                }

                UpdateAccountsList();
                SendGroupsToJS();
                SendToJS("AccountRemoved", new { message = "Аккаунт удалён" });
            }
        }

        /// <summary>
        /// Обновить сессию аккаунта
        /// </summary>
        private async void RefreshSession(string accountName)
        {
            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == accountName);
            if (account != null)
            {
                try
                {
                    // 1. Пробуем через RefreshToken
                    if (await SessionRefreshService.RefreshSessionAsync(account))
                    {
                        account.HasSession = true;
                        _accountManager.SaveAccountSettings(account);
                        UpdateAccountsList();
                        SendToJS("SessionRefreshed", new { message = "Сессия обновлена" });
                        return;
                    }

                    // 2. Если нет RefreshToken - пробуем полную авторизацию
                    var loginResult = await SessionLoginService.FullLoginAsync(
                        account.Username,
                        account.Password ?? "",
                        account.SharedSecret
                    );

                    if (loginResult.Success)
                    {
                        // Сохраняем сессию в аккаунт
                        account.Session = new MaFileSession
                        {
                            AccessToken = loginResult.AccessToken ?? "",
                            RefreshToken = loginResult.RefreshToken ?? "",
                            SteamLoginSecure = loginResult.SteamLoginSecure ?? "",
                            SessionId = loginResult.SessionId ?? "",
                            SteamId = (long)loginResult.SteamId
                        };
                        account.SteamId = (long)loginResult.SteamId;
                        account.HasSession = true;
                        _accountManager.SaveAccountSettings(account);
                        UpdateAccountsList();
                        SendToJS("SessionRefreshed", new { message = "Сессия обновлена" });
                    }
                    else if (loginResult.NeedsEmailCode)
                    {
                        // Нужен код с почты - показываем модалку
                        _pendingLoginAccount = accountName;
                        SendToJS("RequestEmailCode", new { message = "Введите код с почты" });
                    }
                    else if (loginResult.Error == "Нет пароля для авторизации" || string.IsNullOrEmpty(account.Password))
                    {
                        SendToJS("Error", new { message = "Нет пароля для авторизации. Добавьте пароль в .mafile файл." });
                    }
                    else
                    {
                        SendToJS("Error", new { message = loginResult.Error });
                    }
                }
                catch (Exception ex)
                {
                    SendToJS("Error", new { message = ex.Message });
                }
            }
        }

        // Аккаунт ожидающий ввода email кода
        private string? _pendingLoginAccount;

        /// <summary>
        /// Обработать код с почты для авторизации (при обновлении сессии)
        /// </summary>
        private async void SubmitEmailCodeForSession(string code)
        {
            if (string.IsNullOrEmpty(_pendingLoginAccount)) return;

            var account = _accountManager.Accounts.FirstOrDefault(a => a.Username == _pendingLoginAccount);
            if (account == null) return;

            try
            {
                var loginResult = await SessionLoginService.FullLoginAsync(
                    account.Username,
                    account.Password ?? "",
                    account.SharedSecret,
                    code
                );

                if (loginResult.Success)
                {
                    account.Session = new MaFileSession
                    {
                        AccessToken = loginResult.AccessToken ?? "",
                        RefreshToken = loginResult.RefreshToken ?? "",
                        SteamLoginSecure = loginResult.SteamLoginSecure ?? "",
                        SessionId = loginResult.SessionId ?? "",
                        SteamId = (long)loginResult.SteamId
                    };
                    account.SteamId = (long)loginResult.SteamId;
                    account.HasSession = true;
                    _accountManager.SaveAccountSettings(account);
                    UpdateAccountsList();
                    SendToJS("SessionRefreshed", new { message = "Сессия обновлена" });
                }
                else
                {
                    SendToJS("Error", new { message = loginResult.Error });
                }
            }
            catch (Exception ex)
            {
                SendToJS("Error", new { message = ex.Message });
            }

            _pendingLoginAccount = null;
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (webView?.CoreWebView2 != null)
            {
                try
                {
                    await webView.ExecuteScriptAsync(script);
                }
                catch { }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                this.Capture = false;
                var msg = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                this.WndProc(ref msg);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _codeTimer?.Stop();
            _codeTimer?.Dispose();
            _confirmationService?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
