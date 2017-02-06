using Nop.Core.Domain;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Weixin.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Services.Localization;
using System.Web.Routing;
using Nop.Web.Framework;
using Nop.Core;
using System.Globalization;
using System.Net;
using System.Web;
using Nop.Services.Logging;
using Nop.Plugin.Payments.Weixin.Models;
using Nop.Plugin.Payments.Weixin.Services;

namespace Nop.Plugin.Payments.Weixin
{
    public class WeixinPaymentProcessor : BasePlugin, IPaymentMethod
    {
        private readonly HttpContextBase _httpContext;
        private readonly WeixinPaymentSetting _WeixinPaymentSetting;
        private readonly StoreInformationSettings _storeInformationSetting;
        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IStoreContext _storeContext;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly IWeixinPaymentService _weixinPaymentService;

        public WeixinPaymentProcessor(
            ILogger logger,
            HttpContextBase httpContext,
            WeixinPaymentSetting WeixinPaymentSetting,
            StoreInformationSettings storeInformationSettings, IWebHelper webHelper,
            ISettingService settingService,
            IOrderService orderService,
            IStoreContext storeContext,
            IWeixinPaymentService weixinPaymentService,
            IOrderTotalCalculationService orderTotalCalculationService)
        {
            this._logger = logger;
            this._httpContext = httpContext;
            this._WeixinPaymentSetting = WeixinPaymentSetting;
            this._storeInformationSetting = storeInformationSettings;
            this._webHelper = webHelper;
            this._settingService = settingService;
            this._orderService = orderService;
            this._storeContext = storeContext;
            this._weixinPaymentService = weixinPaymentService;
            this._orderTotalCalculationService = orderTotalCalculationService;
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
               _WeixinPaymentSetting.AdditionalFee, false);
            return result;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            if (order.PaymentStatus != PaymentStatus.Pending)
                return false;

            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 1)
                return false;

            return true;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentWeixin";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Weixin.Controllers" }, { "area", null } };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentWeixin";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Weixin.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentWeixinController);
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            if (this._httpContext.Request.UserAgent.ToLowerInvariant().Contains("micromessenger"))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            if (this._weixinPaymentService.OrderQuery(postProcessPaymentRequest.Order))
            {
                _weixinPaymentService.ProcessOrderPaid(postProcessPaymentRequest.Order);
                _httpContext.Response.Redirect($"~/OrderDetails/{postProcessPaymentRequest.Order.Id}");
            }
            else
            {
                var isInWeixinBrowser = this._httpContext.Request.IsInWeixinBrowser();
                if (isInWeixinBrowser)
                {
                    this.PostPRocessPaymentInWeixinBrowser(postProcessPaymentRequest);
                }
                else
                {
                    var clientIp = _webHelper.GetCurrentIpAddress() ?? "";
                    PostWeixinQrcodeOrder(postProcessPaymentRequest.Order, clientIp);
                }
            }
        }

        /// <summary>
        /// 在微信浏览器内支付则先请求微信认证，获取 code, openid 之类
        /// </summary>
        /// <param name="postProcessPaymentRequest"></param>
        private void PostPRocessPaymentInWeixinBrowser(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var appid = this._WeixinPaymentSetting.AppId;
            var clientIp = _webHelper.GetCurrentIpAddress() ?? "";
            var orderId = postProcessPaymentRequest.Order.Id;
            var authNotifyUrl =
                _webHelper.GetStoreLocation(false) +
                $"Plugins/PaymentWeixin/AuthNotify?orderId={orderId}&ip={clientIp}";
            var redirectUri = WebUtility.UrlEncode(authNotifyUrl);
            var url = $"https://open.weixin.qq.com/connect/oauth2/authorize?" +
                $"appid={appid}&redirect_uri={redirectUri}&response_type=code&scope=snsapi_base&state=STATE#wechat_redirect";
            _httpContext.Response.Redirect(url);
        }

        /// <summary>
        /// 在微信浏览器外支付则直接调用统一下单接口下单，然后生成 URL 的二维码，用户用微信扫描后付款
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        private void PostWeixinQrcodeOrder(Order order, string ip)
        {
            var wxdata = new WxPayData(_WeixinPaymentSetting.MchKey);
            var notifyUrl = _webHelper.GetStoreLocation(false) + $"Plugins/PaymentWeixin/PrepayNotify";
            var values = new Dictionary<string, string>();
            values["notify_url"] = notifyUrl;
            values["spbill_create_ip"] = ip;
            values["trade_type"] = "NATIVE";
            values["product_id"] = order.Id.ToString();

            var returnWeixinData = _weixinPaymentService.UnifiedOrder(order, values);
            var resultCode = returnWeixinData.GetValue("result_code");
            var codeUrl = WebUtility.UrlEncode(returnWeixinData.GetValue("code_url"));
            var redirectUrl = _webHelper.GetStoreLocation(false) +
                    $"Plugins/PaymentWeixin/NativePayment?orderId={order.Id}&codeUrl={codeUrl}";
            _httpContext.Response.Redirect(redirectUrl);
        }

        public override void Install()
        {
            var settings = new WeixinPaymentSetting()
            {
                AppId = "",
                AppSecret = "",
                MchId = "",
                MchKey = "",
                AdditionalFee = 0
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.RedirectionTip", "将本次将使用微信支付!");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AppId", "AppID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AppSecret", "AppSecret");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.MchKey", "商户密钥");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.MchId", "商户号");
            //this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.Vmid.Hint", "请输入商户号");
            //this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.Key.Hint", "请输入商户Key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AdditionalFee", "额外费用");
            //this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AdditionalFee.Hint", "请输入额外费用，没有则输0");
            // 验证
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AppIdRequired", "请输入 AppID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AppSecretRequired", "请输入 AppSecret");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.MchIdRequired", "请输入商户号");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.MchKeyRequired", "请输入商户密钥");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Weixin.AdditionalFeeRequired", "请输入额外费用，没有则输0");

            base.Install();
        }

        public override void Uninstall()
        {
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.RedirectionTip");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AppId");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AppId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AppSecret");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AppSecret.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.MchId");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.MchId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.MchKey");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.MchKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AdditionalFee.Hint");
            // 验证
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AppIdRequired");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AppSecretRequired");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.MchIdRequired");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.MchKeyRequired");
            this.DeletePluginLocaleResource("Plugins.Payments.Weixin.AdditionalFeeRequired");

            base.Uninstall();
        }

        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        public bool SkipPaymentInfo
        {
            get { return false; }
        }



    }
}
