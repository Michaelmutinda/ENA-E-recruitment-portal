using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security; // Required for FormsAuthentication.FormsCookieName

namespace Afidep_E_Recruitment
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_EndRequest(object sender, EventArgs e)
        {

            var httpContext = HttpContext.Current;

            for (int i = 0; i < httpContext.Response.Cookies.Count; i++)
            {
                HttpCookie cookie = httpContext.Response.Cookies[i];
                if (cookie.Name == "ASP.NET_SessionId" || cookie.Name == FormsAuthentication.FormsCookieName)
                {
                    cookie.SameSite = SameSiteMode.Lax;
                    if (HttpContext.Current.Request.IsSecureConnection)
                    {
                        cookie.Secure = true;
                    }
                }
            }
        }
    }
}