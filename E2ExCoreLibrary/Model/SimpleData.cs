using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace E2ExCoreLibrary.Model
{
    public class SimpleData
    {
        //[Key, Column(Order = 0), Required]
        [Required, StringLength(36, MinimumLength = 36)]
        public string userid { get; set; }
        //[Key, Column(Order=1), Required]
        [Required]
        public DateTime momenta { get; set; }
        
        public int tilesSoldAmount { get; set; }
        public int tilesBoughtAmount { get; set; }
        public int totalPropertiesOwned { get; set; }
        public int totalPropertiesResold { get; set; }
        public int currentPropertiesOwned { get; set; }
        public Decimal profitsOnSell { get; set; }
        public double returnsOnSell { get; set; }
    }
}
