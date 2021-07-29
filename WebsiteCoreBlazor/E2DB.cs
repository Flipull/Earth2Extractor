using E2ExCoreLibrary.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace WebsiteCoreBlazor
{
    public class E2DB: DbContext
    {
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<LandField> LandFields { get; set; }
        public virtual DbSet<LandFieldTransactions> Transactions { get; set; }

        public virtual DbSet<SimpleData> Simpletons { get; set; }
        

        public E2DB() : base()
        {
            
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=E2;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False");
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LandFieldTransactions>()
                .HasIndex("ownerId");
            modelBuilder.Entity<LandFieldTransactions>()
                .HasIndex("previousOwnerId");
            modelBuilder.Entity<SimpleData>()
                .HasKey(p => new { p.userid, p.momenta });
            base.OnModelCreating(modelBuilder);
        }
    }
}