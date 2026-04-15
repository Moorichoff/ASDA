using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace SteamGuard;

/// <summary>
/// Синхронизация времени с серверами Steam
/// NebulaAuth: /ITwoFactorService/QueryTime/v0001
/// </summary>
public static class TimeAligner
{
    private static long _timeDifference;
    private static DateTime _lastSync = DateTime.MinValue;
    private static readonly object _lock = new();

    /// <summary>
    /// Получить время Steam (без блокировки — если не синхронизировано, использует локальное)
    /// </summary>
    public static long GetSteamTime()
    {
        // Никогда не блокируем UI — если нет данных, используем локальное время
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _timeDifference;
    }

    /// <summary>
    /// Асинхронно синхронизировать время (вызывать из async контекста)
    /// </summary>
    public static async Task EnsureSyncedAsync()
    {
        lock (_lock)
        {
            if ((DateTime.UtcNow - _lastSync).TotalMinutes < 5)
                return; // Уже синхронизировано
        }
        await SyncTimeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Синхронизировать время с сервером Steam
    /// </summary>
    public static async Task SyncTimeAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);

            var resp = await http.PostAsync(
                "https://api.steampowered.com/ITwoFactorService/QueryTime/v0001",
                new StringContent(""));

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var r) &&
                    r.TryGetProperty("server_time", out var st) && st.TryGetInt64(out var serverTime))
                {
                    var localTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    _timeDifference = serverTime - localTime;
                    _lastSync = DateTime.UtcNow;
                }
            }
        }
        catch
        {
            // Если не удалось — используём локальное время без поправки
        }
    }
}

/// <summary>
/// Генерация ключей подтверждения для Steam confirmations
/// NebulaAuth: EncryptionHelper.GenerateConfirmationHash
/// </summary>
public static class ConfirmationKeyGenerator
{
    /// <summary>
    /// Сгенерировать HMAC-SHA1 ключ для подтверждения
    /// </summary>
    /// <param name="time">Время Steam</param>
    /// <param name="identitySecret">IdentitySecret из mafile (base64)</param>
    /// <param name="tag">Тег операции: "list", "allow", "cancel", "conf"</param>
    public static string GenerateConfirmationHash(long time, string identitySecret, string tag = "conf")
    {
        var decode = Convert.FromBase64String(identitySecret);
        int n2 = tag.Length > 32 ? 8 + 32 : 8 + tag.Length;

        var array = new byte[n2];
        // Записываем timestamp в первые 8 байт (little-endian)
        var n3 = 8;
        while (n3 > 0)
        {
            var n4 = n3 - 1;
            array[n4] = (byte)time;
            time >>= 8;
            n3 = n4;
        }
        // Копируем тег после 8 байт
        Array.Copy(Encoding.UTF8.GetBytes(tag), 0, array, 8, n2 - 8);

        // HMAC-SHA1 с identitySecret как ключ
        using var hmac = new HMACSHA1();
        hmac.Key = decode;
        var hashedData = hmac.ComputeHash(array);
        return Convert.ToBase64String(hashedData, Base64FormattingOptions.None);
    }

    /// <summary>
    /// Сгенерировать параметры запроса для подтверждений
    /// </summary>
    public static Dictionary<string, string> GetConfirmationQueryParams(
        ulong steamId, string deviceId, string identitySecret, string tag = "conf")
    {
        var time = TimeAligner.GetSteamTime();
        var hash = GenerateConfirmationHash(time, identitySecret, tag);

        return new Dictionary<string, string>
        {
            ["p"] = deviceId,
            ["a"] = steamId.ToString(),
            ["k"] = hash,
            ["t"] = time.ToString(),
            ["m"] = "react",
            ["tag"] = tag
        };
    }
}

/// <summary>
/// Сервис подтверждений Steam (трейды, торговая площадка)
/// NebulaAuth: SteamMobileConfirmationsApi
/// </summary>
public class SteamConfirmationService : IDisposable
{
    private readonly HttpClient _http;
    private readonly Action<string>? _log;
    private bool _disposed;

