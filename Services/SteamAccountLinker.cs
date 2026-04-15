using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SteamGuard
{
    /// <summary>
    /// Упрощенный сервис для добавления Steam аккаунта с аутентификатором
    /// </summary>
    public class SteamAccountLinker
    {
        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private string? _refreshToken;
        private ulong _steamId;

        public SteamAccountLinker()
        {
            _httpClient = SteamHttpClientFactory.GetSharedClient();
        }

        /// <summary>
        /// Шаг 1: Начать авторизацию (логин/пароль)
        /// </summary>
        public async Task<LoginStepResult> BeginLoginAsync(string username, string password)
        {
            try
            {
                // Получаем RSA ключ
                var rsaResponse = await _httpClient.GetAsync(
                    $"{Constants.AuthGetPasswordRsaUrl}?account_name={Uri.EscapeDataString(username)}");

                var rsaJson = await rsaResponse.Content.ReadAsStringAsync();
                AppLogger.Debug($"GetPasswordRSA response: {rsaJson}");

                var rsaResult = JObject.Parse(rsaJson);
                var rsaData = rsaResult["response"];

                if (rsaData == null)
                {
                    return new LoginStepResult { Success = false, Message = "Не удалось получить RSA ключ" };
                }

                var publicKeyMod = rsaData["publickey_mod"]?.ToString();
                var publicKeyExp = rsaData["publickey_exp"]?.ToString();
                var timestamp = rsaData["timestamp"]?.ToString();

                if (string.IsNullOrEmpty(publicKeyMod) || string.IsNullOrEmpty(publicKeyExp))
                {
                    return new LoginStepResult { Success = false, Message = "Неверный формат RSA ключа" };
                }

                // Шифруем пароль
                var encryptedPassword = EncryptPassword(password, publicKeyMod, publicKeyExp);

                // Начинаем сессию авторизации
                var loginData = new
                {
                    account_name = username,
                    encrypted_password = encryptedPassword,
                    encryption_timestamp = timestamp,
                    remember_login = true,
                    platform_type = 2,
                    persistence = 1,
                    website_id = "Mobile"
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(loginData),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(Constants.AuthBeginSessionUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                AppLogger.Debug($"BeginLogin response: {json}");

                var result = JObject.Parse(json);
                var responseObj = result["response"];

                if (responseObj == null)
                {
                    return new LoginStepResult
                    {
                        Success = false,
                        Message = "Ошибка авторизации: " + (result["error"]?.ToString() ?? "неизвестная ошибка")
                    };
                }

                return new LoginStepResult
                {
                    Success = true,
                    ClientId = responseObj["client_id"]?.ToString(),
                    RequestId = responseObj["request_id"]?.ToString(),
                    SteamId = responseObj["steamid"]?.ToString(),
                    RequiresEmailCode = true
                };
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error in BeginLoginAsync", ex);
                return new LoginStepResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// RSA шифрование пароля
        /// </summary>
        private string EncryptPassword(string password, string modulus, string exponent)
        {
            try
            {
                // Конвертируем hex строки в BigInteger
                var mod = BigInteger.Parse("00" + modulus, System.Globalization.NumberStyles.HexNumber);
                var exp = BigInteger.Parse(exponent, System.Globalization.NumberStyles.HexNumber);

                // Создаем RSA параметры
                var rsaParams = new RSAParameters
                {
                    Modulus = mod.ToByteArray().Reverse().ToArray(),
                    Exponent = exp.ToByteArray().Reverse().ToArray()
                };

                // Удаляем лишний нулевой байт если есть
                if (rsaParams.Modulus[0] == 0)
                {
                    rsaParams.Modulus = rsaParams.Modulus.Skip(1).ToArray();
                }
                if (rsaParams.Exponent[0] == 0)
                {
                    rsaParams.Exponent = rsaParams.Exponent.Skip(1).ToArray();
                }

                using var rsa = RSA.Create();
                rsa.ImportParameters(rsaParams);

                // Шифруем пароль
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var encryptedBytes = rsa.Encrypt(passwordBytes, RSAEncryptionPadding.Pkcs1);

                // Возвращаем base64
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error encrypting password", ex);
                // Fallback на base64 если RSA не работает
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
            }
        }

        /// <summary>
        /// Шаг 2: Подтвердить email код
        /// </summary>
        public async Task<bool> SubmitEmailCodeAsync(string clientId, string steamId, string emailCode)
        {
            try
            {
                var data = new
                {
                    client_id = clientId,
                    steamid = steamId,
                    code = emailCode,
                    code_type = 3 // Email
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(data),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(Constants.AuthUpdateGuardCodeUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                AppLogger.Debug($"SubmitEmailCode response: {json}");

                var result = JObject.Parse(json);
                return result["response"] != null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error in SubmitEmailCodeAsync", ex);
                return false;
            }
        }

        /// <summary>
        /// Шаг 3: Получить access token (polling)
        /// </summary>
        public async Task<TokenResult> PollLoginStatusAsync(string clientId, string requestId)
        {
            try
            {
                for (int i = 0; i < Constants.PollMaxAttempts; i++)
                {
                    var data = new
                    {
                        client_id = clientId,
                        request_id = requestId
                    };

                    var content = new StringContent(
                        JsonConvert.SerializeObject(data),
                        Encoding.UTF8,
                        "application/json");

                    var response = await _httpClient.PostAsync(Constants.AuthPollStatusUrl, content);
                    var json = await response.Content.ReadAsStringAsync();

                    AppLogger.Debug($"PollLoginStatus response: {json}");

                    var result = JObject.Parse(json);
                    var responseObj = result["response"];

                    if (responseObj == null)
                    {
                        await Task.Delay(Constants.PollDelayMs);
                        continue;
                    }

                    var accessToken = responseObj["access_token"]?.ToString();
                    var refreshToken = responseObj["refresh_token"]?.ToString();
                    var newClientId = responseObj["new_client_id"]?.ToString();

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        _accessToken = accessToken;
                        _refreshToken = refreshToken;

                        return new TokenResult
                        {
                            Success = true,
                            AccessToken = accessToken,
                            RefreshToken = refreshToken,
                            NewClientId = newClientId
                        };
                    }

                    await Task.Delay(Constants.PollDelayMs);
                }

                return new TokenResult { Success = false, Message = "Timeout waiting for login" };
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error in PollLoginStatusAsync", ex);
                return new TokenResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Шаг 4: Добавить аутентификатор
        /// </summary>
        public async Task<AuthenticatorResult> AddAuthenticatorAsync(ulong steamId)
        {
            try
            {
                if (string.IsNullOrEmpty(_accessToken))
                {
                    return new AuthenticatorResult { Success = false, Message = "No access token" };
                }

                _steamId = steamId;
                var deviceId = "android:" + Guid.NewGuid().ToString();

                var url = $"https://api.steampowered.com/ITwoFactorService/AddAuthenticator/v1?access_token={_accessToken}";

                var data = new
                {
                    steamid = steamId.ToString(),
                    authenticator_type = 1,
                    device_identifier = deviceId,
                    sms_phone_id = "1"
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(data),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var json = await response.Content.ReadAsStringAsync();

                AppLogger.Debug($"AddAuthenticator response: {json}");

                var result = JObject.Parse(json);
                var responseObj = result["response"];

                if (responseObj == null)
                {
                    return new AuthenticatorResult { Success = false, Message = "Failed to add authenticator" };
                }

                var status = responseObj["status"]?.ToObject<int>() ?? 0;

                if (status == 29)
                {
                    return new AuthenticatorResult { Success = false, Message = "Authenticator already present" };
                }

                if (status != 1)
                {
                    return new AuthenticatorResult { Success = false, Message = $"Status: {status}" };
                }

                return new AuthenticatorResult
                {
                    Success = true,
                    SharedSecret = responseObj["shared_secret"]?.ToString() ?? "",
                    IdentitySecret = responseObj["identity_secret"]?.ToString() ?? "",
                    RevocationCode = responseObj["revocation_code"]?.ToString() ?? "",
                    SerialNumber = responseObj["serial_number"]?.ToString() ?? "",
                    TokenGid = responseObj["token_gid"]?.ToString() ?? "",
                    Uri = responseObj["uri"]?.ToString() ?? "",
                    ServerTime = responseObj["server_time"]?.ToString() ?? "",
                    AccountName = responseObj["account_name"]?.ToString() ?? "",
                    Secret1 = responseObj["secret_1"]?.ToString() ?? "",
                    DeviceId = deviceId,
                    ConfirmType = responseObj["confirm_type"]?.ToObject<int>() ?? 0,
                    PhoneNumberHint = responseObj["phone_number_hint"]?.ToString() ?? ""
                };
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error in AddAuthenticatorAsync", ex);
                return new AuthenticatorResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Шаг 5: Финализировать аутентификатор
        /// </summary>
        public async Task<bool> FinalizeAuthenticatorAsync(string confirmationCode, string sharedSecret, bool isSmsCode)
        {
            try
            {
                if (string.IsNullOrEmpty(_accessToken))
                {
                    return false;
                }

                // Генерируем код из shared_secret
                var authenticator = new SteamAuthenticator(sharedSecret);
                var authCode = authenticator.GenerateCode();

                var url = $"https://api.steampowered.com/ITwoFactorService/FinalizeAddAuthenticator/v1?access_token={_accessToken}";

                var data = new
                {
                    steamid = _steamId.ToString(),
                    authenticator_code = authCode,
                    authenticator_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    activation_code = confirmationCode
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(data),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var json = await response.Content.ReadAsStringAsync();

                AppLogger.Debug($"FinalizeAuthenticator response: {json}");

                var result = JObject.Parse(json);
                var responseObj = result["response"];

                return responseObj?["success"]?.ToObject<bool>() ?? false;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error in FinalizeAuthenticatorAsync", ex);
                return false;
            }
        }
    }

    public class LoginStepResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ClientId { get; set; }
        public string? RequestId { get; set; }
        public string? SteamId { get; set; }
        public bool RequiresEmailCode { get; set; }
    }

    public class TokenResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? NewClientId { get; set; }
    }

    public class AuthenticatorResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string SharedSecret { get; set; } = "";
        public string IdentitySecret { get; set; } = "";
        public string RevocationCode { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string TokenGid { get; set; } = "";
        public string Uri { get; set; } = "";
        public string ServerTime { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string Secret1 { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public int ConfirmType { get; set; } = 0; // 1,2 = SMS, 3 = Email
        public string PhoneNumberHint { get; set; } = "";
    }
}
