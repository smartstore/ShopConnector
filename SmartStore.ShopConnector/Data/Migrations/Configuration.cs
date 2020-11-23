namespace SmartStore.ShopConnector.Data.Migrations
{
    using System.Data.Entity.Migrations;

    internal sealed class Configuration : DbMigrationsConfiguration<ShopConnectorObjectContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            MigrationsDirectory = @"Data\Migrations";
            ContextKey = "SmartStore.ShopConnector"; // DO NOT CHANGE!
        }

        protected override void Seed(ShopConnectorObjectContext context)
        {
        }
    }
}
