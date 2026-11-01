using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web.Services.Protocols;
using System.Web.Services;
using System.Web;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using ServiceProxy.Reference;

namespace ServiceProxy
{
    [WebService(Namespace = "http://afidep.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [System.Web.Script.Services.ScriptService]
    public class ServiceProxy : System.Web.Services.WebService
    {
        private PortalAPI _client;

        public ServiceProxy()
        {
            InitializeClient();
        }

        private void InitializeClient()
        {
            _client = new PortalAPI();

            try
            {
                NetworkCredential credential = new NetworkCredential(
                    ConfigurationManager.AppSettings["NAVUsername"],
                    ConfigurationManager.AppSettings["NAVPassword"],
                    ConfigurationManager.AppSettings["NAVDomain"]);

                _client.Credentials = credential;
                _client.PreAuthenticate = true;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to initialize NAV client: " + ex.Message, ex);
            }
        }

        public string response;
        public string statementvariable = string.Empty;
        private readonly Dictionary<string, List<string>> clientAccess = new Dictionary<string, List<string>>()
        {
            { "MSafts!AyfShHms", new List<string> { "PORTAL" } },
        };

        private string GetClientId()
        {
            string authorizationHeader = HttpContext.Current.Request.Headers["Authorization"];

            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Basic "))
            {
                throw new SoapException("Missing or invalid Authorization header", SoapException.ClientFaultCode);
            }

            string encodedCredentials = authorizationHeader.Substring(6);
            string decodedCredentials = "";
            try
            {
                decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            }
            catch (FormatException)
            {
                throw new SoapException("Invalid Base64 encoding in Authorization header", SoapException.ClientFaultCode);
            }

            string[] credentials = decodedCredentials.Split(':');
            if (credentials.Length != 2)
            {
                throw new SoapException("Invalid authorization format", SoapException.ClientFaultCode);
            }

            string clientId = credentials[0];
            string clientSecret = credentials[1];

            string storedCredential = ConfigurationManager.AppSettings[clientId];

            if (string.IsNullOrEmpty(storedCredential) || storedCredential != Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")))
            {
                throw new SoapException("Invalid client credentials", SoapException.ClientFaultCode);
            }

            return clientId;
        }

        private void ValidateSectionAccess(IEnumerable<string> sections)
        {
            string clientId = GetClientId();

            if (!clientAccess.ContainsKey(clientId) || !sections.Any(section => clientAccess[clientId].Contains(section)))
            {
                throw new SoapException("Unauthorized access", SoapException.ClientFaultCode);
            }
        }

        [WebMethod]
        public string RetrieveJobApplications(string arguments)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.RetrieveUserJobApplications(arguments).Replace("\\", "\\\\");
        }

        [WebMethod]
        public string GetCountries()
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.GetCountries();
        }

        [WebMethod]
        public string RetrieveJobApplication(string arguments)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.RetrieveJobApplication(arguments);
        }

        [WebMethod]
        public string RetrieveOpenJobs(string arguments)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.RetrieveAvailableJobs(arguments);
        }

        [WebMethod]
        public string GetAvailableJobDetails(string arguments)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.RetrieveAvailableJobDetails(arguments);
        }



        [WebMethod]
        public string AddJobApplication(string arguments)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.AddJobApplication(arguments);
        }

        [WebMethod]
        public string ModifyJobApplication(string arguments)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.ModifyJobApplication(arguments);
        }

        [WebMethod]
        public string AddJobAttachment(string applicationnumber, string documentpath, string documenttype)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.AttachJobAppicationDocument(applicationnumber, documentpath, documenttype);
        }

        [WebMethod]
        public string RetrieveJobSummary(string arguments)
        {
            string statementvariable = "";
            ValidateSectionAccess(new[] { "PORTAL" });
            //_client.GenerateJobSummary(arguments, ref(statementvariable));
            return statementvariable;
        }
        [WebMethod]
        public string GetRequisitionJDAttachmentPath(string requisitionNo)
        {
            ValidateSectionAccess(new[] { "PORTAL" });
            return _client.GetRequisitionJDAttachmentPath(requisitionNo);
        }

        [WebMethod]
        public void InsertSharePointAttachment(
         string applicationNo,
         string fileName,
         string fileUrl)
        {
            ValidateSectionAccess(new[] { "PORTAL" });

            _client.InsertSharePointAttachment(
                applicationNo,
                fileName,
                fileUrl
            );
        }

    }


} 
