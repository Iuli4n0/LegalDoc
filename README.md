# LegalDoc

LegalDoc is a multi-service app for document processing and legal clause analysis.

## Services

- `IdentityService` - JWT authentication API
- `DocumentService` - document upload/processing API (AWS S3 + Ollama)
- `LegalDoc.Frontend` - Blazor Server UI
- `classifier/` - FastAPI service for abusive clause prediction

## Prerequisites

- Docker + Docker Compose
- .NET SDK 10.x
- Python 3.11 

## Configuration

`docker-compose.yml` uses root `.env`.

Required variables:

- `POSTGRES_USER`, `POSTGRES_PASSWORD`
- `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRATION_MINUTES`
- `AWS_REGION`, `AWS_BUCKET_NAME`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`
- `OLLAMA_MODEL`, `OLLAMA_CHUNK_SIZE`

## Quick Start (Docker)

```zsh
docker compose up --build
```

Stop:

```zsh
docker compose down
```

## Default URLs

- Frontend: `http://localhost:8080`
- DocumentService: `http://localhost:5163` (`/health`)
- IdentityService: `http://localhost:5164` (`/health`)
- Classifier: `http://localhost:8000` (`/health`, `POST /predict`)

## Testing

```zsh
dotnet test LegalDoc.sln
```
