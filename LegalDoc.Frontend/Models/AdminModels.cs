using System;
using System.Collections.Generic;

namespace LegalDoc.Frontend.Models;

internal record AdminUserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int TotalDocumentsUploaded,
    int MaxDocuments,
    int MaxDocumentSizeMb,
    string SubscriptionPlan,
    int MonthlyDocumentsUploaded,
    DateTime CurrentPeriodEnd
);

internal record GetAllUsersResponse(IEnumerable<AdminUserDto> Users);

internal class UpdateUserLimitsRequest
{
    public int MaxDocuments { get; set; }
    public int MaxDocumentSizeMb { get; set; }
}

internal record UserLimitsResponse(
    int TotalDocumentsUploaded,
    int MaxDocuments,
    int MaxDocumentSizeMb,
    bool CanUpload,
    string SubscriptionPlan,
    int MonthlyDocumentsUploaded,
    DateTime CurrentPeriodEnd,
    int RemainingUploads
);
