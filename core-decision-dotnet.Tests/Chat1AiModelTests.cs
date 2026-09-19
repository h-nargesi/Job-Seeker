using System.Data;
using System.Data.SQLite;

namespace Photon.JobSeeker.Tests;

public class Chat1AiModelTests
{
    [Fact]
    public void JobState_member_names_are_the_locked_set()
    {
        Assert.Equal(
            new[]
            {
                nameof(JobState.Saved),
                nameof(JobState.Revaluation),
                nameof(JobState.NotApprovedRegex),
                nameof(JobState.AiPending),
                nameof(JobState.NotApprovedAI),
                nameof(JobState.AIError),
                nameof(JobState.Attention),
                nameof(JobState.Rejected),
                nameof(JobState.Applied),
            },
            Enum.GetNames<JobState>());
    }

    [Fact]
    public void Job_has_no_AiState_or_AiTitle_and_carries_phase3_columns()
    {
        Assert.Null(typeof(Job).GetProperty("AiState"));
        Assert.Null(typeof(Job).GetProperty("AiTitle"));
        Assert.Equal(typeof(ResumeContext), typeof(Job).GetProperty(nameof(Job.AiOptions))!.PropertyType);
        Assert.Equal(typeof(ResumeText), typeof(Job).GetProperty(nameof(Job.ResumeText))!.PropertyType);
        Assert.Equal(typeof(List<string>), typeof(Job).GetProperty(nameof(Job.AiSkills))!.PropertyType);
    }

    [Theory]
    [InlineData(typeof(AiVerdict), "StrongMatch,Match,Possible,NoMatch,Error")]
    [InlineData(typeof(AiSeniority), "Junior,Mid,Senior,Lead,Unknown")]
    [InlineData(typeof(AiWorkModel), "Onsite,Hybrid,Remote,Unknown")]
    [InlineData(typeof(AiContract), "Permanent,B2B,Temporary,Unknown")]
    [InlineData(typeof(AiPeriod), "Hour,Day,Month,Year,Unknown")]
    public void Extraction_enums_store_locked_names(Type enumType, string names)
    {
        Assert.Equal(names.Split(','), Enum.GetNames(enumType));

        var handlerType = typeof(EnumNameTypeHandler<>).MakeGenericType(enumType);
        var handler = Activator.CreateInstance(handlerType)!;
        var setValue = handlerType.GetMethod(nameof(EnumNameTypeHandler<JobState>.SetValue))!;
        var parse = handlerType.GetMethod(nameof(EnumNameTypeHandler<JobState>.Parse))!;

        foreach (var name in Enum.GetNames(enumType))
        {
            var value = Enum.Parse(enumType, name);
            var parameter = new SQLiteParameter();
            setValue.Invoke(handler, [parameter, value]);
            Assert.Equal(name, parameter.Value);
            Assert.Equal(DbType.String, parameter.DbType);
            Assert.Equal(value, parse.Invoke(handler, [name]));
        }
    }

    [Fact]
    public void ResumeText_handler_round_trips_slot_map_as_camel_case_json()
    {
        var handler = new JsonTypeHandler<ResumeText>();
        var original = new ResumeText
        {
            ["title"] = new ResumeTextSlot
            {
                Live = "Senior Developer",
                Proposal = "Full-stack lead",
                Status = ResumeTextSlotStatus.Pending,
            },
        };

        var parameter = new SQLiteParameter();
        handler.SetValue(parameter, original);
        var json = Assert.IsType<string>(parameter.Value);
        Assert.Contains("\"live\"", json);
        Assert.Contains("\"proposal\"", json);
        Assert.Contains("\"status\"", json);
        Assert.Contains("pending", json);
        Assert.DoesNotContain("Pending", json);

        var parsed = handler.Parse(json);
        Assert.Equal("Senior Developer", parsed["title"].Live);
        Assert.Equal("Full-stack lead", parsed["title"].Proposal);
        Assert.Equal(ResumeTextSlotStatus.Pending, parsed["title"].Status);

        var null_parameter = new SQLiteParameter();
        handler.SetValue(null_parameter, null);
        Assert.Equal(DBNull.Value, null_parameter.Value);
    }

    [Fact]
    public void AiSkills_handler_round_trips_a_json_array()
    {
        var handler = new JsonTypeHandler<List<string>>();
        var parameter = new SQLiteParameter();
        handler.SetValue(parameter, new List<string> { ".NET", "C#" });
        var json = Assert.IsType<string>(parameter.Value);
        Assert.Contains(".NET", json);
        Assert.Contains("C#", json);

        var parsed = handler.Parse(json);
        Assert.Equal([".NET", "C#"], parsed);
    }

    [Fact]
    public void InsertJob_leaves_ai_columns_null()
    {
        using var db = new GoldenDatabase();
        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "ai-empty",
            State = JobState.AiPending,
            Url = "https://example.com/ai-empty",
        };
        db.Database.Job.InsertJob(job);

        Assert.Equal(DBNull.Value, db.Scalar("SELECT AiScore FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT AiVerdict FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT AiOptions FROM Job"));
        Assert.Equal(DBNull.Value, db.Scalar("SELECT ResumeText FROM Job"));

