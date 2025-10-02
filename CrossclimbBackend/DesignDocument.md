Here’s a clean, implementation-free **Stage A (Candidate Generation) Design Doc** you can hand to your agent.

# Stage A — Candidate Generation (Design)

## 1) Purpose & Scope

* **Goal:** For each clue, generate 3–6 plausible **uppercase** candidate words of **exact length N**, each with a brief reason tailored to the clue.
* **Out of scope:** Ordering/ranking across clues, ladder validation, DFS/backtracking, “end caps,” or any downstream assembly (Stage B).

## 2) Inputs & Outputs

**Input (from API layer or Stage A caller):**

* `wordLength: number (N ≥ 3)`
* `clues: string[] (size ≥ 1)`

**Output (to Stage B):**

* JSON object:

  * `wordLength: number`
  * `items: Array<{ clue: string, candidates: Array<{ word: string, reason: string }> }>`
* Each `candidates` array: **3–6 items**, `word` is **^\[A-Z]{N}\$**, `reason` ≤ \~80 chars, clue-specific.

## 3) Functional Requirements

* Produce **3–6** candidates per clue (no fewer, no more).
* Each candidate word:

  * Uppercase ASCII A–Z only.
  * Exact length = `wordLength`.
  * No duplicates **within the same clue**.
* Reasons:

  * Brief, clue-specific, ≤ 15 words preferred (≤ 80 chars hard cap).
  * Avoid generic explanations (“fits the clue”).
* Prefer common, contemporary vocabulary over obscure/archaic terms.

## 4) Non-Functional Requirements

* **Latency target:** p50 ≤ 1.5s, p95 ≤ 3.5s per request (single LLM call).
* **Reliability:** ≥ 99% valid JSON (use JSON mode if available). Fallback strategy if not.
* **Determinism:** Set modest creativity; responses should remain stable given same inputs.
* **Scalability:** Support up to \~30 clues per request without timeouts under standard model quotas.

## 5) Prompting Strategy

### 5.1 System Message (static)

* Responsibilities:

  * Enforce JSON-only output; forbid prose/markdown.
  * Restate hard constraints (uppercase, exact length, no duplicates, 3–6 per clue).
  * Ask for reasons that are short and specific.
* Include: explicit instruction to **return only JSON** adhering to the provided schema.

### 5.2 User Message (parameterized)

* Provides:

  * `WORD_LENGTH = N`
  * `CLUES = [...]`
  * The JSON schema (see §6) embedded directly.
* Reiterate rules:

  * Uppercase A–Z only, exact length N.
  * 3–6 candidates, no duplicates per clue.
  * Short reasons.

### 5.3 Inference Settings

* Temperature: **0.2–0.4** (default 0.3).
* Top-p: default.
* **Response format:** JSON mode (if supported) or schema-in-prompt fallback.
* Max tokens: set to comfortably fit the largest expected `items` (e.g., 30 clues × 6 candidates).
* No tools/functions.

## 6) Output JSON Schema (authoritative)

Use this schema text **verbatim** in the prompt. The agent must not deviate from it.

```json
{
  "type": "object",
  "properties": {
    "wordLength": { "type": "integer" },
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "clue": { "type": "string" },
          "candidates": {
            "type": "array",
            "minItems": 3,
            "maxItems": 6,
            "items": {
              "type": "object",
              "properties": {
                "word": { "type": "string", "pattern": "^[A-Z]{__N__}$" },
                "reason": { "type": "string", "maxLength": 80 }
              },
              "required": ["word", "reason"],
              "additionalProperties": false
            }
          }
        },
        "required": ["clue", "candidates"],
        "additionalProperties": false
      }
    }
  },
  "required": ["wordLength", "items"],
  "additionalProperties": false
}
```

> Replace `__N__` at runtime with the actual `wordLength` to hard-enforce length via regex.

## 7) Validation & Guardrails (post-LLM)

* **JSON parsing:** reject non-JSON; no partial acceptance.
* **Schema validation:** enforce structure, counts, and regex pattern for words.
* **Normalization:** Trim, enforce `.ToUpperInvariant()` before regex check (still require LLM to send uppercase).
* **Deduplication:** Within each clue’s `candidates` only; keep first occurrence, drop rest.
* **Minimum viable output:** If any clue yields < 3 valid candidates **after** validation, mark Stage A as **failed** for that clue and return a structured error (see §9).

## 8) Error Handling & Return Codes (to caller)

