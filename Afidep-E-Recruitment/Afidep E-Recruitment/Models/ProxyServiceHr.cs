namespace Afidep_E_Recruitment.ProxyService
{
    // HR screening's calls to the ServiceProxy, kept beside the generated web reference (Web References\ProxyService\Reference.cs)
    // so updating the reference does not remove them
    public partial class ServiceProxy
    {
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://afidep.org/RetrieveRequisitions", RequestNamespace = "http://afidep.org/", ResponseNamespace = "http://afidep.org/", Use = System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle = System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        public string RetrieveRequisitions(string arguments)
        {
            object[] results = this.Invoke("RetrieveRequisitions", new object[] { arguments });
            return (string)results[0];
        }

        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://afidep.org/RetrieveRequisitionApplications", RequestNamespace = "http://afidep.org/", ResponseNamespace = "http://afidep.org/", Use = System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle = System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        public string RetrieveRequisitionApplications(string arguments)
        {
            object[] results = this.Invoke("RetrieveRequisitionApplications", new object[] { arguments });
            return (string)results[0];
        }
    }
}
