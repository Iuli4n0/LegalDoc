using System;

namespace LegalDoc.Frontend.Models;

public record AddClauseRequest(string Text);
public record MergeClausesRequest(Guid FirstClauseId, Guid SecondClauseId);
