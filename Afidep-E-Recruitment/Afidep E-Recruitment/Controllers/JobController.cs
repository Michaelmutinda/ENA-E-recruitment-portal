using Afidep_E_Recruitment.Models;
using Antlr.Runtime.Misc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.EnterpriseServices.Internal;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages;
using System.Xml.Linq;
using static Afidep_E_Recruitment.Controllers.HomeController;
using static Afidep_E_Recruitment.Controllers.JobController;
using static Afidep_E_Recruitment.Models.HelperClasses;

namespace Afidep_E_Recruitment.Controllers
{
    public class JobController : Controller
    {
        OptionClasses commonOptions = new OptionClasses();
        // GET: Job
        public ActionResult Index(string requestid)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            AvailableJobs jobApplications;
            RequestFilters filters = new RequestFilters
            {
                requestid = requestid,
            };
            using (var client = new ProxyClient())
            {
                jobApplications = JsonConvert.DeserializeObject<AvailableJobs>(client.GetAvailableJobDetails(JsonConvert.SerializeObject(filters)));
            }
            return View(jobApplications);
        }

        public ActionResult ApplyJob(string jobrequestid, string applicationnumber, string firstland)
        {
            if (Session["loggedin"] == null)
                return RedirectToAction("Logout", "Login");

            if (firstland != null)
                TempData["FirstLand"] = "true";

            JobApplication jobApplications = null;
            PersonalInformation userdetails = new PersonalInformation();
            UserService usersrv = new UserService();

            string currentUserId = Session["userid"].ToString();

            RequestFilters filters = new RequestFilters
            {
                applicationnumber = applicationnumber,
                requestid = jobrequestid,
                userid = currentUserId,
            };

            using (var client = new ProxyClient())
            {
                // Try retrieving existing application
                jobApplications = JsonConvert.DeserializeObject<JobApplication>(
                    client.RetrieveJobApplication(
                        JsonConvert.SerializeObject(filters)
                    )
                );

                // Citizenship dropdown
                ViewBag.CitizenshipOptions = new SelectList(
                    JsonConvert.DeserializeObject<List<Country>>(
                        client.GetCountries()
                    ),
                    "code",
                    "name"
                );

                // SECURITY:
                // Ignore application if it belongs to another user
                if (jobApplications != null &&
                    jobApplications.personalinfo != null &&
                    !string.IsNullOrEmpty(jobApplications.personalinfo.idnumber) &&
                    jobApplications.personalinfo.idnumber != currentUserId)
                {
                    jobApplications = null;
                }

                // Create fresh application if none exists
                if (jobApplications == null ||
                    string.IsNullOrEmpty(jobApplications.applicationnumber))
                {
                    // Force clean creation
                    filters.applicationnumber = "";
                    filters.requestid = "";
                    filters.userid = currentUserId;

                    jobApplications = JsonConvert.DeserializeObject<JobApplication>(
                        client.AddJobApplication(
                            JsonConvert.SerializeObject(filters)
                        )
                    );
                }

                // Prevent null collections
                if (jobApplications.academicqualifications == null)
                    jobApplications.academicqualifications =
                        new List<AcademicQualification>();

                if (jobApplications.experiences == null)
                    jobApplications.experiences =
                        new List<WorkExperience>();

                if (jobApplications.profqualifications == null)
                    jobApplications.profqualifications =
                        new List<ProfessionalQualification>();

                if (jobApplications.profmembership == null)
                    jobApplications.profmembership =
                        new List<ProfessionalMembership>();

                if (jobApplications.referees == null)
                    jobApplications.referees =
                        new List<JobReferee>();

                if (jobApplications.attachments == null)
                    jobApplications.attachments =
                        new List<Attachment>();

                // Load ONLY this user's uploaded attachments
                jobApplications.attachments =
                    usersrv.GetJobAttachments(
                        jobApplications.applicationnumber
                    );

                // Load logged-in user details
                userdetails = usersrv.GetUserDetails(
                    currentUserId,
                    "email"
                );

                // Overwrite personal info with logged-in user's info
                jobApplications.personalinfo = userdetails;
            }

            // Dropdowns
            ViewBag.GenderList = new SelectList(
                commonOptions.GenderOptions,
                "Value",
                "Text"
            );

            ViewBag.DisbalingOptions = new SelectList(
                commonOptions.YesorNo,
                "Value",
                "Text"
            );

            ViewBag.EducationTypes = new SelectList(
                commonOptions.EducationTypes,
                "Value",
                "Text"
            );

            ViewBag.EducationLevels = new SelectList(
                commonOptions.EducationLevels,
                "Value",
                "Text"
            );

            return View(jobApplications);
        }


