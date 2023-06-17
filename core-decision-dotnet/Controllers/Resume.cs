namespace Photon.JobSeeker;

public class Resume
{
    public ResumeContext GenerateContext(long jobid)
    {
        var result = new ResumeContext();

        using var database = Database.Open();
        var job = database.Job.Fetch(jobid);

        if (job == default || job.Options == null) return result;

        result.Keys.DOTNET = job.Options.Contains(nameof(ResumeContext.KeysContext.DOTNET));
        result.Keys.JAVA = job.Options.Contains(nameof(ResumeContext.KeysContext.JAVA));
        result.Keys.PYTHON = job.Options.Contains(nameof(ResumeContext.KeysContext.PYTHON));
        result.Keys.GOLANG = job.Options.Contains(nameof(ResumeContext.KeysContext.GOLANG));
        result.Keys.SQL = job.Options.Contains(nameof(ResumeContext.KeysContext.SQL));
        result.Keys.FRONT_END = job.Options.Contains(nameof(ResumeContext.KeysContext.FRONT_END));
        result.Keys.WEB = job.Options.Contains(nameof(ResumeContext.KeysContext.WEB));
        result.Keys.MACHINE_LEARNING = job.Options.Contains(nameof(ResumeContext.KeysContext.MACHINE_LEARNING));

        var more = job.Options.Where(x => !x.StartsWith('-') && !MainKeys.Contains(x))
                              .Select(x => x.Split(':'))
                              .Select(x => new { key = x.Last(), parent = x.Length > 1 ? x.First() : string.Empty })
                              .Where(x => !NotInclude.Contains(x.key.ToLower()))
                              .GroupBy(k => k.parent)
                              .ToDictionary(k => k.Key, v => v.Select(x => x.key).ToArray());

        result.Keys.More = more;

        var not_include = job.Options.Where(x => x.StartsWith('-') && x.Length > 1)
                                     .Select(x => x[1..]);

        result.NotIncluded.UnionWith(not_include);

        return result;
    }

    private static readonly HashSet<string> NotInclude = new()
    {
        "angular", "database"
    };

    private static readonly HashSet<string> MainKeys = new()
    {
        nameof(ResumeContext.KeysContext.DOTNET),
        nameof(ResumeContext.KeysContext.JAVA),
        nameof(ResumeContext.KeysContext.PYTHON),
        nameof(ResumeContext.KeysContext.GOLANG),
        nameof(ResumeContext.KeysContext.SQL),
        nameof(ResumeContext.KeysContext.FRONT_END),
        nameof(ResumeContext.KeysContext.WEB),
        nameof(ResumeContext.KeysContext.MACHINE_LEARNING),
    };
}