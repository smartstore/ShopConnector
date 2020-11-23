using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartStore.Core;

namespace SmartStore.ShopConnector.Data
{
    /// <summary>
    /// Mapping for differing product SKUs.
    /// </summary>
    [Table("ShopConnectorSkuMapping")]
    public partial class ShopConnectorSkuMapping : BaseEntity
    {
        /// <summary>
        /// Product identifier.
        /// </summary>
        [Index("IX_ShopConnectorSkuMapping_ProductId")]
        public int ProductId { get; set; }

        /// <summary>
        /// Domain of the client shop.
        /// </summary>
        [Required, StringLength(400)]
        public string Domain { get; set; }

        /// <summary>
        /// Differing SKU for given client shop.
        /// </summary>
        [StringLength(400)]
        public string Sku { get; set; }
    }
}