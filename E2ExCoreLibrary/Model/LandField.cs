using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace E2ExCoreLibrary.Model
{
    public class LandField
    {
        [Key, StringLength(36, MinimumLength = 36)]
        public string id { get; set; }
        public byte tileClass { get; set; }
        public int tileCount { get; set; }

        public List<LandFieldTransactions> transactionSet { get; set; }
        [NotMapped]
        public List<LandFieldBids> bidentrySet { get; set; }
    }
    
    public class LandFieldTransactions
    {
        [Key, StringLength(36, MinimumLength = 36)]
        public string id { get; set; }
        public Decimal price { get; set; }

        [StringLength(36)]
        public string time { get; set; }
        
        
        [NotMapped]
        public DateTime moment { 
            get
            {
                return DateTime.Parse(time);
            }
            set
            {
                time = value.ToString("MM/dd/yyyy hh:mm:ss");
            }
        }
            
        [NotMapped]
        public User owner { get; set; }
        public string ownerId { 
            get { return owner?.Id; }
            set { owner = new User { Id = value }; } 
        }
        [NotMapped]
        public User previousOwner { get; set; }
        public string previousOwnerId
        {
            get { return previousOwner?.Id; }
            set { previousOwner = new User { Id = value }; }
        }

        public virtual LandField landField { get; set; }
    }

    public class LandFieldBids
    {
        [NotMapped]
        public User buyer { get; set; }
        public string buyerId
        {
            get { return buyer?.Id; }
            set { buyer = new User { Id = value }; }
        }
    }
}
