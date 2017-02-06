using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Services.Logging;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Core;
using Nop.Core.Domain.Orders;

using Nop.Plugin.Payments.Weixin.Models;
using System.Net;

namespace Nop.Plugin.Payments.Weixin.Services
{
    public interface IWeixinPaymentService
    {
        WxPayData PostApiRequest(string url, IDictionary<string, string> values);
        bool OrderQuery(Order order);
        WxPayData UnifiedOrder(Order order, IDictionary<string, string> values);
        void ProcessOrderPaid(Order order);
    }

    public class WeixinPaymentService : IWeixinPaymentService
    {
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly WeixinPaymentSetting _WeixinPaymentSetting;
        private readonly IWebHelper _webHelper;
        private readonly IStoreContext _storeContext;

        public const string WeixinUnifiedOrderUrl = "https://api.mch.weixin.qq.com/pay/unifiedorder";
        public const string WeixinOrderQueryUrl = "https://api.mch.weixin.qq.com/pay/orderquery";

        public WeixinPaymentService(IOrderService orderService,
            ILogger logger,
            ILocalizationService localizationService,
            IOrderProcessingService orderProcessingService,
            WeixinPaymentSetting weixinPaymentSetting,
            IWebHelper webHelper,
            IStoreContext storeContext)
        {
            this._logger = logger;
            this._localizationService = localizationService;
            this._orderProcessingService = orderProcessingService;
            this._WeixinPaymentSetting = weixinPaymentSetting;
            this._webHelper = webHelper;
            this._storeContext = storeContext;
        }

        public WxPayData PostApiRequest(string url, IDictionary<string, string> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var wxdata = new WxPayData(_WeixinPaymentSetting.MchKey);
            foreach (var p in values)
            {
                wxdata.SetValue(p.Key, p.Value);
            }
            wxdata.SetValue("appid", _WeixinPaymentSetting.AppId);
            wxdata.SetValue("mch_id", _WeixinPaymentSetting.MchId);
            wxdata.SetValue("nonce_str", WxPayData.GenerateNonceStr());
            wxdata.SetValue("sign", wxdata.MakeSign());

            if (!wxdata.CheckSign())
            {
                throw new NopException("在请求之前发现签名校验错误！");
            }

            using (var wc = new WebClient())
            {
                wc.Encoding = System.Text.Encoding.UTF8;
                _logger.Information($"Post 微信支付API调用：URL=[{url}], DATA=[{wxdata.ToXml()}]");
                var result = wc.UploadString(url, "POST", wxdata.ToXml());
                _logger.Information($"微信支付调用返回数据：[{result}]");
                var returnWeixinData = new WxPayData(this._WeixinPaymentSetting.MchKey);
                returnWeixinData.FromXml(result);
                var returnCode = returnWeixinData.GetValue("return_code");
                if (returnCode != null && returnCode == "SUCCESS")
                {
                    return returnWeixinData;
                }
                else {
                    var msg = returnWeixinData.GetValue("return_msg");
                    throw new NopException($"微信统一下单接口返回错误：[{msg}]");
                }
            }
        }

        public bool OrderQuery(Order order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var values = new Dictionary<string, string>();

            values["out_trade_no"] = order.Id.ToString();

            var returnPayData = this.PostApiRequest(WeixinOrderQueryUrl, values);
            var returnCode = returnPayData.GetValue("return_code");
            var tradeState = returnPayData.GetValue("trade_state");
            return returnCode != null && returnCode == "SUCCESS"
                && tradeState != null && tradeState == "SUCCESS";
        }

        public void ProcessOrderPaid(Order order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                _orderProcessingService.MarkOrderAsPaid(order);
            }
        }

        public WxPayData UnifiedOrder(Order order, IDictionary<string, string> values)
        {
            var fullValues = new Dictionary<string, string>(values);
            var bodyText = $"支付【{_storeContext.CurrentStore.Name}】订单 #{order.Id}";
            fullValues["body"] = bodyText;
            fullValues["attach"] = _storeContext.CurrentStore.Name;
            fullValues["device_info"] = "WEB";
            fullValues["out_trade_no"] = order.Id.ToString();
            fullValues["total_fee"] = Math.Round(order.OrderTotal * 100).ToString();
            return this.PostApiRequest(WeixinUnifiedOrderUrl, fullValues);
        }

    }
}
