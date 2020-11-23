using System;
using System.Linq;
using System.Web.Mvc;
using FluentValidation;
using SmartStore.Core.Localization;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;
using SmartStore.Web.Framework.Validators;

namespace SmartStore.ShopConnector.Models
{
    public class ConnectionModel : EntityModelBase
    {
        [SmartResourceDisplayName("Common.IsActive")]
        public bool IsActive { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.Url")]
        public string Url { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.PublicKey")]
        public string PublicKey { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.SecretKey")]
        public string SecretKey { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.RequestCount")]
        public long RequestCount { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.LastRequest")]
        public DateTime? LastRequest { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.LastProductCall")]
        public DateTime? LastProductCall { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.LimitedToManufacturerIds")]
        public int[] LimitedToManufacturerIds { get; set; }
        public MultiSelectList AvailableManufacturers { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.LimitedToManufacturerIds")]
        public bool LimitedToManufacturer => LimitedToManufacturerIds != null && LimitedToManufacturerIds.Any();

        [SmartResourceDisplayName("Admin.Common.Store.LimitedTo")]
        public int[] LimitedToStoreIds { get; set; }
        public MultiSelectList AvailableStores { get; set; }

        [SmartResourceDisplayName("Admin.Common.Store.LimitedTo")]
        public bool LimitedToStore => LimitedToStoreIds != null && LimitedToStoreIds.Any();

        [SmartResourceDisplayName("Common.CreatedOn")]
        public DateTime? CreatedOn { get; set; }

        [SmartResourceDisplayName("Common.UpdatedOn")]
        public DateTime? UpdatedOn { get; set; }

        public bool IsForExport { get; set; }
        public string Note { get; set; }
        public string NoteLabelHint => Note.HasValue() ? "info" : "smnet-hide";

        /// <remarks>false == automatically created</remarks>
        public bool KeysRequired => !(IsForExport && Id == 0);
    }

    public class ConnectionModelValidator : SmartValidatorBase<ConnectionModel>
    {
        public ConnectionModelValidator(Localizer T)
        {
            RuleFor(x => x.Url)
                .Must(x => x.HasValue() && x.IsWebUrl())
                .WithMessage(T("Plugins.SmartStore.ShopConnector.Url.Validate"));

            //RuleFor(x => x.Url)
            //	.Must(x => !x.IsSelfUrl())
            //	.WithMessage(localize.GetResource("Plugins.SmartStore.ShopConnector.Url.IsNotSelf"));

            RuleFor(x => x.PublicKey)
                .NotEmpty()
                .When(x => x.KeysRequired);

            RuleFor(x => x.SecretKey)
                .NotEmpty()
                .When(x => x.KeysRequired);
        }
    }
}
