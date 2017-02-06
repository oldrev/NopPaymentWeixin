using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Nop.Plugin.Payments.Weixin
{
    public static class HttpContextExtensions
    {
        public static bool IsInWeixinBrowser(this HttpRequestBase request)
        {
            if(request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            return request.UserAgent.ToLowerInvariant().Contains("micromessenger");
        }
    }
}