        public ActionResult PreviewJobApplication(string jobrequestid, string applicationnumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            JobApplication jobApplications;
            PersonalInformation userdetails = new PersonalInformation();
            UserService usersrv = new UserService();
            RequestFilters filters = new RequestFilters
            {
                applicationnumber = applicationnumber,
                requestid = jobrequestid,
                userid = Session["userid"].ToString(),
            };
            using (var client = new ProxyClient())
            {
                jobApplications = JsonConvert.DeserializeObject<JobApplication>(client.RetrieveJobApplication(JsonConvert.SerializeObject(filters)));

                ViewBag.CitizenshipOptions = new SelectList(JsonConvert.DeserializeObject<List<Country>>(client.GetCountries()), "code", "name");

                if (jobApplications.applicationnumber is null || jobApplications.applicationnumber.IsEmpty())
                {
                    jobApplications = JsonConvert.DeserializeObject<JobApplication>(client.AddJobApplication(JsonConvert.SerializeObject(filters)));
                }

                userdetails = usersrv.GetUserDetails(Session["userid"].ToString(), "email");

                jobApplications.attachments = usersrv.GetJobAttachments(applicationnumber);

                jobApplications.personalinfo = userdetails;
            }

            ViewBag.GenderList = new SelectList(commonOptions.GenderOptions, "Value", "Text");

            ViewBag.DisbalingOptions = new SelectList(commonOptions.YesorNo, "Value", "Text");

            ViewBag.EducationTypes = new SelectList(commonOptions.EducationTypes, "Value", "Text");

            ViewBag.EducationLevels = new SelectList(commonOptions.EducationLevels, "Value", "Text");

            return View(jobApplications);
        }
        [HttpGet]
        public JsonResult GetReferees(string applicationNumber)
        {
            try
            {
                var filters = new RequestFilters { applicationnumber = applicationNumber };
                JobApplication app;
                using (var client = new ProxyClient())
                {
                    app = JsonConvert.DeserializeObject<JobApplication>(
                        client.RetrieveJobApplication(JsonConvert.SerializeObject(filters))
                    );
                }
                return Json(app?.referees ?? new List<JobReferee>(), JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new List<JobReferee>(), JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitJobApplication(JobApplication model)
        {
            if (Session["loggedin"] == null)
                return RedirectToAction("Logout", "Login");

            try
            {
                JobApplicationUpdateInput requestjson = new JobApplicationUpdateInput
                {
                    applicationnumber = model.applicationnumber,
                    modify_type = "submission"
                };

                using (var client = new ProxyClient())
                {
                    client.ModifyJobApplication(
                        JsonConvert.SerializeObject(requestjson)
                    );
                }

                TempData["SuccessMessage"] =
                    "Thank you for submitting your application successfully.";

                return RedirectToAction("MyApplications");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"SubmitJobApplication Error: {ex.Message}"
                );

                TempData["ErrorMessage"] =
                    "An error occurred while submitting the application.";

                return RedirectToAction(
                    "ApplyJob",
                    new { applicationnumber = model.applicationnumber }
                );
            }
        }
        [HttpGet]
        public ActionResult GetApplicationPdfBase64(string applicationNumber)
        {
            if (string.IsNullOrEmpty(applicationNumber))
                return new HttpStatusCodeResult(400, "Application number required.");

            string pdfBase64String = "";

            try
            {
                var filters = new RequestFilters { applicationnumber = applicationNumber };

                using (var client = new ProxyClient())
                {
                    // Attempt 1: plain string — matches how GetRequisitionJDAttachmentPath works
                    pdfBase64String = client.RetrieveJobSummary(applicationNumber);
                    System.Diagnostics.Debug.WriteLine($"[PDF] Attempt1 (plain): {pdfBase64String?.Length ?? 0} chars");

                    // Attempt 2: JSON wrapped
                    if (string.IsNullOrEmpty(pdfBase64String))
                    {
                        pdfBase64String = client.RetrieveJobSummary(
                            Newtonsoft.Json.JsonConvert.SerializeObject(filters)
                        );
                        System.Diagnostics.Debug.WriteLine($"[PDF] Attempt2 (json): {pdfBase64String?.Length ?? 0} chars");
                    }

                    // Attempt 3: lowercase key (BC is case-sensitive sometimes)
                    if (string.IsNullOrEmpty(pdfBase64String))
                    {
                        pdfBase64String = client.RetrieveJobSummary(
                            $"{{\"ApplicationNumber\":\"{applicationNumber}\"}}"
                        );
                        System.Diagnostics.Debug.WriteLine($"[PDF] Attempt3 (PascalCase): {pdfBase64String?.Length ?? 0} chars");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PDF] Exception: {ex.Message}");
                return new HttpStatusCodeResult(500, "Error: " + ex.Message);
            }

            if (string.IsNullOrEmpty(pdfBase64String))
                return new HttpStatusCodeResult(204, "PDF not available.");

            // Unwrap if BC returned a JSON-quoted string e.g. "\"abc123...\""
            pdfBase64String = pdfBase64String.Trim();
            if (pdfBase64String.StartsWith("\"") && pdfBase64String.EndsWith("\""))
                pdfBase64String = pdfBase64String.Substring(1, pdfBase64String.Length - 2);

            return Content(pdfBase64String, "text/plain");
        }
        [HttpGet]
        public ActionResult DebugPdfResponse(string applicationNumber)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b>App number:</b> [{applicationNumber}]<br/><br/>");

            // Check application exists at all
            try
            {
                var filters = new RequestFilters { applicationnumber = applicationNumber };
                string appJson = "";
                using (var client = new ProxyClient())
                    appJson = client.RetrieveJobApplication(
                        Newtonsoft.Json.JsonConvert.SerializeObject(filters)
                    );

                sb.Append($"<b>Application found:</b> {(string.IsNullOrEmpty(appJson) || appJson == "{}" ? "NO — bad app number?" : "YES")}<br/>");
                sb.Append($"<b>App data (first 200):</b> {appJson?.Substring(0, Math.Min(200, appJson?.Length ?? 0))}<br/><br/>");
            }
            catch (Exception ex) { sb.Append($"<b>RetrieveJobApplication error:</b> {ex.Message}<br/>"); }

            // Test all argument formats for RetrieveJobSummary
            var attempts = new Dictionary<string, string>
            {
                ["plain string"] = applicationNumber,
                ["json lowercase"] = $"{{\"applicationnumber\":\"{applicationNumber}\"}}",
                ["json PascalCase"] = $"{{\"ApplicationNumber\":\"{applicationNumber}\"}}",
                ["json No field"] = $"{{\"No\":\"{applicationNumber}\"}}",
            };

            foreach (var attempt in attempts)
            {
                try
                {
                    string result = "";
                    using (var client = new ProxyClient())
                        result = client.RetrieveJobSummary(attempt.Value);

                    sb.Append($"<b>{attempt.Key}:</b> ");
                    if (string.IsNullOrEmpty(result))
                        sb.Append("<span style='color:red'>EMPTY</span>");
                    else
                        sb.Append($"<span style='color:green'>{result.Length} chars — starts: [{result.Substring(0, Math.Min(100, result.Length))}]</span>");
                    sb.Append("<br/>");
                }
                catch (Exception ex)
                {
                    sb.Append($"<b>{attempt.Key}:</b> <span style='color:orange'>EXCEPTION: {ex.Message}</span><br/>");
                }
            }

            // Check application status — BC may require "Submitted" status before generating PDF
            sb.Append("<br/><b>Note:</b> BC may only generate a summary PDF for applications with status = 'Submitted'.<br/>");

            return Content(sb.ToString(), "text/html");
        }
        [HttpPost]
        public JsonResult EditJobApp(JobApplicationUpdateInput updateinput)
        {
            JobApplication jobApplications;
            JsonResultResponse response = new JsonResultResponse();
            UserService usersrv = new UserService();

            try
            {
                using (var client = new ProxyClient())
                {
                    jobApplications = JsonConvert.DeserializeObject<JobApplication>(
                        client.ModifyJobApplication(JsonConvert.SerializeObject(updateinput))
                    );
                }

                if (updateinput.modify_type == "user_details" &&
                    updateinput.application_details?.personalinfo != null)
                {
                    updateinput.application_details.personalinfo.idnumber = Session["userid"].ToString();
                    usersrv.UpdateUserDetails(updateinput.application_details.personalinfo);
                }

                if (jobApplications != null && !string.IsNullOrEmpty(jobApplications.applicationnumber))
                {
                    response.status = true;
                    response.requestno = jobApplications.applicationnumber;
                    response.message = "success";
                }
                else
                {
                    response.status = false;
                    response.message = "error occurred";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditJobApp error: {ex.Message}");
                response.status = false;
                response.message = "error occurred";
            }

            return new JsonResult { Data = response };
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddDocumentAttachment(DocumentAttachmentViewModel model)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }

            string applicationNumber = model.ApplicationNumber;
            string description = model.DocumentDescription;
            HttpPostedFileBase uploadedFile = model.DocumentFile;
            UserService usersrv = new UserService();

            if (uploadedFile != null && uploadedFile.ContentLength > 0)
            {
                try
                {
                    // 📂 Disk upload path
                    string uploadPath = Path.Combine(
                        ConfigurationManager.AppSettings["RecruitmentDocuments"],
                        applicationNumber
                    );

                    if (!System.IO.Directory.Exists(uploadPath))
                    {
                        System.IO.Directory.CreateDirectory(uploadPath);
                    }

                    // File naming
                    string fileExtension = Path.GetExtension(uploadedFile.FileName);
                    string uniqueFileName = $"{model.DocumentType}{fileExtension}";
                    string filePath = Path.Combine(uploadPath, uniqueFileName);

                    model.DocumentPath = filePath;

                    // Save file to disk
                    uploadedFile.SaveAs(filePath);

                    model.ApplicantID = Session["userid"].ToString();

                    //  Save metadata in portal DB
                    bool saveresult = usersrv.SendDocumentToDatabase(model);

                    if (saveresult)
                    {
                        // Generate PUBLIC URL (SharePoint polling uses this)
                        string baseUrl =
                            "https://erecruitment.enafricafoundation.org/Uploads/";

                        string fileUrl =
                            baseUrl + applicationNumber + "/" + uniqueFileName;

                        // Send to Business Central (SharePoint Integration table)
                        using (var client = new ProxyClient())
                        {
                            client.InsertSharePointAttachment(
                                applicationNumber,
                                uniqueFileName,
                                fileUrl
                            );
                        }

                        return Json(new
                        {
                            success = true,
                            message = "Document uploaded successfully!"
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"File upload error: {ex.Message}"
                    );

                    ModelState.AddModelError(
                        "",
                        $"An error occurred during file upload: {ex.Message}"
                    );
                }
            }
            else
            {
                ModelState.AddModelError(
                    "DocumentFile",
                    "Please select a file to upload."
                );
            }

            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new
            {
                success = false,
                message = string.Join("<br/>", errors)
            });
        }

        [HttpGet]
        public ActionResult ViewDocument(int filenumber)
        {
            if (Session["loggedin"] == null)
                return RedirectToAction("Logout", "Login");

            UserService usersrv = new UserService();

            // ✅ Check the file belongs to the logged-in user
            string currentUserId = Session["userid"].ToString();
            if (!usersrv.FilesBelongsToUser(filenumber, currentUserId))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Access denied.");

            string filepath = usersrv.GetFilePathFromDatabase(filenumber);

            if (!System.IO.File.Exists(filepath))
                return HttpNotFound("The document was not found.");

            string contentType = MimeMapping.GetMimeMapping(filepath);
            return File(filepath, contentType);
        }



        [HttpGet]
        public ActionResult AddAcademicQualification(string applicationNumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            ViewBag.EducationTypes = new SelectList(commonOptions.EducationTypes, "Value", "Text");

            ViewBag.EducationLevels = new SelectList(commonOptions.EducationLevels, "Value", "Text");

            using (var client = new ProxyClient())
            {
                ViewBag.Countries = new SelectList(JsonConvert.DeserializeObject<List<Country>>(client.GetCountries()), "code", "name");
            }

            ViewBag.ApplicationNumber = applicationNumber;

            return PartialView(new AcademicQualification());
        }

        [HttpGet]
        public ActionResult AddWorkExperience(string applicationNumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            ViewBag.EducationTypes = new SelectList(commonOptions.EducationTypes, "Value", "Text");
            ViewBag.HierarchyLevels = new SelectList(commonOptions.HierarchyLevels, "Value", "Text");
            ViewBag.PresentEmployer = new SelectList(commonOptions.YesorNo, "Value", "Text");
            using (var client = new ProxyClient())
            {
                ViewBag.Countries = new SelectList(JsonConvert.DeserializeObject<List<Country>>(client.GetCountries()), "code", "name");
            }

            ViewBag.ApplicationNumber = applicationNumber;

            return PartialView(new WorkExperience());
        }

        [HttpGet]
        public ActionResult AddProfessionalCourse(string applicationNumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            ViewBag.ApplicationNumber = applicationNumber;

            return PartialView(new ProfessionalQualification());
        }

        [HttpGet]
        public ActionResult AddProfessionalMembership(string applicationNumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            ViewBag.ApplicationNumber = applicationNumber;

            return PartialView(new ProfessionalMembership());
        }

        [HttpGet]
        public ActionResult AddReferee(string applicationNumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            ViewBag.ApplicationNumber = applicationNumber;

            return PartialView(new JobReferee());
        }

        [HttpGet]
        public ActionResult AddAttachment(string applicationNumber)
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            ViewBag.ApplicationNumber = applicationNumber;

            ViewBag.DocumentTypes = new SelectList(commonOptions.DocumentTypes, "Value", "Text");

            return PartialView(new Attachment());
        }

        [HttpGet]
        public ActionResult DownloadJD(string requestid)
        {
            try
            {
                string url;

                // Call service layer
                UserService userService = new UserService();
                url = userService.GetRequisitionJDAttachmentPath(requestid);

                if (string.IsNullOrEmpty(url))
                {
                    return Content("JD not found for request: " + requestid);
                }

                // Fix all backslash variants to forward slash
                url = url.Replace("\\\\", "/")
                         .Replace("\\", "/")
                         .Trim();

                // The URL may come in as:
                // http://167.86.124.32:8081/Downloads/02.03.2026 JD - Legal Assistant (1) (1).doc
                // We need to encode ONLY the filename/path segments, not the scheme or host

                // Split into base (scheme+host+port) and the path
                int firstSlashAfterHost = url.IndexOf('/', url.IndexOf("//") + 2);

                string baseUrl;   // e.g. http://167.86.124.32:8081
                string pathPart;  // e.g. /Downloads/02.03.2026 JD - Legal Assistant (1) (1).doc

                if (firstSlashAfterHost >= 0)
                {
                    baseUrl = url.Substring(0, firstSlashAfterHost);
                    pathPart = url.Substring(firstSlashAfterHost); // starts with /
                }
                else
                {
                    // No path — nothing to download
                    return Content("Invalid URL format: " + url);
                }

                // Encode each path segment individually (preserves slashes, encodes spaces/parens/dots)
                string encodedPath = string.Join("/",
                    pathPart.Split('/')
                            .Select(segment => Uri.EscapeDataString(segment))
                );

                // EscapeDataString will encode the leading empty segment from the leading slash,
                // so we ensure the path starts with /
                if (!encodedPath.StartsWith("/"))
                    encodedPath = "/" + encodedPath;

                string finalUrl = baseUrl + encodedPath;

                // Download the file
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    var response = httpClient.GetAsync(finalUrl).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        return Content($"Failed to fetch JD (HTTP {(int)response.StatusCode}): {finalUrl}");
                    }

                    var fileBytes = response.Content.ReadAsByteArrayAsync().Result;

                    // Get the original filename (decoded, with spaces) for the download prompt
                    string fileName = Uri.UnescapeDataString(
                        System.IO.Path.GetFileName(pathPart)
                    );

                    // Pick the right MIME type based on extension
                    string ext = System.IO.Path.GetExtension(fileName).ToLower();
                    string mimeType;
                    if (ext == ".doc")
                        mimeType = "application/msword";
                    else if (ext == ".docx")
                        mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    else if (ext == ".pdf")
                        mimeType = "application/pdf";
                    else
                        mimeType = "application/octet-stream";

                    return File(fileBytes, mimeType, fileName);
                }
            }
            catch (Exception ex)
            {
                return Content("ERROR: " + ex.Message);
            }
        }
        // Public - no login required
        public ActionResult PublicJobDetails(string requestid)
        {
            AvailableJobs jobDetails;
            RequestFilters filters = new RequestFilters
            {
                requestid = requestid,
            };

            using (var client = new ProxyClient())
            {
                jobDetails = JsonConvert.DeserializeObject<AvailableJobs>(
                    client.GetAvailableJobDetails(JsonConvert.SerializeObject(filters))
                );
            }

            return View(jobDetails);
        }
        public ActionResult ApplicantEducation()
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }
            return View();
        }

        public ActionResult MyApplications()
        {
            if (Session["loggedin"] == null)
            {
                return RedirectToAction("Logout", "Login");
            }

            var filters = new
            {
                userid = Session["userid"].ToString()
            };

            List<JobApplication> applications;

            using (var client = new ProxyClient())
            {
                applications = JsonConvert.DeserializeObject<List<JobApplication>>(
                    client.RetrieveJobApplications(
                        JsonConvert.SerializeObject(filters)
                    )
                );
            }

            return View(applications);
        }


        public class DocumentAttachmentViewModel
        {
            public string ApplicationNumber { get; set; }
            public string DocumentDescription { get; set; }
            public string DocumentType { get; set; }
            public string DocumentPath { get; set; }
            public string ApplicantID { get; set; }
            public HttpPostedFileBase DocumentFile { get; set; }
        }
        public class JobApplicationUpdateInput
        {
            public string applicationnumber { get; set; }
            public string modify_type { get; set; }
            public JobApplication application_details { get; set; }
        }

        public class JobApplication
        {
            public string applicationnumber { get; set; }
            public DateTime applicationdate { get; set; }
            public string status { get; set; }
            public string stage { get; set; }
            public DateTime dateapplied { get; set; }
            public string description { get; set; }
            public PersonalInformation personalinfo { get; set; }
            public List<AcademicQualification> academicqualifications { get; set; }
            public List<WorkExperience> experiences { get; set; }
            public List<ProfessionalQualification> profqualifications { get; set; }
            public List<ProfessionalMembership> profmembership { get; set; }
            public List<JobReferee> referees { get; set; }
            public List<Attachment> attachments { get; set; }
        }

    public class PersonalInformation
        {
            public string idnumber { get; set; }
            public string firstname { get; set; }
            public string middlename { get; set; }
            public string lastname { get; set; }
            public string gender { get; set; }
            public string specialneeds { get; set; }
            public string disabilitydetails { get; set; }
            public string citizenship { get; set; }
            public string applicantstatus { get; set; }
            public string email { get; set; }
            public string telephone { get; set; }

        }

        public class AcademicQualification
        {
            public string lineno { get; set; }
            public DateTime startdate { get; set; }
            public DateTime enddate { get; set; }
            public string educationtype { get; set; }
            public string educationlevel { get; set; }
            public string fieldofstudy { get; set; }
            public string coursename { get; set; }
            public string institutionname { get; set; }
            public string country { get; set; }
            public string grade { get; set; }
        }

        public class WorkExperience
        {
            public string lineno { get; set; }
            public DateTime startdate { get; set; }
            public DateTime enddate { get; set; }
            public string presentemployer { get; set; }
            public string employer { get; set; }
            public string industry { get; set; }
            public string hierarchylevel { get; set; }
            public string jobtitle { get; set; }
            public string country { get; set; }
            public string summaryresponsibilities { get; set; }
        }

        public class JobReferee
        {
            public int lineno { get; set; }
            public string names { get; set; }
            public string designation { get; set; }
            public string company { get; set; }
            public string address { get; set; }
            public string telephone { get; set; }
            public string email { get; set; }
        }

        public class Attachment
        {
            public int attachmentid { get; set; }
            public string type { get; set; }
            public string description { get; set; }
            public string documentpath { get; set; }
            public DateTime amend_date { get; set; }
        }
    }
}