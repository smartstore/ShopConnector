namespace SmartStore.ShopConnector.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class LimitToStores : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ShopConnectorConnection", "LimitedToStoreIds", c => c.String());
        }

        public override void Down()
        {
            DropColumn("dbo.ShopConnectorConnection", "LimitedToStoreIds");
        }
    }
}
