using System;
using System.Collections.Generic;

namespace IdentityService.Application.Queries.GetAllUsers;

public record GetAllUsersResponse(IEnumerable<UserSummary> Users);

public record UserSummary(
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
