using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using Afidep_E_Recruitment.Controllers;
using Newtonsoft.Json;
using static Afidep_E_Recruitment.Controllers.HomeController;

namespace Afidep_E_Recruitment.Models.Screening
{
    // HR's sign-in, each job's screening criteria and the screening runs, in the portal's database
    // (tables created by Database/HrScreening.sql)
    public class ScreeningStore
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        private T Scalar<T>(string sql, params (string, object)[] ps)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
                conn.Open();
                var o = cmd.ExecuteScalar();
                return o == null || o == DBNull.Value ? default(T) : (T)Convert.ChangeType(o, typeof(T), CultureInfo.InvariantCulture);
            }
        }

        private List<Dictionary<string, object>> Rows(string sql, params (string, object)[] ps)
        {
            var rows = new List<Dictionary<string, object>>();
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                        rows.Add(row);
                    }
            }
            return rows;
        }

        // ---------- HR users ----------

        public string AuthenticateHr(string username, string password)
        {
            var row = Rows("SELECT FullName, PasswordHash FROM Recruitment_HrUsers WHERE Username = @u AND IsActive = 1", ("@u", username)).FirstOrDefault();
            if (row == null || string.IsNullOrEmpty(row["PasswordHash"] as string)) return null;
            return PasswordHashers.VerifyPassword(password ?? "", (string)row["PasswordHash"]) ? (string)row["FullName"] : null;
        }

        // ---------- criteria ----------

        public ScreeningCriteria Criteria(string requestId, out DateTime? savedAt, out string savedBy)
        {
            savedAt = null; savedBy = null;
            var row = Rows("SELECT CriteriaJson, UpdatedAt, UpdatedBy FROM Recruitment_JobCriteria WHERE RequestId = @r", ("@r", requestId)).FirstOrDefault();
            if (row == null) return null;
            savedAt = row["UpdatedAt"] as DateTime?;
            savedBy = row["UpdatedBy"] as string;
            return JsonConvert.DeserializeObject<ScreeningCriteria>((string)row["CriteriaJson"]);
        }

        public void SaveCriteria(string requestId, ScreeningCriteria criteria, string by)
        {
            Scalar<int>(@"MERGE Recruitment_JobCriteria AS t USING (SELECT @r AS RequestId) AS s ON t.RequestId = s.RequestId
                WHEN MATCHED THEN UPDATE SET CriteriaJson = @c, UpdatedBy = @b, UpdatedAt = GETDATE()
                WHEN NOT MATCHED THEN INSERT (RequestId, CriteriaJson, UpdatedBy, UpdatedAt) VALUES (@r, @c, @b, GETDATE());
                SELECT 1;", ("@r", requestId), ("@c", JsonConvert.SerializeObject(criteria)), ("@b", by));
        }

        // ---------- runs ----------

        public int SaveRun(string requestId, ScreeningCriteria criteria, ScreeningOutcome outcome, string by)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    int runId;
                    using (var cmd = new SqlCommand(@"INSERT INTO Recruitment_ScreeningRuns (RequestId, Stage, ShortlistMode, ShortlistValue, CriteriaJson, Considered, Longlisted, Shortlisted, RunBy, RunAt)
                        OUTPUT INSERTED.Id VALUES (@r, @s, @m, @v, @c, @n, @l, @sl, @b, GETDATE())", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", requestId);
                        cmd.Parameters.AddWithValue("@s", outcome.ShortlistMode == null ? "Longlist" : "Shortlist");
                        cmd.Parameters.AddWithValue("@m", (object)outcome.ShortlistMode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@v", (object)outcome.ShortlistValue ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", JsonConvert.SerializeObject(criteria));
                        cmd.Parameters.AddWithValue("@n", outcome.Considered);
                        cmd.Parameters.AddWithValue("@l", outcome.Longlisted);
                        cmd.Parameters.AddWithValue("@sl", outcome.Shortlisted);
                        cmd.Parameters.AddWithValue("@b", by);
                        runId = (int)cmd.ExecuteScalar();
                    }
                    foreach (var r in outcome.Results)
                        using (var cmd = new SqlCommand(@"INSERT INTO Recruitment_ScreeningResults (RunId, ApplicationNo, ApplicantId, ApplicantName, Longlisted, Score, [Rank], Shortlisted,
                            Reason, TieNote, ChecksJson, BreakdownJson, DocumentsUploaded, DocumentsRead, DocumentProblems)
                            VALUES (@run, @a, @id, @name, @l, @score, @rank, @s, @reason, @tie, @checks, @bd, @du, @dr, @dp)", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@run", runId);
                            cmd.Parameters.AddWithValue("@a", r.ApplicationNo);
                            cmd.Parameters.AddWithValue("@id", (object)r.ApplicantId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@name", (object)r.ApplicantName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@l", r.Longlisted);
                            cmd.Parameters.AddWithValue("@score", r.Score);
                            cmd.Parameters.AddWithValue("@rank", (object)r.Rank ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@s", r.Shortlisted);
                            cmd.Parameters.AddWithValue("@reason", (object)r.Reason ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@tie", (object)r.TieNote ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@checks", JsonConvert.SerializeObject(r.Checks));
                            cmd.Parameters.AddWithValue("@bd", JsonConvert.SerializeObject(r.Breakdown));
                            cmd.Parameters.AddWithValue("@du", r.DocumentsUploaded);
                            cmd.Parameters.AddWithValue("@dr", r.DocumentsRead);
                            cmd.Parameters.AddWithValue("@dp", string.Join("\n", r.DocumentProblems));
                            cmd.ExecuteNonQuery();
                        }
                    tx.Commit();
                    return runId;
                }
            }
        }

        public class RunSummary
        {
            public int Id { get; set; }
            public string Stage { get; set; }
            public string ShortlistMode { get; set; }
            public decimal? ShortlistValue { get; set; }
            public int Considered { get; set; }
            public int Longlisted { get; set; }
            public int Shortlisted { get; set; }
            public string RunBy { get; set; }
            public DateTime RunAt { get; set; }
            public List<ScreeningResult> Results { get; set; } = new List<ScreeningResult>();
        }

        public RunSummary LatestRun(string requestId)
        {
            var run = Rows("SELECT TOP 1 * FROM Recruitment_ScreeningRuns WHERE RequestId = @r ORDER BY Id DESC", ("@r", requestId)).FirstOrDefault();
            if (run == null) return null;
            var s = new RunSummary
            {
                Id = (int)run["Id"], Stage = (string)run["Stage"], ShortlistMode = run["ShortlistMode"] as string, ShortlistValue = run["ShortlistValue"] as decimal?,
                Considered = (int)run["Considered"], Longlisted = (int)run["Longlisted"], Shortlisted = (int)run["Shortlisted"],
                RunBy = run["RunBy"] as string, RunAt = (DateTime)run["RunAt"],
            };
            foreach (var r in Rows("SELECT * FROM Recruitment_ScreeningResults WHERE RunId = @id ORDER BY CASE WHEN [Rank] IS NULL THEN 1 ELSE 0 END, [Rank], ApplicantName", ("@id", s.Id)))
                s.Results.Add(new ScreeningResult
                {
                    ApplicationNo = (string)r["ApplicationNo"], ApplicantId = r["ApplicantId"] as string, ApplicantName = r["ApplicantName"] as string,
                    Longlisted = (bool)r["Longlisted"], Score = (decimal)r["Score"], Rank = r["Rank"] as int?, Shortlisted = (bool)r["Shortlisted"],
                    Reason = r["Reason"] as string, TieNote = r["TieNote"] as string,
                    Checks = JsonConvert.DeserializeObject<List<CheckResult>>(r["ChecksJson"] as string ?? "[]"),
                    Breakdown = JsonConvert.DeserializeObject<ScoreBreakdown>(r["BreakdownJson"] as string ?? "{}"),
                    DocumentsUploaded = (int)r["DocumentsUploaded"], DocumentsRead = (int)r["DocumentsRead"],
                    DocumentProblems = ((r["DocumentProblems"] as string) ?? "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                });
            return s;
        }

        // ---------- applicants ----------

        public Dictionary<string, string> ApplicantNames(IEnumerable<string> emails)
        {
            var list = emails.Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in list)
            {
                var row = Rows("SELECT FirstName, MiddleName, LastName FROM Recruitment_DummyData WHERE Email = @e", ("@e", e)).FirstOrDefault();
                names[e] = row == null ? e : string.Join(" ", new[] { row["FirstName"] as string, row["MiddleName"] as string, row["LastName"] as string }.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            return names;
        }

        public List<ApplicantDocument> Documents(string applicationNo, string applicantId)
        {
            return Rows("SELECT Id, DocType, DocLink FROM Recruitment_DocumentData WHERE JOBID = @j AND IDNumber = @u ORDER BY DocType",
                    ("@j", applicationNo), ("@u", applicantId ?? ""))
                .Select(r =>
                {
                    var path = r["DocLink"] as string;
                    var text = DocumentTextReader.Read(path, out var problem);
                    return new ApplicantDocument { Id = (int)r["Id"], Type = r["DocType"] as string, FileName = System.IO.Path.GetFileName(path ?? ""), Text = text, ReadProblem = problem };
                }).ToList();
        }

        public string DocumentPath(int id) => Scalar<string>("SELECT DocLink FROM Recruitment_DocumentData WHERE Id = @i", ("@i", id));

        // an application, as Business Central returns it, made into what screening reads
        public static ApplicantFile ToApplicantFile(JobController.JobApplication app, string name, List<ApplicantDocument> docs)
        {
            var today = DateTime.Today;
            return new ApplicantFile
            {
                ApplicationNo = app.applicationnumber,
                ApplicantId = app.personalinfo?.idnumber,
                Name = name,
                Status = app.status,
                DateApplied = app.dateapplied.Year > 1900 ? app.dateapplied : (DateTime?)null,
                EducationLevels = (app.academicqualifications ?? new List<JobController.AcademicQualification>()).Select(q => q.educationlevel).Where(x => !string.IsNullOrEmpty(x)).ToList(),
                EducationText = string.Join(" \n ", (app.academicqualifications ?? new List<JobController.AcademicQualification>())
                    .Select(q => string.Join(" ", q.educationlevel, q.coursename, q.fieldofstudy, q.institutionname, q.grade))),
                Experience = (app.experiences ?? new List<JobController.WorkExperience>()).Select(e => new ExperienceLine
                {
                    Start = e.startdate,
                    End = string.Equals(e.presentemployer, "Yes", StringComparison.OrdinalIgnoreCase) || e.enddate.Year < 1900 || e.enddate > today ? today : e.enddate,
                    Title = e.jobtitle ?? "",
                    Text = string.Join(" ", e.jobtitle, e.employer, e.industry, e.hierarchylevel, e.summaryresponsibilities),
                }).Where(e => e.Start.Year > 1900).ToList(),
                ProfessionalQualifications = (app.profqualifications ?? new List<ProfessionalQualification>()).Select(q => string.Join(" ", q.description, q.level)).ToList(),
                Memberships = (app.profmembership ?? new List<ProfessionalMembership>()).Select(m => string.Join(" ", m.profbody, m.description, m.memberno)).ToList(),
                Documents = docs,
            };
        }
    }
}
