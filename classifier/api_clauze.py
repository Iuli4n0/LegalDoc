import os
from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from clasificator_clauze import ClasificatorClauzeAbuzive


class PredictRequest(BaseModel):
    clauza: str = Field(..., min_length=1, description="Textul clauzei de analizat")


class PredictResponse(BaseModel):
    label: int = Field(..., ge=0, le=1, description="1=abuziv, 0=neabuziv")
    probabilitate_abuziv: float = Field(..., ge=0.0, le=1.0)


@asynccontextmanager
async def lifespan(app: FastAPI):
    model_path = os.getenv("MODEL_PATH", "./model_clauze_abuzive")
    app.state.classifier = ClasificatorClauzeAbuzive(model_path=model_path)
    yield


app = FastAPI(
    title="API Clasificator Clauze Abuzive",
    description="Endpoint care clasifica o clauza ca abuziva(1) sau neabuziva(0)",
    version="1.0.0",
    lifespan=lifespan,
)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.post("/predict", response_model=PredictResponse)
def predict(payload: PredictRequest) -> PredictResponse:
    try:
        rezultat = app.state.classifier.clasifica_api(payload.clauza)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc

    return PredictResponse(**rezultat)


if __name__ == "__main__":
    import uvicorn

    host = os.getenv("HOST", "0.0.0.0")
    port = int(os.getenv("PORT", "8000"))
    uvicorn.run("api_clauze:app", host=host, port=port)

