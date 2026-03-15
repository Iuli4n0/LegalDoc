using System;
using System.Security.Claims;
using DocumentService.Application.Commands.DeleteDocument;
using DocumentService.Application.Commands.GenerateDocumentClauses;
using DocumentService.Application.Commands.GenerateDocumentResume;
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
    
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<UploadDocumentResponse>> UploadDocument(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        await using var stream = file.OpenReadStream();
        
        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UserId = userId
        };

        var response = await _mediator.Send(command);

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
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetDocumentResponse>> GetDocument(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var query = new GetDocumentQuery(id);
        var response = await _mediator.Send(query);

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
            var document = await _mediator.Send(query);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new GenerateDocumentResumeCommand(id);
            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
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
            var document = await _mediator.Send(query);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var command = new GenerateDocumentClausesCommand(id);
            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
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
            var document = await _mediator.Send(query);

            if (document is null)
                return NotFound();

            if (document.UserId != userId)
                return Forbid();

            var clausesQuery = new GetDocumentClausesQuery(id);
            var response = await _mediator.Send(clausesQuery);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
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
            var result = await _mediator.Send(query);
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
            await _mediator.Send(command);
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

    private string? GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
    }
}