    public SteamConfirmationService(Action<string>? log = null)
    {
        _log = log;
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("User-Agent", "okhttp/3.12.12");
        _http.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
    }

    private void Log(string msg) => _log?.Invoke(msg);

    /// <summary>
    /// Получить список pending подтверждений
    /// </summary>
    public async Task<(bool Success, string Error, List<Confirmation> Confirmations)> GetConfirmationsAsync(
        ulong steamId, string deviceId, string identitySecret, string sessionCookie)
    {
        try
        {
            // Синхронизируем время асинхронно (не блокирует UI)
            await TimeAligner.EnsureSyncedAsync();

            Log($"[Confirmations] Загрузка подтверждений для SteamId={steamId}");

            var queryParams = ConfirmationKeyGenerator.GetConfirmationQueryParams(
                steamId, deviceId, identitySecret, "list");

            var uri = BuildUri("https://steamcommunity.com/mobileconf/getlist", queryParams);

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Cookie", SteamApiConstants.ConfirmationCookieString + sessionCookie);
            request.Headers.Add("Accept", "application/json");

            var resp = await _http.SendAsync(request);

            if (resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Unauthorized)
                return (false, "Сессия истекла. Требуется повторный вход.", new());

            var json = await resp.Content.ReadAsStringAsync();
            Log($"[Confirmations] HTTP={resp.StatusCode}, JSON: {json.Substring(0, Math.Min(200, json.Length))}");

            var confirmations = ParseConfirmations(json);
            return (true, "", confirmations);
        }
        catch (Exception ex)
        {
            Log($"[Confirmations] Ошибка: {ex.Message}");
            return (false, ex.Message, new());
        }
    }

