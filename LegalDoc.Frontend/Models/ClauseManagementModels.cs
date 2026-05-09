using System;

namespace LegalDoc.Frontend.Models;

internal record AddClauseRequest(string Text);
internal record MergeClausesRequest(Guid FirstClauseId, Guid SecondClauseId);
