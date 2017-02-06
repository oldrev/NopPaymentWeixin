using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Weixin.Models {
    public class PrepayInfoModel : BaseNopModel {

        /*
         "appId": "wx2421b1c4370ec43b",     //公众号名称，由商户传入
                    "timeStamp": " 1395712654",         //时间戳，自1970年以来的秒数
                    "nonceStr": "e61463f8efa94090b1f366cccfbbb444", //随机串
                    "package": "prepay_id=u802345jgfjsdfgsdg888",
                    "signType": "MD5",         //微信签名方式：
                    "paySign": "70EA570631E4BB79628FBCA90534C63FF7FADD89" //微信签名
                    */
        public string AppId { get; set; }
        public string TimeStamp { get; set; }
        public string NonceStr { get; set; }
        public string Package { get; set; }
        public string SignType { get; set; }
        public string PaySign { get; set; }
        public int OrderId { get; set; }
        public string PrepayId { get; set; }
        public string WeixinNativePaymentUrl { get; set; }

    }
}
