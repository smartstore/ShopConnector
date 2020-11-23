using Autofac;
using Autofac.Core;
using SmartStore.Core.Data;
using SmartStore.Core.Infrastructure;
using SmartStore.Core.Infrastructure.DependencyManagement;
using SmartStore.Data;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public int Order => 1;

        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, bool isActiveModule)
        {
            builder.RegisterType<ShopConnectorService>().As<IShopConnectorService>().InstancePerRequest();
            builder.RegisterType<ShopConnectorImportService>().As<IShopConnectorImportService>().InstancePerRequest();

            builder.Register<IDbContext>(c => new ShopConnectorObjectContext(DataSettings.Current.DataConnectionString))
                .Named<IDbContext>(ShopConnectorObjectContext.ALIASKEY)
                .InstancePerRequest();

            builder.Register<ShopConnectorObjectContext>(c => new ShopConnectorObjectContext(DataSettings.Current.DataConnectionString))
                .InstancePerRequest();

            builder.RegisterType<EfRepository<ShopConnectorConnectionRecord>>()
                .As<IRepository<ShopConnectorConnectionRecord>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>(ShopConnectorObjectContext.ALIASKEY))
                .InstancePerRequest();

            builder.RegisterType<EfRepository<ShopConnectorSkuMapping>>()
                .As<IRepository<ShopConnectorSkuMapping>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>(ShopConnectorObjectContext.ALIASKEY))
                .InstancePerRequest();
        }
    }
}
