using System.Data.Entity;
using SmartStore.Data;
using SmartStore.Data.Setup;
using SmartStore.ShopConnector.Data.Migrations;

namespace SmartStore.ShopConnector.Data
{
    public class ShopConnectorObjectContext : ObjectContextBase
    {
        public const string ALIASKEY = "sm_object_context_shop_connector_connection";

        static ShopConnectorObjectContext()
        {
            var initializer = new MigrateDatabaseInitializer<ShopConnectorObjectContext, Configuration>
            {
                TablesToCheck = new[] { "ShopConnectorConnection", "ShopConnectorSkuMapping" }
            };
            Database.SetInitializer(initializer);
        }

        public ShopConnectorObjectContext() : base()
        {
        }

        public ShopConnectorObjectContext(string nameOrConnectionString)
            : base(nameOrConnectionString, ALIASKEY)
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShopConnectorConnectionRecord>().ToTable("ShopConnectorConnection");
            modelBuilder.Entity<ShopConnectorSkuMapping>().ToTable("ShopConnectorSkuMapping");

            //disable EdmMetadata generation
            //modelBuilder.Conventions.Remove<IncludeMetadataConvention>();
            base.OnModelCreating(modelBuilder);
        }
    }
}
