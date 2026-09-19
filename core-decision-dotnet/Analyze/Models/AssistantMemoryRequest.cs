using System.Text.Json.Serialization;

namespace Photon.JobSeeker;

public sealed class AssistantMemoryRequest
{
    public const int FieldKeyMaxLength = 200;
    public const int DomainMaxLength = 200;
    public const int FieldLabelMaxLength = 400;
    public const int ValueMaxLength = 4000;
    public const int NoteMaxLength = 2000;

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("fieldKey")]
    public string? FieldKey { get; set; }

    [JsonPropertyName("fieldLabel")]
    public string? FieldLabel { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("confirmed")]
    public bool? Confirmed { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    public bool TryCreate(out MemoryRow row, out string error)
    {
        row = new MemoryRow();
        error = string.Empty;

        if (!TryScope(out var scope) || !TryKind(out var kind))
        {
            error = "scope must be Resume/Apply/Ranking and kind must be Tip/Correction";
            return false;
        }

        var field_key = FieldKey?.Trim() ?? string.Empty;
        if (!TryFieldKey(scope, field_key, out error)) return false;

        if (string.IsNullOrEmpty(Value))
        {
            error = "value is required";
            return false;
        }

        row.Scope = scope;
        row.Kind = kind;
        row.FieldKey = field_key;
        row.FieldLabel = FieldLabel?.Trim();
        row.Confirmed = Confirmed ?? false;
        row.Value = Value;
        row.Note = Note;

        return TryDomainAndLengths(row, out error);
    }

    public bool TryPatch(MemoryRow row, out string error)
    {
        error = string.Empty;

        if (Scope != null)
        {
            if (!TryScope(out var scope))
            {
                error = "scope must be Resume/Apply/Ranking";
                return false;
            }
            row.Scope = scope;
        }

        if (Kind != null)
        {
            if (!TryKind(out var kind))
            {
                error = "kind must be Tip/Correction";
                return false;
            }
            row.Kind = kind;
        }

        if (FieldKey != null) row.FieldKey = FieldKey.Trim();
        if (FieldLabel != null) row.FieldLabel = FieldLabel.Trim();
        if (Value != null) row.Value = Value;
        if (Note != null) row.Note = Note;
        if (Confirmed != null) row.Confirmed = Confirmed.Value;

        if (!TryFieldKey(row.Scope, row.FieldKey, out error)) return false;
        return TryDomainAndLengths(row, out error);
    }

    private bool TryScope(out MemoryScope scope)
    {
        scope = default;
        return Enum.TryParse(Scope, ignoreCase: true, out scope) && Enum.IsDefined(scope);
    }

    private bool TryKind(out MemoryKind kind)
    {
        kind = default;
        return Enum.TryParse(Kind, ignoreCase: true, out kind) && Enum.IsDefined(kind);
    }

    private static bool TryFieldKey(MemoryScope scope, string fieldKey, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrEmpty(fieldKey) || fieldKey.Length > FieldKeyMaxLength)
        {
            error = $"fieldKey is required (<= {FieldKeyMaxLength} characters)";
            return false;
        }

        if (scope == MemoryScope.Ranking && !MemoryRankingKeys.All.Contains(fieldKey))
        {
            error = $"ranking fieldKey must be one of: {string.Join(", ", MemoryRankingKeys.All)}";
            return false;
        }

        return true;
    }

    private bool TryDomainAndLengths(MemoryRow row, out string error)
    {
        error = string.Empty;

        row.AgencyDomain = MemoryBusiness.NormalizeDomain(Domain ?? row.AgencyDomain);

        if (row.AgencyDomain.Length > DomainMaxLength)
        {
            error = $"domain must be <= {DomainMaxLength} characters";
            return false;
        }

        if (row.FieldLabel != null && row.FieldLabel.Length > FieldLabelMaxLength)
        {
            error = $"fieldLabel must be <= {FieldLabelMaxLength} characters";
            return false;
        }

        if (string.IsNullOrEmpty(row.Value) || row.Value.Length > ValueMaxLength)
        {
            error = $"value is required (<= {ValueMaxLength} characters)";
            return false;
        }

        if (row.Note != null && row.Note.Length > NoteMaxLength)
        {
            error = $"note must be <= {NoteMaxLength} characters";
            return false;
        }

        return true;
    }
}