    /// <summary>
    /// Принять или отклонить подтверждение
    /// </summary>
    public async Task<(bool Success, string Error)> SendConfirmationAsync(
        ulong steamId, string deviceId, string identitySecret,
        string sessionCookie, Confirmation confirmation, bool accept)
    {
        try
        {
            await TimeAligner.EnsureSyncedAsync();

            var op = accept ? "allow" : "cancel";
            Log($"[Confirmations] Single: {op} id={confirmation.Id}, type={confirmation.TypeDescription}");

            var queryParams = ConfirmationKeyGenerator.GetConfirmationQueryParams(
                steamId, deviceId, identitySecret, op);

            queryParams["op"] = op;
            queryParams["cid"] = confirmation.Id.ToString();
            queryParams["ck"] = confirmation.Nonce.ToString();

            var uri = BuildUri("https://steamcommunity.com/mobileconf/ajaxop", queryParams);
            Log($"[Confirmations] Single URL: {uri}");

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Cookie", SteamApiConstants.ConfirmationCookieString + sessionCookie);
            request.Headers.Add("Accept", "application/json");

            var resp = await _http.SendAsync(request);
            var result = await resp.Content.ReadAsStringAsync();

            Log($"[Confirmations] Single HTTP={resp.StatusCode}");
            Log($"[Confirmations] Single response: {result}");

            // Проверяем success через JSON (как в NebulaAuth)
            if (TryParseSuccess(result, out var parseError))
            {
                return (true, "");
            }

            if (!string.IsNullOrEmpty(parseError))
            {
                Log($"[Confirmations] Single success=false: {parseError}");
                return (false, $"Steam вернул success=false: {result}");
            }

            // Fallback: старый способ проверки
            if (result.Contains(SteamApiConstants.SuccessResponseTrue1) || result.Contains(SteamApiConstants.SuccessResponseTrue2))
                return (true, "");

            return (false, $"Не удалось {op}. Ответ: {result}");
        }
        catch (Exception ex)
        {
            Log($"[Confirmations] Single exception: {ex.Message}");
            Log($"[Confirmations] StackTrace: {ex.StackTrace}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Принять несколько подтверждений
    /// </summary>
    public async Task<(bool Success, string Error)> SendMultipleConfirmationsAsync(
        ulong steamId, string deviceId, string identitySecret,
        string sessionCookie, IEnumerable<Confirmation> confirmations, bool accept)
    {
        try
        {
            await TimeAligner.EnsureSyncedAsync();

            var confList = confirmations.ToList();
            var op = accept ? "allow" : "cancel";
            Log($"[Confirmations] Multi: {op} {confList.Count} подтверждений");

            var queryParams = ConfirmationKeyGenerator.GetConfirmationQueryParams(
                steamId, deviceId, identitySecret, op);

            // Формируем POST body с массивами cid[] и ck[]
            var postData = new List<KeyValuePair<string, string>>();
            postData.Add(new KeyValuePair<string, string>("op", op));
            
            // Добавляем базовые параметры
            foreach (var kv in queryParams)
            {
                if (kv.Key != "tag") // tag уже установлен в GetConfirmationQueryParams
                    postData.Add(kv);
            }
            
            // Добавляем массивы подтверждений
            foreach (var conf in confList)
            {
                postData.Add(new KeyValuePair<string, string>("cid[]", conf.Id.ToString()));
                postData.Add(new KeyValuePair<string, string>("ck[]", conf.Nonce.ToString()));
            }

            var uri = "https://steamcommunity.com/mobileconf/multiajaxop";
            Log($"[Confirmations] Multi POST: {uri}");
            Log($"[Confirmations] Multi body: {string.Join("&", postData.Select(kv => $"{kv.Key}={kv.Value}"))}");

            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Add("Cookie", SteamApiConstants.ConfirmationCookieString + sessionCookie);
            request.Headers.Add("Accept", "application/json");
            request.Content = new FormUrlEncodedContent(postData);

            var resp = await _http.SendAsync(request);
            var result = await resp.Content.ReadAsStringAsync();

            Log($"[Confirmations] Multi HTTP={resp.StatusCode}");
            Log($"[Confirmations] Multi response: {result}");

            // Проверяем success через JSON (как в NebulaAuth)
            if (TryParseSuccess(result, out var parseError))
            {
                return (true, "");
            }

            if (!string.IsNullOrEmpty(parseError))
            {
                Log($"[Confirmations] Multi success=false: {parseError}");
                return (false, $"Steam вернул success=false: {result}");
            }

            // Fallback: старый способ проверки
            if (result.Contains(SteamApiConstants.SuccessResponseTrue1) || result.Contains(SteamApiConstants.SuccessResponseTrue2))
                return (true, "");

            return (false, $"Не удалось {op}. Ответ: {result}");
        }
        catch (Exception ex)
        {
            Log($"[Confirmations] Multi exception: {ex.Message}");
            Log($"[Confirmations] StackTrace: {ex.StackTrace}");
            return (false, ex.Message);
        }
    }

    private List<Confirmation> ParseConfirmations(string json)
    {
        var list = new List<Confirmation>();
        try
        {
            var doc = JsonDocument.Parse(json);
            
            // Проверяем needauth — сессия невалидна
            if (doc.RootElement.TryGetProperty("needauth", out var needAuthProp) && needAuthProp.GetBoolean())
            {
                Log($"[Confirmations] Сессия невалидна (needauth=true)");
                throw new Exception("Сессия истекла. Требуется повторный вход в Steam.");
            }
            
            // Проверяем success
            if (doc.RootElement.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
            {
                string message = "";
                string detail = "";
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    message = msgProp.GetString() ?? "";
                if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                    detail = detailProp.GetString() ?? "";
                    
                Log($"[Confirmations] Steam вернул success=false: message='{message}', detail='{detail}'");
                
                if (message.Contains("not set up to receive mobile confirmations", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Аккаунт не настроен для получения мобильных подтверждений.");
                    
                throw new Exception($"Steam вернул ошибку: {message}");
            }
            
            if (!doc.RootElement.TryGetProperty("conf", out var confArray))
            {
                Log($"[Confirmations] В ответе нет поля 'conf'. JSON: {json.Substring(0, Math.Min(300, json.Length))}");
                return list;
            }

            foreach (var item in confArray.EnumerateArray())
            {
                var conf = new Confirmation();

                if (item.TryGetProperty("id", out var idProp))
                {
                    // id может быть строкой или числом
                    if (idProp.ValueKind == JsonValueKind.String)
                        conf.Id = long.Parse(idProp.GetString()!);
                    else if (idProp.TryGetInt64(out var id))
                        conf.Id = id;
                }

                if (item.TryGetProperty("nonce", out var nonceProp))
                {
                    // nonce может быть строкой или числом
                    if (nonceProp.ValueKind == JsonValueKind.String)
                        conf.Nonce = ulong.Parse(nonceProp.GetString()!);
                    else if (nonceProp.TryGetUInt64(out var nonce))
                        conf.Nonce = nonce;
                }

                if (item.TryGetProperty("creator_id", out var creatorProp))
                {
                    if (creatorProp.ValueKind == JsonValueKind.String)
                        conf.CreatorId = ulong.Parse(creatorProp.GetString()!);
                    else if (creatorProp.TryGetUInt64(out var creator))
                        conf.CreatorId = creator;
                }

                if (item.TryGetProperty("type", out var typeProp))
                {
                    if (typeProp.TryGetInt32(out var typeInt))
                    {
                        conf.IntType = typeInt;
                        conf.ConfType = (ConfirmationType)typeInt;
                    }
                }

                if (item.TryGetProperty("type_name", out var typeNameProp))
                    conf.TypeName = typeNameProp.GetString() ?? "";

                if (item.TryGetProperty("headline", out var headlineProp))
                    conf.Headline = headlineProp.GetString() ?? "";

                if (item.TryGetProperty("creation_time", out var timeProp))
                {
                    if (timeProp.TryGetInt64(out var time))
                        conf.Time = DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;
                }

                if (item.TryGetProperty("summary", out var summaryProp) && summaryProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in summaryProp.EnumerateArray())
                        conf.Summary.Add(s.GetString() ?? "");
                }
                
                // Лог для отладки
                Log($"[Confirmations] Parse: id={conf.Id}, type={conf.ConfType}({conf.IntType}), headline='{conf.Headline}', summary.Count={conf.Summary.Count}");

                list.Add(conf);
            }
        }
        catch (Exception ex)
        {
            Log($"[Confirmations] Parse error: {ex.Message}");
            throw;
        }
        return list;
    }

    private static Uri BuildUri(string baseUrl, Dictionary<string, string> queryParams)
    {
        var uri = new UriBuilder(baseUrl)
        {
            Query = BuildQueryString(queryParams)
        };
        return uri.Uri;
    }

    private static string BuildQueryString(Dictionary<string, string> queryParams)
    {
        return string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    /// <summary>
    /// Парсинг поля success из JSON-ответа Steam
    /// </summary>
    /// <param name="json">JSON ответ от Steam</param>
    /// <param name="error">Текст ошибки если success=false</param>
    /// <returns>true если операция успешна</returns>
    private static bool TryParseSuccess(string json, out string? error)
    {
        error = null;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("success", out var successProp))
            {
                bool isSuccess = false;
                if (successProp.ValueKind == JsonValueKind.True)
                    isSuccess = true;
                else if (successProp.ValueKind == JsonValueKind.Number && successProp.TryGetInt32(out var code))
                    isSuccess = code == 1;
                else if (successProp.ValueKind == JsonValueKind.String && bool.TryParse(successProp.GetString(), out var boolVal))
                    isSuccess = boolVal;

                if (!isSuccess)
                    error = "success=false";
                return isSuccess;
            }
        }
        catch
        {
            // Не JSON или нет поля success
        }
        return false;
    }

    /// <summary>
    /// Освобождение ресурсов HttpClient
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
