using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Afidep_E_Recruitment.Models;
using Afidep_E_Recruitment.Models.Screening;
using Newtonsoft.Json;
using static Afidep_E_Recruitment.Controllers.HomeController;
using static Afidep_E_Recruitment.Models.HelperClasses;

namespace Afidep_E_Recruitment.Controllers
{
    // HR: job requisitions, each job's screening criteria (from its advert, editable), longlisting and shortlisting
    public class HrController : Controller
    {
        private readonly ScreeningStore store = new ScreeningStore();

        public class Requisition : AvailableJobs
        {
            public int submitted { get; set; }
            public int drafts { get; set; }
        }

        private string HrUser => Session["hruser"] as string;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var action = filterContext.ActionDescriptor.ActionName;
            if (HrUser == null && action != "Login")
                filterContext.Result = RedirectToAction("Login");
            ViewBag.HrUser = HrUser;
            base.OnActionExecuting(filterContext);
        }

        // ---------- sign-in ----------

        [HttpGet]
        public ActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            var name = store.AuthenticateHr((username ?? "").Trim(), password);
            if (name == null)
            {
                TempData["ErrorMessage"] = "Wrong username or password.";
                return RedirectToAction("Login");
            }
            Session["hruser"] = name;
            return RedirectToAction("Index");
        }

        public ActionResult Logout()
        {
            Session.Remove("hruser");
            return RedirectToAction("Login");
        }

        // ---------- jobs ----------

        private List<Requisition> Requisitions()
        {
            using (var client = new ProxyClient())
                return JsonConvert.DeserializeObject<List<Requisition>>(client.RetrieveRequisitions("{}")) ?? new List<Requisition>();
        }

        public ActionResult Index()
        {
            List<Requisition> jobs;
            try { jobs = Requisitions(); }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                jobs = new List<Requisition>();
            }
            ViewBag.Runs = jobs.ToDictionary(j => j.requestid, j => store.LatestRun(j.requestid));
            return View(jobs);
        }

        public class JobPage
        {
            public Requisition Job { get; set; }
            public ScreeningCriteria Criteria { get; set; }
            public bool CriteriaFromAdvert { get; set; }
            public DateTime? CriteriaSavedAt { get; set; }
            public string CriteriaSavedBy { get; set; }
            public ScreeningStore.RunSummary Run { get; set; }
            public Dictionary<string, List<ApplicantDocument>> Documents { get; set; } = new Dictionary<string, List<ApplicantDocument>>();
        }

        private ScreeningCriteria CriteriaFor(Requisition job, out bool fromAdvert, out DateTime? at, out string by)
        {
            var saved = store.Criteria(job.requestid, out at, out by);
            fromAdvert = saved == null;
            return saved ?? ScreeningEngine.FromAdvert(job);
        }

        public ActionResult Job(string id)
        {
            var job = Requisitions().FirstOrDefault(j => j.requestid == id);
            if (job == null) return HttpNotFound("No such job requisition.");
            var page = new JobPage { Job = job };
            page.Criteria = CriteriaFor(job, out var fromAdvert, out var at, out var by);
            page.CriteriaFromAdvert = fromAdvert;
            page.CriteriaSavedAt = at;
            page.CriteriaSavedBy = by;
            page.Run = store.LatestRun(id);
            if (page.Run != null)
                foreach (var r in page.Run.Results)
                    page.Documents[r.ApplicationNo] = store.Documents(r.ApplicationNo, r.ApplicantId).Select(d => new ApplicantDocument { Id = d.Id, Type = d.Type, FileName = d.FileName }).ToList();
            return View(page);
        }

        private static List<string> Lines(string text) =>
            (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveCriteria(string id, string minEducationLevel, string minYearsExperience, string requiredQualifications,
            string requiredMemberships, string[] requiredDocuments, string desirableQualifications, string keywords)
        {
            if (!decimal.TryParse(minYearsExperience ?? "0", NumberStyles.Number, CultureInfo.InvariantCulture, out var years) || years < 0 || years > 50)
            {
                TempData["ErrorMessage"] = "Years of experience must be a number between 0 and 50.";
                return RedirectToAction("Job", new { id });
            }
            if (!string.IsNullOrEmpty(minEducationLevel) && ScreeningEngine.LevelIndex(minEducationLevel) < 0)
            {
                TempData["ErrorMessage"] = "Choose an education level from the list.";
                return RedirectToAction("Job", new { id });
            }
            var criteria = new ScreeningCriteria
            {
                MinEducationLevel = string.IsNullOrEmpty(minEducationLevel) ? null : minEducationLevel,
                MinYearsExperience = years,
                RequiredQualifications = Lines(requiredQualifications),
                RequiredMemberships = Lines(requiredMemberships),
                RequiredDocuments = (requiredDocuments ?? new string[0]).Where(d => ScreeningEngine.DocumentTypes.Contains(d)).ToList(),
                DesirableQualifications = Lines(desirableQualifications),
                Keywords = Lines((keywords ?? "").Replace(",", "\n")),
            };
            store.SaveCriteria(id, criteria, HrUser);
            TempData["SuccessMessage"] = "Screening criteria saved. Run the longlisting again to apply them.";
            return RedirectToAction("Job", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetCriteria(string id)
        {
            var job = Requisitions().FirstOrDefault(j => j.requestid == id);
            if (job == null) return HttpNotFound();
            store.SaveCriteria(id, ScreeningEngine.FromAdvert(job), HrUser);
            TempData["SuccessMessage"] = "Criteria reset to what the job advert asks for.";
            return RedirectToAction("Job", new { id });
        }

        // ---------- longlisting and shortlisting ----------

        private ScreeningOutcome Screen(Requisition job, ScreeningCriteria criteria, string mode, decimal? value)
        {
            List<JobController.JobApplication> apps;
            using (var client = new ProxyClient())
                apps = JsonConvert.DeserializeObject<List<JobController.JobApplication>>(
                    client.RetrieveRequisitionApplications(JsonConvert.SerializeObject(new RequestFilters { requestid = job.requestid }))) ?? new List<JobController.JobApplication>();
            // only submitted applications are screened
            var submitted = apps.Where(a => string.Equals(a.status, "Submitted", StringComparison.OrdinalIgnoreCase)).ToList();
            var names = store.ApplicantNames(submitted.Select(a => a.personalinfo?.idnumber));
            var files = submitted.Select(a =>
            {
                var who = a.personalinfo?.idnumber;
                return ScreeningStore.ToApplicantFile(a, who != null && names.ContainsKey(who) ? names[who] : who, store.Documents(a.applicationnumber, who));
            }).ToList();
            return ScreeningEngine.Run(job.jobdescription, criteria, files, mode, value);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Longlist(string id)
        {
            var job = Requisitions().FirstOrDefault(j => j.requestid == id);
            if (job == null) return HttpNotFound();
            var criteria = CriteriaFor(job, out var fromAdvert, out _, out _);
            if (fromAdvert) store.SaveCriteria(id, criteria, HrUser);
            var outcome = Screen(job, criteria, null, null);
            store.SaveRun(id, criteria, outcome, HrUser);
            TempData["SuccessMessage"] = $"Longlisting done: {outcome.Longlisted} of {outcome.Considered} submitted applications meet every must-have.";
            return RedirectToAction("Job", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Shortlist(string id, string mode, string value)
        {
            var job = Requisitions().FirstOrDefault(j => j.requestid == id);
            if (job == null) return HttpNotFound();
            if (mode != "Number" && mode != "Percent")
            {
                TempData["ErrorMessage"] = "Choose a number of applicants or a percentage.";
                return RedirectToAction("Job", new { id });
            }
            if (!decimal.TryParse(value ?? "", NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || v <= 0
                || (mode == "Percent" && v > 100) || (mode == "Number" && v != Math.Floor(v)))
            {
                TempData["ErrorMessage"] = mode == "Percent" ? "Enter a percentage between 1 and 100." : "Enter a whole number of applicants (1 or more).";
                return RedirectToAction("Job", new { id });
            }
            var criteria = CriteriaFor(job, out var fromAdvert, out _, out _);
            if (fromAdvert) store.SaveCriteria(id, criteria, HrUser);
            var outcome = Screen(job, criteria, mode, v);
            store.SaveRun(id, criteria, outcome, HrUser);
            TempData["SuccessMessage"] = outcome.Longlisted == 0
                ? "Nobody is on the longlist, so nobody could be shortlisted."
                : $"Shortlisted {outcome.Shortlisted} of {outcome.Longlisted} longlisted applicants" + (mode == "Percent" ? $" ({v:0.##}%, rounded up)." : ".");
            return RedirectToAction("Job", new { id });
        }

        // the latest screening as a spreadsheet (CSV)
        public ActionResult Export(string id)
        {
            var run = store.LatestRun(id);
            if (run == null) return HttpNotFound("This job has not been screened yet.");
            string Cell(object o)
            {
                var s = Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
                return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            }
            var sb = new StringBuilder();
            sb.AppendLine("Rank,Application,Applicant,Email,Longlisted,Shortlisted,Score,Education,Experience,Professional,Documents,Reason,Tie-break,Must-haves");
            foreach (var r in run.Results)
                sb.AppendLine(string.Join(",", new object[]
                {
                    r.Rank, r.ApplicationNo, r.ApplicantName, r.ApplicantId, r.Longlisted ? "Yes" : "No", r.Shortlisted ? "Yes" : "No", r.Score.ToString("0.0", CultureInfo.InvariantCulture),
                    r.Breakdown.Education, r.Breakdown.Experience, r.Breakdown.Professional, r.Breakdown.DocumentMatch, r.Reason, r.TieNote,
                    string.Join("; ", r.Checks.Select(c => (c.Met ? "Met: " : "Not met: ") + c.Criterion + " - " + c.Detail)),
                }.Select(Cell)));
            var bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"Screening {id} {run.RunAt:yyyy-MM-dd HHmm}.csv");
        }

        // an applicant's uploaded document
        public ActionResult Document(int id)
        {
            var path = store.DocumentPath(id);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return HttpNotFound("The document was not found.");
            return File(path, MimeMapping.GetMimeMapping(path), Path.GetFileName(path));
        }
    }
}
