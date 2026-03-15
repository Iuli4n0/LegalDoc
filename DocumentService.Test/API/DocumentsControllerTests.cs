using System.Security.Claims;
using DocumentService.API.Controllers;
using DocumentService.Application.Commands.DeleteDocument;
using DocumentService.Application.Commands.GenerateDocumentClauses;
using DocumentService.Application.Commands.GenerateDocumentResume;
using DocumentService.Application.Commands.UploadDocument;
using DocumentService.Application.Queries.DownloadDocument;
using DocumentService.Application.Queries.GetDocument;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DocumentService.Test.API;

public class DocumentsControllerTests
{
    private readonly Mock<IMediator> _mediatorMock = new();

    [Fact]
    public async Task Given_EmptyFile_When_UploadDocument_Then_BadRequestIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var file = new FormFile(new MemoryStream(), 0, 0, "file", "empty.pdf");

        var result = await controller.UploadDocument(file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("File is empty.", badRequest.Value);
    }

    [Fact]
    public async Task Given_NoUser_When_UploadDocument_Then_UnauthorizedIsReturned()
    {
        var controller = CreateControllerWithoutClaims();
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "ok.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await controller.UploadDocument(file);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Given_ValidUpload_When_UploadDocument_Then_OkWithResponseIsReturned()
    {
        var controller = CreateControllerWithClaims(ClaimTypes.NameIdentifier, "user-123");
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "doc.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var expected = new UploadDocumentResponse(Guid.NewGuid(), "user-123", "doc.pdf", "application/pdf", "k", 3, DateTime.UtcNow);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UploadDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await controller.UploadDocument(file);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UploadDocumentResponse>(ok.Value);
        Assert.Equal(expected.Id, payload.Id);
        _mediatorMock.Verify(m => m.Send(It.Is<UploadDocumentCommand>(c => c.UserId == "user-123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Given_NoUser_When_GetDocument_Then_UnauthorizedIsReturned()
    {
        var controller = CreateControllerWithoutClaims();

        var result = await controller.GetDocument(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Given_MissingDocument_When_GetDocument_Then_NotFoundIsReturned()
    {
        var controller = CreateControllerWithClaims();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetDocumentResponse?)null);

        var result = await controller.GetDocument(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Given_DocumentOwnedByAnotherUser_When_GetDocument_Then_ForbidIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var response = new GetDocumentResponse(Guid.NewGuid(), "other-user", "a.pdf", "application/pdf", "key", 1, DateTime.UtcNow, null, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await controller.GetDocument(Guid.NewGuid());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Given_DocumentOwnedByUser_When_GetDocument_Then_OkIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var response = new GetDocumentResponse(Guid.NewGuid(), "user-1", "a.pdf", "application/pdf", "key", 1, DateTime.UtcNow, null, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await controller.GetDocument(Guid.NewGuid());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task Given_SubClaimOnly_When_GetDocument_Then_SubClaimIsUsedForUserId()
    {
        var controller = CreateControllerWithClaims("sub", "sub-user");
        var response = new GetDocumentResponse(Guid.NewGuid(), "sub-user", "a.pdf", "application/pdf", "key", 1, DateTime.UtcNow, null, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await controller.GetDocument(Guid.NewGuid());

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Given_NoUser_When_GenerateResume_Then_UnauthorizedIsReturned()
    {
        var controller = CreateControllerWithoutClaims();

        var result = await controller.GenerateResume(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Given_MissingDocument_When_GenerateResume_Then_NotFoundIsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetDocumentResponse?)null);

        var result = await controller.GenerateResume(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Given_DocumentOwnedByAnotherUser_When_GenerateResume_Then_ForbidIsReturned()
    {
        var controller = CreateControllerWithClaims();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(Guid.NewGuid(), "other-user", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        var result = await controller.GenerateResume(Guid.NewGuid());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Given_ValidRequest_When_GenerateResume_Then_OkIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        var expected = new GenerateDocumentResumeResponse(id, "resume", DateTime.UtcNow, 2);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentResumeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await controller.GenerateResume(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<GenerateDocumentResumeResponse>(ok.Value);
        Assert.Equal("resume", payload.Resume);
    }

    [Fact]
    public async Task Given_NotFoundInvalidOperation_When_GenerateResume_Then_NotFoundWithMessageIsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Document not found"));

        var result = await controller.GenerateResume(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Document not found", notFound.Value);
    }

    [Fact]
    public async Task Given_NotSupported_When_GenerateResume_Then_BadRequestIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentResumeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("unsupported"));

        var result = await controller.GenerateResume(id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("unsupported", badRequest.Value);
    }

    [Fact]
    public async Task Given_Timeout_When_GenerateResume_Then_504IsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentResumeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timeout"));

        var result = await controller.GenerateResume(id);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(504, objectResult.StatusCode);
        Assert.Equal("timeout", objectResult.Value);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_GenerateResume_Then_500IsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentResumeCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var result = await controller.GenerateResume(id);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Contains("Failed to generate resume: boom", objectResult.Value?.ToString());
    }

    [Fact]
    public async Task Given_NoUser_When_DeleteDocument_Then_UnauthorizedIsReturned()
    {
        var controller = CreateControllerWithoutClaims();

        var result = await controller.DeleteDocument(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Given_ValidRequest_When_DeleteDocument_Then_NoContentIsReturned()
    {
        var controller = CreateControllerWithClaims(ClaimTypes.NameIdentifier, "user-1");
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.Is<DeleteDocumentCommand>(c => c.DocumentId == id && c.UserId == "user-1"), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Unit.Value));

        var result = await controller.DeleteDocument(id);

        Assert.IsType<NoContentResult>(result);
        _mediatorMock.Verify(m => m.Send(It.Is<DeleteDocumentCommand>(c => c.DocumentId == id && c.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Given_DocumentNotFound_When_DeleteDocument_Then_NotFoundIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Document not found."));

        var result = await controller.DeleteDocument(id);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Document not found.", notFound.Value);
    }

    [Fact]
    public async Task Given_DocumentOwnedByAnotherUser_When_DeleteDocument_Then_403IsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to delete this document."));

        var result = await controller.DeleteDocument(id);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_DeleteDocument_Then_500IsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("unexpected"));

        var result = await controller.DeleteDocument(Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Contains("Failed to delete document: unexpected", objectResult.Value?.ToString());
    }

    [Fact]
    public async Task Given_NoUser_When_DownloadDocument_Then_UnauthorizedIsReturned()
    {
        var controller = CreateControllerWithoutClaims();

        var result = await controller.DownloadDocument(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Given_DocumentNotFound_When_DownloadDocument_Then_NotFoundIsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DownloadDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Document not found."));

        var result = await controller.DownloadDocument(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Document not found.", notFound.Value);
    }

    [Fact]
    public async Task Given_DocumentOwnedByAnotherUser_When_DownloadDocument_Then_403IsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DownloadDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to download this document."));

        var result = await controller.DownloadDocument(Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task Given_ValidRequest_When_DownloadDocument_Then_FileStreamResultIsReturned()
    {
        var controller = CreateControllerWithClaims(ClaimTypes.NameIdentifier, "user-1");
        var id = Guid.NewGuid();
        var stream = new MemoryStream([1, 2, 3]);

        _mediatorMock
            .Setup(m => m.Send(It.Is<DownloadDocumentQuery>(q => q.Id == id && q.UserId == "user-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadDocumentResult(stream, "application/pdf", "doc.pdf"));

        var result = await controller.DownloadDocument(id);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("doc.pdf", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_DownloadDocument_Then_500IsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DownloadDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("unexpected"));

        var result = await controller.DownloadDocument(Guid.NewGuid());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Contains("Failed to download document: unexpected", objectResult.Value?.ToString());
    }

    [Fact]
    public async Task Given_NoUser_When_ExtractClauses_Then_UnauthorizedIsReturned()
    {
        var controller = CreateControllerWithoutClaims();

        var result = await controller.ExtractClauses(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Given_MissingDocument_When_ExtractClauses_Then_NotFoundIsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetDocumentResponse?)null);

        var result = await controller.ExtractClauses(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Given_DocumentOwnedByAnotherUser_When_ExtractClauses_Then_ForbidIsReturned()
    {
        var controller = CreateControllerWithClaims();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(Guid.NewGuid(), "other-user", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        var result = await controller.ExtractClauses(Guid.NewGuid());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Given_ValidRequest_When_ExtractClauses_Then_OkIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        var expected = new GenerateDocumentClausesResponse(id, ["Clause A", "Clause B"], DateTime.UtcNow, 2);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentClausesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await controller.ExtractClauses(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<GenerateDocumentClausesResponse>(ok.Value);
        Assert.Equal(2, payload.Clauses.Count);
        Assert.Contains("Clause A", payload.Clauses);
    }

    [Fact]
    public async Task Given_NotFoundInvalidOperation_When_ExtractClauses_Then_NotFoundWithMessageIsReturned()
    {
        var controller = CreateControllerWithClaims();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Document not found"));

        var result = await controller.ExtractClauses(Guid.NewGuid());

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Document not found", notFound.Value);
    }

    [Fact]
    public async Task Given_NotSupported_When_ExtractClauses_Then_BadRequestIsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentClausesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("unsupported"));

        var result = await controller.ExtractClauses(id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("unsupported", badRequest.Value);
    }

    [Fact]
    public async Task Given_Timeout_When_ExtractClauses_Then_504IsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentClausesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timeout"));

        var result = await controller.ExtractClauses(id);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(504, objectResult.StatusCode);
        Assert.Equal("timeout", objectResult.Value);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_ExtractClauses_Then_500IsReturned()
    {
        var controller = CreateControllerWithClaims();
        var id = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(id, "user-1", "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GenerateDocumentClausesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var result = await controller.ExtractClauses(id);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Contains("Failed to extract clauses: boom", objectResult.Value?.ToString());
    }

    private DocumentsController CreateControllerWithClaims(string claimType = ClaimTypes.NameIdentifier, string claimValue = "user-1")
    {
        var controller = new DocumentsController(_mediatorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, claimValue)], "mock"))
                }
            }
        };

        return controller;
    }

    private DocumentsController CreateControllerWithoutClaims()
    {
        var controller = new DocumentsController(_mediatorMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        return controller;
    }
}

