using CrossclimbBackend.Models;
using System.Text.Json;

namespace CrossclimbBackend.Core.Services
{
    public interface ICandidatePromptBuilder
    {
        (string systemMessage, string userMessage) BuildPrompts(
            CandidateGenerationRequest request);

        string GetJsonSchema(int wordLength);
    }

    public class CandidatePromptBuilder : ICandidatePromptBuilder
    {
        private const string JsonSchemaTemplate = """
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
        """;

        public (string systemMessage, string userMessage) BuildPrompts(
            CandidateGenerationRequest request)
        {
            var systemMessage = BuildSystemMessage();
            var userMessage = BuildUserMessage(request);
            
            return (systemMessage, userMessage);
        }

        public string GetJsonSchema(int wordLength)
        {
            return JsonSchemaTemplate.Replace("__N__", wordLength.ToString());
        }

        private string BuildSystemMessage()
        {
            return $"""
            You are a careful word-clue solver. Return **ONLY** strict JSON that follows the provided JSON schema. Do not add prose or markdown. Rules: All candidate words MUST be UPPERCASE ASCII A–Z only and have exact length WORD_LENGTH. Provide 3–6 candidates per clue. No duplicates within a clue. Keep "reason" under {CandidateGenerationDefaults.ReasonsMaxLength} words and specific to the clue. Prefer common words over obscure terms.
            """;
        }

        private string BuildUserMessage(CandidateGenerationRequest request)
        {
            var schema = GetJsonSchema(request.WordLength);
            
            var userMessage = $"""
            WORD_LENGTH = {request.WordLength}
            CLUES = {JsonSerializer.Serialize(request.Clues)}

            Generate candidate words following these rules:
            - All words must be UPPERCASE ASCII A–Z only
            - Each word must be exactly {request.WordLength} characters long
            - Provide 3–6 candidates per clue
            - No duplicates within a clue's candidates
            - Keep reasons under {CandidateGenerationDefaults.ReasonsMaxLength} characters and specific to the clue
            - Prefer common, contemporary vocabulary

            Return ONLY JSON matching this exact schema:
            {schema}

            Populate the "wordLength" field with {request.WordLength} and the "items" array with one object per clue. Each clue object should have the original clue text and 3–6 candidate objects with "word" and "reason" fields.
            """;

            return userMessage;
        }
    }
}