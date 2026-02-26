using System;
using DocumentService.Application.Commands.GenerateDocumentResume;
using DocumentService.Application.Commands.UploadDocument;
using DocumentService.Application.Queries.GetDocument;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DocumentService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
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

        await using var stream = file.OpenReadStream();
        
        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length
        };

        var response = await _mediator.Send(command);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetDocumentResponse>> GetDocument(Guid id)
    {
        var query = new GetDocumentQuery(id);
        var response = await _mediator.Send(query);

        if (response is null)
            return NotFound();

        return Ok(response);
    }

    [HttpPost("{id:guid}/generate-resume")]
    public async Task<ActionResult<GenerateDocumentResumeResponse>> GenerateResume(Guid id)
    {
        try
        {
            var command = new GenerateDocumentResumeCommand(id);
            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to generate resume: {ex.Message}");
        }
    }
}