* **OK (200):** Valid JSON meeting all constraints.
* **422 Unprocessable Entity:** JSON parsed but failed schema/constraints; payload includes a per-clue report of violations (e.g., wrong length, lowercase, duplicates).
* **502/503 Upstream Error:** Model/transport failure; include a retryable flag.
* **408 Timeout:** Exceeded internal SLA; indicate safe retry window.

Error payload (suggested shape):

```json
{
  "status": "error",
  "code": "VALIDATION_FAILED",
  "details": {
    "wordLength": 5,
    "violations": [
      { "clueIndex": 2, "reason": "Only 2 valid candidates after filtering" },
      { "clueIndex": 4, "reason": "Found lowercase or non A–Z characters" }
    ]
  }
}
```

## 9) Observability

* **Structured logs:** request id, wordLength, clueCount, candidateCounts per clue, validation failures per type.
* **Metrics:** latency (p50/p95), failure rates by code, average candidates per clue, distribution of regex failures.
* **Sampling:** Log redacted prompts/responses with hashing to avoid storing full clues in plain text if needed.

## 10) Security & Compliance

* **PII:** Clues may be user-provided text; treat as sensitive. Avoid persistent storage of raw clues by default.
* **Data in transit:** TLS everywhere.
* **Data at rest:** If logging prompts, redact or hash; configurable toggle.
* **AOAI data controls:** Ensure deployments are configured with no data retention if required.

## 11) Configuration

* `MODEL_DEPLOYMENT_NAME`
* `TEMPERATURE` (default 0.3)
* `MAX_TOKENS`
* `USE_JSON_MODE` (bool)
* `TIMEOUT_MS`
* `RETRY_POLICY` (max attempts, backoff)
* `STRICT_REGEX` (bool, default true)
* `REASONS_MAX_LEN` (default 80)
* `CANDIDATES_MIN` (3), `CANDIDATES_MAX` (6)

## 12) Quality Gates (Acceptance Criteria)

* **Constraint fidelity:** ≥ 98% of returned words match `^[A-Z]{N}$` without post-normalization.
* **Completeness:** For ≥ 99% of clues, 3–6 candidates pass validation on first attempt.
* **Specificity:** ≥ 90% reasons contain a clue-specific token (term from clue or synonym).
* **Stability:** Repeated runs with same seed/settings produce variations **within limits** (no empty arrays, no runaway hallucinations).

## 13) Test Plan (pre-code wiring)

* **Golden set:** 10 small puzzles (N=4/5) with known mainstream answers; verify candidate coverage includes at least one correct answer per clue ≥ 80% of the time.
* **Edge cases:**

  * N=3 (short words) and N=8 (longer words).
  * Ambiguous clues (“Ball”)—ensure diversity across parts of speech.
  * Similar clues back-to-back—ensure **within-clue** dedup only.
  * Non-ASCII input in clues—ensure output remains ASCII A–Z.
* **Stress:** 30 clues in one request; confirm latency and token limits.

## 14) Interfacing with Stage B

* Stage A delivers **layered candidates**; Stage B must **not** assume any ordering beyond 1:1 mapping with input clues.
* Provide `items[i].clue` unchanged and normalized candidates; this becomes Stage B’s **layer i**.
* If Stage A fails for specific clues, return clear indices so Stage B (or the orchestrator) can choose to **repair** only those layers later.

## 15) Rollout & Ops

* **Canary:** Enable for internal callers at low QPS; track validation failure metrics.
* **Feature flags:** Toggle JSON mode vs. schema-in-prompt; adjust temperature.
* **Playbook:** On spike in `VALIDATION_FAILED`, first reduce temperature, then re-enable JSON mode, then increase max tokens.

---

## Appendix A — Prompt Templates (no implementation details)

### System Message

> You are a careful word-clue solver. Return **ONLY** strict JSON that follows the provided JSON schema. Do not add prose or markdown. Rules: All candidate words MUST be UPPERCASE ASCII A–Z only and have exact length WORD\_LENGTH. Provide 3–6 candidates per clue. No duplicates within a clue. Keep “reason” under 15 words and specific to the clue. Prefer common words over obscure terms.

### User Message (parameterized)

* Defines `WORD_LENGTH = {{N}}`
* Lists `CLUES = {{JSON array of clues}}`
* Embeds the JSON schema from §6 with `__N__` replaced by `N`.
* Restates rules (uppercase, exact length, 3–6, no duplicates, short reasons).
* Asks to populate `wordLength` and `items` accordingly and **nothing else**.

---

This document gives your agent everything needed to implement Stage A safely—clear contracts, constraints, prompts, validation, and ops—without prescribing code or SDK specifics.
