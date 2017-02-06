using Nop.Web.Framework.Mvc.Routes;
using System.Web.Mvc;
using System.Web.Routing;

namespace Nop.Plugin.Payments.Weixin
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            // PaymentInfo
            routes.MapRoute("Plugin.Payments.Weixin.PaymentInfo",
                "Plugins/PaymentPay/Weixin/PaymentInfo",
                new { controller = "PaymentsWeixin", action = "PaymentInfo" },
                new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            // Configure
            routes.MapRoute("Plugin.Payments.Weixin.Configure",
                "Plugins/PaymentPay/Weixin/Configure",
                new { controller = "PaymentsWeixin", action = "Configure" },
                new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            //Notify
            routes.MapRoute("Plugin.Payments.Weixin.Notify",
                 "Plugins/PaymentWeixin/Notify",
                 new { controller = "PaymentWeixin", action = "Notify" },
                 new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            //NotifyAuth
            routes.MapRoute("Plugin.Payments.Weixin.AuthNotify",
                 "Plugins/PaymentWeixin/AuthNotify",
                 new { controller = "PaymentWeixin", action = "AuthNotify" },
                 new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            //NotifyAuth
            routes.MapRoute("Plugin.Payments.Weixin.PrepayNotify",
                 "Plugins/PaymentWeixin/PrepayNotify",
                 new { controller = "PaymentWeixin", action = "PrepayNotify" },
                 new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            //NativePayment
            routes.MapRoute("Plugin.Payments.Weixin.NativePayment",
                 "Plugins/PaymentWeixin/NativePayment",
                 new { controller = "PaymentWeixin", action = "NativePayment" },
                 new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            //NotifyAuth
            routes.MapRoute("Plugin.Payments.Weixin.CheckPaymentResult",
                 "Plugins/PaymentWeixin/CheckPaymentResult",
                 new { controller = "PaymentWeixin", action = "CheckPaymentResult" },
                 new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

            //Return
            routes.MapRoute("Plugin.Payments.Weixin.Return",
                 "Plugins/PaymentWeixin/Return",
                 new { controller = "PaymentWeixin", action = "Return" },
                 new[] { "Nop.Plugin.Payments.Weixin.Controllers" });

        }

        public int Priority
        {
            get { return 0; }
        }
    }
}
