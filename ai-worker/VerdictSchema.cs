namespace AiWorker;

public static class VerdictSchema
{
    public const string ResponseFormat = """
    {
      "type": "json_schema",
      "json_schema": {
        "name": "job_verdict",
        "strict": true,
        "schema": {
          "type": "object",
          "properties": {
            "relevance": { "type": "integer", "minimum": 0, "maximum": 100 },
            "verdict": { "type": "string", "enum": ["StrongMatch", "Match", "Possible", "NoMatch"] },
            "reason": { "type": "string" },
            "seniority": { "type": "string", "enum": ["Junior", "Mid", "Senior", "Lead", "Unknown"] },
            "salary_min": { "type": ["integer", "null"], "minimum": 0 },
            "salary_max": { "type": ["integer", "null"], "minimum": 0 },
            "currency": { "type": ["string", "null"] },
            "period": { "type": "string", "enum": ["Hour", "Day", "Month", "Year", "Unknown"] },
            "work_model": { "type": "string", "enum": ["Onsite", "Hybrid", "Remote", "Unknown"] },
            "contract": { "type": "string", "enum": ["Permanent", "B2B", "Temporary", "Unknown"] },
            "experience_years": { "type": ["integer", "null"], "minimum": 0, "maximum": 50 },
            "skills": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["relevance", "verdict", "reason", "seniority", "salary_min", "salary_max",
            "currency", "period", "work_model", "contract", "experience_years", "skills"],
          "additionalProperties": false
        }
      }
    }
    """;
}
