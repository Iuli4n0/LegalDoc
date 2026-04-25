using System;

namespace IdentityService.Application.Queries.GetUserById;

public record GetUserByIdResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string Role,
    int TotalDocumentsUploaded,
    int MaxDocuments,
    int MaxDocumentSizeMb,
    string SubscriptionPlan,
    int MonthlyDocumentsUploaded,
    DateTime CurrentPeriodEnd
);
