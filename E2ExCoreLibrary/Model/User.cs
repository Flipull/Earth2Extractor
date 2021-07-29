using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace E2ExCoreLibrary.Model
{
    public class User
    {
        [Key, StringLength(36, MinimumLength = 36)]
        public string Id { get; set; }

        public bool locked { get; set; }
        public DateTime? updated { get; set; }

        [StringLength(8)]
        public string countryCode { get; set; }
        public string customPhoto { get; set; }
        public string name { get; set; }
        [NotMapped]
        public string username { get; set; }
    }
}
