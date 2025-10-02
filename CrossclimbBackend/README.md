# Crossclimb Backend — Detailed Status for LLM/Developer

This README is intentionally detailed so an LLM (or a new developer) can quickly understand the current state of the repository, what has been implemented, what has not, how the AOAI integration works now, and what to do next.

---

## 1) High-level project summary

- Purpose: Minimal Azure Functions backend to accept puzzle clues and forward them to Azure OpenAI (AOAI), returning the LLM response.
- Scope: Proof-of-concept only — no persistence, no auth, no rate limiting, minimal validation.
- Primary design decision: AOAI calls are made by a single service (`AoaiService`) using direct REST calls (HttpClient) rather than the Azure SDK. This avoids runtime/SDK version issues encountered during development and isolates the AOAI integration behind an interface (`IAoaiService`).

---

## 2) What is implemented (explicit)

Files and responsibilities (current implementation):

- `CrossclimbBackend/CrossclimbBackend.csproj`
  - Project file for the Functions app (net8.0 / Azure Functions v4).

- `Functions/SolveFunction.cs`
  - HTTP trigger: POST `/api/solve`.
  - Reads JSON body into `Models.SolveRequest` (array of `Clue` objects).
  - Validates at least one clue present.
  - Delegates to `IAoaiService.GetResponseForCluesAsync` to get the LLM output.
  - Returns JSON: `{ solution, model, tokenUsage: { prompt, completion } }`.

- `Functions/TestFunction.cs`
  - HTTP trigger: GET `/api/test`.
  - Delegates to `IAoaiService.GetTestResponseAsync` (simple test prompt) and returns response and usage.
  - Useful for quick connectivity checks to AOAI.

- `Models/SolveModels.cs`
  - DTOs: `SolveRequest` (List<Clue>) and `Clue` (`Text`, `Length`).

- `Services/IAoaiService.cs`
  - Interface describing: `GetResponseForCluesAsync` and `GetTestResponseAsync`.
  - `AoaiResponse` record encapsulates content, prompt/completion token counts, and model/deployment.

- `Services/AoaiService.cs`
  - Concrete implementation of `IAoaiService`.
  - Reads `AOAI_ENDPOINT`, `AOAI_API_KEY`, `AOAI_DEPLOYMENT` from environment in constructor.
  - Implements chat completions by issuing POST to AOAI REST endpoint `/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview` using HttpClient.
  - Serializes messages as JSON and parses `choices[0].message.content` plus `usage` tokens from response JSON.
  - Returns `AoaiResponse`.

- `Startup.cs`
  - FunctionsStartup class that registers `IAoaiService` -> `AoaiService` as a singleton in DI container.

- `local.settings.json` (local dev)
  - Stores local values (not committed with secrets). Add AOAI variables here for local testing.

- `README.md` (this file)

Build status: Project builds locally (dotnet build succeeded after refactor).

---

## 3) What is explicitly NOT implemented

- No authentication on the HTTP endpoints.
- No persistence layer or datastore (no DB migrations, no queues, no caching).
- No budget guard or token accounting enforcement — token counts are returned by the service but not enforced.
- No retries or exponential backoff (current AoaiService is a single call; you may add retry logic around transient 429/5xx responses).
- No unit or integration tests are present (test scaffolding missing).
- No OpenAPI/OpenAPI docs, no swagger.

---

## 4) API contract (updated)

Two endpoints are implemented by the refactor: `POST /api/solve/ladder` and `POST /api/solve/ends`.

### 4.1 Solve Ladder

Request (POST /api/solve/ladder)

```json
{
  "wordLength": 5,
  "clues": [
    { "text": "Become dry through extreme heat", "length": 5 },
    { "text": "Covered entrance to a house", "length": 5 }
  ]
}
```

Response (200 OK)

```json
{
  "ladder": ["PARCH", "PORCH"],
  "pairs": [
    { "word": "PARCH", "clue": "Become dry through extreme heat", "reasoning": "PARCH means to dry out; fits length 5." },
    { "word": "PORCH", "clue": "Covered entrance to a house", "reasoning": "PORCH matches the clue; one-letter from PARCH." }
  ],
  "model": "<AOAI_DEPLOYMENT>",
  "tokenUsage": { "prompt": 123, "completion": 45 }
}
```

Errors:
- `400` for invalid input (missing/empty clues, length mismatch, bad pattern).
- `409` for violation of adjacency/schema after one repair attempt.
- `503` for AOAI errors/timeouts.

### 4.2 Solve Ends

Request (POST /api/solve/ends)

```json
{
  "wordLength": 5,
  "topNeighbor": "CORD",
  "bottomNeighbor": "CARD",
  "clue": "Opposite of hot"
}
```

Response (200 OK)

```json
{
  "top": "COLD",
  "bottom": "WARD",
  "topReasoning": "COLD matches the clue and differs by one letter from CORD.",
  "bottomReasoning": "WARD differs by one letter from CARD and fits common usage.",
  "model": "<AOAI_DEPLOYMENT>",
  "tokenUsage": { "prompt": 78, "completion": 16 }
}
```

