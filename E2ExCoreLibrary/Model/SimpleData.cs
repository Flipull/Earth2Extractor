using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace E2ExCoreLibrary.Model
{
    class SimpleData
    {
        [Key, Column(Order = 0), Required]
        public User user { get; set; }
        [Key, Column(Order = 1), Required]
        public DateTime Momenta { get; set; }

        public int tilesSoldAmount { get; set; }
        public int tilesBoughtAmount { get; set; }
        public int totalPropertiesOwned { get; set; }
        public int totalPropertiesResold { get; set; }
        public int currentPropertiesOwned { get; set; }
        public int profitsOnSell { get; set; }
        public int returnsOnSell { get; set; }
    }
}
