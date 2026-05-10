using System;
using System.Net.Http.Json;
using System.Security.Claims;
using DocumentService.Application.Commands.AskDocumentQuestion;
using DocumentService.Application.Commands.ClassifyDocumentClauses;
using DocumentService.Application.Commands.DeleteDocument;
using DocumentService.Application.Commands.GenerateDocumentClauses;
using DocumentService.Application.Commands.GenerateDocumentResume;
using DocumentService.Application.Commands.IndexDocument;
using DocumentService.Application.Commands.UploadDocument;
using DocumentService.Application.Queries.DownloadDocument;
using DocumentService.Application.Queries.GetDocument;
using DocumentService.Application.Queries.GetDocumentClauses;
using DocumentService.Application.Queries.GetUserDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private const int GatewayTimeoutStatusCode = 504;
    private const int InternalServerErrorStatusCode = 500;
    private const string NotFoundMessage = "not found";

    private readonly IMediator _mediator;
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentsController(IMediator mediator, IHttpClientFactory httpClientFactory)
    {
        _mediator = mediator;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(104857600)]
    public async Task<ActionResult<UploadDocumentResponse>> UploadDocument(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        // Check user limits via IdentityService
        try
        {
            var identityClient = CreateIdentityClient();
            var limitsResponse = await identityClient.GetAsync($"/api/auth/users/{userId}/limits").ConfigureAwait(false);

            if (limitsResponse.IsSuccessStatusCode)
            {
                var limits = await limitsResponse.Content.ReadFromJsonAsync<UserLimitsDto>().ConfigureAwait(false);
                if (limits is not null)
                {
                    // Check document count limit
                    if (!limits.CanUpload)
                    {
                        return BadRequest($"Document limit reached. You have uploaded {limits.TotalDocumentsUploaded} of {limits.MaxDocuments} allowed documents.");
                    }

                    // Check file size limit
                    var maxSizeBytes = (long)limits.MaxDocumentSizeMb * 1024 * 1024;
                    if (file.Length > maxSizeBytes)
                    {
                        return BadRequest($"File size ({file.Length / (1024 * 1024.0):F1} MB) exceeds the maximum allowed size of {limits.MaxDocumentSizeMb} MB.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not check user limits: {ex.Message}");
            // Continue with upload even if limit check fails (fail-open for availability)
        }

        await using var stream = file.OpenReadStream();

        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UserId = userId
        };

        var response = await _mediator.Send(command).ConfigureAwait(false);

        // Increment document count in IdentityService
        try
        {
            var identityClient = CreateIdentityClient();
            await identityClient.PostAsync($"/api/auth/users/{userId}/increment-documents", null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not increment document count: {ex.Message}");
        }

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<GetUserDocumentsResponse>> GetUserDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "UploadedAt",
        [FromQuery] bool ascending = false)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 10;

        var query = new GetUserDocumentsQuery(userId, page, pageSize, sortBy, ascending);
        var response = await _mediator.Send(query).ConfigureAwait(false);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetDocumentResponse>> GetDocument(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var query = new GetDocumentQuery(id);
        var response = await _mediator.Send(query).ConfigureAwait(false);

        if (response is null)
            return NotFound();

        if (response.UserId != userId)
            return Forbid();

        return Ok(response);
    }

    [HttpPost("{id:guid}/generate-resume")]
    public async Task<ActionResult<GenerateDocumentResumeResponse>> GenerateResume(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            // Verify ownership before generating resume
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new GenerateDocumentResumeCommand(id);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(GatewayTimeoutStatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to generate resume: {ex.Message}");
        }
    }

    [HttpPost("{id:guid}/extract-clauses")]
    public async Task<ActionResult<GenerateDocumentClausesResponse>> ExtractClauses(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            // Verify ownership before extracting clauses
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new GenerateDocumentClausesCommand(id);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(GatewayTimeoutStatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to extract clauses: {ex.Message}");
        }
    }

    [HttpGet("{id:guid}/clauses")]
    public async Task<ActionResult<GetDocumentClausesResponse>> GetDocumentClauses(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            // Verify ownership
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var clausesQuery = new GetDocumentClausesQuery(id);
            var response = await _mediator.Send(clausesQuery).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to retrieve clauses: {ex.Message}");
        }
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = new DownloadDocumentQuery(id, userId);
            var result = await _mediator.Send(query).ConfigureAwait(false);
            return File(result.Stream, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to download document: {ex.Message}");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var command = new DeleteDocumentCommand(id, userId);
            await _mediator.Send(command).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to delete document: {ex.Message}");
        }
    }

    [HttpPost("{id:guid}/classify-clauses")]
    public async Task<ActionResult<ClassifyDocumentClausesResponse>> ClassifyClauses(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new ClassifyDocumentClausesCommand(id);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, $"Classifier service unavailable: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            return StatusCode(GatewayTimeoutStatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to classify clauses: {ex.Message}");
        }
    }

    [HttpPost("{id:guid}/index")]
    public async Task<ActionResult<IndexDocumentResponse>> IndexDocument(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new IndexDocumentCommand(id);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(GatewayTimeoutStatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to index document: {ex.Message}");
        }
    }

    [HttpPost("{id:guid}/ask")]
    public async Task<ActionResult<AskDocumentQuestionResponse>> AskQuestion(Guid id, [FromBody] AskQuestionRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question cannot be empty.");

        try
        {
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new AskDocumentQuestionCommand(id, request.Question);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(GatewayTimeoutStatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to answer question: {ex.Message}");
        }
    }

    [HttpGet("{id:guid}/qa-history")]
    public async Task<ActionResult<DocumentService.Application.Queries.GetDocumentConversation.GetDocumentConversationResponse>> GetQaHistory(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = new GetDocumentQuery(id);
            var document = await _mediator.Send(query).ConfigureAwait(false);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var conversationQuery = new DocumentService.Application.Queries.GetDocumentConversation.GetDocumentConversationQuery(id);
            var response = await _mediator.Send(conversationQuery).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains(NotFoundMessage, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to retrieve Q&A history: {ex.Message}");
        }
    }

    [HttpPost("{id:guid}/clauses")]
    public async Task<ActionResult<DocumentService.Application.Commands.AddDocumentClause.AddDocumentClauseResponse>> AddClause(Guid id, [FromBody] AddClauseRequest request)
    {
        var userIdString = GetUserId();
        if (userIdString is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Clause text cannot be empty.");

        try
        {
            var command = new DocumentService.Application.Commands.AddDocumentClause.AddDocumentClauseCommand(id, userIdString, request.Text);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to add clause: {ex.Message}");
        }
    }

    [HttpDelete("{id:guid}/clauses/{clauseId:guid}")]
    public async Task<IActionResult> DeleteClause(Guid id, Guid clauseId)
    {
        var userIdString = GetUserId();
        if (userIdString is null)
            return Unauthorized();

        try
        {
            var command = new DocumentService.Application.Commands.DeleteDocumentClause.DeleteDocumentClauseCommand(id, userIdString, clauseId);
            await _mediator.Send(command).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to delete clause: {ex.Message}");
        }
    }

    [HttpPost("{id:guid}/clauses/merge")]
    public async Task<ActionResult<DocumentService.Application.Commands.MergeDocumentClauses.MergeDocumentClausesResponse>> MergeClauses(Guid id, [FromBody] MergeClausesRequest request)
    {
        var userIdString = GetUserId();
        if (userIdString is null)
            return Unauthorized();

        if (request.FirstClauseId == request.SecondClauseId)
            return BadRequest("Cannot merge a clause with itself.");

        try
        {
            var command = new DocumentService.Application.Commands.MergeDocumentClauses.MergeDocumentClausesCommand(id, userIdString, request.FirstClauseId, request.SecondClauseId);
            var response = await _mediator.Send(command).ConfigureAwait(false);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, $"Failed to merge clauses: {ex.Message}");
        }
    }

    private HttpClient CreateIdentityClient()
    {
        var client = _httpClientFactory.CreateClient("IdentityAPI");
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
        }
        return client;
    }

    private string? GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
    }
}

// DTO for IdentityService limits response
public record UserLimitsDto(int TotalDocumentsUploaded, int MaxDocuments, int MaxDocumentSizeMb, bool CanUpload);

public record AskQuestionRequest(string Question);

public record AddClauseRequest(string Text);
public record MergeClausesRequest(Guid FirstClauseId, Guid SecondClauseId);