Errors:
- `400` invalid input (missing neighbors, length mismatch).
- `409` result words not one letter away from neighbors.
- `503` AOAI unavailable or error.

---

## 5) How AOAI is called (implementation details — important for LLM)

The `AoaiService` composes JSON with `messages` array where each message is `{ role: string, content: string }`. Roles used:
- `system` — short instruction about the assistant behavior
- `user` — the serialized puzzle clues or test prompt

Example REST request body (pseudo):

```json
{
  "messages": [
    { "role": "system", "content": "You are a word puzzle solver. Provide a concise solution following the lengths provided." },
    { "role": "user", "content": "Solve these clues (respect lengths):\n- (5) Quick mind" }
  ],
  "temperature": 0.2
}
```

The service POSTs this to:

```
{AOAI_ENDPOINT}/openai/deployments/{AOAI_DEPLOYMENT}/chat/completions?api-version=2024-02-15-preview
```

Headers:
- `api-key: <AOAI_API_KEY>`
- `Content-Type: application/json`

Response handling (current):
- Parse `choices[0].message.content` for the assistant output.
- Parse `usage.prompt_tokens` and `usage.completion_tokens` if present.
- Return these values in AoaiResponse.

Note for LLM: the code expects the AOAI response to follow the standard `chat/completions` JSON shape (choices -> message -> content and usage fields).

---

## 6) Environment & configuration (for local + Azure)

Required environment variables (local settings or Azure Function App configuration):

- `AOAI_ENDPOINT` — base AOAI endpoint, e.g. `https://<your-aoai>.openai.azure.com/`
- `AOAI_API_KEY` — key for AOAI (or use managed identity if implementing token acquisition instead)
- `AOAI_DEPLOYMENT` — deployment name of your model (e.g. `gpt-4o-mini` or whatever you deployed)

Where to set them:
- Local: `local.settings.json` in the project directory (included in the repo but do not commit secrets).
- Azure: Function App → Configuration (Application settings)

---

## 7) How to run & test locally (step-by-step)

1. Ensure .NET SDK and Azure Functions Core Tools are installed.
2. Add your AOAI settings to `local.settings.json` in the project folder (do not commit secrets):
   - `AOAI_ENDPOINT`, `AOAI_API_KEY`, `AOAI_DEPLOYMENT`.
3. Build the project:

```powershell
cd CrossclimbBackend/CrossclimbBackend
dotnet build
```

4. Start the Functions host:

```powershell
func start
```

5. Test connectivity:

```bash
curl http://localhost:7071/api/test
```

6. Test solve endpoint:

```bash
curl -X POST http://localhost:7071/api/solve \
  -H "Content-Type: application/json" \
  -d '{"clues":[{"text":"Quick mind","length":5}]}'
```

---

## 8) Known issues / reasoning from development history

- There were SDK compatibility issues earlier when attempting to use `Azure.AI.OpenAI` directly — this led to build/runtime load errors (assembly mismatches). To unblock progress, the code now uses REST calls via HttpClient placed behind `IAoaiService`.
- Duplicate function definitions were present earlier (root-level and `Functions/` folder). These duplicates caused the Functions host to fail metadata generation. The root-level definitions were replaced with a placeholder to avoid duplicate function registration.

---

## 9) Suggested next steps (prioritized)

1. Add unit tests (mock `IAoaiService`) and at least one contract test hitting the `Test` endpoint with a mocked response.
2. Implement retry/backoff in `AoaiService` for transient errors (consider Polly or simple exponential backoff).
3. Add request validation and stronger error formatting.
4. Add telemetry (Application Insights): log puzzle id (if supplied), model/deployment, prompt/completion tokens, and latency.
5. If comfortable with the `Azure.AI.OpenAI` SDK and dependencies are compatible with your environment, replace the REST implementation behind the `IAoaiService` with an SDK-based implementation (keeps interface stable for callers).

---

## 10) How to orient an LLM to this repo (when asking it to modify or inspect code)

If you give this repository to an LLM, the most useful information to provide (in addition to the repo itself):

- The environment variables: `AOAI_ENDPOINT`, `AOAI_API_KEY`, `AOAI_DEPLOYMENT`.
- The `IAoaiService` abstraction is the single place where the LLM client is used — focus changes there to alter AOAI behavior.
- `Functions/SolveFunction.cs` and `Functions/TestFunction.cs` are thin wrappers — most logic should live in services for testability.
- The REST endpoint used is `chat/completions` with API version `2024-02-15-preview`.
- Token usage is returned in JSON under `usage` with `prompt_tokens` and `completion_tokens`.

Suggested LLM instructions (short):
- "Read `Services/AoaiService.cs` to see how the AOAI REST API is called. If asked to swap to the Azure SDK, implement a new service implementing `IAoaiService` and register it in `Startup.cs` without touching the functions."

