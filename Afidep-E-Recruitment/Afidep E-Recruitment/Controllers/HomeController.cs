using Afidep_E_Recruitment.Models;
using Newtonsoft;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static Afidep_E_Recruitment.Controllers.JobController;
using static Afidep_E_Recruitment.Models.HelperClasses;

namespace Afidep_E_Recruitment.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            PersonalInformation userdetails = new PersonalInformation();
            UserService usersrv = new UserService();
            List<AvailableJobs> jobApplications;
            RequestFilters filters = new RequestFilters();
            using (var client = new ProxyClient())
            {
                jobApplications = JsonConvert.DeserializeObject<List<AvailableJobs>>(client.GetAvailableJobs(JsonConvert.SerializeObject(filters)));
            }

            userdetails = usersrv.GetUserDetails(Session["userid"].ToString(), "email");

            //if (userdetails.firstname == null)
            //{
            //    userdetails = usersrv.GetUserDetails(Session["userid"].ToString(), "idnumber");
            //}
            Session["userid"] = userdetails.email;
            Session["MemberName"] = userdetails.firstname;
            return View(jobApplications);
        }
        public ActionResult MyApplications()
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            List<JobApplication> jobApplications;
            RequestFilters filters = new RequestFilters
            {
                userid = Session["userid"].ToString(),
            };
            using (var client = new ProxyClient())
            {
                jobApplications = JsonConvert.DeserializeObject<List<JobApplication>>(client.RetrieveJobApplications(JsonConvert.SerializeObject(filters)));
            }
            return View(jobApplications);
        }
        // Public - no login required
        public ActionResult PublicJobDetails(string requestid)
        {
            AvailableJobs job;
            RequestFilters filters = new RequestFilters
            {
                requestid = requestid,
            };

            using (var client = new ProxyClient())
            {
                job = JsonConvert.DeserializeObject<AvailableJobs>(
                    client.GetAvailableJobDetails(JsonConvert.SerializeObject(filters))
                );
            }

            return View(job);
        }
        public ActionResult About()
        {
            return RedirectToAction("Index");
        }

        public ActionResult Contact()
        {
            return RedirectToAction("Index");
        }

        public class JobsApplied
        {
            public string applicationnumber { get; set; }
            public string status { get; set; }
            public string stage { get; set; }
            public string dateapplied { get; set; }
            public string membername { get; set; }
            public string idnumber { get; set; }
        }

        public class AvailableJobs
        {
            public string requestid { get; set; }
            public string jobid { get; set; }
            public string jobdescription { get; set; }
            public DateTime postingdate { get; set; }
            public DateTime closingdate { get; set; }
            public string contracttype { get; set; }
            public string mainobjective { get; set; }
            public List<JobResponsibility> jobresponsibilities { get; set; }
            public List<AcademicQualification> acadquals { get; set; }
            public List<JobExperience> experience { get; set; }
            public List<ProfessionalQualification> profquals { get; set; }
            public List<ProfessionalMembership> profmembs { get; set; }
        }

        public class JobResponsibility
        {
            public string responsibility { get; set; }
        }
        public class AcademicQualification
        {
            public string type { get; set; }
            public string description { get; set; }
        }
        public class JobExperience
        {
            public string hlevel { get; set; }
            public string iname { get; set; }
            public decimal minyears { get; set; }
        }

        public class ProfessionalQualification
        {
            public int lineno { get; set; }
            public string description { get; set; }
            public string level { get; set; }
        }

        public class ProfessionalMembership
        {
            public int lineno { get; set; }
            public string description { get; set; }
            public string profbody { get; set; }
            public string memberno { get; set; }
        }
    }
}
