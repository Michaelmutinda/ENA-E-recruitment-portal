using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ServiceProxy
{
    // TEST MODE ONLY (appSetting SimulateBusinessCentral=true): answers the portal's calls from a SQL Server test database
    // (appSetting TestBackendConnection, tables in the "bc" schema) instead of Business Central's PortalAPI codeunit, returning
    // the same JSON shapes the portal reads. Never enable it on the live site.
    public static class SimulatedBusinessCentral
    {
        public static bool Enabled =>
            string.Equals(ConfigurationManager.AppSettings["SimulateBusinessCentral"], "true", StringComparison.OrdinalIgnoreCase);

        private static string ConnectionString => ConfigurationManager.AppSettings["TestBackendConnection"];

        private class Filters
        {
            public string userid { get; set; }
            public string requestid { get; set; }
            public string applicationnumber { get; set; }
        }

        private static Filters Parse(string arguments) =>
            string.IsNullOrWhiteSpace(arguments) ? new Filters() : JsonConvert.DeserializeObject<Filters>(arguments) ?? new Filters();

        private static List<Dictionary<string, object>> Query(string sql, params (string name, object value)[] parameters)
        {
            var rows = new List<Dictionary<string, object>>();
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                    }
            }
            return rows;
        }

        private static JArray Array(object json) => JArray.Parse(json as string ?? "[]");

        // ---- jobs ----

        private const string JobColumns = "RequestId, JobId, JobTitle, ContractType, MainObjective, PostingDate, ClosingDate, Responsibilities, " +
                                          "AcademicQualifications, Experience, ProfessionalQualifications, ProfessionalMemberships";

        private static JObject Job(Dictionary<string, object> r) => new JObject
        {
            ["requestid"] = (string)r["RequestId"],
            ["jobid"] = (string)r["JobId"],
            ["jobdescription"] = (string)r["JobTitle"],
            ["postingdate"] = (DateTime)r["PostingDate"],
            ["closingdate"] = (DateTime)r["ClosingDate"],
            ["contracttype"] = (string)r["ContractType"],
            ["mainobjective"] = (string)r["MainObjective"],
            ["jobresponsibilities"] = Array(r["Responsibilities"]),
            ["acadquals"] = Array(r["AcademicQualifications"]),
            ["experience"] = Array(r["Experience"]),
            ["profquals"] = Array(r["ProfessionalQualifications"]),
            ["profmembs"] = Array(r["ProfessionalMemberships"]),
        };

        // open jobs: posted and not yet closed
        public static string RetrieveAvailableJobs(string arguments) =>
            new JArray(Query($"SELECT {JobColumns} FROM bc.JobRequisitions WHERE PostingDate <= CAST(GETDATE() AS date) AND ClosingDate >= CAST(GETDATE() AS date) ORDER BY ClosingDate")
                .Select(Job)).ToString(Formatting.None);

        public static string RetrieveAvailableJobDetails(string arguments)
        {
            var row = Query($"SELECT {JobColumns} FROM bc.JobRequisitions WHERE RequestId = @r", ("@r", Parse(arguments).requestid)).FirstOrDefault();
            return row == null ? "{}" : Job(row).ToString(Formatting.None);
        }

        // HR: every requisition, open and closed, with how many applications were submitted
        public static string RetrieveRequisitions(string arguments) =>
            new JArray(Query($@"SELECT {JobColumns}, (SELECT COUNT(*) FROM bc.JobApplications a WHERE a.RequestId = j.RequestId AND a.Status = 'Submitted') AS Submitted,
                    (SELECT COUNT(*) FROM bc.JobApplications a WHERE a.RequestId = j.RequestId AND a.Status <> 'Submitted') AS Drafts
                    FROM bc.JobRequisitions j ORDER BY ClosingDate DESC")
                .Select(r => { var j = Job(r); j["submitted"] = (int)r["Submitted"]; j["drafts"] = (int)r["Drafts"]; return j; })).ToString(Formatting.None);

        // HR: every application for a requisition (drafts included, with their status)
        public static string RetrieveRequisitionApplications(string arguments) =>
            new JArray(Query(ApplicationSelect + " WHERE a.RequestId = @r ORDER BY a.ApplicationNo", ("@r", Parse(arguments).requestid ?? ""))
                .Select(Application)).ToString(Formatting.None);

        public static string GetCountries() =>
            new JArray(Query("SELECT Code, Name FROM bc.Countries ORDER BY Name")
                .Select(r => new JObject { ["code"] = (string)r["Code"], ["name"] = (string)r["Name"] })).ToString(Formatting.None);

        // the job description's download link: files in the proxy site's Downloads folder
        public static string GetRequisitionJDAttachmentPath(string requisitionNo)
        {
            var row = Query("SELECT JDFileName FROM bc.JobRequisitions WHERE RequestId = @r", ("@r", requisitionNo)).FirstOrDefault();
            var file = row?["JDFileName"] as string;
            if (string.IsNullOrEmpty(file)) return "";
            var baseUrl = ConfigurationManager.AppSettings["TestDownloadsUrl"] ?? "http://localhost:51393/Downloads/";
            return baseUrl + file;
        }

        // ---- applications ----

        private const string ApplicationSelect = @"SELECT a.ApplicationNo, a.ApplicantId, a.ApplicationDate, a.Status, a.Stage, a.DateApplied, a.AcademicQualifications,
            a.Experiences, a.ProfessionalQualifications, a.ProfessionalMemberships, a.Referees, j.JobTitle
            FROM bc.JobApplications a LEFT JOIN bc.JobRequisitions j ON j.RequestId = a.RequestId";

        private static JObject Application(Dictionary<string, object> r) => new JObject
        {
            ["applicationnumber"] = (string)r["ApplicationNo"],
            ["applicationdate"] = (DateTime)r["ApplicationDate"],
            ["status"] = (string)r["Status"],
            ["stage"] = (string)r["Stage"],
            ["dateapplied"] = r["DateApplied"] as DateTime? ?? DateTime.MinValue,
            ["description"] = r["JobTitle"] as string ?? "",
            // the portal identifies an applicant by the id it signs them in with
            ["personalinfo"] = new JObject { ["idnumber"] = (string)r["ApplicantId"] },
            ["academicqualifications"] = Array(r["AcademicQualifications"]),
            ["experiences"] = Array(r["Experiences"]),
            ["profqualifications"] = Array(r["ProfessionalQualifications"]),
            ["profmembership"] = Array(r["ProfessionalMemberships"]),
            ["referees"] = Array(r["Referees"]),
            ["attachments"] = new JArray(),
        };

        private static Dictionary<string, object> ApplicationRow(string applicationNo) =>
            string.IsNullOrEmpty(applicationNo) ? null : Query(ApplicationSelect + " WHERE a.ApplicationNo = @n", ("@n", applicationNo)).FirstOrDefault();

        // the applicant's application for a job, started if they have none yet (as applying in Business Central does)
        private static Dictionary<string, object> ApplicationFor(string userid, string requestid, bool create)
        {
            if (string.IsNullOrEmpty(userid) || string.IsNullOrEmpty(requestid)) return null;
            var row = Query(ApplicationSelect + " WHERE a.ApplicantId = @u AND a.RequestId = @r", ("@u", userid), ("@r", requestid)).FirstOrDefault();
            if (row != null || !create) return row;
            var open = Query("SELECT 1 AS x FROM bc.JobRequisitions WHERE RequestId = @r AND ClosingDate >= CAST(GETDATE() AS date)", ("@r", requestid)).Any();
            if (!open) return null;
            var no = (string)Query("INSERT INTO bc.JobApplications (RequestId, ApplicantId) OUTPUT INSERTED.ApplicationNo VALUES (@r, @u)",
                ("@r", requestid), ("@u", userid)).Single()["ApplicationNo"];
            return ApplicationRow(no);
        }

        public static string RetrieveJobApplication(string arguments)
        {
            var f = Parse(arguments);
            var row = ApplicationRow(f.applicationnumber) ?? ApplicationFor(f.userid, f.requestid, create: true);
            return row == null ? "{}" : Application(row).ToString(Formatting.None);
        }

        public static string AddJobApplication(string arguments)
        {
            var f = Parse(arguments);
            var row = ApplicationFor(f.userid, f.requestid, create: true)
                      // called without the job: the applicant's latest draft
                      ?? Query(ApplicationSelect + " WHERE a.ApplicantId = @u AND a.Status = 'Open' ORDER BY a.ApplicationDate DESC", ("@u", f.userid ?? "")).FirstOrDefault();
            return row == null ? "{}" : Application(row).ToString(Formatting.None);
        }

        public static string RetrieveUserJobApplications(string arguments) =>
            new JArray(Query(ApplicationSelect + " WHERE a.ApplicantId = @u ORDER BY a.ApplicationDate DESC", ("@u", Parse(arguments).userid ?? ""))
                .Select(Application)).ToString(Formatting.None);

        // modify_type: academic_details / experience_details / prof_course_details / prof_memb_details / referee_details add lines,
        // user_details (kept by the portal itself), submission
        public static string ModifyJobApplication(string arguments)
        {
            var input = JObject.Parse(arguments);
            var no = (string)input["applicationnumber"];
            var row = ApplicationRow(no);
            if (row == null) return "{}";
            var details = input["application_details"] as JObject ?? new JObject();
            var modifyType = (string)input["modify_type"];
            var lists = new Dictionary<string, (string column, string key)>
            {
                ["academic_details"] = ("AcademicQualifications", "academicqualifications"),
                ["experience_details"] = ("Experiences", "experiences"),
                ["prof_course_details"] = ("ProfessionalQualifications", "profqualifications"),
                ["prof_memb_details"] = ("ProfessionalMemberships", "profmembership"),
                ["referee_details"] = ("Referees", "referees"),
            };
            if (lists.TryGetValue(modifyType ?? "", out var target))
            {
                var current = Array(row[target.column]);
                var next = current.Count == 0 ? 1 : current.Max(x => (int?)x["lineno"] ?? 0) + 1;
                foreach (var line in (details[target.key] as JArray ?? new JArray()).OfType<JObject>())
                {
                    line["lineno"] = target.key == "academicqualifications" || target.key == "experiences" ? (JToken)next.ToString() : next;
                    current.Add(line);
                    next++;
                }
                Query($"UPDATE bc.JobApplications SET {target.column} = @v OUTPUT INSERTED.Id WHERE ApplicationNo = @n",
                    ("@v", current.ToString(Formatting.None)), ("@n", no));
            }
            else if (modifyType == "submission")
            {
                Query("UPDATE bc.JobApplications SET Status = 'Submitted', Stage = 'Application Received', DateApplied = GETDATE() OUTPUT INSERTED.Id WHERE ApplicationNo = @n",
                    ("@n", no));
            }
            return Application(ApplicationRow(no)).ToString(Formatting.None);
        }

        public static string AttachJobAppicationDocument(string applicationNo, string documentPath, string documentType) => "OK";

        public static void InsertSharePointAttachment(string applicationNo, string fileName, string fileUrl) =>
            Query("INSERT INTO bc.SharePointAttachments (ApplicationNo, FileName, FileUrl) OUTPUT INSERTED.Id VALUES (@a, @f, @u)",
                ("@a", applicationNo), ("@f", fileName), ("@u", fileUrl));
    }
}
