using E2ExCoreLibrary.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Earth2ExtractorCore
{
    class E2DB: DbContext
    {
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<LandField> LandFields { get; set; }
        public virtual DbSet<LandFieldTransactions> Transactions { get; set; }
        
        
        public E2DB() : base()
        {
            
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=E2;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False");
            base.OnConfiguring(optionsBuilder);
        }
    }
}
