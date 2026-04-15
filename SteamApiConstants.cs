namespace SteamGuard;

/// <summary>
/// Типы подтверждения сессии Steam
/// </summary>
public enum AuthConfirmationType
{
    Unknown = 0,
    None = 1,
    EmailCode = 2,
    DeviceCode = 3,          // 2FA из мобильного приложения
    DeviceConfirmation = 4,
    EmailConfirmation = 5,
    MachineToken = 6,
    LegacyMachineAuth = 7
}

/// <summary>
/// Типы кодов для UpdateAuthSession
/// </summary>
public enum AuthCodeType
{
    Unknown = 0,
    EmailCode = 2,
    DeviceCode = 3
}

/// <summary>
/// Коди результата Steam API
/// </summary>
public static class EResult
{
    public const int OK = 1;
    public const int ServiceUnavailable = 2;
    public const int InvalidCredentials = 8;
    public const int RateLimit = 5;
    public const int SessionExpired = 6;
    public const int InvalidLoginAuthCode = 65;

    public static string GetMessage(int eresult) => eresult switch
    {
        OK => "OK",
        InvalidLoginAuthCode => "Неверный код подтверждения",
        SessionExpired => "Сессия истекла",
        RateLimit => "Слишком много попыток",
        ServiceUnavailable => "Сервер Steam недоступен",
        InvalidCredentials => "Неверные учётные данные",
        _ => $"Ошибка Steam (eresult={eresult})"
    };
}

/// <summary>
/// Константы Steam API
/// </summary>
public static class SteamApiConstants
{
    public const string ApiBaseUrl = "https://api.steampowered.com";
    public const string CommunityUrl = "https://steamcommunity.com";
    public const string LoginUrl = "https://login.steampowered.com";

    // Endpoints
    public const string Endpoint_GetPasswordRSAPublicKey = "/IAuthenticationService/GetPasswordRSAPublicKey/v1";
    public const string Endpoint_BeginAuthSession = "/IAuthenticationService/BeginAuthSessionViaCredentials/v1";
    public const string Endpoint_PollAuthSession = "/IAuthenticationService/PollAuthSessionStatus/v1";
    public const string Endpoint_UpdateAuthSession = "/IAuthenticationService/UpdateAuthSessionWithSteamGuardCode/v1";
    public const string Endpoint_FinalizeLogin = "/jwt/finalizelogin";
    public const string Endpoint_AddAuthenticator = "/ITwoFactorService/AddAuthenticator/v1";
    public const string Endpoint_FinalizeAuthenticator = "/ITwoFactorService/FinalizeAddAuthenticator/v1";
    public const string Endpoint_QueryTime = "/ITwoFactorService/QueryTime/v0001";
    public const string Endpoint_GenerateAccessToken = "/IAuthenticationService/GenerateAccessTokenForApp/v1";

    // Confirmation endpoints
    public const string Endpoint_ConfList = "/mobileconf/getlist";
    public const string Endpoint_ConfOp = "/mobileconf/ajaxop";
    public const string Endpoint_ConfMultiOp = "/mobileconf/multiajaxop";

    // User-Agent для мобильных запросов
    public const string MobileUserAgent = "okhttp/3.12.12";
    public const string MobileClientVersion = "777777 3.6.1";
    public const string MobileClient = "android";
    public const string MobileLanguage = "english";

    // Device
    public const string DeviceFriendlyName = "Pixel 6 Pro";
    public const int DevicePlatformType = 3;       // MobileApp
    public const int DeviceOsType = -500;           // AndroidUnknown
    public const uint DeviceGamingDeviceType = 528;

    // Mobile cookies
    public const string CookieMobileClient = "mobileClient";
    public const string CookieMobileClientVersion = "mobileClientVersion";
    public const string CookieSteamLanguage = "Steam_Language";
    public const string MobileCookieValue = "android";

    // Confirmation cookie string
    public const string ConfirmationCookieString = "mobileClient=android; mobileClientVersion=777777 3.6.1; steamLoginSecure=";

    // Success response patterns
    public const string SuccessResponseTrue1 = "\"success\":true";
    public const string SuccessResponseTrue2 = "\"success\": true";

    // Retry
    public const int MaxPollAttempts = 30;
    public const int MaxRetryAttempts = 3;
    public const int PollDelayMs = 2000;
    public const int FinalizeMaxAttempts = 30;
}
