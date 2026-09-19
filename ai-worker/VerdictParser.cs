using System.Text.Json;

namespace AiWorker;

public static class VerdictParser
{
    public static readonly string[] ModelVerdicts = ["StrongMatch", "Match", "Possible", "NoMatch"];
    public static readonly string[] Seniorities = ["Junior", "Mid", "Senior", "Lead", "Unknown"];
    public static readonly string[] Periods = ["Hour", "Day", "Month", "Year", "Unknown"];
    public static readonly string[] WorkModels = ["Onsite", "Hybrid", "Remote", "Unknown"];
    public static readonly string[] Contracts = ["Permanent", "B2B", "Temporary", "Unknown"];

    public static VerdictPayload Parse(string content, long jobId, string fingerprint)
    {
        using var doc = TryParse(content, out var parseError);
        if (doc is null) throw new ModelOutputException(parseError);

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new ModelOutputException("verdict root must be a JSON object");

        var relevance = RequiredInt(root, "relevance", 0, 100);
        var verdict = RequiredString(root, "verdict");
        if (!ModelVerdicts.Contains(verdict))
            throw new ModelOutputException($"verdict must be one of {string.Join('|', ModelVerdicts)}");

        var reason = OptionalString(root, "reason");
        if (reason is not null && reason.Length > VerdictPayload.ReasonMaxLength)
            reason = reason[..VerdictPayload.ReasonMaxLength];

        var seniority = OptionalEnum(root, "seniority", Seniorities);
        var period = OptionalEnum(root, "period", Periods);
        var workModel = OptionalEnum(root, "work_model", WorkModels);
        var contract = OptionalEnum(root, "contract", Contracts);
        var currency = OptionalString(root, "currency");

        var salaryMin = OptionalInt(root, "salary_min", 0, int.MaxValue);
        var salaryMax = OptionalInt(root, "salary_max", 0, int.MaxValue);
        var experience = OptionalInt(root, "experience_years", 0, 50);
        var skills = OptionalSkills(root);

        return new VerdictPayload
        {
            JobId = jobId,
            Relevance = relevance,
            Verdict = verdict,
            Reason = reason,
            Seniority = seniority,
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            Currency = currency,
            Period = period,
            WorkModel = workModel,
            Contract = contract,
            ExperienceYears = experience,
            Skills = skills,
            Fingerprint = fingerprint,
        };
    }

    public static JsonDocument? TryParse(string content, out string error)
    {
        try
        {
            error = string.Empty;
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            error = $"invalid JSON: {ex.Message}";
            return null;
        }
    }

    private static int RequiredInt(JsonElement root, string name, int min, int max)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) || number < min || number > max)
            throw new ModelOutputException($"{name} must be an integer {min}-{max}");
        return number;
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(value.GetString()))
            throw new ModelOutputException($"{name} must be a non-empty string");
        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new ModelOutputException($"{name} must be a string or null");
        return value.GetString();
    }

    private static string? OptionalEnum(JsonElement root, string name, string[] allowed)
    {
        var value = OptionalString(root, name);
        if (value is null) return null;
        if (!allowed.Contains(value))
            throw new ModelOutputException($"{name} must be one of {string.Join('|', allowed)} or null");
        return value;
    }

    private static int? OptionalInt(JsonElement root, string name, int min, int max)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) ||
            number < min || number > max)
            throw new ModelOutputException($"{name} must be an integer {min}-{max} or null");
        return number;
    }

    private static List<string>? OptionalSkills(JsonElement root)
    {
        if (!root.TryGetProperty("skills", out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Array)
            throw new ModelOutputException("skills must be an array of strings or null");

        var skills = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new ModelOutputException("skills must be an array of strings or null");
            if (skills.Count < VerdictPayload.SkillsMaxCount) skills.Add(item.GetString()!);
        }
        return skills;
    }
}
