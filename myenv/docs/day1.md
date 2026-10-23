# Day 1: the AI service skeleton

Goal: a tiny Python service next to the .NET backend that will later answer questions about FlowForge data. Today it only has to run, be testable, be containerised and have a way to measure answers.

## What exists

| File | Purpose |
|---|---|
| `app/main.py` | Creates the FastAPI app and plugs in the two routers |
| `app/routes/health.py` | `GET /health` liveness check |
| `app/routes/ask.py` | `POST /ask`, returns a fixed answer for now |
| `app/schemas.py` | Pydantic models for every request and response |
| `app/config.py` | Settings read from environment variables |
| `app/embeddings.py` | Calls the OpenAI embeddings endpoint, prints the vector length |
| `evals/golden.json` | 18 questions with the text each answer must contain |
| `tests/test_health.py` | One pytest that calls `/health` |
| `Dockerfile` | Builds the service image |

## Run it

```powershell
cd D:\Dektop\FlowForge\myenv
.\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload          # http://localhost:8000/docs
pytest                                 # runs tests/test_health.py
copy .env.example .env                 # then add OPENAI_API_KEY
python -m app.embeddings               # prints: embedding length: 1536
docker build -t flowforge-ai .
docker run -p 8000:8000 flowforge-ai
```

## Decisions

- **FastAPI plus Pydantic.** The response model is declared once and FastAPI uses it to validate output and generate `/docs`. `/ask` returns an `AskResponse` with an `answer` and a `sources` list, so the final shape is fixed before retrieval exists.
- **Routers in separate files.** `main.py` only wires things together, so each endpoint can grow without touching the others.
- **Settings from the environment.** No key is ever in code. A missing `OPENAI_API_KEY` raises a clear error at the point of use.
- **`base_url` is configurable.** The same code works against OpenAI or OpenRouter (the provider the .NET backend already uses).
- **Golden questions are facts from the domain model.** Every expected answer (roles, priorities, sprint states, the 25 MB attachment limit) comes from the backend source, so a wrong answer is the AI's fault, not a bad test.
- **The Dockerfile copies `requirements.txt` first** so dependency installation is cached, and runs as a non-root user.

## Known limits

- `/ask` ignores the question. That is intentional for day 1.
- The golden file has no runner yet. Next step: a script that calls `/ask` for each question and reports the pass rate.
- The folder is both the virtual environment and the project. `.gitignore` excludes the environment parts, but a separate `ai-service/` folder with the venv outside it would be cleaner.
- The embeddings call needs a real key and network, so it is not part of the automated test.

## Next

1. Eval runner that scores `/ask` against `golden.json`.
2. Embed FlowForge records (tasks, comments) and store the vectors.
3. Retrieve the closest records and build the answer from them.
