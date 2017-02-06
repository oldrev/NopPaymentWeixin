using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Weixin
{
    public class WeixinPaymentSetting : ISettings
    {
        /// <summary>
        /// AppId
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Secret
        /// </summary>
        public string AppSecret { get; set; }

        /// <summary>
        /// 商户号
        /// </summary>
        public string MchId { get; set; }

        /// <summary>
        /// 商户密钥
        /// </summary>
        public string MchKey { get; set; }

        /// <summary>
        /// 额外费用
        /// </summary>
        public decimal AdditionalFee { get; set; }
    }
}
