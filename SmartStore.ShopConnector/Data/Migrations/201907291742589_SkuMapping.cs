namespace SmartStore.ShopConnector.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class SkuMapping : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ShopConnectorSkuMapping",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    ProductId = c.Int(nullable: false),
                    Domain = c.String(nullable: false, maxLength: 400),
                    Sku = c.String(maxLength: 400),
                })
                .PrimaryKey(t => t.Id)
                .Index(t => t.ProductId, name: "IX_ShopConnectorSkuMapping_ProductId");

            CreateIndex("dbo.ShopConnectorConnection", new[] { "IsForExport", "CreatedOnUtc" }, name: "IX_ShopConnectorConnection_IsForExport_CreatedOn");
        }

        public override void Down()
        {
            DropIndex("dbo.ShopConnectorSkuMapping", "IX_ShopConnectorSkuMapping_ProductId");
            DropIndex("dbo.ShopConnectorConnection", "IX_ShopConnectorConnection_IsForExport_CreatedOn");
            DropTable("dbo.ShopConnectorSkuMapping");
        }
    }
}
