using System.Text.RegularExpressions;

namespace Photon.JobSeeker;

public class Resume
{
    public ResumeContext GenerateContext(long jobid)
    {
        var result = new ResumeContext();

        using var database = Database.Open();
        var job = database.Job.Fetch(jobid);

        if (job == default || job.Log == null) return result;

        result.Keys.DOTNET = DOTNET.IsMatch(job.Log);
        result.Keys.JAVA = JAVA.IsMatch(job.Log);
        result.Keys.PYTHON = PYTHON.IsMatch(job.Log);
        result.Keys.GOLANG = GOLANG.IsMatch(job.Log);
        result.Keys.SQL = EXPERT_SQL.IsMatch(job.Log);
        result.Keys.FRONT_END = FRONT_END.IsMatch(job.Log);
        result.Keys.WEB = LOW_LEVEL.IsMatch(job.Log);
        result.Keys.MACHINE_LEARNING = MACHINE_LEARNING.IsMatch(job.Log);

        if (WEB_API.IsMatch(job.Log)) result.Keys.More.Add("Web-API", nameof(FRONT_END).ToLower());
        if (TENSORFLOW.IsMatch(job.Log)) result.Keys.More.Add("TensorFlow", nameof(MACHINE_LEARNING).ToLower());
        if (GIT.IsMatch(job.Log)) result.Keys.More.Add("GIT", null!);
        if (ERP.IsMatch(job.Log)) result.Keys.More.Add("ERP", null!);

        return result;
    }

    private static readonly Regex DOTNET = new(@"\*C#.NET\*");
    private static readonly Regex JAVA = new(@"\*Java\*");
    private static readonly Regex GOLANG = new(@"\*GO-Lang\*");
    private static readonly Regex FRONT_END = new(@"\*(Frontend|Web-API)\*");
    private static readonly Regex EXPERT_SQL = new(@"\*Expert-SQL\*");
    private static readonly Regex LOW_LEVEL = new(@"\*Low-Level\*");
    private static readonly Regex MACHINE_LEARNING  = new(@"\*(Machine-Learning|TensorFlow)\*");
    private static readonly Regex PYTHON = new(@"\*Python\*");

    private static readonly Regex WEB_API = new(@"\*Web-API\*");
    private static readonly Regex TENSORFLOW = new(@"\*TensorFlow\*");
    private static readonly Regex GIT = new(@"\*Git\*");
    private static readonly Regex ERP = new(@"\*ERP\*");
}