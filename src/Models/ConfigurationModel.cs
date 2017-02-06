using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;
using System;
using FluentValidation.Attributes;
using Nop.Plugin.Payments.Weixin.Validatiors;

namespace Nop.Plugin.Payments.Weixin.Models
{
    [Validator(typeof(ConfigurationValidator))]
    public class ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.Weixin.AppId")]
        public string AppId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Weixin.AppSecret")]
        public string AppSecret { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Weixin.MchId")]
        public string MchId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Weixin.MchKey")]
        public string MchKey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Weixin.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
    }
}
