namespace SteamGuard;

/// <summary>
/// Тип подтверждения Steam
/// </summary>
public enum ConfirmationType
{
    Unknown = 0,
    Trade = 2,
    MarketSellTransaction = 3,
    AccountRecovery = 6,
    RegisterApiKey = 9,
    Purchase = 12
}

/// <summary>
/// Подтверждение Steam (трейд, торговая площадка и т.д.)
/// </summary>
public class Confirmation
{
    public long Id { get; set; }
    public ulong Nonce { get; set; }
    public ulong CreatorId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public ConfirmationType ConfType { get; set; }
    public int IntType { get; set; }
    public List<string> Summary { get; set; } = new();

    /// <summary>
    /// Отображаемое описание типа
    /// </summary>
    public string TypeDescription => ConfType switch
    {
        ConfirmationType.Trade => "Обмен",
        ConfirmationType.MarketSellTransaction => "Продажа на торговой площадке",
        ConfirmationType.AccountRecovery => "Восстановление аккаунта",
        ConfirmationType.RegisterApiKey => "Регистрация API ключа",
        ConfirmationType.Purchase => "Покупка",
        _ => "Неизвестно"
    };

    /// <summary>
    /// Для трейдов — ID предложения обмена
    /// </summary>
    public ulong TradeId => CreatorId;
}
