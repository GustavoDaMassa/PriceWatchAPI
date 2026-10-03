namespace PriceWatch.Application.DTOs.Users;

/// <summary>
/// Cópia dos dados pessoais do usuário (LGPD, direito de acesso e portabilidade). Nunca inclui hash de senha,
/// tokens de verificação nem o token push do aparelho.
/// </summary>
public record UserDataExport(
    DateTime ExportedAt,
    UserProfileResponse Profile,
    IReadOnlyList<ExportedList> Lists,
    IReadOnlyList<ExportedTrackedProduct> LegacyProducts,
    IReadOnlyList<ExportedSubscription> Subscriptions,
    IReadOnlyList<ExportedNotification> Notifications,
    IReadOnlyList<ExportedClick> Clicks,
    IReadOnlyList<ExportedMatchFeedback> MatchFeedback,
    IReadOnlyList<ExportedDevice> Devices);

public record ExportedList(string Id, string Name, string? Description, DateTime CreatedAt);

public record ExportedTrackedProduct(
    string Id, string Name, string Url, decimal TargetPrice, decimal CurrentPrice, decimal LowestPrice,
    bool IsActive, DateTime CreatedAt);

public record ExportedSubscription(string Id, string ProductId, string? ListId, decimal TargetPrice, bool IsActive, DateTime CreatedAt);

public record ExportedNotification(string Id, string ProductId, string ProductName, string Message, bool IsRead, DateTime CreatedAt);

public record ExportedClick(string OfferId, DateTime CreatedAt);

public record ExportedMatchFeedback(string ProductId, string Url, bool IsSameProduct, bool Track, DateTime CreatedAt);

public record ExportedDevice(string Platform, DateTime CreatedAt, DateTime LastSeenAt);
