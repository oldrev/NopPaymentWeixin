using Nop.Plugin.Payments.Weixin.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using FluentValidation;

namespace Nop.Plugin.Payments.Weixin.Validatiors
{
    public class ConfigurationValidator : BaseNopValidator<ConfigurationModel>
    {
        public ConfigurationValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.AppId).NotEmpty().WithMessage(localizationService.GetResource("Plugins.Payments.Weixin.AppIdRequired"));
            RuleFor(x => x.AppSecret).NotEmpty().WithMessage(localizationService.GetResource("Plugins.Payments.Weixin.AppSecretRequired"));
            RuleFor(x => x.MchId).NotEmpty().WithMessage(localizationService.GetResource("Plugins.Payments.Weixin.MchIdRequired"));
            RuleFor(x => x.AdditionalFee).GreaterThanOrEqualTo(0).WithMessage(localizationService.GetResource("Plugins.Payments.Weixin.AdditionalFeeRequired"));
        }
    }
}
