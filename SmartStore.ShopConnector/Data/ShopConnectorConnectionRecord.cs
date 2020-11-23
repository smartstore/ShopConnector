using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartStore.Core;

namespace SmartStore.ShopConnector.Data
{
    [Table("ShopConnectorConnection")]
    public partial class ShopConnectorConnectionRecord : BaseEntity
    {
        /// <summary>
        /// Whether the connecton is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether the connecton is for import or export
        /// </summary>
        [Index("IX_ShopConnectorConnection_IsForExport_CreatedOn", Order = 0)]
        public bool IsForExport { get; set; }

        /// <summary>
        /// Url to connect
        /// </summary>
        [Required, StringLength(2000)]
        public string Url { get; set; }

        /// <summary>
        /// Public key
        /// </summary>
        [Required, StringLength(50)]
        public string PublicKey { get; set; }

        /// <summary>
        /// Secret key (only known by provider and consumer)
        /// </summary>
        [Required, StringLength(50)]
        public string SecretKey { get; set; }

        /// <summary>
        /// Number of requests made
        /// </summary>
        public long RequestCount { get; set; }

        /// <summary>
        /// Date of last request
        /// </summary>
        public DateTime? LastRequestUtc { get; set; }

        /// <summary>
        /// Date when products were lastly fetched
        /// </summary>
        public DateTime? LastProductCallUtc { get; set; }

        /// <summary>
        /// Gets or sets the ids of manufactures the connection is limited/restricted to
        /// </summary>
        [MaxLength]
        public string LimitedToManufacturerIds { get; set; }

        /// <summary>
        /// Gets or sets the ids of stores the connection is limited/restricted to
        /// </summary>
        [MaxLength]
        public string LimitedToStoreIds { get; set; }

        /// <summary>
        /// Creation date of this record
        /// </summary>
        [Index("IX_ShopConnectorConnection_IsForExport_CreatedOn", Order = 1)]
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Date of last update of this record
        /// </summary>
        public DateTime UpdatedOnUtc { get; set; }
    }
}