        var fetched = db.Database.Job.Fetch(job.JobID);
        Assert.NotNull(fetched);
        Assert.Null(fetched!.AiScore);
        Assert.Null(fetched.AiVerdict);
        Assert.Null(fetched.AiOptions);
        Assert.Null(fetched.ResumeText);
        Assert.Equal(JobState.AiPending, fetched.State);
    }

    [Fact]
    public void Fetch_round_trips_verdict_extraction_and_phase3_json()
    {
        using var db = new GoldenDatabase();
        var job = new Job
        {
            AgencyID = GoldenDatabase.AgencyId,
            Country = "NL",
            Code = "ai-full",
            State = JobState.Attention,
            Url = "https://example.com/ai-full",
            AiScore = 81,
            AiVerdict = AiVerdict.Match,
            AiReason = "Genuine .NET role",
            AiSeniority = AiSeniority.Senior,
            AiSalaryMin = 60000,
            AiSalaryMax = 80000,
            AiCurrency = "EUR",
            AiPeriod = AiPeriod.Year,
            AiWorkModel = AiWorkModel.Hybrid,
            AiContract = AiContract.B2B,
            AiExperienceYears = 5,
            AiSkills = [".NET", "C#"],
            AiOptions = new ResumeContext { JobTitle = "Tailored" },
            ResumeText = new ResumeText
            {
                ["summary"] = new ResumeTextSlot
                {
                    Live = null,
                    Proposal = "I build .NET services.",
                    Status = ResumeTextSlotStatus.Pending,
                },
            },
        };
        db.Database.Job.InsertJob(job);
        WriteAiColumns(db, job);

        Assert.Equal("Match", db.Scalar("SELECT AiVerdict FROM Job"));
        Assert.Equal("Senior", db.Scalar("SELECT AiSeniority FROM Job"));
        Assert.Equal("Year", db.Scalar("SELECT AiPeriod FROM Job"));
        Assert.Equal("Hybrid", db.Scalar("SELECT AiWorkModel FROM Job"));
        Assert.Equal("B2B", db.Scalar("SELECT AiContract FROM Job"));
        Assert.Contains("pending", Assert.IsType<string>(db.Scalar("SELECT ResumeText FROM Job")));

        var fetched = db.Database.Job.Fetch(job.JobID);
        Assert.NotNull(fetched);
        Assert.Equal(81, fetched!.AiScore);
        Assert.Equal(AiVerdict.Match, fetched.AiVerdict);
        Assert.Equal("Genuine .NET role", fetched.AiReason);
        Assert.Equal(AiSeniority.Senior, fetched.AiSeniority);
        Assert.Equal(60000, fetched.AiSalaryMin);
        Assert.Equal(80000, fetched.AiSalaryMax);
        Assert.Equal("EUR", fetched.AiCurrency);
        Assert.Equal(AiPeriod.Year, fetched.AiPeriod);
        Assert.Equal(AiWorkModel.Hybrid, fetched.AiWorkModel);
        Assert.Equal(AiContract.B2B, fetched.AiContract);
        Assert.Equal(5, fetched.AiExperienceYears);
        Assert.Equal([".NET", "C#"], fetched.AiSkills);
        Assert.Equal("Tailored", fetched.AiOptions?.JobTitle);
        Assert.Equal("I build .NET services.", fetched.ResumeText?["summary"].Proposal);
        Assert.Equal(ResumeTextSlotStatus.Pending, fetched.ResumeText?["summary"].Status);
        Assert.Null(fetched.ResumeText?["summary"].Live);
    }

    [Fact]
    public void Schema_files_include_additive_ai_columns_and_explicit_app_setting_install()
    {
        var root = RepoRoot();
        var job_sql = File.ReadAllText(Path.Combine(root, "database", "structure", "job.sql"));
        var app_sql = File.ReadAllText(Path.Combine(root, "database", "structure", "app-setting.sql"));
        var install = File.ReadAllText(Path.Combine(root, "database", "installation.sh"));

        foreach (var column in new[]
        {
            "AiScore", "AiVerdict", "AiReason", "AiSeniority", "AiSalaryMin", "AiSalaryMax",
            "AiCurrency", "AiPeriod", "AiWorkModel", "AiContract", "AiExperienceYears",
            "AiSkills", "AiOptions", "ResumeText",
        })
            Assert.Contains(column, job_sql);

        Assert.DoesNotContain("AiState", job_sql);
        Assert.DoesNotContain("AiTitle", job_sql);

        Assert.Contains("floor", app_sql);
        Assert.Contains("aipassmark", app_sql);
        Assert.Contains("scorecap", app_sql);
        Assert.Contains("w_regex", app_sql);
        Assert.Contains("w_ai", app_sql);

        Assert.Contains("structure/app-setting.sql", install);
        Assert.DoesNotContain("structure/*", install);
    }

    private static void WriteAiColumns(GoldenDatabase db, Job job)
    {
        db.Database.Execute(@"
UPDATE Job SET
    AiScore = @AiScore, AiVerdict = @AiVerdict, AiReason = @AiReason,
    AiSeniority = @AiSeniority, AiSalaryMin = @AiSalaryMin, AiSalaryMax = @AiSalaryMax,
    AiCurrency = @AiCurrency, AiPeriod = @AiPeriod, AiWorkModel = @AiWorkModel,
    AiContract = @AiContract, AiExperienceYears = @AiExperienceYears,
    AiSkills = @AiSkills, AiOptions = @AiOptions, ResumeText = @ResumeText
WHERE JobID = @JobID", new
        {
            job.JobID,
            job.AiScore,
            AiVerdict = job.AiVerdict?.ToString(),
            job.AiReason,
            AiSeniority = job.AiSeniority?.ToString(),
            job.AiSalaryMin,
            job.AiSalaryMax,
            job.AiCurrency,
            AiPeriod = job.AiPeriod?.ToString(),
            AiWorkModel = job.AiWorkModel?.ToString(),
            AiContract = job.AiContract?.ToString(),
            job.AiExperienceYears,
            job.AiSkills,
            job.AiOptions,
            job.ResumeText,
        });
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "database", "structure")))
            dir = dir.Parent!;

        Assert.NotNull(dir);
        return dir.FullName;
    }
}
