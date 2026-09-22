using Dapper;

namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public const string AppliedViaDashboard = "Applied via dashboard";

        public const string AppliedViaAssistant = "Applied via assistant";

        private readonly Database database;

        public JobBusiness(Database database) => this.database = database;

        public List<JobListItem> Fetch(string[] agency_titles, string[] country_codes)
        {
            var where = string.Empty;
            var parameters = RankingParameters();

            if (agency_titles?.Length > 0)
            {
                where += $" AND Agency.Title IN ({string.Join(", ", agency_titles.Select((_, i) => $"@a{i}"))})";
                for (var i = 0; i < agency_titles.Length; i++)
                    parameters.Add($"a{i}", agency_titles[i]);
            }

            if (country_codes?.Length > 0)
            {
                where += $" AND Job.Country IN ({string.Join(", ", country_codes.Select((_, i) => $"@c{i}"))})";
                for (var i = 0; i < country_codes.Length; i++)
                    parameters.Add($"c{i}", country_codes[i]);
            }

            if (!string.IsNullOrEmpty(where))
                where = "WHERE" + where.Substring(4);

            var remoteHybrid = database.AppSetting.RemoteHybrid() == 1;
            var rows = database.Query<Job, long, long, string,
                (Job Job, long Relocation, long Remote, string AgencyName)>(
                Q_INDEX.Replace("@where@", where),
                (job, relocation, remote, agency) => (job, relocation, remote, agency),
                parameters, splitOn: "Relocation,Remote,AgencyName");

            return rows.Select(r => new JobListItem(
                r.Job,
                FlagCell.ForRelocation(r.Job.AiRelocation, (int)r.Relocation),
                FlagCell.ForRemote(r.Job.AiWorkModel, remoteHybrid, (int)r.Remote),
                r.AgencyName)).ToList();
        }

        public long FetchFromCount(DateTime time)
        {
            return database.ExecuteScalar<long>(Q_FETCH_FROM_COUNT, new { date = time });
        }

        public void ResetRevaluations()
        {
            database.Execute(Q_FETCH_UPDATE_REVAL, new { now = DateTime.Now });
        }

        public int ResurrectFloorPassing(int floor)
        {
            return database.Execute(Q_RESURRECT, new { floor, now = DateTime.Now });
        }

        public Job? FetchFrom(DateTime time)
        {
            database.BeginTransaction();
            try
            {
                var result = database.QueryFirstOrDefault<Job>(Q_FETCH_FROM, new { date = time });
                if (result == null) return default;

                ChangeState(result.JobID, JobState.Revaluation);
                database.Commit();

                return result;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }

        public Job? Fetch(long agency_id, string code)
        {
            return database.Query<Job>(Q_FETCH_BY_CODE, new { agency = agency_id, code }).FirstOrDefault();
        }

        public Job? Fetch(long job_id)
        {
            return database.Query<Job>(Q_FETCH_ID, new { job = job_id }).FirstOrDefault();
        }

        public string? GetFirstJob(long agency_id)
        {
            var owned = !database.InTransaction;
            if (owned) database.BeginTransaction();
            try
            {
                var data = database.Query<FirstJobRow>(Q_FETCH_FIRST, new { agency = agency_id }).FirstOrDefault();

                if (data != null)
                {
                    var now = DateTime.Now;
                    var this_time = data.Attempts + 1;

                    var this_tries = $"{this_time}: {now} (age {(int)(now - data.RegTime).TotalDays}d)"
                        + (this_time > 1 ? "\n" + data.Tries : "");

                    RegisterAttempt(data.JobID, this_time, this_tries);
                }

                if (owned) database.Commit();

                return data?.Url;
            }
            catch
            {
                if (owned) database.Rollback();
                throw;
            }
        }

        public void InsertFromSearch(long agencyId, string country, string url, string code)
        {
            database.Execute(Q_INSERT_FROM_SEARCH, new { agencyId, country, url, code });
        }

        public void InsertJob(Job job)
        {
            database.Execute(Q_INSERT_JOB, new
            {
                agencyId = job.AgencyID,
                country = job.Country,
                code = job.Code,
                title = job.Title,
                state = job.State.ToString(),
                score = job.Score,
                url = job.Url,
                html = job.Html,
                content = job.Content,
                link = job.Link,
                log = job.Log,
                options = job.Options,
                tries = job.Tries,
            });

            if (database.Changes() == 1)
                job.JobID = database.LastInsertRowId();
            else
                job.JobID = database.ExecuteScalar<long?>(Q_GET_ID_BY_CODE,
                    new { agency = job.AgencyID, code = job.Code }) ?? 0;
        }

        public void UpdateScrapedJob(Job job, bool codeChanged, bool linkFound, bool includeState)
        {
            var sets = new List<string>
            {
                "Title = @title",
                "Country = @country",
                "Html = @html",
                "Content = @content",
            };

            if (codeChanged) sets.Add("Code = @code");
            if (linkFound) sets.Add("Link = @link");
            if (includeState) sets.Add("State = @state");

            database.Execute($@"
UPDATE Job SET {string.Join(", ", sets)}, ModifiedOn = @now
WHERE JobID = @jobId", new
            {
                title = job.Title,
                country = job.Country,
                html = job.Html,
                content = job.Content,
                code = job.Code,
                link = job.Link,
                state = job.State.ToString(),
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void UpdateJobContent(Job job)
        {
            database.Execute(Q_UPDATE_CONTENT, new
            {
                html = job.Html,
                content = job.Content,
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void UpdateEvaluation(Job job, bool clearContent)
        {
            var clear = clearContent ? ", Html = null, Content = null" : "";

            database.Execute($@"
UPDATE Job SET State = @state, Log = @log, Options = @options, Score = @score,
    AiOptions = @aiOptions, ResumeText = @resumeText{clear}, ModifiedOn = @now
WHERE JobID = @jobId", new
            {
                state = job.State.ToString(),
                log = job.Log,
                options = job.Options,
                score = job.Score,
                aiOptions = job.AiOptions,
                resumeText = job.ResumeText,
                now = DateTime.Now,
                jobId = job.JobID,
            });
        }

        public void RegisterAttempt(long id, int attempt, string tries)
        {
            database.Execute(Q_REGISTER_ATTEMPT, new { tries, attempt, now = DateTime.Now, jobId = id });
        }

        public void ChangeState(long id, JobState state)
        {
            database.Execute(Q_CHANGE_STATE, new { state = state.ToString(), now = DateTime.Now, jobId = id });
        }

        public bool MarkApplied(long id, string source)
        {
            var job = database.QueryFirstOrDefault<MetaRow>(Q_FETCH_META, new { job = id });
            if (job == null) return false;
            if (job.State == JobState.Applied) return true;

            database.Execute(Q_MARK_APPLIED, new
            {
                log = AppendLog(job.Log, $"{source} — {DateTime.Now:yyyy-MM-dd}"),
                now = DateTime.Now,
                jobId = id,
            });
            return true;
        }

        public void RemoveHtmlContent(long id)
        {
            database.Execute(Q_REMOVE_HTML, new { now = DateTime.Now, jobId = id });
        }

        public void ChangeOptions(long id, ResumeContext? options)
        {
            database.Execute(Q_CHANGE_OPTIONS, new { options, now = DateTime.Now, jobId = id });
        }

        public void Delete(long id)
        {
            database.Execute(Q_DELETE, new { jobId = id });
        }

        public void Clean(int mounths, bool vacuum = false)
        {
            database.Execute(Q_CLEAN, new { date = DateTime.Now.AddMonths(-mounths) });
            database.Execute(Q_CLEAN_ATTENTION, RankingParameters(new
            {
                date = DateTime.Now.AddDays(-mounths * 7),
            }));
            database.Execute(Q_CLEAN_NOT_APPROVED, new { date = DateTime.Now.AddDays(-7) });
            if (vacuum) database.Execute(Q_VACUUM);
        }

        private DynamicParameters RankingParameters(object? extra = null)
        {
            var parameters = extra == null
                ? new DynamicParameters()
                : new DynamicParameters(extra);
            parameters.Add("scoreCap", database.AppSetting.ScoreCap());
            parameters.Add("wRegex", database.AppSetting.WRegex());
            parameters.Add("wAi", database.AppSetting.WAi());
            return parameters;
        }

        private sealed class FirstJobRow
        {
            public long JobID { get; set; }

            public string Url { get; set; } = string.Empty;

            public string? Tries { get; set; }

            public int Attempts { get; set; }

            public DateTime RegTime { get; set; }
        }

        private sealed class MetaRow
        {
            public long JobID { get; set; }

            public JobState State { get; set; }

            public string? Log { get; set; }
        }
    }
}
