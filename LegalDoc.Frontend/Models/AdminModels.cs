using System;
using System.Collections.Generic;

namespace LegalDoc.Frontend.Models;

public record AdminUserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int TotalDocumentsUploaded,
    int MaxDocuments,
    int MaxDocumentSizeMb
);

public record GetAllUsersResponse(IEnumerable<AdminUserDto> Users);

public class UpdateUserLimitsRequest
{
    public int MaxDocuments { get; set; }
    public int MaxDocumentSizeMb { get; set; }
}

public record UserLimitsResponse(
    int TotalDocumentsUploaded,
    int MaxDocuments,
    int MaxDocumentSizeMb,
    bool CanUpload
);
