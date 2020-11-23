using System.Collections.Generic;
using System.Linq;
using SmartStore.Core.Domain.Customers;
using SmartStore.Core.Domain.Security;
using SmartStore.Core.Security;

namespace SmartStore.ShopConnector.Security
{
    public static class ShopConnectorPermissions
    {
        public const string Self = "shopconnector";
        public const string Read = "shopconnector.read";
        public const string Update = "shopconnector.update";
        public const string Create = "shopconnector.create";
        public const string Delete = "shopconnector.delete";
        public const string Upload = "shopconnector.upload";
        public const string Import = "shopconnector.import";
        public const string Export = "shopconnector.export";
        public const string EditSkuMapping = "shopconnector.editskumapping";
    }


    public class ShopConnectorPermissionProvider : IPermissionProvider
    {
        public IEnumerable<PermissionRecord> GetPermissions()
        {
            var permissionSystemNames = PermissionHelper.GetPermissions(typeof(ShopConnectorPermissions));
            var permissions = permissionSystemNames.Select(x => new PermissionRecord { SystemName = x });

            return permissions;
        }

        public IEnumerable<DefaultPermissionRecord> GetDefaultPermissions()
        {
            return new[]
            {
                new DefaultPermissionRecord
                {
                    CustomerRoleSystemName = SystemCustomerRoleNames.Administrators,
                    PermissionRecords = new[]
                    {
                        new PermissionRecord { SystemName = ShopConnectorPermissions.Self }
                    }
                }
            };
        }
    }
}