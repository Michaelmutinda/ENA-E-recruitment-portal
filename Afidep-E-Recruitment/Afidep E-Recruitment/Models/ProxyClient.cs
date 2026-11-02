using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Web;
using Afidep_E_Recruitment.ProxyService;

namespace Afidep_E_Recruitment.Models
{

    public class ProxyClient : IDisposable
    {
        private ServiceProxy _client;

        public ProxyClient()
        {
            InitializeClient();
        }
        public class CustomProxyService : ServiceProxy
        {
            private readonly string _authorizationHeader;

            public CustomProxyService(string username, string password, string endpointUrl)
            {
                _authorizationHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                Url = endpointUrl;
            }

            protected override WebRequest GetWebRequest(Uri uri)
            {
                var request = (HttpWebRequest)base.GetWebRequest(uri);

                request.Headers.Add("Authorization", $"Basic {_authorizationHeader}");

                return request;
            }
        }


        private void InitializeClient()
        {
            string endpointUrl = ConfigurationManager.AppSettings["ProxyServiceEndpoint"];
            string username = ConfigurationManager.AppSettings["APPUsername"];
            string password = ConfigurationManager.AppSettings["APPPassword"];

            _client = new CustomProxyService(username, password, endpointUrl);
        }


        public string RetrieveJobApplications(string arguments)
        {
            try
            {
                return _client.RetrieveJobApplications(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }

        public string GetAvailableJobs(string arguments)
        {
            try
            {
                return _client.RetrieveOpenJobs(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }

        public string GetAvailableJobDetails(string arguments)
        {
            try
            {
                return _client.GetAvailableJobDetails(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }

        public string RetrieveJobApplication(string arguments)
        {
            try
            {
                return _client.RetrieveJobApplication(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }

        public string AddJobApplication(string arguments)
        {
            try
            {
                return _client.AddJobApplication(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }

        public string ModifyJobApplication(string arguments)
        {
            try
            {
                return _client.ModifyJobApplication(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }

        public string AddJobAttachment(
    string applicationnumber,
    string documentpath,
    string documenttype)
        {
            try
            {
                return _client.AddJobAttachment(
                    applicationnumber,
                    documentpath,
                    documenttype
                );
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Failed to send attachment to Business Central. " + ex.Message
                );
            }
        }
        public void InsertSharePointAttachment(
     string applicationNo,
     string fileName,
     string fileUrl)
        {
            try
            {
                _client.InsertSharePointAttachment(
                    applicationNo,
                    fileName,
                    fileUrl
                );
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Failed to send attachment to SharePoint Integration. "
                    + ex.Message
                );
            }
        }


        public string RetrieveJobSummary(string arguments)
        {
            try
            {
                return _client.RetrieveJobSummary(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }
        public string GetRequisitionJDAttachmentPath(string requisitionNo)
        {
            try
            {
                return _client.GetRequisitionJDAttachmentPath(requisitionNo);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to retrieve Job Description path. " + ex.Message);
            }
        }




        // HR screening: every job requisition (open and closed), and every application for one
        public string RetrieveRequisitions(string arguments)
        {
            try
            {
                return _client.RetrieveRequisitions(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Could not load the job requisitions. " + ex.Message);
            }
        }

        public string RetrieveRequisitionApplications(string arguments)
        {
            try
            {
                return _client.RetrieveRequisitionApplications(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception("Could not load the job's applications. " + ex.Message);
            }
        }

        public string GetCountries()
        {
            try
            {
                return _client.GetCountries();
            }
            catch (Exception ex)
            {
                throw new Exception("Something went wrong. Please try again later." + ex.Message);
            }
        }



        public void Dispose()
        {
            if (_client != null)
            {
                try
                {
                    _client.Abort();
                }
                catch (Exception ex)
                {
                    throw new Exception("Error disposing NAV client: " + ex.Message, ex);
                }
            }
        }
    }
}