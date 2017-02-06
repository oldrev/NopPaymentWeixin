using Nop.Core;
using Nop.Plugin.Payments.Weixin.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Nop.Plugin.Payments.Weixin.Services;

namespace Nop.Plugin.Payments.Weixin.Controllers
{
    public class PaymentWeixinController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly WeixinPaymentSetting _WeixinPaymentSetting;
        private readonly IWebHelper _webHelper;
        private readonly IStoreContext _storeContext;
        private readonly IWeixinPaymentService _weixinPaymentService;

        public PaymentWeixinController(ISettingService settingService, IPaymentService paymentService,
            IOrderService orderService, ILogger logger, ILocalizationService localizationService,
            IWebHelper webHelper, IOrderProcessingService orderProcessingService,
            WeixinPaymentSetting WeixinPaymentSetting, IStoreContext storeContext,
            IWeixinPaymentService weixinPaymentService)
        {
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._webHelper = webHelper;
            this._logger = logger;
            this._localizationService = localizationService;
            this._orderProcessingService = orderProcessingService;
            this._WeixinPaymentSetting = WeixinPaymentSetting;
            this._storeContext = storeContext;
            this._weixinPaymentService = weixinPaymentService;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel();
            model.AppId = _WeixinPaymentSetting.AppId;
            model.AppSecret = _WeixinPaymentSetting.AppSecret;
            model.MchId = _WeixinPaymentSetting.MchId;
            model.MchKey = _WeixinPaymentSetting.MchKey;
            model.AdditionalFee = _WeixinPaymentSetting.AdditionalFee;

            return View("~/Plugins/Payments.Weixin/Views/PaymentWeixin/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            // save settings
            _WeixinPaymentSetting.AppId = model.AppId;
            _WeixinPaymentSetting.AppSecret = model.AppSecret;
            _WeixinPaymentSetting.MchId = model.MchId;
            _WeixinPaymentSetting.MchKey = model.MchKey;
            _WeixinPaymentSetting.AdditionalFee = model.AdditionalFee;
            _settingService.SaveSetting(_WeixinPaymentSetting);

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();
            return View("~/Plugins/Payments.Weixin/Views/PaymentWeixin/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [ValidateInput(false)]
        public ActionResult Notify()
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Weixin") as WeixinPaymentProcessor;
            if (processor == null || !processor.PluginDescriptor.Installed)
                throw new NopException("Weixin module can not be loaded");

            string status_msg = String.Empty;
            //            string v_oid = Request["v_oid"]; // 订单编号
            return Content("");
        }

        [ValidateInput(false)]
        public ActionResult NativePayment(int orderId, string codeUrl)
        {
            var model = new PrepayInfoModel()
            {
                OrderId = orderId,
                WeixinNativePaymentUrl = codeUrl,
            };

            return View("Plugin.Payments.Weixin.NativePayment", model);
        }

        [ValidateInput(false)]
        public ActionResult AuthNotify(int orderId, string ip, string code, string state)
        {
            _logger.Information(
                $"收到微信验证通知：orderId=[{orderId}], ip=[{ip}], code=[{code}], state=[{state}]");
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Weixin") as WeixinPaymentProcessor;
            if (processor == null || !processor.PluginDescriptor.Installed)
            {
                throw new NopException("Weixin module can not be loaded");
            }

            if (!string.IsNullOrEmpty(code))
            {
                var authResult = this.RequestWeixinOauth2(code);
                var values = new Dictionary<string, string>();
                var notifyUrl = _webHelper.GetStoreLocation(false) + $"Plugins/PaymentWeixin/PrepayNotify";
                var order = _orderService.GetOrderById(orderId);
                values["notify_url"] = notifyUrl;
                values["spbill_create_ip"] = ip;
                values["trade_type"] = "JSAPI";
                values["openid"] = authResult["openid"];

                var returnWeixinData = this._weixinPaymentService.UnifiedOrder(order, values);
                var resultCode = returnWeixinData.GetValue("result_code");
                var err_code = returnWeixinData.GetValue("err_code");
                if (resultCode != null && resultCode == "SUCCESS")
                {
                    PrepayInfoModel model = PreparePrepayInfo(returnWeixinData.GetValue("prepay_id"), orderId);
                    _logger.Information($"进入微信支付界面...");
                    return View("~/Plugins/Payments.Weixin/Views/PaymentWeixin/JsapiPrepay.cshtml", model);
                }
                else if (err_code == "ORDERPAID")
                {
                    _weixinPaymentService.ProcessOrderPaid(order);
                    return Redirect($"~/OrderDetails/{orderId}");
                }
                else
                {
                    return Redirect($"~/OrderDetails/{orderId}");
                }
            }
            else {
                _logger.Error("微信支付失败：微信没有返回合适的 code");
                throw new NopException("支付失败");
            }
        }

        private PrepayInfoModel PreparePrepayInfo(string prepayId, int orderId)
        {
            var payData = new WxPayData(_WeixinPaymentSetting.MchKey);
            payData.SetValue("appId", _WeixinPaymentSetting.AppId);
            payData.SetValue("timeStamp", WxPayData.GenerateTimeStamp());
            payData.SetValue("nonceStr", WxPayData.GenerateNonceStr());
            payData.SetValue("package", $"prepay_id={prepayId}");
            payData.SetValue("signType", "MD5");
            payData.SetValue("paySign", payData.MakeSign());
            var model = new PrepayInfoModel()
            {
                AppId = payData.GetValue("appId"),
                TimeStamp = payData.GetValue("timeStamp"),
                NonceStr = payData.GetValue("nonceStr"),
                Package = payData.GetValue("package"),
                SignType = payData.GetValue("signType"),
                PaySign = payData.GetValue("paySign"),
                PrepayId = prepayId,
                OrderId = orderId,
            };
            return model;
        }

        [ValidateInput(false)]
        public ActionResult CheckPaymentResult(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            var success = _weixinPaymentService.OrderQuery(order);
            if (success)
            {
                _weixinPaymentService.ProcessOrderPaid(order);
            }

            return Redirect($"~/OrderDetails/{orderId}");
        }

        [ValidateInput(false)]
        public ActionResult PrepayNotify()
        {
            using (var r = new System.IO.StreamReader(Request.InputStream))
            {
                _logger.Information($"通知查询参数：[{Request.QueryString.ToString()}]");
                _logger.Information($"通知内容：[{r.ReadToEnd()}]");
            }
            return Content("");
        }

        [ValidateInput(false)]
        public ActionResult Return()
        {
            var processor = _paymentService.LoadPaymentMethodBySystemName("Payments.Weixin") as WeixinPaymentProcessor;
            if (processor == null || !processor.PluginDescriptor.Installed)
                throw new NopException("Weixin module can not be loaded");

            string status_msg = String.Empty;
            string v_oid = Request["v_oid"]; // 订单编号
            string v_pstatus = Request["v_pstatus"]; // 支付状态
            string v_pstring = Request["v_pstring"]; // 支付结果信息
            string v_pmode = Request["v_pmode"]; // 支付银行
            string v_md5str = Request["v_md5str"]; // 订单MD5校验码
            string v_amount = Request["v_amount"]; // 订单实际支付金额
            string v_moneytype = Request["v_moneytype"]; // 订单实际支付币种
            string remark1 = Request["remark1"]; // 备注字段1
            string remark2 = Request["remark2"]; // 备注字段2，返回通知结果地址

            //                return RedirectToRoute("CheckoutCompleted", new { orderId = 0 });
            //自定义错误页面，需要商户开发页面
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        private Dictionary<string, string> RequestWeixinOauth2(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentNullException(code);
            }
            var appid = _WeixinPaymentSetting.AppId;
            var secret = _WeixinPaymentSetting.AppSecret;
            var url = $"https://api.weixin.qq.com/sns/oauth2/access_token?appid={appid}&secret={secret}&code={code}&grant_type=authorization_code";

            var jsonText = this.DoHttpRequest(url);
            var jss = new JavaScriptSerializer();
            var dict = jss.Deserialize<Dictionary<string, string>>(jsonText);
            return dict;
        }

        private string DoHttpRequest(string url)
        {
            using (var wb = new WebClient())
            {
                var response = wb.DownloadString(url);
                return response;
            }
        }
    }
}
