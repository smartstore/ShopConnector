namespace SmartStore.ShopConnector.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            //if (DbMigrationContext.Current.SuppressInitialCreate<ShopConnectorObjectContext>())
            //	return;

            CreateTable(
                "dbo.ShopConnectorConnection",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    IsActive = c.Boolean(nullable: false),
                    IsForExport = c.Boolean(nullable: false),
                    Url = c.String(nullable: false, maxLength: 2000),
                    PublicKey = c.String(nullable: false, maxLength: 50),
                    SecretKey = c.String(nullable: false, maxLength: 50),
                    RequestCount = c.Long(nullable: false),
                    LastRequestUtc = c.DateTime(),
                    LastProductCallUtc = c.DateTime(),
                    LimitedToManufacturerIds = c.String(),
                    CreatedOnUtc = c.DateTime(nullable: false),
                    UpdatedOnUtc = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id);

        }

        public override void Down()
        {
            DropTable("dbo.ShopConnectorConnection");
        }
    }
}
