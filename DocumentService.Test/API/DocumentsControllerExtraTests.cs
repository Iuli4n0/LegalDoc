using System.Net.Http;
using System.Security.Claims;
using DocumentService.API.Controllers;
using DocumentService.Application.Commands.AddDocumentClause;
using DocumentService.Application.Commands.AskDocumentQuestion;
using DocumentService.Application.Commands.ClassifyDocumentClauses;
using DocumentService.Application.Commands.DeleteDocumentClause;
using DocumentService.Application.Commands.IndexDocument;
using DocumentService.Application.Commands.MergeDocumentClauses;
using DocumentService.Application.Queries.GetDocument;
using DocumentService.Application.Queries.GetDocumentClauses;
using DocumentService.Application.Queries.GetDocumentConversation;
using DocumentService.Application.Queries.GetUserDocuments;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DocumentService.Test.API;

public class DocumentsControllerExtraTests
{
    private readonly Mock<IMediator> _med = new();
    private readonly Mock<IHttpClientFactory> _hcf = new();

    private DocumentsController WithUser(string type = ClaimTypes.NameIdentifier, string val = "user-1")
    {
        var c = new DocumentsController(_med.Object, _hcf.Object);
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(type, val) }, "mock"))
            }
        };
        return c;
    }

    private DocumentsController NoUser()
    {
        var c = new DocumentsController(_med.Object, _hcf.Object);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };
        return c;
    }

    private void SetupDoc(string userId = "user-1")
    {
        _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentResponse(Guid.NewGuid(), userId, "a", "application/pdf", "k", 1, DateTime.UtcNow, null, null));
    }

    // ── GetUserDocuments ──
    [Fact] public async Task GetUserDocs_NoUser_Unauthorized() => Assert.IsType<UnauthorizedResult>((await NoUser().GetUserDocuments()).Result);

    [Fact]
    public async Task GetUserDocs_Valid_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<GetUserDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserDocumentsResponse([], 0));
        var r = await WithUser().GetUserDocuments();
        Assert.IsType<OkObjectResult>(r.Result);
    }

    [Fact]
    public async Task GetUserDocs_PageClamp()
    {
        _med.Setup(m => m.Send(It.IsAny<GetUserDocumentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserDocumentsResponse([], 0));
        await WithUser().GetUserDocuments(page: -1, pageSize: 999);
        _med.Verify(m => m.Send(It.Is<GetUserDocumentsQuery>(q => q.Page == 1 && q.PageSize == 10), It.IsAny<CancellationToken>()));
    }

    // ── GetDocumentClauses ──
    [Fact] public async Task GetClauses_NoUser_Unauth() => Assert.IsType<UnauthorizedResult>((await NoUser().GetDocumentClauses(Guid.NewGuid())).Result);
    [Fact] public async Task GetClauses_NotFound() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((GetDocumentResponse?)null); Assert.IsType<NotFoundResult>((await WithUser().GetDocumentClauses(Guid.NewGuid())).Result); }
    [Fact] public async Task GetClauses_Forbid() { SetupDoc("other"); Assert.IsType<ForbidResult>((await WithUser().GetDocumentClauses(Guid.NewGuid())).Result); }

    [Fact]
    public async Task GetClauses_Ok()
    {
        SetupDoc();
        _med.Setup(m => m.Send(It.IsAny<GetDocumentClausesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentClausesResponse(Guid.NewGuid(), [], null));
        Assert.IsType<OkObjectResult>((await WithUser().GetDocumentClauses(Guid.NewGuid())).Result);
    }

    [Fact]
    public async Task GetClauses_NotFoundEx()
    {
        _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not found"));
        Assert.IsType<NotFoundObjectResult>((await WithUser().GetDocumentClauses(Guid.NewGuid())).Result);
    }

    [Fact]
    public async Task GetClauses_500()
    {
        SetupDoc();
        _med.Setup(m => m.Send(It.IsAny<GetDocumentClausesQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));
        var r = Assert.IsType<ObjectResult>((await WithUser().GetDocumentClauses(Guid.NewGuid())).Result);
        Assert.Equal(500, r.StatusCode);
    }

    // ── IndexDocument ──
    [Fact] public async Task Index_NoUser_Unauth() => Assert.IsType<UnauthorizedResult>((await NoUser().IndexDocument(Guid.NewGuid())).Result);
    [Fact] public async Task Index_NotFound() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((GetDocumentResponse?)null); Assert.IsType<NotFoundResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result); }
    [Fact] public async Task Index_Forbid() { SetupDoc("other"); Assert.IsType<ForbidResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result); }

    [Fact]
    public async Task Index_Ok()
    {
        SetupDoc();
        _med.Setup(m => m.Send(It.IsAny<IndexDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexDocumentResponse(Guid.NewGuid(), 5, DateTime.UtcNow));
        Assert.IsType<OkObjectResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result);
    }

    [Fact] public async Task Index_NotFoundEx() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not found")); Assert.IsType<NotFoundObjectResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result); }
    [Fact] public async Task Index_NotSupported() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<IndexDocumentCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new NotSupportedException("ns")); Assert.IsType<BadRequestObjectResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result); }
    [Fact] public async Task Index_Timeout() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<IndexDocumentCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("t")); var r = Assert.IsType<ObjectResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result); Assert.Equal(504, r.StatusCode); }
    [Fact] public async Task Index_500() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<IndexDocumentCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await WithUser().IndexDocument(Guid.NewGuid())).Result); Assert.Equal(500, r.StatusCode); }

    // ── AskQuestion ──
    [Fact] public async Task Ask_NoUser() => Assert.IsType<UnauthorizedResult>((await NoUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result);
    [Fact] public async Task Ask_EmptyQ() => Assert.IsType<BadRequestObjectResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest(""))).Result);
    [Fact] public async Task Ask_NotFound() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((GetDocumentResponse?)null); Assert.IsType<NotFoundResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result); }
    [Fact] public async Task Ask_Forbid() { SetupDoc("other"); Assert.IsType<ForbidResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result); }

    [Fact]
    public async Task Ask_Ok()
    {
        SetupDoc();
        _med.Setup(m => m.Send(It.IsAny<AskDocumentQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AskDocumentQuestionResponse("A", [], false));
        Assert.IsType<OkObjectResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result);
    }

    [Fact] public async Task Ask_NotFoundEx() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not found")); Assert.IsType<NotFoundObjectResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result); }
    [Fact] public async Task Ask_NotSupported() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<AskDocumentQuestionCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new NotSupportedException("ns")); Assert.IsType<BadRequestObjectResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result); }
    [Fact] public async Task Ask_Timeout() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<AskDocumentQuestionCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("t")); var r = Assert.IsType<ObjectResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result); Assert.Equal(504, r.StatusCode); }
    [Fact] public async Task Ask_500() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<AskDocumentQuestionCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await WithUser().AskQuestion(Guid.NewGuid(), new AskQuestionRequest("Q"))).Result); Assert.Equal(500, r.StatusCode); }

    // ── QA History ──
    [Fact] public async Task QaHistory_NoUser() => Assert.IsType<UnauthorizedResult>((await NoUser().GetQaHistory(Guid.NewGuid())).Result);
    [Fact] public async Task QaHistory_NotFound() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((GetDocumentResponse?)null); Assert.IsType<NotFoundResult>((await WithUser().GetQaHistory(Guid.NewGuid())).Result); }
    [Fact] public async Task QaHistory_Forbid() { SetupDoc("other"); Assert.IsType<ForbidResult>((await WithUser().GetQaHistory(Guid.NewGuid())).Result); }

    [Fact]
    public async Task QaHistory_Ok()
    {
        SetupDoc();
        _med.Setup(m => m.Send(It.IsAny<GetDocumentConversationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDocumentConversationResponse([]));
        Assert.IsType<OkObjectResult>((await WithUser().GetQaHistory(Guid.NewGuid())).Result);
    }

    [Fact] public async Task QaHistory_NotFoundEx() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not found")); Assert.IsType<NotFoundObjectResult>((await WithUser().GetQaHistory(Guid.NewGuid())).Result); }
    [Fact] public async Task QaHistory_500() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<GetDocumentConversationQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await WithUser().GetQaHistory(Guid.NewGuid())).Result); Assert.Equal(500, r.StatusCode); }

    // ── AddClause ──
    [Fact] public async Task AddClause_NoUser() => Assert.IsType<UnauthorizedResult>((await NoUser().AddClause(Guid.NewGuid(), new AddClauseRequest("t"))).Result);
    [Fact] public async Task AddClause_EmptyText() => Assert.IsType<BadRequestObjectResult>((await WithUser().AddClause(Guid.NewGuid(), new AddClauseRequest(""))).Result);

    [Fact]
    public async Task AddClause_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<AddDocumentClauseCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddDocumentClauseResponse(Guid.NewGuid(), "t", null, null, null));
        Assert.IsType<OkObjectResult>((await WithUser().AddClause(Guid.NewGuid(), new AddClauseRequest("t"))).Result);
    }

    [Fact] public async Task AddClause_InvalidOp() { _med.Setup(m => m.Send(It.IsAny<AddDocumentClauseCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("e")); Assert.IsType<BadRequestObjectResult>((await WithUser().AddClause(Guid.NewGuid(), new AddClauseRequest("t"))).Result); }
    [Fact] public async Task AddClause_500() { _med.Setup(m => m.Send(It.IsAny<AddDocumentClauseCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await WithUser().AddClause(Guid.NewGuid(), new AddClauseRequest("t"))).Result); Assert.Equal(500, r.StatusCode); }

    // ── DeleteClause ──
    [Fact] public async Task DelClause_NoUser() => Assert.IsType<UnauthorizedResult>(await NoUser().DeleteClause(Guid.NewGuid(), Guid.NewGuid()));
    [Fact] public async Task DelClause_Ok() { _med.Setup(m => m.Send(It.IsAny<DeleteDocumentClauseCommand>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(Unit.Value)); Assert.IsType<NoContentResult>(await WithUser().DeleteClause(Guid.NewGuid(), Guid.NewGuid())); }
    [Fact] public async Task DelClause_InvalidOp() { _med.Setup(m => m.Send(It.IsAny<DeleteDocumentClauseCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("e")); Assert.IsType<BadRequestObjectResult>(await WithUser().DeleteClause(Guid.NewGuid(), Guid.NewGuid())); }
    [Fact] public async Task DelClause_500() { _med.Setup(m => m.Send(It.IsAny<DeleteDocumentClauseCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>(await WithUser().DeleteClause(Guid.NewGuid(), Guid.NewGuid())); Assert.Equal(500, r.StatusCode); }

    // ── MergeClauses ──
    [Fact] public async Task Merge_NoUser() => Assert.IsType<UnauthorizedResult>((await NoUser().MergeClauses(Guid.NewGuid(), new MergeClausesRequest(Guid.NewGuid(), Guid.NewGuid()))).Result);
    [Fact] public async Task Merge_Same() { var id = Guid.NewGuid(); Assert.IsType<BadRequestObjectResult>((await WithUser().MergeClauses(Guid.NewGuid(), new MergeClausesRequest(id, id))).Result); }

    [Fact]
    public async Task Merge_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<MergeDocumentClausesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MergeDocumentClausesResponse(Guid.NewGuid(), "merged", null, null, null));
        Assert.IsType<OkObjectResult>((await WithUser().MergeClauses(Guid.NewGuid(), new MergeClausesRequest(Guid.NewGuid(), Guid.NewGuid()))).Result);
    }

    [Fact] public async Task Merge_InvalidOp() { _med.Setup(m => m.Send(It.IsAny<MergeDocumentClausesCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("e")); Assert.IsType<BadRequestObjectResult>((await WithUser().MergeClauses(Guid.NewGuid(), new MergeClausesRequest(Guid.NewGuid(), Guid.NewGuid()))).Result); }
    [Fact] public async Task Merge_500() { _med.Setup(m => m.Send(It.IsAny<MergeDocumentClausesCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await WithUser().MergeClauses(Guid.NewGuid(), new MergeClausesRequest(Guid.NewGuid(), Guid.NewGuid()))).Result); Assert.Equal(500, r.StatusCode); }

    // ── ClassifyClauses extra ──
    [Fact] public async Task Classify_NotFound() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((GetDocumentResponse?)null); Assert.IsType<NotFoundResult>((await WithUser().ClassifyClauses(Guid.NewGuid())).Result); }
    [Fact] public async Task Classify_Forbid() { SetupDoc("other"); Assert.IsType<ForbidResult>((await WithUser().ClassifyClauses(Guid.NewGuid())).Result); }
    [Fact] public async Task Classify_NotFoundEx() { _med.Setup(m => m.Send(It.IsAny<GetDocumentQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not found")); Assert.IsType<NotFoundObjectResult>((await WithUser().ClassifyClauses(Guid.NewGuid())).Result); }
    [Fact] public async Task Classify_HttpReqEx() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<ClassifyDocumentClausesCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down")); var r = Assert.IsType<ObjectResult>((await WithUser().ClassifyClauses(Guid.NewGuid())).Result); Assert.Equal(502, r.StatusCode); }
    [Fact] public async Task Classify_Timeout() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<ClassifyDocumentClausesCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("t")); var r = Assert.IsType<ObjectResult>((await WithUser().ClassifyClauses(Guid.NewGuid())).Result); Assert.Equal(504, r.StatusCode); }
    [Fact] public async Task Classify_500() { SetupDoc(); _med.Setup(m => m.Send(It.IsAny<ClassifyDocumentClausesCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await WithUser().ClassifyClauses(Guid.NewGuid())).Result); Assert.Equal(500, r.StatusCode); }
}
