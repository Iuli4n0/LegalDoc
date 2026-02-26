using System;
using MediatR;

namespace DocumentService.Application.Commands.GenerateDocumentResume;

public record GenerateDocumentResumeCommand(Guid DocumentId) : IRequest<GenerateDocumentResumeResponse>;

